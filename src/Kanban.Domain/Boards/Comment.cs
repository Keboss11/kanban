using Kanban.Domain.Common;

namespace Kanban.Domain.Boards;

public sealed class Comment
{
    public const int TextMaxLength = 5000;

    private Comment()
    {
        AuthorId = null!;
        Text = null!;
    }

    public Comment(Guid cardId, string authorId, string text)
    {
        if (string.IsNullOrWhiteSpace(authorId))
            throw new DomainException("El comentario necesita un autor.");

        CardId = cardId;
        AuthorId = authorId;
        Text = Guard.Required(text, TextMaxLength, "El texto del comentario");
        CreatedAt = DateTime.UtcNow;
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid CardId { get; private set; }
    public string AuthorId { get; private set; }
    public string Text { get; private set; }
    public DateTime CreatedAt { get; private set; }
}
