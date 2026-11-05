using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using RaceDay.Web.Models;
using RaceDay.Web.Services.Api;

namespace RaceDay.Web.Infrastructure;

/// <summary>
/// Turns API-level failures into something the user can act on: an expired API session signs the user out
/// and sends them to the login page; an unreachable API shows a friendly "service unavailable" page.
/// </summary>
public class ApiExceptionFilter(ILogger<ApiExceptionFilter> logger, ITempDataDictionaryFactory tempDataFactory) : IAsyncExceptionFilter
{
    public async Task OnExceptionAsync(ExceptionContext context)
    {
        var http = context.HttpContext;
        switch (context.Exception)
        {
            case ApiSessionExpiredException:
                await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                http.Session.Clear();
                tempDataFactory.GetTempData(http)[Flash.ErrorKey] = "Your session has expired. Please log in again.";
                context.Result = new RedirectToActionResult("Login", "Account",
                    new { area = "", returnUrl = http.Request.Method == "GET" ? http.Request.Path + http.Request.QueryString : null });
                context.ExceptionHandled = true;
                break;

            case HttpRequestException:
            case TaskCanceledException when !http.RequestAborted.IsCancellationRequested:
                logger.LogError(context.Exception, "The RaceDay API could not be reached.");
                context.Result = new ViewResult
                {
                    ViewName = "~/Views/Shared/Error.cshtml",
                    StatusCode = StatusCodes.Status503ServiceUnavailable,
                    ViewData = new ViewDataDictionary<ErrorViewModel>(
                        new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(), context.ModelState)
                    {
                        Model = new ErrorViewModel
                        {
                            RequestId = http.TraceIdentifier,
                            Title = "We can't reach the race office",
                            Message = "The RaceDay service is not responding right now. Please try again in a moment."
                        }
                    }
                };
                context.ExceptionHandled = true;
                break;
        }
    }
}
