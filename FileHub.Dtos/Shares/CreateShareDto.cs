using System.ComponentModel.DataAnnotations;

namespace Dtos.Shares;

public sealed class CreateShareDto
{
    public Guid BasePathId { get; set; }

    /// <summary>Path below the base path; empty shares the base path itself.</summary>
    [MaxLength(4096)]
    public string RelativePath { get; set; }

    /// <summary>0 means unlimited, which is what a link gets unless the creator caps it.</summary>
    [Range(0, int.MaxValue)]
    public int MaxDownloadCount { get; set; }

    /// <summary>
    /// Who the link is for. Null — the default — is today's behaviour: anonymous by URL. Set to a
    /// group, the link only answers a signed-in member of it. Only a member of that group, or an
    /// admin, may aim a link at it.
    /// </summary>
    public Guid? AudienceGroupId { get; set; }
}
