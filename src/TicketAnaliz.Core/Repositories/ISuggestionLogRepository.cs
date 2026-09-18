using TicketAnaliz.Core.Entities;

namespace TicketAnaliz.Core.Repositories;

public interface ISuggestionLogRepository
{
    Task AddAsync(SuggestionLog log, CancellationToken ct = default);
    Task<IReadOnlyList<SuggestionLog>> GetRecentAsync(int count, CancellationToken ct = default);
    Task<SuggestionLog?> GetByIdAsync(Guid id, CancellationToken ct = default);
}
