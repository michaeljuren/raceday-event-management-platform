using System.Net;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Tests.Infrastructure;

namespace RaceDay.Tests;

/// <summary>Organizers publishing results by marking a past event as Completed.</summary>
public class CompleteEventTests(RaceDayApiFactory factory) : IClassFixture<RaceDayApiFactory>
{
    private async Task<int> SeedPastEventAsync(int organizerId)
    {
        var (_, participant) = await factory.NewUserClientAsync("Participant");
        var entryId = await TestData.SeedPastEventWithEntryAsync(factory, organizerId, participant.User.UserId);
        return await factory.WithDbAsync(db => db.Entries.Where(e => e.EntryId == entryId).Select(e => e.Category.EventId).SingleAsync());
    }

    [Fact]
    public async Task Organizer_CanCompleteOwnEvent_AfterItHasTakenPlace()
    {
        var (organizer, tokens) = await factory.NewUserClientAsync("Organizer");
        var eventId = await SeedPastEventAsync(tokens.User.UserId);

        var response = await organizer.PostAsync($"/api/events/{eventId}/complete", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(EventStatus.Completed, (await response.ReadAsync<EventDetailResponse>()).Status);
        Assert.Equal(EventStatus.Completed, await factory.WithDbAsync(db => db.Events.Where(e => e.EventId == eventId).Select(e => e.Status).SingleAsync()));
    }

    [Fact]
    public async Task Complete_BeforeTheEventDate_Returns400()
    {
        var (organizer, _) = await factory.NewUserClientAsync("Organizer");
        var (eventId, _) = await TestData.CreatePublishedEventAsync(organizer);

        var response = await organizer.PostAsync($"/api/events/{eventId}/complete", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var evt = await (await organizer.GetAsync($"/api/events/{eventId}")).ReadAsync<EventDetailResponse>();
        Assert.Equal(EventStatus.Published, evt.Status);
    }

    [Fact]
    public async Task Complete_AnEventThatIsNotPublished_Returns400()
    {
        var (organizer, tokens) = await factory.NewUserClientAsync("Organizer");
        var eventId = await SeedPastEventAsync(tokens.User.UserId);

        var first = await organizer.PostAsync($"/api/events/{eventId}/complete", null);
        var again = await organizer.PostAsync($"/api/events/{eventId}/complete", null);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
    }

    [Fact]
    public async Task Complete_AnotherOrganizersEvent_Returns403()
    {
        var (_, owner) = await factory.NewUserClientAsync("Organizer");
        var (other, _) = await factory.NewUserClientAsync("Organizer");
        var eventId = await SeedPastEventAsync(owner.User.UserId);

        var response = await other.PostAsync($"/api/events/{eventId}/complete", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_CanCompleteAnyEvent()
    {
        var (_, owner) = await factory.NewUserClientAsync("Organizer");
        var admin = await factory.SeedUserClientAsync(ApiClient.SeedAdminEmail);
        var eventId = await SeedPastEventAsync(owner.User.UserId);

        var response = await admin.PostAsync($"/api/events/{eventId}/complete", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Complete_AsParticipantOrAnonymous_IsRejected()
    {
        var (participant, _) = await factory.NewUserClientAsync("Participant");

        var asParticipant = await participant.PostAsync("/api/events/1/complete", null);
        var anonymous = await factory.CreateClient().PostAsync("/api/events/1/complete", null);

        Assert.Equal(HttpStatusCode.Forbidden, asParticipant.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    [Fact]
    public async Task Complete_UnknownEvent_Returns404()
    {
        var (organizer, _) = await factory.NewUserClientAsync("Organizer");

        var response = await organizer.PostAsync("/api/events/999999/complete", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
