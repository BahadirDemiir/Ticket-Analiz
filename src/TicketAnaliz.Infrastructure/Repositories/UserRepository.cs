using Microsoft.EntityFrameworkCore;
using TicketAnaliz.Core.Entities;
using TicketAnaliz.Core.Repositories;
using TicketAnaliz.Infrastructure.Context;

namespace TicketAnaliz.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _context;

    public UserRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<AppUser?> GetByUserNameAsync(string userName, CancellationToken ct = default)
    {
        return _context.Users.FirstOrDefaultAsync(u => u.UserName == userName, ct);
    }

    public async Task AddAsync(AppUser user, CancellationToken ct = default)
    {
        await _context.Users.AddAsync(user, ct);
        await _context.SaveChangesAsync(ct);
    }
}
