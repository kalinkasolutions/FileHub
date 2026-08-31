using Dal.Extensions;
using Dal.Repositories.Admin;
using Dal.Repositories.Shares;
using Dtos.Admin;
using Entities.Account;
using FileHub.BusinessLogic.Email;
using FileHub.BusinessLogic.Validation;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Shared;

namespace FileHub.BusinessLogic.Services.Admin;

public sealed class UserAdminService : IUserAdminService
{
    /// <summary>
    /// Refused on every route that could take the last usable admin away. Losing it is not
    /// recoverable from inside the app: there is no registration page and no self-service way back
    /// into the admin area, so the installation would have to be repaired in the database.
    /// </summary>
    private const string LastAdminMessage =
        "This is the last administrator who can sign in. Give another account the Admin role first.";

    /// <summary>
    /// Serialises every operation that can take an admin away. The last-admin guard reads the
    /// admins in one step and writes in another, so two requests that interleave between the two —
    /// a self-demotion and a delete of the other admin — each still see the other's admin and both
    /// go through, leaving none. That is not repairable from inside the app.
    ///
    /// This is a process-wide lock, not a distributed one: FileHub is a single container over a
    /// single SQLite file, which is the only reason it is enough. Running two instances against one
    /// database would need the check and the write inside one serialisable transaction instead.
    /// </summary>
    private static readonly SemaphoreSlim s_adminMutationLock = new(1, 1);

    private readonly ILogger<UserAdminService> m_logger;
    private readonly IUserAdminRepository m_userAdminRepository;
    private readonly IShareRepository m_shareRepository;
    private readonly UserManager<FileHubUser> m_userManager;
    private readonly IEmailService m_emailService;

    public UserAdminService(
        ILogger<UserAdminService> logger,
        IUserAdminRepository userAdminRepository,
        IShareRepository shareRepository,
        UserManager<FileHubUser> userManager,
        IEmailService emailService
    )
    {
        m_logger = logger;
        m_userAdminRepository = userAdminRepository;
        m_shareRepository = shareRepository;
        m_userManager = userManager;
        m_emailService = emailService;
    }

    public async Task<OperationResult<UserDto[]>> ListUsersAsync()
    {
        var users = await m_userAdminRepository.ListUsersWithRolesAsync();
        var grantCounts = await m_userAdminRepository.CountBasePathGrantsPerUserAsync();
        var now = DateTimeOffset.UtcNow;

        return OperationResult<UserDto[]>.Success(
            users.Select(u => ToDto(u, now, grantCounts.GetValueOrDefault(u.User.Id))).ToArray());
    }

    public async Task<OperationResult<InviteResultDto>> InviteUserAsync(InviteUserDto inviteUserDto)
    {
        var validation = DtoValidator.Validate(inviteUserDto);
        if (!validation.IsSuccess)
        {
            return validation.MapError<InviteResultDto>();
        }

        var roles = ResolveRoles(inviteUserDto.Roles);
        if (!roles.IsSuccess)
        {
            return roles.MapError<InviteResultDto>();
        }

        var username = inviteUserDto.Username.Trim();
        var email = inviteUserDto.Email.Trim();

        if (!EmailAddressCheck.IsSendable(email))
        {
            // [EmailAddress] lets through addresses MimeKit refuses to parse. Catching them here,
            // before the account row exists, is what keeps the invite path from leaving an account
            // whose invitation can never be sent and whose address an admin cannot correct.
            return OperationResult<InviteResultDto>.Validation(new Dictionary<string, string[]>
            {
                [nameof(InviteUserDto.Email)] = ["This is not an address email can be sent to."]
            });
        }

        var user = new FileHubUser
        {
            UserName = username,
            Email = email,
            // No password and no confirmed address: the invitation link supplies both. Until it is
            // followed the account cannot sign in, which is exactly what "invited" means.
            EmailConfirmed = false,
            MustChangePassword = false
        };

        var created = await m_userManager.CreateAsync(user);
        if (!created.Succeeded)
        {
            m_logger.LogInformation("Inviting <{Email}> failed: {Error}", email, created.ToErrorString());
            return OperationResult<InviteResultDto>.BadRequest(created.ToErrorString());
        }

        var roled = await m_userManager.AddToRolesAsync(user, roles.Value);
        if (!roled.Succeeded)
        {
            // A roleless account can sign in and reach nothing; don't leave one lying around for an
            // admin to puzzle over.
            await m_userManager.DeleteAsync(user);
            m_logger.LogError("Assigning roles to the new account <{Email}> failed: {Error}", email, roled.ToErrorString());
            return OperationResult<InviteResultDto>.Error("The account could not be given its roles.");
        }

        var token = await m_userManager.GenerateEmailConfirmationTokenAsync(user);
        var mail = await m_emailService.SendInviteMailAsync(email, user.Id, token);

        if (!mail.IsSuccess)
        {
            // The account is real either way. A failed delivery is an SMTP problem the admin fixes
            // and then resends from, not a reason to unwind a created account.
            m_logger.LogWarning(
                "Invited user {UserId} <{Email}> but the invitation email could not be sent: {Error}",
                user.Id, email, mail.ErrorMessage);
        }

        m_logger.LogInformation(
            "Invited user {UserId} <{Email}> with roles {Roles}", user.Id, email, string.Join(", ", roles.Value));

        return OperationResult<InviteResultDto>.Success(new InviteResultDto
        {
            UserId = user.Id,
            InviteMailSent = mail.IsSuccess,
            InviteMailError = mail.IsSuccess ? string.Empty : mail.ErrorMessage
        });
    }

