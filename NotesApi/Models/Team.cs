using System.ComponentModel.DataAnnotations;

namespace NotesApi.Models;

public class Team
{
    public int Id { get; set; }

    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public List<TeamMember> Members { get; set; } = new();
    public List<Note> Notes { get; set; } = new();
}