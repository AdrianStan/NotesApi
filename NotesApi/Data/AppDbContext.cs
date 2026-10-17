using Microsoft.EntityFrameworkCore;
using NotesApi.Models;

namespace NotesApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();
    public DbSet<Note> Notes => Set<Note>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Join table: the pair is the key, so a user can't join the same team twice
        modelBuilder.Entity<TeamMember>()
            .HasKey(tm => new { tm.TeamId, tm.UserId });

        modelBuilder.Entity<TeamMember>()
            .HasOne(tm => tm.Team)
            .WithMany(t => t.Members)
            .HasForeignKey(tm => tm.TeamId);

        modelBuilder.Entity<TeamMember>()
            .HasOne(tm => tm.User)
            .WithMany(u => u.Memberships)
            .HasForeignKey(tm => tm.UserId);

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<Note>()
            .HasOne(n => n.Team)
            .WithMany(t => t.Notes)
            .HasForeignKey(n => n.TeamId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Note>()
            .HasOne(n => n.Author)
            .WithMany()
            .HasForeignKey(n => n.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        // EF checks Version on every update; a stale edit fails instead of overwriting
        modelBuilder.Entity<Note>()
            .Property(n => n.Version)
            .IsConcurrencyToken();

        // Listing a team's notes, newest first
        modelBuilder.Entity<Note>()
            .HasIndex(n => new { n.TeamId, n.UpdatedAtUtc });

        // Soft-deleted notes are excluded from every query automatically
        modelBuilder.Entity<Note>()
            .HasQueryFilter(n => n.IsActive);

        var seedDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<User>().HasData(
            new User { Id = 1, FirstName = "Alice", LastName = "Johnson", Email = "alice@example.com" },
            new User { Id = 2, FirstName = "Bob", LastName = "Smith", Email = "bob@example.com" },
            new User { Id = 3, FirstName = "Carol", LastName = "Lee", Email = "carol@example.com" });

        modelBuilder.Entity<Team>().HasData(
            new Team { Id = 1, Name = "Development" },
            new Team { Id = 2, Name = "Operations" },
            new Team { Id = 3, Name = "Testing" });

        modelBuilder.Entity<TeamMember>().HasData(
            new TeamMember { TeamId = 1, UserId = 1 },
            new TeamMember { TeamId = 1, UserId = 2 },
            new TeamMember { TeamId = 2, UserId = 1 },
            new TeamMember { TeamId = 2, UserId = 3 },
            new TeamMember { TeamId = 3, UserId = 3 });

        modelBuilder.Entity<Note>().HasData(
            new Note
            {
                Id = 1,
                Title = "Sprint planning",
                Content = "Agenda for Monday's planning session.",
                TeamId = 1,
                AuthorId = 1,
                CreatedAtUtc = seedDate,
                UpdatedAtUtc = seedDate,
                IsActive = true,
                Version = 1
            });
    }
}