using Dal.Repositories.BasePaths;
using Dal.Repositories.Groups;
using Dal.Repositories.Shares;
using Dtos.Groups;
using Entities.Groups;
using FileHub.BusinessLogic.Auditing;
using FileHub.BusinessLogic.Validation;
using Microsoft.Extensions.Logging;
using Shared;

namespace FileHub.BusinessLogic.Services.Groups;

public sealed class GroupService : IGroupService
{
    private readonly ILogger<GroupService> m_logger;
    private readonly IAuditActor m_auditActor;
    private readonly IGroupRepository m_groupRepository;
    private readonly IBasePathRepository m_basePathRepository;
    private readonly IShareRepository m_shareRepository;

    public GroupService(
        ILogger<GroupService> logger,
        IAuditActor auditActor,
        IGroupRepository groupRepository,
        IBasePathRepository basePathRepository,
        IShareRepository shareRepository
    )
    {
        m_logger = logger;
        m_auditActor = auditActor;
        m_groupRepository = groupRepository;
        m_basePathRepository = basePathRepository;
        m_shareRepository = shareRepository;
    }

    public async Task<OperationResult<List<GroupDto>>> ListAsync()
    {
        var groups = await m_groupRepository.ListAsync();
        return OperationResult<List<GroupDto>>.Success(groups.Select(MapGroup).ToList());
    }

    public async Task<OperationResult<GroupDto>> CreateAsync(SaveGroupDto dto)
    {
        var validation = DtoValidator.Validate(dto);
        if (validation.HasError)
        {
            return validation.MapError<GroupDto>();
        }

        var nameResult = await ResolveNameAsync(dto.Name, excludeId: null);
        if (nameResult.HasError)
        {
            return nameResult.MapError<GroupDto>();
        }

        var group = new Group { Name = nameResult.Value };
        m_groupRepository.Add(group);
        await m_groupRepository.SaveChangesAsync();

        // An empty group grants nothing to nobody, so creating one is not by itself a change in
        // what anyone can read.
        m_logger.LogInformation(
            "{Actor:l} created group \"{Name:l}\" ({GroupId})",
            m_auditActor.Describe(), group.Name, group.Id);
        return OperationResult<GroupDto>.Success(MapNewGroup(group));
    }

    public async Task<OperationResult<GroupDto>> RenameAsync(Guid id, SaveGroupDto dto)
    {
        var validation = DtoValidator.Validate(dto);
        if (validation.HasError)
        {
            return validation.MapError<GroupDto>();
        }

        var group = await m_groupRepository.GetAsync(id);
        if (group is null)
        {
            return OperationResult<GroupDto>.NotFound(NotFoundMessage);
        }

        var nameResult = await ResolveNameAsync(dto.Name, excludeId: id);
        if (nameResult.HasError)
        {
            return nameResult.MapError<GroupDto>();
        }

        var previousName = group.Name;
        group.Name = nameResult.Value;
        await m_groupRepository.SaveChangesAsync();

        m_logger.LogInformation(
            "{Actor:l} renamed group {GroupId} from \"{OldName:l}\" to \"{Name:l}\"",
            m_auditActor.Describe(), group.Id, previousName, group.Name);

        var memberCount = (await m_groupRepository.GetMemberIdsAsync(id)).Count;
        var basePathCount = (await m_groupRepository.GetBasePathIdsAsync(id)).Count;

        return OperationResult<GroupDto>.Success(new GroupDto
        {
            Id = group.Id,
            Name = group.Name,
            MemberCount = memberCount,
            BasePathCount = basePathCount,
            CreatedAt = group.CreatedAt
        });
    }

    public async Task<OperationResult<Empty>> DeleteAsync(Guid id)
    {
        var group = await m_groupRepository.GetAsync(id);
        if (group is null)
        {
            return OperationResult<Empty>.NotFound(NotFoundMessage);
        }

        // Nothing survives the group: no base path is still granted through it and nobody is still
        // a member of it, so both pending lists are empty. The links aimed *at* the group are not
        // this call's business — the foreign key cascades them, which is the point of configuring
        // it that way rather than trusting this method to remember.
        var revoked = await m_shareRepository.DeleteSharesLosingGroupAccessAsync(id, [], []);

        m_groupRepository.Remove(group);
        await m_groupRepository.SaveChangesAsync();

        m_logger.LogInformation(
            "{Actor:l} deleted group \"{Name:l}\" ({GroupId}); {ShareCount} link(s) revoked with it",
            m_auditActor.Describe(), group.Name, id, revoked);
        return OperationResult<Empty>.Success();
    }

    public async Task<OperationResult<List<Guid>>> GetMembersAsync(Guid id)
    {
        var group = await m_groupRepository.GetAsync(id);
        if (group is null)
        {
            return OperationResult<List<Guid>>.NotFound(NotFoundMessage);
        }

        return OperationResult<List<Guid>>.Success(await m_groupRepository.GetMemberIdsAsync(id));
    }

