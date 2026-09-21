using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TicketAnaliz.Api.Contracts;
using TicketAnaliz.Core.Auth;
using TicketAnaliz.Core.Entities;

namespace TicketAnaliz.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IUserService _userService;

    public AuthController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<CurrentUserResponse>> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var result = await _userService.RegisterAsync(request.UserName, request.Password, ct);
        if (result.User is null)
        {
            return BadRequest(new ErrorResponse { Message = result.Error! });
        }

        await SignInAsync(result.User);
        return Ok(ToResponse(result.User));
    }

    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<CurrentUserResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var user = await _userService.ValidateCredentialsAsync(request.UserName, request.Password, ct);
        if (user is null)
        {
            return Unauthorized(new ErrorResponse { Message = "Kullanıcı adı veya şifre hatalı." });
        }

        await SignInAsync(user);
        return Ok(ToResponse(user));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public ActionResult<CurrentUserResponse> Me()
    {
        return Ok(new CurrentUserResponse
        {
            UserName = User.Identity!.Name!,
            Role = User.FindFirstValue(ClaimTypes.Role)!
        });
    }

    private Task SignInAsync(AppUser user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName),
            new(ClaimTypes.Role, user.Role.ToString())
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));

        return HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
    }

    private static CurrentUserResponse ToResponse(AppUser user) => new()
    {
        UserName = user.UserName,
        Role = user.Role.ToString()
    };
}
