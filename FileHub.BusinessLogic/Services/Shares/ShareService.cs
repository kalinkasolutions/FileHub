using Dal.Repositories.BasePaths;
using Dal.Repositories.Groups;
using Dal.Repositories.Shares;
using Dtos.Shares;
using Entities.Groups;
using Entities.Shares;
using FileHub.BusinessLogic.Authorization;
using FileHub.BusinessLogic.Validation;
using Microsoft.Extensions.Logging;
using Shared;

namespace FileHub.BusinessLogic.Services.Shares;

public sealed class ShareService : IShareService
{
    private readonly ILogger<ShareService> m_logger;
    private readonly IShareRepository m_shareRepository;
    private readonly IBasePathRepository m_basePathRepository;
    private readonly IGroupRepository m_groupRepository;

    public ShareService(
        ILogger<ShareService> logger,
        IShareRepository shareRepository,
        IBasePathRepository basePathRepository,
        IGroupRepository groupRepository
    )
    {
        m_logger = logger;
        m_shareRepository = shareRepository;
        m_basePathRepository = basePathRepository;
        m_groupRepository = groupRepository;
    }

    public async Task<OperationResult<ShareDto>> CreateAsync(
        Guid userId, bool callerIsAdmin, bool callerCanCreateShares, CreateShareDto dto)
    {
        var validation = DtoValidator.Validate(dto);
        if (validation.HasError)
        {
            return validation.MapError<ShareDto>();
        }

        // Publishing is its own permission. Being able to browse a base path settles what the caller
        // may read; it does not settle whether they may put an anonymous URL to it into the world.
        // Refused before anything else is looked at, so a caller without the role learns nothing
        // about the base path or the path they named.
        if (!callerIsAdmin && !callerCanCreateShares)
        {
            m_logger.LogWarning(
                "User {UserId} tried to create a link without the {Role} role", userId, Roles.CreateShares);
            return OperationResult<ShareDto>.Forbidden(CreateRefused);
        }

        // Who the link is for. Null is the default and means anonymous by URL. A caller may only
        // aim a link at a group they belong to; an admin may aim one at any. A group that does not
        // exist is refused with the same message as one the caller is not in, so the error cannot
        // be used to find out which groups there are.
        Group? audienceGroup = null;

        if (dto.AudienceGroupId is not null)
        {
            audienceGroup = await m_groupRepository.GetAsync(dto.AudienceGroupId.Value);

            if (audienceGroup is null)
            {
                return OperationResult<ShareDto>.BadRequest(AudienceRefused);
            }

            if (!callerIsAdmin && !await m_groupRepository.IsMemberAsync(audienceGroup.Id, userId))
            {
                m_logger.LogWarning(
                    "User {UserId} tried to aim a link at group {GroupId} without belonging to it",
                    userId, audienceGroup.Id);
                return OperationResult<ShareDto>.BadRequest(AudienceRefused);
            }
        }

        var basePath = await m_basePathRepository.GetForUserAsync(dto.BasePathId, userId, callerIsAdmin);
        if (basePath is null)
        {
            m_logger.LogWarning(
                "User {UserId} tried to share base path {BasePathId} without access", userId, dto.BasePathId);
            return OperationResult<ShareDto>.NotFound("Base path not found");
        }

        var relativePath = dto.RelativePath ?? string.Empty;

        if (!PathSandbox.TryResolve(basePath.Path, relativePath, out var fullPath))
        {
            m_logger.LogWarning(
                "User {UserId} tried to share outside base path {BasePathId} with \"{RelativePath}\"",
                userId, basePath.Id, relativePath);
            return OperationResult<ShareDto>.NotFound("Path not found");
        }

        var isDirectory = Directory.Exists(fullPath);
        if (!isDirectory && !File.Exists(fullPath))
        {
            return OperationResult<ShareDto>.NotFound("Path not found");
        }

        // The one place the tree is ever walked. This route is authenticated and rate-limited by
        // being a deliberate user action; the public routes are neither, so a walk there would be
        // free IO amplification for anyone holding a link.
        var size = MeasureSize(fullPath, isDirectory);

        var share = new Share
        {
            BasePathId = basePath.Id,
            // Stored back in the sandbox's own normal form rather than as the caller typed it, so
            // the link re-resolves to the same file however the request was spelled.
            RelativePath = PathSandbox.ToRelative(basePath.Path, fullPath),
            MaxDownloadCount = dto.MaxDownloadCount,
            Size = size,
            AudienceGroupId = audienceGroup?.Id,
            CreatedById = userId
        };

        m_shareRepository.Add(share);
        await m_shareRepository.SaveChangesAsync();

        m_logger.LogInformation(
            "User {UserId} shared {Path} as {ShareId} ({Size} bytes)", userId, fullPath, share.Id, size);

        return OperationResult<ShareDto>.Success(
            MapShare(share, basePath.Name, isDirectory, audienceGroup?.Name));
    }

