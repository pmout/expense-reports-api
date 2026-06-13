using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExpenseReports.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExpenseAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "expense_audit_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpenseId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Decision = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DecidedByEmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expense_audit_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_expense_audit_entries_employees_DecidedByEmployeeId",
                        column: x => x.DecidedByEmployeeId,
                        principalTable: "employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_expense_audit_entries_employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_expense_audit_entries_expenses_ExpenseId",
                        column: x => x.ExpenseId,
                        principalTable: "expenses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_expense_audit_entries_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_expense_audit_entries_DecidedByEmployeeId",
                table: "expense_audit_entries",
                column: "DecidedByEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_expense_audit_entries_EmployeeId",
                table: "expense_audit_entries",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_expense_audit_entries_ExpenseId",
                table: "expense_audit_entries",
                column: "ExpenseId");

            migrationBuilder.CreateIndex(
                name: "IX_expense_audit_entries_TenantId_ExpenseId",
                table: "expense_audit_entries",
                columns: new[] { "TenantId", "ExpenseId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "expense_audit_entries");
        }
    }
}
