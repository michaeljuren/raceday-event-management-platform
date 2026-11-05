using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using RaceDay.Web.Infrastructure;
using RaceDay.Web.Models.Account;
using RaceDay.Web.Services.Api;

namespace RaceDay.Web.Controllers;

/// <summary>View and edit your own profile. Available to every signed-in user (fallback policy).</summary>
public class ProfileController(IRaceDayApi api) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var profile = await api.GetProfileAsync(ct);
        if (profile.Value is null)
        {
            this.Error(profile.Message);
            return LocalRedirect(User.HomeUrl());
        }

        return View(profile.Value);
    }

    public async Task<IActionResult> Edit(CancellationToken ct)
    {
        var profile = await api.GetProfileAsync(ct);
        if (profile.Value is null)
        {
            this.Error(profile.Message);
            return RedirectToAction(nameof(Index));
        }

        var model = ProfileFormViewModel.From(profile.Value);
        await FillClubsAsync(model, ct);
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(ProfileFormViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            await FillClubsAsync(model, ct);
            return View(model);
        }

        var updated = await api.UpdateProfileAsync(model.ToRequest(), ct);
        if (!updated.Succeeded || updated.Value is null)
        {
            updated.AddTo<ProfileFormViewModel>(ModelState);
            await FillClubsAsync(model, ct);
            return View(model);
        }

        await RefreshNameClaimsAsync(updated.Value.FirstName, updated.Value.LastName);
        this.Success("Profile updated.");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Keep the navbar name in step with the profile without forcing a new login.</summary>
    private async Task RefreshNameClaimsAsync(string firstName, string lastName)
    {
        if (User.Identity is not ClaimsIdentity identity)
        {
            return;
        }

        var claims = identity.Claims
            .Where(c => c.Type is not (ClaimTypes.Name or ClaimTypes.GivenName))
            .Append(new Claim(ClaimTypes.Name, $"{firstName} {lastName}"))
            .Append(new Claim(ClaimTypes.GivenName, firstName));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, identity.AuthenticationType));
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
    }

    private async Task FillClubsAsync(ProfileFormViewModel model, CancellationToken ct)
    {
        var clubs = await api.GetClubsAsync(ct);
        model.Clubs = (clubs.Value ?? [])
            .OrderBy(c => c.ClubName)
            .Select(c => new SelectListItem($"{c.ClubName} ({c.Province})", c.ClubId.ToString()))
            .ToList();
    }
}
