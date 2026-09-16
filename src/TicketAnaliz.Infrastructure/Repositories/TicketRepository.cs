using Microsoft.EntityFrameworkCore;
using TicketAnaliz.Core.Entities;
using TicketAnaliz.Core.Repositories;
using TicketAnaliz.Infrastructure.Context;

namespace TicketAnaliz.Infrastructure.Repositories;

public class TicketRepository : ITicketRepository
{
    private readonly AppDbContext _context;

    public TicketRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<Ticket?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return _context.Tickets.FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public async Task<IReadOnlyList<Ticket>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        return await _context.Tickets
            .Where(t => ids.Contains(t.Id))
            .ToListAsync(ct);
    }

    public async Task AddAsync(Ticket ticket, CancellationToken ct = default)
    {
        await _context.Tickets.AddAsync(ticket, ct);
        await _context.SaveChangesAsync(ct);
    }
}
