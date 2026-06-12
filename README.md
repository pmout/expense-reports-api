# Expense Reports API

API REST multi-tenant para controlo de reembolsos corporativos: colaboradores submetem despesas, managers aprovam ou rejeitam, com teto mensal por colaborador definido por empresa (tenant).

**Stack:** .NET 9 · Minimal APIs · PostgreSQL · EF Core · JWT · xUnit · Docker Compose

---

## Como correr

Pré-requisito: Docker.

```bash
docker compose up --build
```

Isto levanta o PostgreSQL, espera pelo healthcheck, aplica as migrations e faz o seed automaticamente. A API fica em `http://localhost:8080` (Swagger UI em `/swagger`, healthcheck em `/healthz`).

### Credenciais de demonstração (seed)

| Tenant | Limite mensal | Utilizadores | Password |
|---|---|---|---|
| Acme Corporation | 1 000,00 | `alice@acme.test`, `bruno@acme.test` (Employee), `manager@acme.test` (Manager) | `Passw0rd!demo` |
| Globex Brasil | 5 000,00 | `alice@globex.test`, `bruno@globex.test` (Employee), `manager@globex.test` (Manager) | `Passw0rd!demo` |

```bash
curl -X POST http://localhost:8080/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"alice@acme.test","password":"Passw0rd!demo"}'
```

### Como correr os testes

```bash
dotnet test
```

