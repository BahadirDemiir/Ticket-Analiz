namespace TicketAnaliz.Core.Entities;

public enum UserRole
{
    User,
    Admin
}

public class AppUser
{
    public Guid Id { get; set; }
    public string UserName { get; set; } = default!;

    // Sifrenin kendisi degil, salt'li hash'i saklaniyor.
    public string PasswordHash { get; set; } = default!;
    public UserRole Role { get; set; } = UserRole.User;
    public DateTime CreatedAt { get; set; }
}
