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
}
