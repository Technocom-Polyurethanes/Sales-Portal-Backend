using sales_portal.core.DTOs;
using sales_portal.core.Notifications;
using sales_portal.core.Services;
using sales_portal.domain.Enums;
using sales_portal.domain.Models;
using Xunit;

namespace SalesPortal.Tests;

// Which writes create an inbox event, and which stay silent.
public class NotificationEmissionTests
{
    [Fact]
    public async Task Create_offer_emits_offer_created_once()
    {
        using var db = new TestDatabase();
        var basics = TestFactory.SeedBasics(db.Context);
        var publisher = new RecordingPublisher();
        var service = TestFactory.CreateOfferService(db.Context, Actor(basics.User), publisher);

        var offer = await service.CreateAsync(Draft(basics), basics.User.Id, CancellationToken.None);

        var notification = Assert.Single(publisher.Events);
        Assert.Equal(NotificationTypes.OfferCreated, notification.Type);
        Assert.Equal("offer", notification.EntityType);
        Assert.Equal(offer.Id, notification.EntityId);
        Assert.Equal(offer.Reference, notification.EntityLabel);
        Assert.Equal(basics.User.FullName, notification.ActorName);
    }

    [Fact]
    public async Task Revise_offer_emits_offer_revised_not_created()
    {
        using var db = new TestDatabase();
        var basics = TestFactory.SeedBasics(db.Context);
        var publisher = new RecordingPublisher();
        var service = TestFactory.CreateOfferService(db.Context, Actor(basics.User), publisher);
        var created = await service.CreateAsync(Draft(basics), basics.User.Id, CancellationToken.None);
        publisher.Events.Clear();

        var revised = await service.ReviseAsync(created.Id, basics.User.Id, CancellationToken.None);

        var notification = Assert.Single(publisher.Events);
        Assert.Equal(NotificationTypes.OfferRevised, notification.Type);
        Assert.Equal(revised.Id, notification.EntityId);
        Assert.Equal(revised.Version, notification.Meta!["version"]);
        Assert.DoesNotContain(NotificationTypes.OfferCreated, publisher.Events.Select(e => e.Type));
    }

    [Fact]
    public async Task Issuing_an_offer_emits_one_status_changed_with_from_and_to()
    {
        using var db = new TestDatabase();
        var basics = TestFactory.SeedBasics(db.Context);
        var (materialId, packageTypeId) = AddIssueLine(db);
        var publisher = new RecordingPublisher();
        var service = TestFactory.CreateOfferService(db.Context, Actor(basics.User), publisher);
        var draft = Draft(basics);
        draft.Lines.Add(new OfferDraftLineDto
        {
            MaterialId = materialId,
            RatePerKg = 10m,
            Packaging =
            [
                new OfferDraftPackagingDto
                {
                    PackageTypeId = packageTypeId,
                    WeightPerPackageKg = 210m,
                    Packages = 1
                }
            ]
        });
        var created = await service.CreateAsync(draft, basics.User.Id, CancellationToken.None);
        publisher.Events.Clear();

        await service.UpdateStatusAsync(
            created.Id,
            new UpdateOfferStatusDto { Status = OfferStatus.Issued },
            basics.User.Id,
            CancellationToken.None);

        var notification = Assert.Single(publisher.Events);
        Assert.Equal(NotificationTypes.OfferStatusChanged, notification.Type);
        Assert.Equal(created.Id, notification.EntityId);
        Assert.Equal("Draft", notification.Meta!["from"]);
        Assert.Equal("Issued", notification.Meta!["to"]);
    }

    [Fact]
    public async Task Same_status_emits_nothing()
    {
        using var db = new TestDatabase();
        var basics = TestFactory.SeedBasics(db.Context);
        var publisher = new RecordingPublisher();
        var service = TestFactory.CreateOfferService(db.Context, Actor(basics.User), publisher);
        var created = await service.CreateAsync(Draft(basics), basics.User.Id, CancellationToken.None);
        publisher.Events.Clear();

        await service.UpdateStatusAsync(
            created.Id,
            new UpdateOfferStatusDto { Status = OfferStatus.Draft },
            basics.User.Id,
            CancellationToken.None);

        Assert.Empty(publisher.Events);
    }

    [Fact]
    public async Task Delete_offer_emits_offer_deleted_with_the_reference()
    {
        using var db = new TestDatabase();
        var basics = TestFactory.SeedBasics(db.Context);
        var publisher = new RecordingPublisher();
        var service = TestFactory.CreateOfferService(db.Context, Actor(basics.User), publisher);
        var created = await service.CreateAsync(Draft(basics), basics.User.Id, CancellationToken.None);
        publisher.Events.Clear();

        Assert.True(await service.DeleteAsync(created.Id, CancellationToken.None));

        var notification = Assert.Single(publisher.Events);
        Assert.Equal(NotificationTypes.OfferDeleted, notification.Type);
        Assert.Null(notification.EntityId);
        Assert.Equal(created.Reference, notification.EntityLabel);
    }

