using System.ComponentModel.DataAnnotations;

namespace RaceDay.Web.Models.Account;

public class LoginViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

public class RegisterViewModel
{
    [Required, StringLength(100), Display(Name = "First name")]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(100), Display(Name = "Last name")]
    public string LastName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 8), DataType(DataType.Password)]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$",
        ErrorMessage = "Password must contain an upper-case letter, a lower-case letter and a digit.")]
    public string Password { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Display(Name = "Confirm password")]
    [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required, DataType(DataType.Date), Display(Name = "Date of birth")]
    public DateOnly? DateOfBirth { get; set; }

    [Required]
    public string Gender { get; set; } = string.Empty;

    [Phone, StringLength(20), Display(Name = "Phone number")]
    public string? PhoneNumber { get; set; }

    [Required, Display(Name = "I want to")]
    public string Role { get; set; } = "Participant";

    public static readonly string[] Genders = ["Female", "Male", "Other"];
}
