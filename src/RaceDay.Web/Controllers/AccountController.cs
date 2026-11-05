using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Infrastructure;
using RaceDay.Web.Models.Account;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Services.Api;

namespace RaceDay.Web.Controllers;

/// <summary>Register, log in and log out. Credentials are checked by the API; this app only keeps the session.</summary>
public class AccountController(IRaceDayApi api, ITokenStore tokens, ILogger<AccountController> logger) : Controller
{
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return LocalRedirect(User.HomeUrl());
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await api.LoginAsync(new LoginRequest(model.Email, model.Password), ct);
        if (!result.Succeeded || result.Value is null)
        {
            ModelState.AddModelError(string.Empty, result.StatusCode == HttpStatusCode.Unauthorized
                ? "The email address or password is incorrect."
                : result.Message);
            return View(model);
        }

        var principal = await SignInAsync(result.Value);
        return LocalRedirect(Url.IsLocalUrl(model.ReturnUrl) ? model.ReturnUrl! : principal.HomeUrl());
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return LocalRedirect(User.HomeUrl());
        }

        return View(new RegisterViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Register(RegisterViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var registered = await api.RegisterAsync(new RegisterRequest(
            model.FirstName, model.LastName, model.Email, model.Password, model.DateOfBirth,
            model.Gender, string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber, model.Role), ct);
        if (!registered.Succeeded)
        {
            registered.AddTo<RegisterViewModel>(ModelState);
            return View(model);
        }

        // The API's register endpoint does not issue tokens, so log straight in with the new credentials.
        var login = await api.LoginAsync(new LoginRequest(model.Email, model.Password), ct);
        if (!login.Succeeded || login.Value is null)
        {
            this.Success("Your account has been created. Please log in.");
            return RedirectToAction(nameof(Login));
        }

        var principal = await SignInAsync(login.Value);
        this.Success($"Welcome to RaceDay, {model.FirstName}!");
        return LocalRedirect(principal.HomeUrl());
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        try
        {
            // Revoke the API session so its tokens stop working immediately.
            await api.LogoutAsync(ct);
        }
        catch (Exception ex) when (ex is ApiSessionExpiredException or HttpRequestException or TaskCanceledException)
        {
            logger.LogInformation(ex, "API logout failed; ending the local session anyway.");
        }

        tokens.Clear();
        HttpContext.Session.Clear();
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        this.Success("You have been logged out.");
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied() => View();

    private async Task<ClaimsPrincipal> SignInAsync(TokenResponse login)
    {
        // Drop anything left in the session by a previous user of this browser.
        HttpContext.Session.Clear();
        tokens.Save(login);

        var user = login.User;
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"),
            new(ClaimTypes.GivenName, user.FirstName),
            new(ClaimTypes.Email, user.Email)
        };
        claims.AddRange(user.Roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
        return principal;
    }
}