    [Fact]
    public async Task Draft_update_emits_nothing()
    {
        using var db = new TestDatabase();
        var basics = TestFactory.SeedBasics(db.Context);
        var publisher = new RecordingPublisher();
        var service = TestFactory.CreateOfferService(db.Context, Actor(basics.User), publisher);
        var created = await service.CreateAsync(Draft(basics), basics.User.Id, CancellationToken.None);
        publisher.Events.Clear();

        await service.UpdateAsync(created.Id, Draft(basics), basics.User.Id, CancellationToken.None);

        Assert.Empty(publisher.Events);
    }

    [Fact]
    public async Task Customer_create_and_delete_emit_but_update_does_not()
    {
        using var db = new TestDatabase();
        var basics = TestFactory.SeedBasics(db.Context);
        var publisher = new RecordingPublisher();
        var service = TestFactory.CreateCustomerService(db.Context, Actor(basics.User), publisher);

        var created = await service.CreateAsync(Customer(), CancellationToken.None);
        var createdEvent = Assert.Single(publisher.Events);
        Assert.Equal(NotificationTypes.CustomerCreated, createdEvent.Type);
        Assert.Equal("customer", createdEvent.EntityType);
        Assert.Equal(created.Id, createdEvent.EntityId);
        Assert.Equal(created.Name, createdEvent.EntityLabel);

        publisher.Events.Clear();
        await service.UpdateAsync(created.Id, Customer(), CancellationToken.None);
        Assert.Empty(publisher.Events);

        Assert.True(await service.DeleteAsync(created.Id, CancellationToken.None));
        var deleted = Assert.Single(publisher.Events);
        Assert.Equal(NotificationTypes.CustomerDeleted, deleted.Type);
        Assert.Null(deleted.EntityId);
        Assert.Equal(created.Name, deleted.EntityLabel);
    }

    [Fact]
    public async Task Material_create_and_delete_emit_but_update_does_not()
    {
        using var db = new TestDatabase();
        var basics = TestFactory.SeedBasics(db.Context);
        var publisher = new RecordingPublisher();
        var service = TestFactory.CreateMaterialService(db.Context, Actor(basics.User), publisher);

        var created = await service.CreateAsync(Material(), CancellationToken.None);
        var createdEvent = Assert.Single(publisher.Events);
        Assert.Equal(NotificationTypes.MaterialCreated, createdEvent.Type);
        Assert.Equal("material", createdEvent.EntityType);
        Assert.Equal(created!.Id, createdEvent.EntityId);
        Assert.Equal(created.Name, createdEvent.EntityLabel);

        publisher.Events.Clear();
        await service.UpdateAsync(created.Id, Material(), CancellationToken.None);
        Assert.Empty(publisher.Events);

        Assert.True(await service.DeleteAsync(created.Id, CancellationToken.None));
        var deleted = Assert.Single(publisher.Events);
        Assert.Equal(NotificationTypes.MaterialDeleted, deleted.Type);
        Assert.Null(deleted.EntityId);
        Assert.Equal(created.Name, deleted.EntityLabel);
    }

    [Fact]
    public async Task Bank_create_and_delete_emit_but_update_does_not()
    {
        using var db = new TestDatabase();
        var basics = TestFactory.SeedBasics(db.Context);
        var publisher = new RecordingPublisher();
        var service = TestFactory.CreateBankAccountService(db.Context, actor: Actor(basics.User), publisher: publisher);

        var created = await service.CreateAsync(Bank(), CancellationToken.None);
        var createdEvent = Assert.Single(publisher.Events);
        Assert.Equal(NotificationTypes.BankCreated, createdEvent.Type);
        Assert.Equal("bank", createdEvent.EntityType);
        Assert.Equal(created.Id, createdEvent.EntityId);
        Assert.Equal(created.Bank, createdEvent.EntityLabel);

        publisher.Events.Clear();
        await service.UpdateAsync(created.Id, Bank(), CancellationToken.None);
        Assert.Empty(publisher.Events);

        Assert.True(await service.DeleteAsync(created.Id, CancellationToken.None));
        var deleted = Assert.Single(publisher.Events);
        Assert.Equal(NotificationTypes.BankDeleted, deleted.Type);
        Assert.Null(deleted.EntityId);
        Assert.Equal(created.Bank, deleted.EntityLabel);
    }

