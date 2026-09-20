using Microsoft.EntityFrameworkCore;

namespace PiAiAssistant.Infrastructure.Catalog;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<TagCatalogEntity> Tags => Set<TagCatalogEntity>();
    public DbSet<TagAliasEntityRow> Aliases => Set<TagAliasEntityRow>();
    public DbSet<TagRelationshipEntity> Relationships => Set<TagRelationshipEntity>();
    public DbSet<TagDocumentationEntity> Documentation => Set<TagDocumentationEntity>();
    public DbSet<ChatAuditEntity> ChatAudits => Set<ChatAuditEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TagCatalogEntity>(e =>
        {
            e.ToTable("TagCatalog");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.CanonicalName).IsUnique();
            e.Property(x => x.CanonicalName).HasMaxLength(512).IsRequired();
            e.Property(x => x.PiPointName).HasMaxLength(512);
            e.Property(x => x.AfAttributePath).HasMaxLength(1024);
            e.Property(x => x.PiWebId).HasMaxLength(255);
            e.Property(x => x.ExpectedMin).HasPrecision(18, 4);
            e.Property(x => x.ExpectedMax).HasPrecision(18, 4);
        });

        modelBuilder.Entity<TagAliasEntityRow>(e =>
        {
            e.ToTable("TagAliases");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Alias).IsUnique();
            e.Property(x => x.Alias).HasMaxLength(512).IsRequired();
            e.HasOne(x => x.Tag)
                .WithMany(x => x.Aliases)
                .HasForeignKey(x => x.TagCatalogId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TagRelationshipEntity>(e =>
        {
            e.ToTable("TagRelationships");
            e.HasKey(x => x.Id);
            e.Property(x => x.FromCanonicalName).HasMaxLength(512).IsRequired();
            e.Property(x => x.ToCanonicalName).HasMaxLength(512).IsRequired();
            e.Property(x => x.Relationship).HasMaxLength(200).IsRequired();
        });

        modelBuilder.Entity<TagDocumentationEntity>(e =>
        {
            e.ToTable("TagDocumentation");
            e.HasKey(x => x.Id);
            e.Property(x => x.CanonicalName).HasMaxLength(512).IsRequired();
            e.Property(x => x.Title).HasMaxLength(300).IsRequired();
        });

        modelBuilder.Entity<ChatAuditEntity>(e =>
        {
            e.ToTable("ChatAudit");
            e.HasKey(x => x.Id);
            e.Property(x => x.ConversationId).HasMaxLength(100);
            e.Property(x => x.CorrelationId).HasMaxLength(100);
        });
    }
}

public sealed class TagCatalogEntity
{
    public Guid Id { get; set; }
    public string? PiWebId { get; set; }
    public string? PiPointName { get; set; }
    public string? AfAttributePath { get; set; }
    public required string CanonicalName { get; set; }
    public string? DisplayName { get; set; }
    public string? DescriptionOverride { get; set; }
    public string? EquipmentId { get; set; }
    public string? OwnerTeam { get; set; }
    public string? Criticality { get; set; }
    public decimal? ExpectedMin { get; set; }
    public decimal? ExpectedMax { get; set; }
    public string? Unit { get; set; }
    public bool IsSearchable { get; set; } = true;
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public List<TagAliasEntityRow> Aliases { get; set; } = [];
}

public sealed class TagAliasEntityRow
{
    public Guid Id { get; set; }
    public Guid TagCatalogId { get; set; }
    public required string Alias { get; set; }
    public TagCatalogEntity? Tag { get; set; }
}

public sealed class TagRelationshipEntity
{
    public Guid Id { get; set; }
    public required string FromCanonicalName { get; set; }
    public required string ToCanonicalName { get; set; }
    public required string Relationship { get; set; }
    public string? Description { get; set; }
}

public sealed class TagDocumentationEntity
{
    public Guid Id { get; set; }
    public required string CanonicalName { get; set; }
    public required string Title { get; set; }
    public required string Body { get; set; }
    public string Source { get; set; } = "TagDocumentation";
}

public sealed class ChatAuditEntity
{
    public Guid Id { get; set; }
    public string? ConversationId { get; set; }
    public string? CorrelationId { get; set; }
    public required string UserMessage { get; set; }
    public string? AssistantAnswer { get; set; }
    public string? ResolvedTag { get; set; }
    public string? ToolsUsedJson { get; set; }
    public int DurationMs { get; set; }
    public DateTime CreatedUtc { get; set; }
}
