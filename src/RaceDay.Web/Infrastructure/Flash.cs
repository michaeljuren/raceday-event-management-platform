using Microsoft.AspNetCore.Mvc;

namespace RaceDay.Web.Infrastructure;

/// <summary>One-shot success and error messages shown by the shared _StatusMessages partial.</summary>
public static class Flash
{
    public const string SuccessKey = "flash.success";
    public const string ErrorKey = "flash.error";

    public static void Success(this Controller controller, string message) => controller.TempData[SuccessKey] = message;

    public static void Error(this Controller controller, string message) => controller.TempData[ErrorKey] = message;
}
