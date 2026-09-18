using TicketAnaliz.Core.Entities;

namespace TicketAnaliz.Core.Repositories;

public interface ITicketRepository
{
    Task<Ticket?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Ticket>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
    Task<IReadOnlyList<Ticket>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(Ticket ticket, CancellationToken ct = default);
}
