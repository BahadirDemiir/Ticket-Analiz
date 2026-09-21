namespace TicketAnaliz.Api.Contracts;

public class LoginRequest
{
    public string UserName { get; set; } = default!;
    public string Password { get; set; } = default!;
}

// Bilerek "Role" alani yok: kayit olan herkes User olur, rol istekten alinamaz.
public class RegisterRequest
{
    public string UserName { get; set; } = default!;
    public string Password { get; set; } = default!;
}

public class CurrentUserResponse
{
    public string UserName { get; set; } = default!;
    public string Role { get; set; } = default!;
}

public class ErrorResponse
{
    public string Message { get; set; } = default!;
}
