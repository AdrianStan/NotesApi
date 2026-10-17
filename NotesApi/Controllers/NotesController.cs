using Microsoft.AspNetCore.Mvc;
using NotesApi.Dtos;
using NotesApi.Services;

namespace NotesApi.Controllers;

[ApiController]
[Route("api")]
public class NotesController : ControllerBase
{
    private readonly INotesService _notes;

    public NotesController(INotesService notes)
    {
        _notes = notes;
    }

    [HttpGet("teams/{teamId:int}/notes")]
    public async Task<ActionResult<PagedResult<NoteResponse>>> List(
        int teamId, [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _notes.ListAsync(teamId, search, page, pageSize, ct));

    [HttpPost("teams/{teamId:int}/notes")]
    public async Task<ActionResult<NoteResponse>> Create(int teamId, CreateNoteRequest request, CancellationToken ct)
    {
        var note = await _notes.CreateAsync(teamId, request, ct);
        return CreatedAtAction(nameof(Get), new { id = note.Id }, note);
    }

    [HttpGet("notes/{id:int}")]
    public async Task<ActionResult<NoteResponse>> Get(int id, CancellationToken ct)
        => Ok(await _notes.GetAsync(id, ct));

    [HttpPut("notes/{id:int}")]
    public async Task<ActionResult<NoteResponse>> Update(int id, UpdateNoteRequest request, CancellationToken ct)
        => Ok(await _notes.UpdateAsync(id, request, ct));

    [HttpDelete("notes/{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _notes.DeleteAsync(id, ct);
        return NoContent();
    }
}