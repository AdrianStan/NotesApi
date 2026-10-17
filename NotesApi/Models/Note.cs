using System.ComponentModel.DataAnnotations;

namespace NotesApi.Models;

public class Note
{
    public int Id { get; set; }

    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public int AuthorId { get; set; }
    public User Author { get; set; } = null!;

    public int TeamId { get; set; }
    public Team Team { get; set; } = null!;

    public bool IsActive { get; set; } = true;
    public int Version { get; set; } = 1;
}