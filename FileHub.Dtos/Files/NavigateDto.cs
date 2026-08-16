using System.ComponentModel.DataAnnotations;

namespace Dtos.Files;

public sealed class NavigateDto
{
    public Guid BasePathId { get; set; }

    /// <summary>
    /// Path below the base path; empty or null means the base path itself. Caller-supplied, so it is
    /// only ever joined through <c>PathSandbox.TryResolve</c>.
    /// </summary>
    [MaxLength(4096)]
    public string Path { get; set; }
}
