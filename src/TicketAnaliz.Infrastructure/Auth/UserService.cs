using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TicketAnaliz.Core.Auth;
using TicketAnaliz.Core.Entities;
using TicketAnaliz.Core.Repositories;

namespace TicketAnaliz.Infrastructure.Auth;

public class UserService : IUserService
{
    private const int MinPasswordLength = 8;
    private const int MaxPasswordLength = 100;
    private static readonly Regex UserNameRegex = new("^[a-zA-Z0-9._-]{3,50}$", RegexOptions.Compiled);

    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher<AppUser> _passwordHasher;

    private readonly string _dummyHash;

    public UserService(IUserRepository userRepository, IPasswordHasher<AppUser> passwordHasher)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _dummyHash = passwordHasher.HashPassword(new AppUser(), Guid.NewGuid().ToString());
    }

    public async Task<AuthResult> RegisterAsync(string userName, string password, CancellationToken ct = default)
    {
        userName = (userName ?? string.Empty).Trim();
        password ??= string.Empty;

        if (!UserNameRegex.IsMatch(userName))
        {
            return AuthResult.Fail("Kullanıcı adı 3-50 karakter olmalı, sadece harf, rakam, nokta, tire ve alt çizgi içerebilir.");
        }

        if (password.Length < MinPasswordLength || password.Length > MaxPasswordLength)
        {
            return AuthResult.Fail($"Şifre {MinPasswordLength}-{MaxPasswordLength} karakter arasında olmalı.");
        }

        if (await _userRepository.GetByUserNameAsync(userName, ct) is not null)
        {
            return AuthResult.Fail("Bu kullanıcı adı zaten alınmış.");
        }

        var user = NewUser(userName, password, UserRole.User);

        try
        {
            await _userRepository.AddAsync(user, ct);
        }
        catch (DbUpdateException)
        {
            // Ayni anda ayni ada kayit olunduysa benzersiz indeks burada yakalar.
            return AuthResult.Fail("Bu kullanıcı adı zaten alınmış.");
        }

        return AuthResult.Ok(user);
    }

    public async Task<AppUser?> ValidateCredentialsAsync(string userName, string password, CancellationToken ct = default)
    {
        var user = await _userRepository.GetByUserNameAsync((userName ?? string.Empty).Trim(), ct);

        var hashToCheck = user?.PasswordHash ?? _dummyHash;
        var result = _passwordHasher.VerifyHashedPassword(user ?? new AppUser(), hashToCheck, password ?? string.Empty);

        return user is not null && result != PasswordVerificationResult.Failed ? user : null;
    }

    public async Task EnsureAdminAsync(string userName, string password, CancellationToken ct = default)
    {
        if (await _userRepository.GetByUserNameAsync(userName, ct) is not null)
        {
            return;
        }

        await _userRepository.AddAsync(NewUser(userName, password, UserRole.Admin), ct);
    }

    private AppUser NewUser(string userName, string password, UserRole role)
    {
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = userName,
            Role = role,
            CreatedAt = DateTime.UtcNow
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, password);
        return user;
    }
}
