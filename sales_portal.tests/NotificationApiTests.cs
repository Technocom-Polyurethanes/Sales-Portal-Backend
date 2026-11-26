using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using sales_portal.core.Notifications;
using sales_portal.domain.Enums;
using sales_portal.domain.Models;
using sales_portal.infrastructure.Contexts;
using Xunit;

namespace SalesPortal.Tests;

[Collection(ProcessEnvironment.Name)]
public class NotificationApiTests : IClassFixture<SalesPortalApiFactory>
{
    private readonly SalesPortalApiFactory _factory;

    public NotificationApiTests(SalesPortalApiFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Sales")]
    [InlineData("Accountant")]
    public async Task Every_authenticated_role_can_read_the_inbox(string role)
    {
        using var client = _factory.CreateAuthenticatedClient(role: role);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/notifications")).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.GetAsync("/api/notifications/unread-count")).StatusCode);
    }

    [Fact]
    public async Task An_overflowing_page_returns_an_empty_page_not_a_server_error()
    {
        using var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/notifications?page=2147483647&pageSize=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Empty(body.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Inbox_is_scoped_to_the_signed_in_user()
    {
        var ownerId = _factory.SeedUser(UserRole.Admin);
        var otherId = _factory.SeedUser(UserRole.Sales);
        var notificationId = Guid.NewGuid();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Notifications.Add(new Notification
            {
                Id = notificationId,
                RecipientUserId = ownerId,
                Type = NotificationTypes.OfferCreated,
                ActorName = "Actor",
                EntityType = "offer",
                EntityLabel = "Loc/0001-2026/AA",
                Meta = "{}",
                CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        using var owner = _factory.CreateAuthenticatedClient(ownerId);
        using var other = _factory.CreateAuthenticatedClient(otherId, "Sales");

        var ownerPage = await owner.GetFromJsonAsync<JsonElement>("/api/notifications");
        Assert.Equal(1, ownerPage.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, ownerPage.GetProperty("unreadCount").GetInt32());
        Assert.Equal(notificationId, ownerPage.GetProperty("items")[0].GetProperty("id").GetGuid());

        var otherPage = await other.GetFromJsonAsync<JsonElement>("/api/notifications");
        Assert.Equal(0, otherPage.GetProperty("totalCount").GetInt32());
        Assert.Equal(0, otherPage.GetProperty("unreadCount").GetInt32());
        Assert.Empty(otherPage.GetProperty("items").EnumerateArray());

        var otherUnread = await other.GetFromJsonAsync<JsonElement>("/api/notifications/unread-count");
        Assert.Equal(0, otherUnread.GetProperty("unreadCount").GetInt32());

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await other.PostAsync($"/api/notifications/{notificationId}/read", null)).StatusCode);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await owner.PostAsync($"/api/notifications/{notificationId}/read", null)).StatusCode);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await owner.PostAsync("/api/notifications/read-all", null)).StatusCode);

        var finalUnread = await owner.GetFromJsonAsync<JsonElement>("/api/notifications/unread-count");
        Assert.Equal(0, finalUnread.GetProperty("unreadCount").GetInt32());
    }
}
