using Microsoft.EntityFrameworkCore;
using TicketAnaliz.Core.Entities;

namespace TicketAnaliz.Infrastructure.Context;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Ticket> Tickets => Set<Ticket>();

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

            entity.HasIndex(t => t.Category);
            entity.HasIndex(t => t.Status);
            entity.HasIndex(t => t.Department);
            entity.HasIndex(t => t.CreatedDate);
        });
    }
}
