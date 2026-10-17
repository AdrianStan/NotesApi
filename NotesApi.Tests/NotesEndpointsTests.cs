using System.Net;
using System.Net.Http.Json;
using NotesApi.Dtos;

namespace NotesApi.Tests;

public class NotesEndpointsTests : IClassFixture<NotesApiFactory>
{
    // Seeded: Alice (1) in Development and Operations, Bob (2) in Development only
    private const int Alice = 1, Bob = 2;
    private const int Development = 1, Operations = 2;

    private readonly NotesApiFactory _factory;

    public NotesEndpointsTests(NotesApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetTeams_ReturnsOnlyTheCallersTeams()
    {
        var teams = await _factory.CreateClientAs(Alice).GetFromJsonAsync<List<TeamResponse>>("/api/teams");

        Assert.NotNull(teams);
        Assert.Equal(new[] { "Development", "Operations" }, teams.Select(t => t.Name));
    }

    [Fact]
    public async Task ListNotes_WithoutUserHeader_Returns401()
    {
        var response = await _factory.CreateClient().GetAsync($"/api/teams/{Development}/notes");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ListNotes_ForTeamTheUserIsNotIn_Returns403()
    {
        var response = await _factory.CreateClientAs(Bob).GetAsync($"/api/teams/{Operations}/notes");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateNote_WithoutTitle_Returns400()
    {
        var response = await _factory.CreateClientAs(Bob)
            .PostAsJsonAsync($"/api/teams/{Development}/notes", new { content = "No title" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateNote_ThenGet_ReturnsTheNoteWithItsAuthor()
    {
        var client = _factory.CreateClientAs(Bob);
        var created = await CreateNoteAsync(client, "Release checklist");

        var fetched = await client.GetFromJsonAsync<NoteResponse>($"/api/notes/{created.Id}");

        Assert.NotNull(fetched);
        Assert.Equal("Release checklist", fetched.Title);
        Assert.Equal(Bob, fetched.AuthorId);
        Assert.Equal(1, fetched.Version);
    }

    [Fact]
    public async Task UpdateNote_WithCurrentVersion_SavesAndIncrementsVersion()
    {
        var client = _factory.CreateClientAs(Alice);
        var note = await CreateNoteAsync(client, "Draft");

        var response = await client.PutAsJsonAsync($"/api/notes/{note.Id}",
            new UpdateNoteRequest { Title = "Final", Content = "Done", Version = note.Version });

        response.EnsureSuccessStatusCode();
        var updated = await response.Content.ReadFromJsonAsync<NoteResponse>();
        Assert.Equal("Final", updated!.Title);
        Assert.Equal(note.Version + 1, updated.Version);
    }

    [Fact]
    public async Task UpdateNote_WithOutdatedVersion_Returns409AndKeepsTheFirstEdit()
    {
        var alice = _factory.CreateClientAs(Alice);
        var bob = _factory.CreateClientAs(Bob);
        var note = await CreateNoteAsync(alice, "Shared note");

        // Alice saves first; Bob is still editing version 1
        var aliceResponse = await alice.PutAsJsonAsync($"/api/notes/{note.Id}",
            new UpdateNoteRequest { Title = "Alice's edit", Version = note.Version });
        aliceResponse.EnsureSuccessStatusCode();

        var bobResponse = await bob.PutAsJsonAsync($"/api/notes/{note.Id}",
            new UpdateNoteRequest { Title = "Bob's edit", Version = note.Version });

        Assert.Equal(HttpStatusCode.Conflict, bobResponse.StatusCode);
        var current = await alice.GetFromJsonAsync<NoteResponse>($"/api/notes/{note.Id}");
        Assert.Equal("Alice's edit", current!.Title);
    }

    [Fact]
    public async Task DeleteNote_ThenGet_Returns404()
    {
        var client = _factory.CreateClientAs(Alice);
        var note = await CreateNoteAsync(client, "Temporary");

        var deleteResponse = await client.DeleteAsync($"/api/notes/{note.Id}");
        var getResponse = await client.GetAsync($"/api/notes/{note.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task ListNotes_WithSearch_ReturnsOnlyMatchingNotes()
    {
        var client = _factory.CreateClientAs(Alice);
        var marker = Guid.NewGuid().ToString("N");
        await CreateNoteAsync(client, $"Incident {marker}");
        await CreateNoteAsync(client, "Unrelated note");

        var page = await client.GetFromJsonAsync<PagedResult<NoteResponse>>(
            $"/api/teams/{Development}/notes?search={marker}");

        Assert.NotNull(page);
        Assert.Single(page.Items);
        Assert.Contains(marker, page.Items[0].Title);
    }
    [Fact]
    public async Task Health_ReturnsOk()
    {
        var response = await _factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
    private static async Task<NoteResponse> CreateNoteAsync(HttpClient client, string title)
    {
        var response = await client.PostAsJsonAsync($"/api/teams/{Development}/notes",
            new CreateNoteRequest { Title = title, Content = "Test content" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<NoteResponse>())!;
    }
}