    public async Task<OperationResult<Empty>> ResendInviteAsync(Guid userId)
    {
        var user = await m_userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return OperationResult<Empty>.NotFound("No such user.");
        }

        if (user.EmailConfirmed)
        {
            return OperationResult<Empty>.BadRequest("This account has already accepted its invitation.");
        }

        var token = await m_userManager.GenerateEmailConfirmationTokenAsync(user);
        var mail = await m_emailService.SendInviteMailAsync(user.Email!, user.Id, token);

        if (!mail.IsSuccess)
        {
            // Unlike the invite itself, sending the mail *is* the whole operation here, so a
            // delivery failure is the result.
            m_logger.LogWarning(
                "Could not re-send the invitation for user {UserId}: {Error}", user.Id, mail.ErrorMessage);
            return mail;
        }

        m_logger.LogInformation("Re-sent the invitation for user {UserId}", user.Id);
        return OperationResult<Empty>.Success();
    }

    public async Task<OperationResult<Empty>> UpdateUserAsync(Guid userId, UpdateUserDto updateUserDto)
    {
        var validation = DtoValidator.Validate(updateUserDto);
        if (!validation.IsSuccess)
        {
            return validation;
        }

        // A role change can drop the Admin role, so it runs under the same lock as lockout and
        // delete; see s_adminMutationLock.
        await s_adminMutationLock.WaitAsync();

        try
        {
            return await UpdateUserCoreAsync(userId, updateUserDto);
        }
        finally
        {
            s_adminMutationLock.Release();
        }
    }

    private async Task<OperationResult<Empty>> UpdateUserCoreAsync(Guid userId, UpdateUserDto updateUserDto)
    {
        var user = await m_userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return OperationResult<Empty>.NotFound("No such user.");
        }

        var email = updateUserDto.Email.Trim();
        if (!string.Equals(email, user.Email, StringComparison.OrdinalIgnoreCase))
        {
            // Moving an account to a new address means proving the new address can be read, which
            // only the account holder can do. Half-doing it here — writing the address and marking
            // it unconfirmed — would lock the user out of an account they could still use.
            return OperationResult<Empty>.BadRequest(
                "An email address cannot be changed here. The user changes it from their own account screen, "
                + "where the new address is confirmed before it takes effect.");
        }

        var roles = ResolveRoles(updateUserDto.Roles);
        if (!roles.IsSuccess)
        {
            return roles.MapError<Empty>();
        }

        var currentRoles = await m_userManager.GetRolesAsync(user);
        var keepsAdmin = roles.Value.Contains(Roles.Admin, StringComparer.Ordinal);

        if (!keepsAdmin && await IsLastActiveAdminAsync(user))
        {
            return OperationResult<Empty>.BadRequest(LastAdminMessage);
        }

        var username = updateUserDto.Username.Trim();
        if (!string.Equals(username, user.UserName, StringComparison.Ordinal))
        {
            // This rotates the target user's security stamp and so signs *them* out everywhere. The
            // caller is a different account, so there is no cookie of our own to refresh.
            var renamed = await m_userManager.SetUserNameAsync(user, username);
            if (!renamed.Succeeded)
            {
                return OperationResult<Empty>.BadRequest(renamed.ToErrorString());
            }
        }

        // Losing the right to publish takes the published links with it, the same way losing a
        // base-path grant does. It happens before the role is written, so a failure here over-revokes
        // rather than leaving live anonymous URLs behind an account that can no longer make them —
        // the redemption path carries no permission lookup that could catch it later.
        //
        // Both routes to the right count: the CreateShares role, and the Admin role that implies it.
        // A demoted admin is the case this exists for — every link they published under the admin
        // wildcard points at a base path they may now have no grant to at all.
        if (Roles.CanCreateShares(currentRoles) && !Roles.CanCreateShares(roles.Value))
        {
            var revoked = await m_shareRepository.DeleteAllSharesOfUserAsync(user.Id);

            if (revoked > 0)
            {
                m_logger.LogInformation(
                    "Revoked {Count} share link(s) of user {UserId}, who can no longer create them",
                    revoked, user.Id);
            }
        }

        var toRemove = currentRoles.Except(roles.Value, StringComparer.Ordinal).ToArray();
        if (toRemove.Length > 0)
        {
            var removed = await m_userManager.RemoveFromRolesAsync(user, toRemove);
            if (!removed.Succeeded)
            {
                return OperationResult<Empty>.BadRequest(removed.ToErrorString());
            }
        }

        var toAdd = roles.Value.Except(currentRoles, StringComparer.Ordinal).ToArray();
        if (toAdd.Length > 0)
        {
            var added = await m_userManager.AddToRolesAsync(user, toAdd);
            if (!added.Succeeded)
            {
                return OperationResult<Empty>.BadRequest(added.ToErrorString());
            }
        }

        if (toRemove.Length > 0 || toAdd.Length > 0)
        {
            // Roles are baked into the sign-in cookie, so a session that is already open keeps the
            // Admin role it was issued with until it signs in again. Rotating the security stamp is
            // what ends those sessions, within SecurityStampValidator's interval.
            var stamped = await m_userManager.UpdateSecurityStampAsync(user);
            if (!stamped.Succeeded)
            {
                m_logger.LogError(
                    "Changed the roles of user {UserId} but could not rotate their security stamp: {Error}",
                    user.Id, stamped.ToErrorString());

                return OperationResult<Empty>.Error(
                    "The roles were changed, but the user's existing sessions could not be ended. Try again.");
            }
        }

        m_logger.LogInformation(
            "Updated user {UserId}: name \"{Username}\", roles {Roles}", user.Id, username, string.Join(", ", roles.Value));

        return OperationResult<Empty>.Success();
    }

    public async Task<OperationResult<Empty>> SetLockoutAsync(Guid callerId, Guid userId, SetLockoutDto setLockoutDto)
    {
        var validation = DtoValidator.Validate(setLockoutDto);
        if (!validation.IsSuccess)
        {
            return validation;
        }

        if (setLockoutDto.Locked && callerId == userId)
        {
            return OperationResult<Empty>.BadRequest("You cannot disable your own account.");
        }

        await s_adminMutationLock.WaitAsync();

        try
        {
            return await SetLockoutCoreAsync(userId, setLockoutDto);
        }
        finally
        {
            s_adminMutationLock.Release();
        }
    }

    private async Task<OperationResult<Empty>> SetLockoutCoreAsync(Guid userId, SetLockoutDto setLockoutDto)
    {
        var user = await m_userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return OperationResult<Empty>.NotFound("No such user.");
        }

        if (setLockoutDto.Locked && await IsLastActiveAdminAsync(user))
        {
            return OperationResult<Empty>.BadRequest(LastAdminMessage);
        }

        if (setLockoutDto.Locked)
        {
            return await LockAsync(user);
        }

        return await UnlockAsync(user);
    }

    public async Task<OperationResult<Empty>> DeleteUserAsync(Guid callerId, Guid userId)
    {
        if (callerId == userId)
        {
            return OperationResult<Empty>.BadRequest("You cannot delete your own account from the user list.");
        }

        await s_adminMutationLock.WaitAsync();

        try
        {
            return await DeleteUserCoreAsync(userId);
        }
        finally
        {
            s_adminMutationLock.Release();
        }
    }

    private async Task<OperationResult<Empty>> DeleteUserCoreAsync(Guid userId)
    {
        var user = await m_userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return OperationResult<Empty>.NotFound("No such user.");
        }

        if (await IsLastActiveAdminAsync(user))
        {
            return OperationResult<Empty>.BadRequest(LastAdminMessage);
        }

        // The user's base-path grants and the share links they created are ON DELETE CASCADE (see
        // FileHubContext), so removing the row takes them with it.
        var deleted = await m_userManager.DeleteAsync(user);
        if (!deleted.Succeeded)
        {
            m_logger.LogError("Deleting user {UserId} failed: {Error}", user.Id, deleted.ToErrorString());
            return OperationResult<Empty>.Error("The account could not be deleted.");
        }

        m_logger.LogInformation("Deleted user {UserId} <{Email}>", user.Id, user.Email);
        return OperationResult<Empty>.Success();
    }

    private async Task<OperationResult<Empty>> LockAsync(FileHubUser user)
    {
        // Identity refuses to set an end date while lockout is off for the account, and an account
        // created before that default was in place would have it off.
        var enabled = await m_userManager.SetLockoutEnabledAsync(user, true);
        if (!enabled.Succeeded)
        {
            return OperationResult<Empty>.BadRequest(enabled.ToErrorString());
        }

        // Far future rather than DateTimeOffset.MaxValue: the max value round-trips badly through
        // time-zone conversion, and a century is indistinguishable from forever here.
        var result = await m_userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));
        if (!result.Succeeded)
        {
            return OperationResult<Empty>.BadRequest(result.ToErrorString());
        }

        // Lockout is only consulted at sign-in, so on its own it refuses new logins and leaves every
        // session the account already has alive — for the 30 days the cookie is good for. Rotating
        // the security stamp is what makes SecurityStampValidator throw those cookies out, within
        // the one-minute interval Program.cs configures. Disabling an account has to end it.
        var stamped = await m_userManager.UpdateSecurityStampAsync(user);
        if (!stamped.Succeeded)
        {
            m_logger.LogError(
                "Disabled user {UserId} but could not rotate their security stamp: {Error}",
                user.Id, stamped.ToErrorString());

            return OperationResult<Empty>.Error(
                "The account was disabled, but its existing sessions could not be ended. Try again.");
        }

        m_logger.LogInformation("Disabled user {UserId}", user.Id);
        return OperationResult<Empty>.Success();
    }

    private async Task<OperationResult<Empty>> UnlockAsync(FileHubUser user)
    {
        var result = await m_userManager.SetLockoutEndDateAsync(user, null);
        if (!result.Succeeded)
        {
            return OperationResult<Empty>.BadRequest(result.ToErrorString());
        }

        // Clear the failed sign-in counter too, otherwise the automatic lockout from wrong passwords
        // snaps back after a single further mistake.
        await m_userManager.ResetAccessFailedCountAsync(user);

        m_logger.LogInformation("Enabled user {UserId}", user.Id);
        return OperationResult<Empty>.Success();
    }

    /// <summary>
    /// True when <paramref name="user"/> holds the Admin role and no other account with that role
    /// can currently sign in. Every route that could take an admin away — delete, lockout and a
    /// role change that drops Admin — asks this before acting.
    /// </summary>
    private async Task<bool> IsLastActiveAdminAsync(FileHubUser user)
    {
        var isAdmin = await m_userManager.IsInRoleAsync(user, Roles.Admin);
        if (!isAdmin)
        {
            return false;
        }

        var activeAdminIds = await m_userAdminRepository.ListActiveUserIdsInRoleAsync(Roles.Admin);
        return !activeAdminIds.Any(id => id != user.Id);
    }

    /// <summary>
    /// Maps the requested role names onto the fixed set, rejecting anything unknown, and always
    /// includes <see cref="Roles.User"/> — that role is what browsing checks for, so an account
    /// without it could sign in and see nothing. <see cref="Roles.CreateShares"/> is not implied by
    /// anything and is not added here: it is granted account by account, and an omission means no.
    /// </summary>
    private static OperationResult<string[]> ResolveRoles(string[] requestedRoles)
    {
        var roles = new List<string> { Roles.User };

        foreach (var requestedRole in requestedRoles)
        {
            var known = Roles.All.FirstOrDefault(
                r => string.Equals(r, requestedRole?.Trim(), StringComparison.OrdinalIgnoreCase));

            if (known is null)
            {
                return OperationResult<string[]>.BadRequest($"Unknown role \"{requestedRole}\".");
            }

            if (roles.Contains(known, StringComparer.Ordinal))
            {
                continue;
            }

            roles.Add(known);
        }

        return OperationResult<string[]>.Success(roles.ToArray());
    }

    private static UserDto ToDto(UserWithRoles userWithRoles, DateTimeOffset now, int basePathCount)
    {
        var user = userWithRoles.User;

        return new UserDto
        {
            Id = user.Id,
            Username = user.UserName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            EmailConfirmed = user.EmailConfirmed,
            Roles = userWithRoles.Roles,
            MustChangePassword = user.MustChangePassword,
            IsLockedOut = user.LockoutEnd > now,
            BasePathCount = basePathCount,
            CreatedAt = user.CreatedAt
        };
    }
}
