using System.ComponentModel.DataAnnotations;

namespace NotesApi.Dtos;

public class CreateNoteRequest
{
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(10000)]
    public string Content { get; set; } = string.Empty;
}

public class UpdateNoteRequest
{
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(10000)]
    public string Content { get; set; } = string.Empty;

    [Required]
    public int? Version { get; set; }
}

public record NoteResponse(
    int Id, int TeamId, string Title, string Content,
    int AuthorId, string AuthorName,
    DateTime CreatedAtUtc, DateTime UpdatedAtUtc, int Version);

public record TeamResponse(int Id, string Name);

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);