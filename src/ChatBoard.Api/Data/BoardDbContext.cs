using System.Text.Json;
using ChatBoard.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace ChatBoard.Api.Data;

/// <summary>
/// The persistence boundary for the board. This is the abstraction — there is one
/// implementation and no realistic second, so endpoints talk to it directly and the
/// rules live on the entities.
/// </summary>
public class BoardDbContext : DbContext
{
    public BoardDbContext(DbContextOptions<BoardDbContext> options) : base(options) { }

    public DbSet<Card> Cards => Set<Card>();

    public DbSet<ImportedChat> ImportedChats => Set<ImportedChat>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var card = modelBuilder.Entity<Card>();
        card.HasKey(c => c.Id);
        card.Property(c => c.Title).IsRequired();

        // Persisted as a string so the SQLite file reads by hand.
        card.Property(c => c.Status)
            .HasConversion<string>()
            .IsRequired();

        // Tags stored as a JSON array. The ValueComparer is required for EF to track
        // mutations of the collection correctly (it warns without one).
        var tagsComparer = new ValueComparer<IReadOnlyList<string>>(
            (a, b) => a!.SequenceEqual(b!),
            v => v.Aggregate(0, (hash, s) => HashCode.Combine(hash, s.GetHashCode())),
            v => v.ToList());

        var tags = card.Property(c => c.Tags)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());
        // Tags is a read-only view over the _tags backing field; tell EF to read/write the field.
        tags.Metadata.SetValueComparer(tagsComparer);
        tags.Metadata.SetPropertyAccessMode(PropertyAccessMode.Field);

        // Unique only when present — manual cards may have no chat uuid.
        card.HasIndex(c => c.ChatUuid)
            .IsUnique()
            .HasFilter("[ChatUuid] IS NOT NULL");

        // The board query: cards within a column ordered by position.
        card.HasIndex(c => new { c.Status, c.Position });

        var chat = modelBuilder.Entity<ImportedChat>();
        chat.HasKey(c => c.ChatUuid);
        chat.Property(c => c.Name).IsRequired();
    }
}
