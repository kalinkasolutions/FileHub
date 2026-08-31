using Dal.Extensions;
using Entities.Account;
using Microsoft.AspNetCore.Identity;
using Shared;

namespace Dal.Repositories.Identity;

public sealed class IdentityRepository : IIdentityRepository
{
    /// <summary>
    /// The account <see cref="VerifyDummyPassword"/> pretends to check. It is never stored and never
    /// looked up; the hasher only needs an instance to hand to its optional rehash callback.
    /// </summary>
    private static readonly FileHubUser s_nobody = new();

    /// <summary>
    /// A real hash to verify against when no account matched. Built once per process — hashing costs
    /// what verifying costs, so doing it per request would make the unknown-address path twice as
    /// expensive as the known one and leak in the other direction. Its own hasher, with the default
    /// options the app never overrides, because the work is set by the iteration count baked into the
    /// hash rather than by whoever verifies it.
    /// </summary>
    private static readonly Lazy<string> s_nobodysPasswordHash =
        new(() => new PasswordHasher<FileHubUser>().HashPassword(s_nobody, "no account has this password"));

    private readonly UserManager<FileHubUser> m_userManager;
    private readonly SignInManager<FileHubUser> m_signInManager;
    private readonly IPasswordHasher<FileHubUser> m_passwordHasher;

    public IdentityRepository(
        UserManager<FileHubUser> userManager,
        SignInManager<FileHubUser> signInManager,
        IPasswordHasher<FileHubUser> passwordHasher
    )
    {
        m_userManager = userManager;
        m_signInManager = signInManager;
        m_passwordHasher = passwordHasher;
    }

    public Task<FileHubUser?> FindByEmailAsync(string email)
    {
        return m_userManager.FindByEmailAsync(email);
    }

    public Task<FileHubUser?> FindByIdAsync(Guid userId)
    {
        return m_userManager.FindByIdAsync(userId.ToString());
    }

    public async Task<SignInOutcome> PasswordSignInAsync(FileHubUser user, string password)
    {
        var result = await m_signInManager.PasswordSignInAsync(
            user,
            password,
            isPersistent: true,
            // This form is on the public internet, so a wrong password has to cost something: enough
            // failures lock the account for the window Identity is configured with.
            lockoutOnFailure: true
        );

        if (result.Succeeded)
        {
            return SignInOutcome.Success;
        }

        if (result.RequiresTwoFactor)
        {
            return SignInOutcome.RequiresTwoFactor;
        }

        if (result.IsLockedOut)
        {
            return SignInOutcome.LockedOut;
        }

        return result.IsNotAllowed ? SignInOutcome.NotAllowed : SignInOutcome.Failed;
    }

    public Task<bool> CheckPasswordAsync(FileHubUser user, string password)
    {
        return m_userManager.CheckPasswordAsync(user, password);
    }

    public Task AccessFailedAsync(FileHubUser user)
    {
        return m_userManager.AccessFailedAsync(user);
    }

    public void VerifyDummyPassword(string password)
    {
        m_passwordHasher.VerifyHashedPassword(s_nobody, s_nobodysPasswordHash.Value, password);
    }

    public async Task<OperationResult<Empty>> ValidatePasswordAsync(FileHubUser user, string password)
    {
        foreach (var validator in m_userManager.PasswordValidators)
        {
            var result = await validator.ValidateAsync(m_userManager, user, password);
            if (!result.Succeeded)
            {
                return OperationResult<Empty>.BadRequest(result.ToErrorString());
            }
        }

        return OperationResult<Empty>.Success();
    }

    public async Task<OperationResult<Empty>> ConfirmEmailAsync(FileHubUser user, string token)
    {
        var result = await m_userManager.ConfirmEmailAsync(user, token);
        return result.Succeeded
            ? OperationResult<Empty>.Success()
            : OperationResult<Empty>.BadRequest(result.ToErrorString());
    }

    public async Task<OperationResult<Empty>> SetEmailUnconfirmedAsync(FileHubUser user)
    {
        user.EmailConfirmed = false;
        var result = await m_userManager.UpdateAsync(user);
        return result.Succeeded
            ? OperationResult<Empty>.Success()
            : OperationResult<Empty>.Error(result.ToErrorString());
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
