using Entities.Email;
using Microsoft.EntityFrameworkCore;

namespace Dal.Repositories.Email;

public sealed class EmailSettingRepository : IEmailSettingRepository
{
    private readonly FileHubContext m_context;

    public EmailSettingRepository(FileHubContext context)
    {
        m_context = context;
    }

    // Ordered by CreatedAt so a second row — which nothing writes, but a restored database could
    // carry — resolves to the oldest one every time rather than to whatever SQLite returns first.
    public Task<EmailSetting?> GetAsync() =>
        m_context.EmailSettings.OrderBy(s => s.CreatedAt).FirstOrDefaultAsync();

    public void Add(EmailSetting setting) => m_context.EmailSettings.Add(setting);

    public Task SaveChangesAsync() => m_context.SaveChangesAsync();
}
