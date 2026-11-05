using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Infrastructure;
using RaceDay.Web.Models;
using RaceDay.Web.Models.Api;
using RaceDay.Web.Services.Api;

namespace RaceDay.Web.Controllers;

[AllowAnonymous]
public class HomeController(IRaceDayApi api) : Controller
{
    /// <summary>Landing page with the next few race days.</summary>
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var upcoming = await api.GetEventsAsync(new EventQuery(FromDate: Display.Today, PageSize: 6), ct);
        var events = upcoming.Value?.Items.Where(e => e.Status == "Published").ToList() ?? [];
        return View(events);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });

    /// <summary>Friendly pages for 404s and other non-success status codes.</summary>
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Status(int code)
    {
        var model = code switch
        {
            404 => new ErrorViewModel { Title = "Off course", Message = "We couldn't find the page you were looking for." },
            403 => new ErrorViewModel { Title = "Restricted area", Message = "You don't have access to that page." },
            _ => new ErrorViewModel { Title = "Something went wrong", Message = $"The request could not be completed ({code})." }
        };
        model.StatusCode = code;
        model.RequestId = HttpContext.TraceIdentifier;
        return View("Error", model);
    }
}
