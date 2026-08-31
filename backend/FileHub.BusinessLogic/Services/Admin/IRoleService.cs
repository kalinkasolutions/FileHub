using Dtos.Admin;
using Shared;

namespace FileHub.BusinessLogic.Services.Admin;

/// <summary>
/// Read-only view of the fixed role set. Roles are constants in <c>Shared.Roles</c>, seeded at
/// startup — there is deliberately no create or delete here, because the authorization checks name
/// those two roles directly and an invented role would grant nothing.
/// </summary>
public interface IRoleService
{
    Task<OperationResult<RoleDto[]>> ListRolesAsync();
}
