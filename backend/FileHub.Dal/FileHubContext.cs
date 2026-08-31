using Entities;
using Entities.Account;
using Entities.Email;
using Entities.Groups;
using Entities.Paths;
using Entities.Shares;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Dal;

public class FileHubContext : IdentityDbContext<FileHubUser, IdentityRole<Guid>, Guid>
{
    public FileHubContext(DbContextOptions<FileHubContext> options) : base(options)
    {
    }

    public DbSet<BasePath> BasePaths { get; set; }
    public DbSet<BasePathAccess> BasePathAccesses { get; set; }
    public DbSet<BasePathGroupAccess> BasePathGroupAccesses { get; set; }
    public DbSet<Group> Groups { get; set; }
    public DbSet<GroupMembership> GroupMemberships { get; set; }
    public DbSet<Share> Shares { get; set; }
    public DbSet<EmailSetting> EmailSettings { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<BasePath>(entity =>
        {
            entity.Property(p => p.Path).IsRequired().HasMaxLength(4096);
            entity.Property(p => p.Name).HasMaxLength(200);

            // The same directory twice would give one target two ids, two access lists and two
            // sets of shares.
            entity.HasIndex(p => p.Path).IsUnique();
        });

        builder.Entity<BasePathAccess>(entity =>
        {
            entity.HasIndex(a => new { a.BasePathId, a.UserId }).IsUnique();

            // Both sides cascade: removing a base path or deleting a user takes the grants with it,
            // and a grant carries no state worth keeping once either end is gone.
            entity.HasOne(a => a.BasePath)
                .WithMany(p => p.Access)
                .HasForeignKey(a => a.BasePathId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(a => a.User)
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Group>(entity =>
        {
            // NOCASE so the unique index and every `Name ==` comparison agree, and so that "Family"
            // and "family" are one group rather than two an admin cannot tell apart in a list.
            entity.Property(g => g.Name).IsRequired().HasMaxLength(200).UseCollation("NOCASE");

            entity.HasIndex(g => g.Name).IsUnique();
        });

        builder.Entity<GroupMembership>(entity =>
        {
            entity.HasIndex(m => new { m.GroupId, m.UserId }).IsUnique();

            // Both sides cascade, exactly like BasePathAccess: a membership carries no state worth
            // keeping once either the group or the account is gone.
            entity.HasOne(m => m.Group)
                .WithMany(g => g.Memberships)
                .HasForeignKey(m => m.GroupId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(m => m.User)
                .WithMany()
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<BasePathGroupAccess>(entity =>
        {
            entity.HasIndex(a => new { a.BasePathId, a.GroupId }).IsUnique();

            entity.HasOne(a => a.BasePath)
                .WithMany(p => p.GroupAccess)
                .HasForeignKey(a => a.BasePathId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(a => a.Group)
                .WithMany(g => g.BasePathAccess)
                .HasForeignKey(a => a.GroupId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Share>(entity =>
        {
            entity.Property(s => s.RelativePath).IsRequired().HasMaxLength(4096);
            entity.Ignore(s => s.DownloadLimitReached);

            // Deleting a base path revokes every link into it. This is the whole reason a share
            // stores (base path, relative path) instead of a resolved absolute path.
            entity.HasOne(s => s.BasePath)
                .WithMany(p => p.Shares)
                .HasForeignKey(s => s.BasePathId)
                .OnDelete(DeleteBehavior.Cascade);

            // Deleting the user who created a link revokes it too — an admin removing an account
            // should not leave that account's public links alive.
            entity.HasOne(s => s.CreatedBy)
                .WithMany()
                .HasForeignKey(s => s.CreatedById)
                .OnDelete(DeleteBehavior.Cascade);

            // Cascade rather than the default SET NULL for an optional relationship: a link aimed
            // at a group must not become an anonymous one because the group was deleted. The
            // database enforces it, so no service can forget.
            entity.HasOne(s => s.AudienceGroup)
                .WithMany(g => g.Shares)
                .HasForeignKey(s => s.AudienceGroupId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<EmailSetting>(entity =>
        {
            entity.Property(s => s.SmtpHost).HasMaxLength(255);
            entity.Property(s => s.Username).HasMaxLength(255);
            entity.Property(s => s.FromAddress).HasMaxLength(255);
            entity.Property(s => s.FromName).HasMaxLength(255);
            entity.Property(s => s.SecureSocketOptions).HasMaxLength(32);
        });
    }

    public override int SaveChanges()
    {
        UpdateAuditFields();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateAuditFields();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateAuditFields()
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is not IBaseEntity baseEntity)
            {
                continue;
            }

            var now = DateTime.UtcNow;

            switch (entry.State)
            {
                case EntityState.Added:
                    baseEntity.CreatedAt = now;
                    baseEntity.LastUpdatedAt = now;
                    break;
                case EntityState.Modified:
                    baseEntity.LastUpdatedAt = now;
                    break;
                case EntityState.Detached:
                case EntityState.Unchanged:
                case EntityState.Deleted:
                default:
                    break;
            }
        }
    }
}