    public async Task<OperationResult<List<ShareDto>>> ListForUserAsync(Guid userId)
    {
        var shares = await m_shareRepository.GetByCreatorAsync(userId);
        var dtos = shares.Select(s => MapShare(s, s.BasePath.Name, IsDirectory(s), s.AudienceGroup?.Name)).ToList();
        return OperationResult<List<ShareDto>>.Success(dtos);
    }

    public async Task<OperationResult<List<AdminShareDto>>> ListAllAsync()
    {
        var shares = await m_shareRepository.GetAllAsync();
        var dtos = shares.Select(MapAdminShare).ToList();
        return OperationResult<List<AdminShareDto>>.Success(dtos);
    }

    public async Task<OperationResult<Empty>> DeleteAsync(Guid callerId, bool callerIsAdmin, Guid shareId)
    {
        var share = await m_shareRepository.GetAsync(shareId);
        if (share is null)
        {
            return OperationResult<Empty>.NotFound("Share not found");
        }

        if (!callerIsAdmin && share.CreatedById != callerId)
        {
            return OperationResult<Empty>.Forbidden("Only the creator or an admin can revoke this link");
        }

        m_shareRepository.Remove(share);
        await m_shareRepository.SaveChangesAsync();

        m_logger.LogInformation("User {UserId} revoked share {ShareId}", callerId, shareId);
        return OperationResult<Empty>.Success();
    }

    public async Task<OperationResult<ResolvedShare>> ResolvePublicAsync(
        Guid shareId, Guid? callerId, bool callerIsAdmin)
    {
        var share = await m_shareRepository.GetAsync(shareId);

        if (share is null)
        {
            return OperationResult<ResolvedShare>.NotFound(PublicFailure);
        }

        if (!await IsInAudienceAsync(share, callerId, callerIsAdmin))
        {
            // The same answer an unknown id gets: "this link is not for you" and "no such link"
            // must be indistinguishable, or the response tells a stranger which links exist and
            // who they are for. The Open Graph page falls back to its generic body for the same
            // reason — the chat client unfurling a link is never signed in.
            m_logger.LogInformation("Share {ShareId} was not resolved: the caller is not in its audience", shareId);
            return OperationResult<ResolvedShare>.NotFound(PublicFailure);
        }

        if (share.DownloadLimitReached)
        {
            m_logger.LogInformation("Share {ShareId} has reached its download limit", shareId);
            return OperationResult<ResolvedShare>.NotFound(PublicFailure);
        }

        // Re-resolved on every hit rather than trusting a path stored at creation time: the base
        // path may have been repointed since, and the target may have become a symlink out.
        if (!PathSandbox.TryResolve(share.BasePath.Path, share.RelativePath, out var fullPath))
        {
            m_logger.LogWarning("Share {ShareId} no longer resolves inside its base path", shareId);
            return OperationResult<ResolvedShare>.NotFound(PublicFailure);
        }

        var isDirectory = Directory.Exists(fullPath);
        if (!isDirectory && !File.Exists(fullPath))
        {
            return OperationResult<ResolvedShare>.NotFound(PublicFailure);
        }

        var resolved = new ResolvedShare
        {
            Id = share.Id,
            Name = TargetName(share, share.BasePath.Name),
            FullPath = fullPath,
            IsDirectory = isDirectory,
            // The stored measurement, never a fresh walk.
            Size = share.Size
        };

        return OperationResult<ResolvedShare>.Success(resolved);
    }

