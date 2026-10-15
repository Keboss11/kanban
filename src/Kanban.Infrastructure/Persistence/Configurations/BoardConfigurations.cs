using Kanban.Domain.Activity;
using Kanban.Domain.Boards;
using Kanban.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kanban.Infrastructure.Persistence.Configurations;

internal sealed class BoardConfiguration : IEntityTypeConfiguration<Board>
{
    public void Configure(EntityTypeBuilder<Board> builder)
    {
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever(); // Lo genera el dominio (Guid v7).
        builder.Property(b => b.Name).HasMaxLength(Board.NameMaxLength);
        builder.Property(b => b.Description).HasMaxLength(Board.DescriptionMaxLength);
        builder.HasIndex(b => b.OwnerId);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(b => b.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);

        // La colección pública devuelve una copia ordenada; EF trabaja con el campo privado.
        builder.Ignore(b => b.Columns);
        builder.HasMany<Column>("_columns")
            .WithOne()
            .HasForeignKey(c => c.BoardId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ColumnConfiguration : IEntityTypeConfiguration<Column>
{
    public void Configure(EntityTypeBuilder<Column> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever(); // Lo genera el dominio (Guid v7).
        builder.Property(c => c.Name).HasMaxLength(Column.NameMaxLength);
        builder.Ignore(c => c.IsAtWipLimit);

        builder.Ignore(c => c.Cards);
        builder.HasMany<Card>("_cards")
            .WithOne()
            .HasForeignKey(c => c.ColumnId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class CardConfiguration : IEntityTypeConfiguration<Card>
{
    public void Configure(EntityTypeBuilder<Card> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever(); // Lo genera el dominio (Guid v7).
        builder.Property(c => c.Title).HasMaxLength(Card.TitleMaxLength);
        builder.Property(c => c.Description).HasMaxLength(Card.DescriptionMaxLength);
        // List<string> se guarda como columna JSON (colección primitiva de EF Core 8+).
        builder.Property(c => c.Labels);
        builder.HasIndex(c => new { c.ColumnId, c.Position });
    }
}

internal sealed class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever(); // Lo genera el dominio (Guid v7).
        builder.Property(c => c.Text).HasMaxLength(Comment.TextMaxLength);
        builder.HasIndex(c => new { c.CardId, c.CreatedAt });

        builder.HasOne<Card>()
            .WithMany()
            .HasForeignKey(c => c.CardId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(c => c.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ActivityLogConfiguration : IEntityTypeConfiguration<ActivityLog>
{
    public void Configure(EntityTypeBuilder<ActivityLog> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever(); // Lo genera el dominio (Guid v7).
        builder.Property(a => a.Summary).HasMaxLength(500);
        builder.HasIndex(a => new { a.BoardId, a.CreatedAt });

        builder.HasOne<Board>()
            .WithMany()
            .HasForeignKey(a => a.BoardId)
            .OnDelete(DeleteBehavior.Cascade);

        // Sin FK a la tarjeta: el historial sobrevive cuando se borra.
        builder.Property(a => a.CardId);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
