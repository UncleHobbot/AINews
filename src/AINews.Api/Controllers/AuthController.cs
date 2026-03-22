using AINews.Api.Data;
using AINews.Api.DTOs;
using AINews.Api.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AINews.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AppDbContext db, IConfiguration config) : ControllerBase
{
    [HttpGet("login/google")]
    public IActionResult LoginGoogle([FromQuery] string? returnUrl = "/")
    {
        var redirectUrl = Url.Action(nameof(GoogleCallback), "Auth", new { returnUrl });
        var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    [HttpGet("callback/google")]
    public async Task<IActionResult> GoogleCallback([FromQuery] string? returnUrl = "/")
    {
        var result = await HttpContext.AuthenticateAsync(GoogleDefaults.AuthenticationScheme);
        if (!result.Succeeded) return Redirect("/?error=auth_failed");

        var googleSub = result.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = result.Principal?.FindFirstValue(ClaimTypes.Email);
        var displayName = result.Principal?.FindFirstValue(ClaimTypes.Name);

        if (string.IsNullOrEmpty(googleSub) || string.IsNullOrEmpty(email))
            return Redirect("/?error=missing_claims");

        // Check whitelist
        var allowedEmails = config.GetSection("AllowedEmails").Get<string[]>() ?? [];
        var user = await db.AppUsers.FirstOrDefaultAsync(u => u.GoogleSubject == googleSub);

        if (user == null)
        {
            if (!allowedEmails.Contains(email, StringComparer.OrdinalIgnoreCase))
                return Redirect("/?error=not_allowed");

            user = new AppUser
            {
                GoogleSubject = googleSub,
                Email = email,
                DisplayName = displayName,
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow
            };
            db.AppUsers.Add(user);
        }
        else
        {
            user.LastLoginAt = DateTime.UtcNow;
            user.DisplayName = displayName;
        }
        await db.SaveChangesAsync();

        // Sign in with cookie
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.DisplayName ?? user.Email)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30) });

        return Redirect(returnUrl ?? "/");
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok();
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()
    {
        var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var email = User.FindFirstValue(ClaimTypes.Email)!;
        var name = User.FindFirstValue(ClaimTypes.Name);
        return Ok(new AuthMeResponse(id, email, name));
    }
}