    [Fact]
    public async Task User_deactivation_emits_and_closes_streams()
    {
        using var db = new TestDatabase();
        var basics = TestFactory.SeedBasics(db.Context);
        var publisher = new RecordingPublisher();
        var broadcaster = new RecordingNotificationBroadcaster();
        var service = TestFactory.CreateUserService(
            db.Context, Actor(basics.User), publisher, broadcaster);
        var update = Update(basics.SecondUser);
        update.IsActive = false;

        await service.UpdateAsync(basics.SecondUser.Id, update, basics.User.Id, CancellationToken.None);

        var notification = Assert.Single(publisher.Events);
        Assert.Equal(NotificationTypes.UserDeactivated, notification.Type);
        Assert.Equal("user", notification.EntityType);
        Assert.Equal(basics.SecondUser.Id, notification.EntityId);
        Assert.Equal(basics.SecondUser.FullName, notification.EntityLabel);
        Assert.Contains(basics.SecondUser.Id, broadcaster.Closed);
    }

    [Fact]
    public async Task User_role_change_emits_role_changed()
    {
        using var db = new TestDatabase();
        var basics = TestFactory.SeedBasics(db.Context);
        var publisher = new RecordingPublisher();
        var broadcaster = new RecordingNotificationBroadcaster();
        var service = TestFactory.CreateUserService(
            db.Context, Actor(basics.User), publisher, broadcaster);
        var update = Update(basics.SecondUser);
        update.Role = UserRole.Sales;

        await service.UpdateAsync(basics.SecondUser.Id, update, basics.User.Id, CancellationToken.None);

        var notification = Assert.Single(publisher.Events);
        Assert.Equal(NotificationTypes.UserRoleChanged, notification.Type);
        Assert.Equal(basics.SecondUser.Id, notification.EntityId);
        Assert.Equal("Admin", notification.Meta!["from"]);
        Assert.Equal("Sales", notification.Meta!["to"]);
        Assert.Contains(basics.SecondUser.Id, broadcaster.Closed);
    }

    [Fact]
    public async Task User_profile_edit_emits_nothing()
    {
        using var db = new TestDatabase();
        var basics = TestFactory.SeedBasics(db.Context);
        var publisher = new RecordingPublisher();
        var broadcaster = new RecordingNotificationBroadcaster();
        var service = TestFactory.CreateUserService(
            db.Context, Actor(basics.User), publisher, broadcaster);
        var update = Update(basics.SecondUser);
        update.FullName = "Renamed User";

        await service.UpdateAsync(basics.SecondUser.Id, update, basics.User.Id, CancellationToken.None);

        Assert.Empty(publisher.Events);
        Assert.Empty(broadcaster.Closed);
    }

    [Fact]
    public async Task No_actor_emits_nothing()
    {
        using var db = new TestDatabase();
        var publisher = new RecordingPublisher();
        var service = TestFactory.CreateCustomerService(db.Context, publisher: publisher);

        await service.CreateAsync(Customer(), CancellationToken.None);

        Assert.Empty(publisher.Events);
    }

    private static OfferDraftDto Draft(SeededBasics basics) => new()
    {
        OfferType = OfferType.Loc,
        IssueDate = new DateOnly(2026, 10, 1),
        CustomerId = basics.Customer.Id,
        PaymentTermId = basics.PaymentTerm.Id
    };

    private static (Guid MaterialId, Guid PackageTypeId) AddIssueLine(TestDatabase db)
    {
        var material = new Material
        {
            ItemCode = "MAT-LINE",
            Name = "Polyol 100",
            Category = "Polyol"
        };
        var packageType = new PackageType { Name = "Drum", NameAr = "برميل" };
        db.Context.AddRange(material, packageType);
        db.Context.SaveChanges();
        return (material.Id, packageType.Id);
    }

    private static UpsertCustomerDto Customer() => new()
    {
        CustomerCode = "CUST-NEW",
        Name = "New Customer",
        Address = "New address"
    };

    private static UpsertMaterialDto Material() => new()
    {
        ItemCode = "MAT-NEW",
        Name = "New Material",
        Category = "Polyol"
    };

    private static UpsertBankAccountDto Bank() => new()
    {
        Bank = "QNB",
        Branch = "Cairo",
        AccountNumber = "123456789"
    };

    private static UpdateUserDto Update(User user) => new()
    {
        FullName = user.FullName,
        Initials = user.Initials,
        Role = user.Role,
        IsActive = user.IsActive
    };

    private static ICurrentActor Actor(User user) => new TestActor
    {
        UserId = user.Id,
        DisplayName = user.FullName
    };

    private sealed class TestActor : ICurrentActor
    {
        public Guid? UserId { get; init; }

        public string? DisplayName { get; init; }
    }

    private sealed class RecordingPublisher : INotificationPublisher
    {
        public List<NotificationEvent> Events { get; } = new();

        public Task PublishAsync(
            NotificationEvent notification,
            CancellationToken cancellationToken = default)
        {
            Events.Add(notification);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingNotificationBroadcaster : INotificationBroadcaster
    {
        public List<Guid> Closed { get; } = new();

        public INotificationSubscription Subscribe(Guid userId) =>
            NoNotificationBroadcaster.Instance.Subscribe(userId);

        public void Publish(NotificationEventPayload payload)
        {
        }

        public void CloseUserSubscriptions(Guid userId) => Closed.Add(userId);
    }
}
