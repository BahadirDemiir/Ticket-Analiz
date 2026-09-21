using TicketAnaliz.Core.Entities;

namespace TicketAnaliz.Core.Repositories;

public interface IUserRepository
{
    Task<AppUser?> GetByUserNameAsync(string userName, CancellationToken ct = default);
    Task AddAsync(AppUser user, CancellationToken ct = default);
}
