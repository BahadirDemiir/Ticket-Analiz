using Microsoft.EntityFrameworkCore;
using TicketAnaliz.Core.Entities;
using TicketAnaliz.Core.Repositories;
using TicketAnaliz.Infrastructure.Context;

namespace TicketAnaliz.Infrastructure.Repositories;

public class SuggestionLogRepository : ISuggestionLogRepository
{
    private readonly AppDbContext _context;

    public SuggestionLogRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(SuggestionLog log, CancellationToken ct = default)
    {
        await _context.SuggestionLogs.AddAsync(log, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<SuggestionLog>> GetRecentAsync(int count, CancellationToken ct = default)
    {
        return await _context.SuggestionLogs
            .OrderByDescending(l => l.CreatedAt)
            .Take(count)
            .ToListAsync(ct);
    }

    public Task<SuggestionLog?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return _context.SuggestionLogs.FirstOrDefaultAsync(l => l.Id == id, ct);
    }
}
