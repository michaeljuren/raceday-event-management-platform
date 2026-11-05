namespace RaceDay.Web.Models.Api;

// Mirrors of the RaceDay API's request and response contracts. The web app never touches the database;
// everything it shows or changes goes through these shapes over HTTP.

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount, int TotalPages);

public record UserSummary(int UserId, string FirstName, string LastName, string Email, IReadOnlyList<string> Roles);

public record TokenResponse(string AccessToken, string RefreshToken, int ExpiresIn, string TokenType, UserSummary User);

public record RegisterRequest(
    string FirstName, string LastName, string Email, string Password,
    DateOnly? DateOfBirth, string Gender, string? PhoneNumber, string Role);

public record LoginRequest(string Email, string Password);

public record RefreshTokenRequest(string RefreshToken);

public record VenueSummary(int VenueId, string VenueName, string City, string Province);

public record Venue(int VenueId, string VenueName, string? AddressLine, string City, string Province, string? PostalCode);

public record Club(int ClubId, string ClubName, string Province);

public record EventSummary(
    int EventId, string EventName, string Discipline, DateOnly EventDate, TimeOnly StartTime, string Status,
    int OrganizerId, string OrganizerName, VenueSummary Venue, IReadOnlyList<decimal> DistancesKm, string? BannerImageUrl);

public record EventDetail(
    int EventId, string EventName, string? Description, string Discipline, DateOnly EventDate, TimeOnly StartTime,
    DateTime RegistrationOpenDate, DateTime RegistrationCloseDate, string Status, string? BannerImageUrl,
    int OrganizerId, string OrganizerName, VenueSummary Venue, IReadOnlyList<decimal> DistancesKm,
    IReadOnlyList<Category> Categories, DateTime CreatedAt, DateTime UpdatedAt);

public record EventRequest(
    int? VenueId, string EventName, string? Description, string Discipline, DateOnly? EventDate, TimeOnly? StartTime,
    DateTime? RegistrationOpenDate, DateTime? RegistrationCloseDate, string? BannerImageUrl);

public record EventQuery(
    string? Search = null, string? Discipline = null, string? Province = null,
    DateOnly? FromDate = null, DateOnly? ToDate = null, bool Mine = false, int Page = 1, int PageSize = 12);

public record BannerUploadResponse(int EventId, string BannerImageUrl);

public record DashboardEvent(
    int EventId, string EventName, string Discipline, DateOnly EventDate, TimeOnly StartTime, string Status,
    string VenueName, string City, int CategoryCount, int TotalEnrolments, int ResultsRecorded, bool IsUpcoming);

public record Category(
    int CategoryId, int EventId, int? RouteId, string CategoryName, decimal DistanceKm, int? MinAge,
    string GenderRestriction, decimal EntryFee, int? MaxParticipants, int EntryCount, int? SpotsRemaining);

public record CategoryRequest(
    int? RouteId, string CategoryName, decimal DistanceKm, int? MinAge, string? GenderRestriction,
    decimal EntryFee, int? MaxParticipants);

public record Entry(
    int EntryId, int EventId, string EventName, DateOnly EventDate, int CategoryId, string CategoryName,
    decimal DistanceKm, int ParticipantId, string ParticipantName, string? BibNumber, DateTime EntryDate,
    string Status, decimal AmountPaid, string PaymentStatus, int? ResultId);

public record RosterEntry(
    int EntryId, int CategoryId, string CategoryName, int ParticipantId, string FirstName, string LastName,
    string Email, string Gender, DateOnly DateOfBirth, string? ClubName, string? EmergencyContactName,
    string? EmergencyContactPhone, string? BibNumber, DateTime EntryDate, string Status, string PaymentStatus);

public record Result(
    int ResultId, int EntryId, int EventId, string EventName, int CategoryId, string CategoryName,
    int ParticipantId, string ParticipantName, string? BibNumber, TimeOnly? FinishTime, TimeOnly? GunTime,
    TimeOnly? ChipTime, int? OverallPosition, int? CategoryPosition, int? GenderPosition, string FinishStatus,
    DateTime RecordedAt);

public record ResultRequest(
    TimeOnly? FinishTime, TimeOnly? GunTime, TimeOnly? ChipTime, int? OverallPosition, int? CategoryPosition,
    int? GenderPosition, string? FinishStatus);

public record UserProfile(
    int UserId, string FirstName, string LastName, string Email, string? PhoneNumber, DateOnly DateOfBirth,
    string Gender, int? ClubId, string? ClubName, string? EmergencyContactName, string? EmergencyContactPhone,
    DateTime CreatedAt, bool IsActive, IReadOnlyList<string> Roles);

public record UpdateProfileRequest(
    string FirstName, string LastName, string? PhoneNumber, int? ClubId,
    string? EmergencyContactName, string? EmergencyContactPhone);

public record PerformanceHistoryItem(
    int EntryId, int EventId, string EventName, DateOnly EventDate, string Discipline, int CategoryId,
    string CategoryName, decimal DistanceKm, TimeOnly? FinishTime, int? OverallPosition, int? CategoryPosition,
    int? GenderPosition, string FinishStatus);
