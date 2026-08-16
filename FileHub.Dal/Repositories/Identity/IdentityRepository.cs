using Dal.Extensions;
using Entities.Account;
using Microsoft.AspNetCore.Identity;
using Shared;

namespace Dal.Repositories.Identity;

public sealed class IdentityRepository : IIdentityRepository
{
    private readonly UserManager<FileHubUser> m_userManager;

    public IdentityRepository(
        UserManager<FileHubUser> userManager
    )
    {
        m_userManager = userManager;
    }

    public Task<FileHubUser?> FindByEmailAsync(string email)
    {
        return m_userManager.FindByEmailAsync(email);
    }

    public Task<FileHubUser?> FindByIdAsync(Guid userId)
    {
        return m_userManager.FindByIdAsync(userId.ToString());
    }

    public async Task<OperationResult<Empty>> ConfirmEmailAsync(FileHubUser user, string token)
    {
        var result = await m_userManager.ConfirmEmailAsync(user, token);
        return result.Succeeded
            ? OperationResult<Empty>.Success()
            : OperationResult<Empty>.BadRequest(result.ToErrorString());
    }

    public async Task<OperationResult<Empty>> SetFirstPasswordAsync(FileHubUser user, string password)
    {
        // AddPasswordAsync fails outright when a hash is already stored, and the caller has just
        // proven ownership of the address with the invitation token, so replacing it is safe.
        if (await m_userManager.HasPasswordAsync(user))
        {
            var removed = await m_userManager.RemovePasswordAsync(user);
            if (!removed.Succeeded)
            {
                return OperationResult<Empty>.BadRequest(removed.ToErrorString());
            }
        }

        var added = await m_userManager.AddPasswordAsync(user, password);
        return added.Succeeded
            ? OperationResult<Empty>.Success()
            : OperationResult<Empty>.BadRequest(added.ToErrorString());
    }

    public async Task<OperationResult<Empty>> SetUsernameAsync(FileHubUser user, string username)
    {
        var result = await m_userManager.SetUserNameAsync(user, username);
        return result.Succeeded
            ? OperationResult<Empty>.Success()
            : OperationResult<Empty>.BadRequest(result.ToErrorString());
    }

    public async Task<OperationResult<Empty>> ClearMustChangePasswordAsync(FileHubUser user)
    {
        if (!user.MustChangePassword)
        {
            return OperationResult<Empty>.Success();
        }

        user.MustChangePassword = false;
        var result = await m_userManager.UpdateAsync(user);
        return result.Succeeded
            ? OperationResult<Empty>.Success()
            : OperationResult<Empty>.Error(result.ToErrorString());
    }

    public async Task<OperationResult<Empty>> ChangeEmailAsync(FileHubUser user, string newEmail, string token)
    {
        var result = await m_userManager.ChangeEmailAsync(user, newEmail, token);
        return result.Succeeded
            ? OperationResult<Empty>.Success()
            : OperationResult<Empty>.BadRequest(result.ToErrorString());
    }

    public Task<string> GeneratePasswordResetTokenAsync(FileHubUser user)
    {
        return m_userManager.GeneratePasswordResetTokenAsync(user);
    }

    public async Task<OperationResult<Empty>> ResetPasswordAsync(FileHubUser user, string token, string newPassword)
    {
        var result = await m_userManager.ResetPasswordAsync(user, token, newPassword);
        return result.Succeeded
            ? OperationResult<Empty>.Success()
            : OperationResult<Empty>.BadRequest(result.ToErrorString());
    }
}
