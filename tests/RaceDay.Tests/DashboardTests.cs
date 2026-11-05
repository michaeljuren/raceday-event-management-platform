using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Tests.Infrastructure;

namespace RaceDay.Tests;

/// <summary>The organizer dashboard summary.</summary>
public class DashboardTests(RaceDayApiFactory factory) : IClassFixture<RaceDayApiFactory>
{
    [Fact]
    public async Task Dashboard_CountsOnlyActiveEnrolments_ForOwnEvents()
    {
        var (organizer, _) = await factory.NewUserClientAsync("Organizer");
        var (eventId, categoryId) = await TestData.CreatePublishedEventAsync(organizer);

        var (first, _) = await factory.NewUserClientAsync("Participant");
        var (second, _) = await factory.NewUserClientAsync("Participant");
        var (withdrawn, _) = await factory.NewUserClientAsync("Participant");
        foreach (var participant in new[] { first, second, withdrawn })
        {
            Assert.Equal(HttpStatusCode.Created, (await participant.PostAsync($"/api/categories/{categoryId}/entries", null)).StatusCode);
        }

        var withdrawnEntry = Assert.Single(await (await withdrawn.GetAsync("/api/users/me/entries")).ReadAsync<List<EntryResponse>>());
        Assert.Equal(HttpStatusCode.OK, (await withdrawn.PutAsync($"/api/entries/{withdrawnEntry.EntryId}/cancel", null)).StatusCode);

        var response = await organizer.GetAsync("/api/events/dashboard");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var row = Assert.Single(await response.ReadAsync<List<DashboardEventResponse>>());
        Assert.Equal(eventId, row.EventId);
        Assert.Equal(2, row.TotalEnrolments);
        Assert.Equal(1, row.CategoryCount);
        Assert.Equal(0, row.ResultsRecorded);
        Assert.Equal(EventStatus.Published, row.Status);
        Assert.True(row.IsUpcoming);
    }

    [Fact]
    public async Task Dashboard_IncludesDrafts_AndListsUpcomingBeforePastEvents()
    {
        var (organizer, tokens) = await factory.NewUserClientAsync("Organizer");
        var (_, participantTokens) = await factory.NewUserClientAsync("Participant");
        await TestData.SeedPastEventWithEntryAsync(factory, tokens.User.UserId, participantTokens.User.UserId);
        var draft = await (await organizer.PostAsJsonAsync("/api/events", TestData.EventBody("Draft Dash"))).ReadAsync<EventDetailResponse>();

        var rows = await (await organizer.GetAsync("/api/events/dashboard")).ReadAsync<List<DashboardEventResponse>>();

        Assert.Equal(2, rows.Count);
        Assert.Equal(draft.EventId, rows[0].EventId);
        Assert.Equal(EventStatus.Draft, rows[0].Status);
        Assert.True(rows[0].IsUpcoming);
        Assert.False(rows[1].IsUpcoming);
        Assert.Equal(1, rows[1].TotalEnrolments);
    }

    [Fact]
    public async Task Dashboard_ExcludesOtherOrganizersEvents()
    {
        var (owner, _) = await factory.NewUserClientAsync("Organizer");
        var (other, _) = await factory.NewUserClientAsync("Organizer");
        await TestData.CreatePublishedEventAsync(owner);

        var rows = await (await other.GetAsync("/api/events/dashboard")).ReadAsync<List<DashboardEventResponse>>();

        Assert.Empty(rows);
    }

    [Fact]
    public async Task Dashboard_ForSeededOrganizer_CountsSeededEntriesAndResults()
    {
        // Seeded event 1 (Sarah) has entries 1 and 2, both confirmed and both with results.
        var organizer = await factory.SeedUserClientAsync(ApiClient.SeedOrganizerEmail);

        var rows = await (await organizer.GetAsync("/api/events/dashboard")).ReadAsync<List<DashboardEventResponse>>();

        var row = Assert.Single(rows, r => r.EventId == 1);
        Assert.Equal(2, row.TotalEnrolments);
        Assert.Equal(2, row.ResultsRecorded);
    }

    [Fact]
    public async Task Dashboard_AsParticipantOrAnonymous_IsRejected()
    {
        var (participant, _) = await factory.NewUserClientAsync("Participant");

        var asParticipant = await participant.GetAsync("/api/events/dashboard");
        var anonymous = await factory.CreateClient().GetAsync("/api/events/dashboard");

        Assert.Equal(HttpStatusCode.Forbidden, asParticipant.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }
}
