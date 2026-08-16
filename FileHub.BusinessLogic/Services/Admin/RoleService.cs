using Dal.Repositories.Admin;
using Dtos.Admin;
using Shared;

namespace FileHub.BusinessLogic.Services.Admin;

public sealed class RoleService : IRoleService
{
    private readonly IUserAdminRepository m_userAdminRepository;

    public RoleService(
        IUserAdminRepository userAdminRepository
    )
    {
        m_userAdminRepository = userAdminRepository;
    }

    public async Task<OperationResult<RoleDto[]>> ListRolesAsync()
    {
        // The role names come from the constants rather than from the table: those constants are
        // what the authorization policies check, so a row that drifted from them would be a lie.
        var roles = new List<RoleDto>();

        foreach (var role in Roles.All)
        {
            var userCount = await m_userAdminRepository.CountUsersInRoleAsync(role);
            roles.Add(new RoleDto
            {
                Name = role,
                UserCount = userCount
            });
        }

        return OperationResult<RoleDto[]>.Success(roles.ToArray());
    }
}
