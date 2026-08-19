using System.ComponentModel.DataAnnotations;

namespace Dtos.Groups;

/// <summary>Creating a group and renaming one take the same body: a group is only its name.</summary>
public sealed class SaveGroupDto
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; }
}
