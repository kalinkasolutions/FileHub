using Dal.Extensions;
using Entities.Account;
using Microsoft.AspNetCore.Identity;
using Shared;

namespace Dal.Repositories.Account;

public sealed class AccountRepository : IAccountRepository
{
    private readonly UserManager<FileHubUser> m_userManager;

    public AccountRepository(
        UserManager<FileHubUser> userManager
    )
    {
        m_userManager = userManager;
    }

    public Task<FileHubUser?> FindByIdAsync(Guid userId)
    {
        return m_userManager.FindByIdAsync(userId.ToString());
    }

    public Task<FileHubUser?> FindByEmailAsync(string email)
    {
        return m_userManager.FindByEmailAsync(email);
    }

    public Task<bool> CheckPasswordAsync(FileHubUser user, string password)
    {
        return m_userManager.CheckPasswordAsync(user, password);
    }

    public async Task<OperationResult<Empty>> ChangePasswordAsync(FileHubUser user, string currentPassword, string newPassword)
    {
        var result = await m_userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        return result.Succeeded
            ? OperationResult<Empty>.Success()
            : OperationResult<Empty>.BadRequest(result.ToErrorString());
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

    public Task<string> GenerateChangeEmailTokenAsync(FileHubUser user, string newEmail)
    {
        return m_userManager.GenerateChangeEmailTokenAsync(user, newEmail);
    }

    public Task UpdateSecurityStampAsync(FileHubUser user)
    {
        return m_userManager.UpdateSecurityStampAsync(user);
    }

    public Task<string?> GetAuthenticatorKeyAsync(FileHubUser user)
    {
        return m_userManager.GetAuthenticatorKeyAsync(user);
    }

    public async Task<string> ResetAuthenticatorKeyAsync(FileHubUser user)
    {
        await m_userManager.ResetAuthenticatorKeyAsync(user);
        return await m_userManager.GetAuthenticatorKeyAsync(user) ?? string.Empty;
    }

    public Task<bool> VerifyAuthenticatorCodeAsync(FileHubUser user, string code)
    {
        return m_userManager.VerifyTwoFactorTokenAsync(user, m_userManager.Options.Tokens.AuthenticatorTokenProvider, code);
    }

    public async Task<OperationResult<Empty>> SetTwoFactorEnabledAsync(FileHubUser user, bool enabled)
    {
        var result = await m_userManager.SetTwoFactorEnabledAsync(user, enabled);
        return result.Succeeded
            ? OperationResult<Empty>.Success()
            : OperationResult<Empty>.BadRequest(result.ToErrorString());
    }

    public async Task<List<string>> GenerateRecoveryCodesAsync(FileHubUser user, int count)
    {
        var codes = await m_userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, count);
        return codes?.ToList() ?? [];
    }

    public Task<int> CountRecoveryCodesAsync(FileHubUser user)
    {
        return m_userManager.CountRecoveryCodesAsync(user);
    }
}
