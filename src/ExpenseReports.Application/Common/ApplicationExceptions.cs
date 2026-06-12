namespace ExpenseReports.Application.Common;

/// <summary>
/// The resource does not exist *for the caller*. Cross-tenant access lands here
/// on purpose: answering 403/409 would confirm the resource exists in another
/// tenant, which is itself a data leak.
/// </summary>
public sealed class NotFoundException(string resource)
    : Exception($"{resource} was not found.");

/// <summary>
/// Login failed. Always the same message regardless of whether the e-mail
/// exists, to prevent account enumeration.
/// </summary>
public sealed class InvalidCredentialsException()
    : Exception("Invalid e-mail or password.");
