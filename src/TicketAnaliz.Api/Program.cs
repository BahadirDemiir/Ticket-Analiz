using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using TicketAnaliz.Core.Auth;
using TicketAnaliz.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddSemanticKernelServices(builder.Configuration);
builder.Services.AddQdrantServices(builder.Configuration);
builder.Services.AddTicketSearchServices();
builder.Services.AddRagServices();

// Giris: cookie tabanli kimlik dogrulama. Cookie JavaScript'ten okunamaz (HttpOnly) ve baska
// sitelerden gelen isteklerle gonderilmez (SameSite=Lax).
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "TicketAnaliz.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.LoginPath = "/login.html";

        // API cagrilarinda login sayfasina yonlendirmek yerine duz 401/403 don, sayfalarda yonlendir.
        options.Events.OnRedirectToLogin = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            }
            else
            {
                context.Response.Redirect(context.RedirectUri);
            }
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
            }
            else
            {
                context.Response.Redirect("/");
            }
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();

// Giris/kayit denemelerini sinirla (sifre denemesiyle kaba kuvvet saldirisini yavaslatir).
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
});

var app = builder.Build();

// Ayarlardaki admin hesabini olustur (yoksa). Sifre appsettings.Development.json'da, repoda degil.
using (var scope = app.Services.CreateScope())
{
    var adminUserName = app.Configuration["Auth:AdminUserName"];
    var adminPassword = app.Configuration["Auth:AdminPassword"];

    if (!string.IsNullOrWhiteSpace(adminUserName) && !string.IsNullOrWhiteSpace(adminPassword))
    {
        var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
        await userService.EnsureAdminAsync(adminUserName, adminPassword);
    }
    else
    {
        app.Logger.LogWarning("Auth:AdminUserName / Auth:AdminPassword tanimli degil, admin hesabi olusturulmadi.");
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();

// Sayfalari sunucu tarafinda koru: giris yapmayan ana sayfayi, admin olmayan admin sayfasini hic gormesin.
// (login.html herkese acik. API uclari ayrica kendi [Authorize] kurallariyla korunuyor.)
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value?.ToLowerInvariant();
    var isMainPage = path is "/" or "/index.html";
    var isAdminPage = path == "/admin.html";

    if (isMainPage || isAdminPage)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            context.Response.Redirect("/login.html");
            return;
        }

        if (isAdminPage && !context.User.IsInRole("Admin"))
        {
            context.Response.Redirect("/");
            return;
        }
    }

    await next();
});

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseHttpsRedirection();

app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();