    public async Task<OperationResult<Empty>> SetMembersAsync(Guid id, SetGroupMembersDto dto)
    {
        var validation = DtoValidator.Validate(dto);
        if (validation.HasError)
        {
            return validation.MapError<Empty>();
        }

        var group = await m_groupRepository.GetAsync(id);
        if (group is null)
        {
            return OperationResult<Empty>.NotFound(NotFoundMessage);
        }

        // A stale user id would fail on the foreign key at save time and take the whole membership
        // change down with it — same treatment as a stale id in the base-path grant screens.
        var userIds = await m_basePathRepository.FilterExistingUserIdsAsync(dto.UserIds ?? []);

        // Losing a membership loses whatever the group granted, so it revokes links exactly like
        // losing a direct grant does. The base paths the group holds are unchanged; the members
        // after the change are the pending list. Before the save, as everywhere else.
        var basePathIds = await m_groupRepository.GetBasePathIdsAsync(id);
        var revoked = await m_shareRepository.DeleteSharesLosingGroupAccessAsync(id, basePathIds, userIds);

        await m_groupRepository.ReplaceMembersAsync(id, userIds);
        await m_groupRepository.SaveChangesAsync();

        m_logger.LogInformation(
            "{Actor:l} set the members of group \"{Name:l}\" ({GroupId}) to {MemberCount} account(s); "
            + "{ShareCount} link(s) revoked with it",
            m_auditActor.Describe(), group.Name, id, userIds.Count, revoked);
        return OperationResult<Empty>.Success();
    }

    public async Task<OperationResult<List<Guid>>> GetBasePathsAsync(Guid id)
    {
        var group = await m_groupRepository.GetAsync(id);
        if (group is null)
        {
            return OperationResult<List<Guid>>.NotFound(NotFoundMessage);
        }

        return OperationResult<List<Guid>>.Success(await m_groupRepository.GetBasePathIdsAsync(id));
    }

    public async Task<OperationResult<Empty>> SetBasePathsAsync(Guid id, SetGroupBasePathsDto dto)
    {
        var validation = DtoValidator.Validate(dto);
        if (validation.HasError)
        {
            return validation.MapError<Empty>();
        }

        var group = await m_groupRepository.GetAsync(id);
        if (group is null)
        {
            return OperationResult<Empty>.NotFound(NotFoundMessage);
        }

        var known = (await m_basePathRepository.GetAllAsync()).Select(p => p.Id).ToHashSet();
        var basePathIds = (dto.BasePathIds ?? []).Where(known.Contains).ToList();

        // The membership is unchanged, so the members are read as they stand and the base paths
        // after the change are the pending list.
        var memberIds = await m_groupRepository.GetMemberIdsAsync(id);
        var revoked = await m_shareRepository.DeleteSharesLosingGroupAccessAsync(id, basePathIds, memberIds);

        await m_groupRepository.ReplaceBasePathsAsync(id, basePathIds);
        await m_groupRepository.SaveChangesAsync();

        m_logger.LogInformation(
            "{Actor:l} granted group \"{Name:l}\" ({GroupId}) {Count} base path(s); "
            + "{ShareCount} link(s) revoked with it",
            m_auditActor.Describe(), group.Name, id, basePathIds.Count, revoked);
        return OperationResult<Empty>.Success();
    }

    public async Task<OperationResult<List<GroupSummaryDto>>> ListForCallerAsync(Guid userId, bool callerIsAdmin)
    {
        var groups = await m_groupRepository.GetForUserAsync(userId, callerIsAdmin);
        var dtos = groups.Select(g => new GroupSummaryDto { Id = g.Id, Name = g.Name }).ToList();
        return OperationResult<List<GroupSummaryDto>>.Success(dtos);
    }

    /// <summary>
    /// The trimmed name, or a plain 400 when another group already holds it. The unique index would
    /// otherwise surface as a 500 on a mistake an admin makes routinely. A missing or blank name is
    /// already a validation error by then — <c>[Required]</c> trims before it decides.
    /// </summary>
    private async Task<OperationResult<string>> ResolveNameAsync(string name, Guid? excludeId)
    {
        var trimmed = name.Trim();

        if (await m_groupRepository.NameExistsAsync(trimmed, excludeId))
        {
            return OperationResult<string>.BadRequest($"\"{trimmed}\" is already a group");
        }

        return OperationResult<string>.Success(trimmed);
    }

    private static GroupDto MapGroup(GroupWithCounts row) => new()
    {
        Id = row.Group.Id,
        Name = row.Group.Name,
        MemberCount = row.MemberCount,
        BasePathCount = row.BasePathCount,
        CreatedAt = row.Group.CreatedAt
    };

    /// <summary>A group that has just been created holds nothing, so both counts are known to be 0.</summary>
    private static GroupDto MapNewGroup(Group group) => new()
    {
        Id = group.Id,
        Name = group.Name,
        MemberCount = 0,
        BasePathCount = 0,
        CreatedAt = group.CreatedAt
    };

    private const string NotFoundMessage = "Group not found";
}
