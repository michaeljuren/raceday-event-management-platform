using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using RaceDay.Api.Dtos;
using RaceDay.Tests.Infrastructure;

namespace RaceDay.Tests;

/// <summary>Organizers uploading event banner images to blob storage.</summary>
public class BannerUploadTests(RaceDayApiFactory factory) : IClassFixture<RaceDayApiFactory>
{
    private static MultipartFormDataContent ImageForm(string contentType = "image/png", int bytes = 2048, string fileName = "banner.png")
    {
        var file = new ByteArrayContent(new byte[bytes]);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { file, "file", fileName } };
    }

    private static async Task<int> CreateEventAsync(HttpClient organizer)
    {
        var created = await organizer.PostAsJsonAsync("/api/events", TestData.EventBody());
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return (await created.ReadAsync<EventDetailResponse>()).EventId;
    }

    [Theory]
    [InlineData("image/png", "banner.png", ".png")]
    [InlineData("image/jpeg", "banner.jpg", ".jpg")]
    [InlineData("image/webp", "banner.webp", ".webp")]
    public async Task Organizer_CanUploadBanner_ForOwnEvent(string contentType, string fileName, string extension)
    {
        var (organizer, _) = await factory.NewUserClientAsync("Organizer");
        var eventId = await CreateEventAsync(organizer);

        var response = await organizer.PostAsync($"/api/events/{eventId}/banner", ImageForm(contentType, fileName: fileName));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var upload = await response.ReadAsync<BannerUploadResponse>();
        Assert.StartsWith($"{InMemoryBannerStorage.BaseUrl}events/{eventId}/", upload.BannerImageUrl);
        Assert.EndsWith(extension, upload.BannerImageUrl);
        Assert.Equal(contentType, factory.BannerStorage.Blobs[upload.BannerImageUrl].ContentType);

        var evt = await (await organizer.GetAsync($"/api/events/{eventId}")).ReadAsync<EventDetailResponse>();
        Assert.Equal(upload.BannerImageUrl, evt.BannerImageUrl);
    }

    [Fact]
    public async Task UploadedBanner_AppearsOnEventSummaries()
    {
        var (organizer, _) = await factory.NewUserClientAsync("Organizer");
        var (eventId, _) = await TestData.CreatePublishedEventAsync(organizer);
        var upload = await (await organizer.PostAsync($"/api/events/{eventId}/banner", ImageForm())).ReadAsync<BannerUploadResponse>();

        var page = await (await factory.CreateClient().GetAsync("/api/events?pageSize=100")).ReadAsync<PagedResult<EventSummaryResponse>>();

        Assert.Equal(upload.BannerImageUrl, Assert.Single(page.Items, e => e.EventId == eventId).BannerImageUrl);
    }

    [Fact]
    public async Task ReplacingBanner_DeletesThePreviousBlob()
    {
        var (organizer, _) = await factory.NewUserClientAsync("Organizer");
        var eventId = await CreateEventAsync(organizer);

        var first = await (await organizer.PostAsync($"/api/events/{eventId}/banner", ImageForm())).ReadAsync<BannerUploadResponse>();
        var second = await (await organizer.PostAsync($"/api/events/{eventId}/banner", ImageForm())).ReadAsync<BannerUploadResponse>();

        Assert.NotEqual(first.BannerImageUrl, second.BannerImageUrl);
        Assert.False(factory.BannerStorage.Blobs.ContainsKey(first.BannerImageUrl));
        Assert.True(factory.BannerStorage.Blobs.ContainsKey(second.BannerImageUrl));
    }

    [Theory]
    [InlineData("image/gif", "banner.gif")]
    [InlineData("application/pdf", "banner.pdf")]
    [InlineData("text/plain", "banner.txt")]
    public async Task UploadBanner_WithUnsupportedType_Returns400(string contentType, string fileName)
    {
        var (organizer, _) = await factory.NewUserClientAsync("Organizer");
        var eventId = await CreateEventAsync(organizer);

        var response = await organizer.PostAsync($"/api/events/{eventId}/banner", ImageForm(contentType, fileName: fileName));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadBanner_LargerThan5MB_Returns400()
    {
        var (organizer, _) = await factory.NewUserClientAsync("Organizer");
        var eventId = await CreateEventAsync(organizer);

        var response = await organizer.PostAsync($"/api/events/{eventId}/banner", ImageForm(bytes: 5 * 1024 * 1024 + 1));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadBanner_WithoutFile_Returns400()
    {
        var (organizer, _) = await factory.NewUserClientAsync("Organizer");
        var eventId = await CreateEventAsync(organizer);

        var response = await organizer.PostAsync($"/api/events/{eventId}/banner", new MultipartFormDataContent());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadBanner_ForAnotherOrganizersEvent_Returns403()
    {
        var (owner, _) = await factory.NewUserClientAsync("Organizer");
        var (other, _) = await factory.NewUserClientAsync("Organizer");
        var eventId = await CreateEventAsync(owner);

        var response = await other.PostAsync($"/api/events/{eventId}/banner", ImageForm());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UploadBanner_AsParticipantOrAnonymous_IsRejected()
    {
        var (organizer, _) = await factory.NewUserClientAsync("Organizer");
        var (participant, _) = await factory.NewUserClientAsync("Participant");
        var eventId = await CreateEventAsync(organizer);

        var asParticipant = await participant.PostAsync($"/api/events/{eventId}/banner", ImageForm());
        var anonymous = await factory.CreateClient().PostAsync($"/api/events/{eventId}/banner", ImageForm());

        Assert.Equal(HttpStatusCode.Forbidden, asParticipant.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    [Fact]
    public async Task UploadBanner_ForUnknownEvent_Returns404()
    {
        var (organizer, _) = await factory.NewUserClientAsync("Organizer");

        var response = await organizer.PostAsync("/api/events/999999/banner", ImageForm());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
