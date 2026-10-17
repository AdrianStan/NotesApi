using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using NotesApi.Data;
using NotesApi.Dtos;
using NotesApi.Infrastructure;
using NotesApi.Models;

namespace NotesApi.Services;

public interface INotesService
{
    Task<IReadOnlyList<TeamResponse>> GetMyTeamsAsync(CancellationToken ct);
    Task<PagedResult<NoteResponse>> ListAsync(int teamId, string? search, int page, int pageSize, CancellationToken ct);
    Task<NoteResponse> GetAsync(int id, CancellationToken ct);
    Task<NoteResponse> CreateAsync(int teamId, CreateNoteRequest request, CancellationToken ct);
    Task<NoteResponse> UpdateAsync(int id, UpdateNoteRequest request, CancellationToken ct);
    Task DeleteAsync(int id, CancellationToken ct);
}

public class NotesService : INotesService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public NotesService(AppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<TeamResponse>> GetMyTeamsAsync(CancellationToken ct)
    {
        var userId = await _currentUser.GetUserIdAsync(ct);

        return await _db.TeamMembers
            .Where(m => m.UserId == userId)
            .OrderBy(m => m.Team.Name)
            .Select(m => new TeamResponse(m.Team.Id, m.Team.Name))
            .ToListAsync(ct);
    }

    public async Task<PagedResult<NoteResponse>> ListAsync(int teamId, string? search, int page, int pageSize, CancellationToken ct)
    {
        await _currentUser.EnsureMemberAsync(teamId, ct);

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.Notes.AsNoTracking().Where(n => n.TeamId == teamId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(n => EF.Functions.Like(n.Title, pattern) || EF.Functions.Like(n.Content, pattern));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(n => n.UpdatedAtUtc)
            .ThenBy(n => n.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ToResponse)
            .ToListAsync(ct);

        return new PagedResult<NoteResponse>(items, page, pageSize, total);
    }

    public async Task<NoteResponse> GetAsync(int id, CancellationToken ct)
    {
        var note = await _db.Notes.AsNoTracking()
            .Where(n => n.Id == id)
            .Select(ToResponse)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException($"Note {id} was not found.");

        await _currentUser.EnsureMemberAsync(note.TeamId, ct);
        return note;
    }

    public async Task<NoteResponse> CreateAsync(int teamId, CreateNoteRequest request, CancellationToken ct)
    {
        await _currentUser.EnsureMemberAsync(teamId, ct);
        var userId = await _currentUser.GetUserIdAsync(ct);
        var now = DateTime.UtcNow;

        var note = new Note
        {
            TeamId = teamId,
            AuthorId = userId,
            Title = request.Title.Trim(),
            Content = request.Content,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            Version = 1,
            IsActive = true
        };

        _db.Notes.Add(note);
        await _db.SaveChangesAsync(ct);

        return await GetAsync(note.Id, ct);
    }

    public async Task<NoteResponse> UpdateAsync(int id, UpdateNoteRequest request, CancellationToken ct)
    {
        var note = await LoadForChangeAsync(id, ct);

        if (note.Version != request.Version)
            throw new ConflictException($"Note {id} was changed by someone else (current version {note.Version}). Reload it and try again.");

        note.Title = request.Title.Trim();
        note.Content = request.Content;
        note.UpdatedAtUtc = DateTime.UtcNow;
        note.Version++;

        await SaveWithConcurrencyCheckAsync(id, ct);
        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var note = await LoadForChangeAsync(id, ct);

        note.IsActive = false;
        note.UpdatedAtUtc = DateTime.UtcNow;
        note.Version++;

        await SaveWithConcurrencyCheckAsync(id, ct);
    }

    private async Task<Note> LoadForChangeAsync(int id, CancellationToken ct)
    {
        var note = await _db.Notes.FirstOrDefaultAsync(n => n.Id == id, ct)
            ?? throw new NotFoundException($"Note {id} was not found.");

        await _currentUser.EnsureMemberAsync(note.TeamId, ct);
        return note;
    }

    private async Task SaveWithConcurrencyCheckAsync(int id, CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another request saved this note between our read and our write
            throw new ConflictException($"Note {id} was changed by someone else. Reload it and try again.");
        }
    }

    private static readonly Expression<Func<Note, NoteResponse>> ToResponse = n => new NoteResponse(
        n.Id, n.TeamId, n.Title, n.Content, n.AuthorId,
        n.Author.FirstName + " " + n.Author.LastName,
        DateTime.SpecifyKind(n.CreatedAtUtc, DateTimeKind.Utc),
        DateTime.SpecifyKind(n.UpdatedAtUtc, DateTimeKind.Utc),
        n.Version);
}