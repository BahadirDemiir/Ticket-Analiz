using TicketAnaliz.Core.Entities;

namespace TicketAnaliz.Core.Auth;

public record AuthResult(AppUser? User, string? Error)
{
    public static AuthResult Ok(AppUser user) => new(user, null);
    public static AuthResult Fail(string error) => new(null, error);
}

public interface IUserService
{
    // Kayit olan herkes "User" rolunu alir; rol disaridan (istekten) hic alinmaz.
    Task<AuthResult> RegisterAsync(string userName, string password, CancellationToken ct = default);

    // Kullanici adi veya sifre yanlissa null doner (hangisinin yanlis oldugu belli edilmez).
    Task<AppUser?> ValidateCredentialsAsync(string userName, string password, CancellationToken ct = default);

    // Uygulama acilisinda ayarlardan gelen admin hesabini olusturur (zaten varsa dokunmaz).
    Task EnsureAdminAsync(string userName, string password, CancellationToken ct = default);
}
