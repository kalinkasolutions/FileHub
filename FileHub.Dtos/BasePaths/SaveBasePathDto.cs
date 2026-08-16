using System.ComponentModel.DataAnnotations;

namespace Dtos.BasePaths;

public sealed class SaveBasePathDto
{
    /// <summary>Absolute path on the host. It has to exist and be a directory.</summary>
    [Required]
    [MaxLength(4096)]
    public string Path { get; set; }

    /// <summary>Optional label; the directory name is used when it is empty.</summary>
    [MaxLength(200)]
    public string Name { get; set; }
}
