using Dtos.Admin;
using Shared;

namespace FileHub.BusinessLogic.Services.Admin;

/// <summary>
/// The admin's view of the user list. FileHub has no registration page, so this service is the
/// only way an account comes into existence: an admin invites an address, and the invitation link
/// sets the first password and confirms the address in one step.
/// </summary>
public interface IUserAdminService
{
    Task<OperationResult<UserDto[]>> ListUsersAsync();

    /// <summary>
    /// Creates a passwordless, unconfirmed account and mails the invitation. Succeeds even when the
    /// mail does not go out; check <see cref="InviteResultDto.InviteMailSent"/>.
    /// </summary>
    Task<OperationResult<InviteResultDto>> InviteUserAsync(InviteUserDto inviteUserDto);

    /// <summary>Mails a fresh invitation to an account that has not accepted its first one.</summary>
    Task<OperationResult<Empty>> ResendInviteAsync(Guid userId);

    /// <summary>Updates display name and roles. Rejects a change of email address.</summary>
    Task<OperationResult<Empty>> UpdateUserAsync(Guid userId, UpdateUserDto updateUserDto);

    /// <summary>Disables or re-enables an account by moving its lockout into the far future.</summary>
    Task<OperationResult<Empty>> SetLockoutAsync(Guid callerId, Guid userId, SetLockoutDto setLockoutDto);

    Task<OperationResult<Empty>> DeleteUserAsync(Guid callerId, Guid userId);
}
