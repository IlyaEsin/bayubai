using Bayubai.Identity.Domain;
using Bayubai.SharedKernel.Consultants;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace Bayubai.Identity.Persistence;

internal sealed class IdentityModuleDbContext(
    DbContextOptions<IdentityModuleDbContext> options,
    ICurrentConsultant currentConsultant)
    : IdentityDbContext<User, IdentityRole<Guid>, Guid>(options), IConsultantScopedDbContext, IDataProtectionKeyContext
{
    public const string Schema = "identity";

    public Guid? CurrentConsultantId => currentConsultant.ConsultantId;

    public DbSet<MagicLinkToken> MagicLinkTokens => Set<MagicLinkToken>();

    public DbSet<Invitation> Invitations => Set<Invitation>();

    public DbSet<ClientLink> ClientLinks => Set<ClientLink>();

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    public static void ConfigureNpgsql(NpgsqlDbContextOptionsBuilder npgsql) =>
        npgsql.UseNodaTime().MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schema);

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema(Schema);

        builder.Entity<User>(user =>
        {
            user.Property(u => u.DisplayName).HasMaxLength(100);
            user.Property(u => u.Language).HasMaxLength(5);
            user.Property(u => u.TimeZone).HasMaxLength(64);
        });

        builder.Entity<IdentityRole<Guid>>().HasData(RoleSeed.Roles);

        builder.Entity<MagicLinkToken>(token =>
        {
            token.ToTable("magic_link_tokens");
            token.Property(t => t.TokenHash).HasMaxLength(64);
            token.Property(t => t.Email).HasMaxLength(256);
            token.Property(t => t.Language).HasMaxLength(5);
            token.Property(t => t.TimeZone).HasMaxLength(64);
            token.Property(t => t.BrowserNonceHash).HasMaxLength(64);
            token.HasIndex(t => t.TokenHash).IsUnique();
            token.HasIndex(t => new { t.Email, t.CreatedAt });
            token.HasOne<User>().WithMany().HasForeignKey(t => t.LinkUserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Invitation>(invitation =>
        {
            invitation.ToTable("invitations");
            invitation.Property(i => i.TokenHash).HasMaxLength(64);
            invitation.HasIndex(i => i.TokenHash).IsUnique();
            invitation.HasOne<User>().WithMany().HasForeignKey(i => i.ConsultantId).OnDelete(DeleteBehavior.Cascade);
            invitation.HasOne<User>().WithMany().HasForeignKey(i => i.AcceptedByUserId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<ClientLink>(link =>
        {
            link.ToTable("client_links");
            link.HasKey(l => new { l.ConsultantId, l.ParentUserId });
            link.HasOne<User>().WithMany().HasForeignKey(l => l.ConsultantId).OnDelete(DeleteBehavior.Cascade);
            link.HasOne<User>().WithMany().HasForeignKey(l => l.ParentUserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<DataProtectionKey>().ToTable("data_protection_keys");

        builder.ApplyConsultantQueryFilters(this);
    }
}
