using Microsoft.AspNetCore.Mvc;
using NotesApi.Dtos;
using NotesApi.Services;

namespace NotesApi.Controllers;

[ApiController]
[Route("api/teams")]
public class TeamsController : ControllerBase
{
    private readonly INotesService _notes;

    public TeamsController(INotesService notes)
    {
        _notes = notes;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TeamResponse>>> GetMyTeams(CancellationToken ct)
        => Ok(await _notes.GetMyTeamsAsync(ct));
}