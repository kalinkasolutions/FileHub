using Entities.Email;

namespace Dal.Repositories.Email;

/// <summary>
/// Pure data access for the single <see cref="EmailSetting"/> row. Seeding it from configuration,
/// encrypting the password and deciding what an admin may see live in the business layer.
/// </summary>
public interface IEmailSettingRepository
{
    /// <summary>The one settings row, or null while the install has never had one written.</summary>
    Task<EmailSetting?> GetAsync();

    void Add(EmailSetting setting);

    Task SaveChangesAsync();
}
