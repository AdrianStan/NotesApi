using Microsoft.EntityFrameworkCore;
using NotesApi.Data;

namespace NotesApi.Infrastructure;

public interface ICurrentUser
{
    Task<int> GetUserIdAsync(CancellationToken ct = default);
    Task EnsureMemberAsync(int teamId, CancellationToken ct = default);
}

public class HeaderCurrentUser : ICurrentUser
{
    public const string HeaderName = "X-User-Id";

    private readonly IHttpContextAccessor _http;
    private readonly AppDbContext _db;
    private int? _userId;

    public HeaderCurrentUser(IHttpContextAccessor http, AppDbContext db)
    {
        _http = http;
        _db = db;
    }

    public async Task<int> GetUserIdAsync(CancellationToken ct = default)
    {
        if (_userId.HasValue)
            return _userId.Value;

        var raw = _http.HttpContext?.Request.Headers[HeaderName].FirstOrDefault();
        if (!int.TryParse(raw, out var id))
            throw new UnauthorizedException($"Missing or invalid {HeaderName} header.");

        if (!await _db.Users.AnyAsync(u => u.Id == id, ct))
            throw new UnauthorizedException($"User {id} does not exist.");

        _userId = id;
        return id;
    }

    public async Task EnsureMemberAsync(int teamId, CancellationToken ct = default)
    {
        var userId = await GetUserIdAsync(ct);

        if (!await _db.Teams.AnyAsync(t => t.Id == teamId, ct))
            throw new NotFoundException($"Team {teamId} was not found.");

        var isMember = await _db.TeamMembers.AnyAsync(m => m.TeamId == teamId && m.UserId == userId, ct);
        if (!isMember)
            throw new ForbiddenException($"You are not a member of team {teamId}.");
    }
}