    public async Task<OperationResult<Empty>> RegisterDownloadAsync(
        Guid shareId, Guid? callerId, bool callerIsAdmin)
    {
        // The limit is enforced here, not by the check in ResolvePublicAsync — that one only keeps
        // an exhausted link from rendering a landing page. A conditional UPDATE is what makes the
        // limit hold when several anonymous callers arrive at once, all having read the same count.
        if (!await m_shareRepository.TryRegisterDownloadAsync(shareId, callerId, callerIsAdmin))
        {
            m_logger.LogInformation("Share {ShareId} was not downloaded: unknown or at its limit", shareId);
            return OperationResult<Empty>.NotFound(PublicFailure);
        }

        return OperationResult<Empty>.Success();
    }

    private static bool IsDirectory(Share share) =>
        PathSandbox.TryResolve(share.BasePath.Path, share.RelativePath, out var fullPath)
        && Directory.Exists(fullPath);

    /// <summary>
    /// Total bytes below the target. Symlinks are skipped rather than followed: one pointing out of
    /// the base path would count foreign data, and one pointing back in would count it twice.
    /// </summary>
    private static long MeasureSize(string fullPath, bool isDirectory)
    {
        if (!isDirectory)
        {
            return FileLength(fullPath);
        }

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint
        };

        long total = 0;

        foreach (var file in Directory.EnumerateFiles(fullPath, "*", options))
        {
            total += FileLength(file);
        }

        return total;
    }

    private static long FileLength(string path)
    {
        try
        {
            return new FileInfo(path).Length;
        }
        catch (IOException)
        {
            return 0;
        }
        catch (UnauthorizedAccessException)
        {
            return 0;
        }
    }

    private static string TargetName(Share share, string basePathName) =>
        share.RelativePath.Length == 0 ? basePathName : Path.GetFileName(share.RelativePath);

    private static ShareDto MapShare(
        Share share, string basePathName, bool isDirectory, string? audienceGroupName) => new()
    {
        Id = share.Id,
        Name = TargetName(share, basePathName),
        BasePathId = share.BasePathId,
        RelativePath = share.RelativePath,
        IsDir = isDirectory,
        Size = share.Size,
        DownloadCount = share.DownloadCount,
        MaxDownloadCount = share.MaxDownloadCount,
        CreatedAt = share.CreatedAt,
        AudienceGroupId = share.AudienceGroupId,
        AudienceGroupName = audienceGroupName,
        Link = string.Empty
    };

    private static AdminShareDto MapAdminShare(Share share) => new()
    {
        Id = share.Id,
        Name = TargetName(share, share.BasePath.Name),
        BasePathId = share.BasePathId,
        BasePathName = share.BasePath.Name,
        RelativePath = share.RelativePath,
        IsDir = IsDirectory(share),
        Size = share.Size,
        DownloadCount = share.DownloadCount,
        MaxDownloadCount = share.MaxDownloadCount,
        CreatedAt = share.CreatedAt,
        CreatedById = share.CreatedById,
        CreatedBy = share.CreatedBy?.Email ?? string.Empty,
        AudienceGroupId = share.AudienceGroupId,
        AudienceGroupName = share.AudienceGroup?.Name,
        Link = string.Empty
    };

    /// <summary>
    /// Whether this caller may redeem the link. A link with no audience — today's anonymous-by-URL
    /// one, and still the default — costs no query at all, which is what keeps the anonymous routes
    /// as cheap as they were.
    /// </summary>
    private async Task<bool> IsInAudienceAsync(Share share, Guid? callerId, bool callerIsAdmin)
    {
        if (share.AudienceGroupId is null)
        {
            return true;
        }

        if (callerIsAdmin)
        {
            return true;
        }

        if (callerId is null)
        {
            return false;
        }

        return await m_groupRepository.IsMemberAsync(share.AudienceGroupId.Value, callerId.Value);
    }

    /// <summary>One message for every way a public link can fail, so the response cannot be used to
    /// tell an unknown id from an exhausted one, or from one aimed at somebody else.</summary>
    private const string PublicFailure = "Share not found";

    /// <summary>One message for both ways aiming a link at a group can be refused, so it cannot be
    /// used to tell a group that does not exist from one the caller is not in.</summary>
    private const string AudienceRefused = "You can only share with a group you belong to";

    /// <summary>Shown to a user who can browse but not publish. It names what is missing rather than
    /// pretending the path is gone, because the caller can see the file in the listing either
    /// way.</summary>
    private const string CreateRefused = "Your account is not allowed to create share links";
}
