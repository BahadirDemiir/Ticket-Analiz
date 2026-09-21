using Microsoft.EntityFrameworkCore;
using TicketAnaliz.Core.Entities;

namespace TicketAnaliz.Infrastructure.Context;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<SuggestionLog> SuggestionLogs => Set<SuggestionLog>();
    public DbSet<AppUser> Users => Set<AppUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Ticket>(entity =>
        {
            entity.ToTable("Tickets");
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Title).HasMaxLength(300).IsRequired();
            entity.Property(t => t.Description).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(t => t.Resolution).HasColumnType("nvarchar(max)");
            entity.Property(t => t.Category).HasMaxLength(100).IsRequired();
            entity.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(t => t.Department).HasConversion<string>().HasMaxLength(50);
            entity.Property(t => t.Priority).HasMaxLength(20);
            entity.Property(t => t.ScenarioKey).HasMaxLength(50);

            entity.HasIndex(t => t.ScenarioKey);
            entity.HasIndex(t => t.Category);
            entity.HasIndex(t => t.Status);
            entity.HasIndex(t => t.Department);
            entity.HasIndex(t => t.CreatedDate);
        });

        modelBuilder.Entity<SuggestionLog>(entity =>
        {
            entity.ToTable("SuggestionLogs");
            entity.HasKey(l => l.Id);
            entity.Property(l => l.Query).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(l => l.Answer).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(l => l.HallucinationExplanation).HasColumnType("nvarchar(max)");
            entity.Property(l => l.SourcesJson).HasColumnType("nvarchar(max)");
            entity.Property(l => l.AnswerSource).HasConversion<string>().HasMaxLength(30);
            entity.Property(l => l.WebSourcesJson).HasColumnType("nvarchar(max)");
            entity.Property(l => l.WebSearchQuery).HasMaxLength(500);

            entity.Property(l => l.UserName).HasMaxLength(50);

            entity.HasIndex(l => l.CreatedAt);
        });

        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(u => u.Id);
            entity.Property(u => u.UserName).HasMaxLength(50).IsRequired();
            entity.Property(u => u.PasswordHash).HasMaxLength(500).IsRequired();
            entity.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);

            // Ayni kullanici adiyla iki hesap olmasin (SQL Server'da buyuk/kucuk harf duyarsiz).
            entity.HasIndex(u => u.UserName).IsUnique();
        });
    }
}