Os testes de integração usam [Testcontainers](https://dotnet.testcontainers.org/) — precisam do Docker ativo; o resto é automático. Para correr só os unitários (sem Docker): `dotnet test tests/ExpenseReports.UnitTests`.

### Seed manual

O seed corre no arranque quando `Database__SeedOnStartup=true` (já definido no compose) e é idempotente. Fora do compose: `dotnet run --project src/ExpenseReports.Api` com a mesma variável de ambiente.

---

## Endpoints

| Método | Rota | Quem | Resultado |
|---|---|---|---|
| POST | `/auth/login` | anónimo (rate-limited) | JWT com `sub`, `tenant_id`, `role`, `exp` |
| POST | `/expenses` | colaborador autenticado | 201, despesa `Pending` |
| GET | `/expenses?page=&pageSize=` | colaborador | só as próprias despesas, paginadas |
| GET | `/expenses/pending` | manager | pendentes do seu tenant |
| GET | `/expenses/{id}` | dono ou manager do tenant | detalhe; de outro tenant → 404 |
| POST | `/expenses/{id}/approve` | manager | aprova (regras 1–4) |
| POST | `/expenses/{id}/reject` | manager | rejeita com `reason` (10–500 chars) |
| GET | `/healthz` | anónimo | estado da app + BD |

**Modelo de erros:** validação de input → `400` (Problem Details com os campos); credenciais → `401`; falta de role → `403`; recurso inexistente *para o chamador* → `404`; decisão concorrente/estado já decidido → `409`; violação de regra de negócio → `422`. Nunca há stack traces na resposta.

---

## Decisões de arquitetura

### Camadas

```
Domain ← Application ← Infrastructure ← Api
```

- **Domain** — entidades, value objects e as 6 invariantes. Zero dependências. As regras vivem nas entidades: `Expense.Approve(...)` recusa aprovador de outro tenant, não-manager, autoaprovação, dupla decisão e excesso de limite; é impossível construir uma despesa inválida (estado válido por construção).
- **Application** — um handler fino por caso de uso e os *ports* (interfaces de repositório, relógio, tokens, hashing). Sem lógica de negócio.
- **Infrastructure** — EF Core + Npgsql, migrations, repositórios, BCrypt, emissão de JWT.
- **Api** — endpoints Minimal API, autenticação/autorização, validação de input, mapeamento de erros, logging.

### Multi-tenancy: invariante de sistema, não filtro ad-hoc

- O `TenantId` vem **exclusivamente do JWT validado** — nunca do payload.
- **Global query filters** do EF Core comparam o `TenantId` de `employees`/`expenses` com o tenant do request em **todas as queries** — o filtro está no SQL gerado, não nos handlers. Sem tenant autenticado, as queries não devolvem nada (*fail closed*).
- A única query sem filtro é o lookup de login por e-mail (corre antes de existir tenant) — está isolada num método com nome explícito e é o único `IgnoreQueryFilters` do código de produção.
- Acesso cross-tenant responde **404, não 403**: confirmar que o recurso existe noutro tenant já seria fuga de informação.
- Defesa em profundidade: além do filtro na query, a própria entidade recusa decisões de aprovadores de outro tenant.

### EF Core em vez de Dapper

1. **Migrations integradas** — versionamento de schema sem ferramenta adicional;
2. **Global query filters** — o isolamento de tenant fica garantido centralmente na camada de dados, em vez de depender de cada query escrita à mão ter o `WHERE` certo;
3. **Concurrency tokens** (`xmin`) de borla para a transição única de estado.

O custo (SQL menos explícito) é mitigado por a app ter queries simples. Com um modelo de leitura complexo, Dapper seria reconsiderado para queries de leitura.

### Sem MediatR

Sete casos de uso com uma chamada direta a um handler cada. O MediatR acrescentaria indireção (descobrir o handler deixa de ser *go to definition*) sem benefício aqui — não há pipeline de behaviors que o justifique. Validação e tratamento de erros vivem em endpoint filters e no exception handler do ASP.NET Core.

### Concorrência nas aprovações (o detalhe menos óbvio)

Duas corridas possíveis:

1. **Mesma despesa, duas decisões em simultâneo** — token de concorrência otimista no `xmin` do Postgres: o segundo `SaveChanges` falha e responde `409`.
2. **Despesas diferentes do mesmo colaborador, aprovadas em simultâneo** — ambas leriam um total mensal que ainda não inclui a outra e o limite seria furado. A aprovação corre numa transação que adquire `pg_advisory_xact_lock` com chave no colaborador: aprovações do mesmo colaborador serializam-se e o total lido já inclui a aprovação anterior. O lock é por colaborador (não global) e liberta-se no commit/rollback.

### Decisões sobre ambiguidades do enunciado

| Ambiguidade | Decisão | Justificação |
|---|---|---|
| Limite mensal vs. moedas (BRL/EUR/USD) | O limite aplica-se **por moeda**, sem conversão | Não há taxas de câmbio no domínio; somar 100 BRL + 100 EUR é inválido — o VO `Money` recusa. Alternativa (limite na moeda base do tenant com conversão) exigiria uma fonte de câmbio fora do âmbito. |
| "Mês corrente" | Mês civil (UTC) da **`ExpenseDate`** da despesa a aprovar | O orçamento diz respeito ao mês em que a despesa ocorreu; usar a data de aprovação permitiria contornar o limite aprovando no dia 1 do mês seguinte. |
| Manager rejeitar a própria despesa | Permitido | A regra 2 proíbe explicitamente *aprovar* a própria despesa (risco financeiro); rejeitar a própria equivale a retirá-la, sem risco. |
| Datas | Tudo em UTC; `ExpenseDate` é `date` (sem hora) | Evita ambiguidade de fusos na janela de 90 dias. |

### Segurança

- Passwords com **BCrypt** (work factor 12); nunca em texto simples, nunca em logs.
- Login devolve sempre a mesma mensagem para e-mail inexistente vs. password errada (sem enumeração de contas) e tem **rate limiting** (5/min por IP).
- Logs estruturados (Serilog, JSON) sem corpos de request — passwords e tokens não podem aparecer.
- SQL sempre parametrizado (EF Core; o único SQL manual — o advisory lock — usa `FormattableString` parameterizado).

---

## Testes

- **38 unitários** sobre o domínio: pelo menos um por regra de negócio, com sad paths e fronteiras (limite exato, moeda divergente, janelas de datas, comprimentos de razão de rejeição).
- **17 de integração** com Postgres real (Testcontainers): isolamento de tenant em leitura, decisão e listagem de pendentes; ciclo completo aprovar/rejeitar; limite mensal via HTTP; autenticação e sad paths. Não há mocks de repositórios — testa-se o SQL que corre em produção.

## O que ficou de fora (e porquê)

- **Refresh tokens / logout** — JWT de 60 min é suficiente para o âmbito; gestão de sessões é um projeto em si.
- **Conversão cambial** — ver decisão acima.
- **Gestão de tenants/utilizadores via API** — o enunciado centra-se no fluxo de despesas; o seed cobre os dados necessários.
- **Restantes bónus** (audit log, upload de recibos, idempotency key, RLS, background job) — preferi o must-have sólido; dos bónus, implementei rate limiting no login e Swagger, por serem os de melhor rácio valor/risco.
- **CI/CD** — não avaliado; o repositório compila e testa com dois comandos.

---

## Respostas de entrega

**O que priorizei e porquê.** O domínio com as invariantes e testes primeiro (é onde está o valor de negócio), e o isolamento de tenant como propriedade do sistema — resolvido uma vez na camada de dados, não repetido em cada handler. Decisões de concorrência pensadas antes da API, porque corrigi-las depois é caro.

**O que faria de diferente com mais tempo.** Audit log das decisões (tabela separada, já que toda a informação passa num único ponto); idempotência no `POST /expenses`; testes de carga à corrida do limite mensal para validar o lock sob contenção real; Row-Level Security no Postgres como segunda linha de defesa do isolamento.

**A parte de que mais me orgulho.** O tratamento da corrida na aprovação com limite mensal — advisory lock por colaborador + token `xmin` — porque é o tipo de bug que não aparece em demos e custa dinheiro em produção. E o facto de o isolamento de tenant ser *fail closed*: sem tenant no contexto, as queries devolvem vazio.

**Onde sinto que posso melhorar.** A cobertura de observabilidade (métricas/tracing ficaram de fora) e a estratégia de paginação (offset-based é simples mas degrada com volume; keyset seria o próximo passo).
