using sales_portal.core.DTOs;
using sales_portal.core.Exceptions;
using sales_portal.core.Services;
using sales_portal.domain.Enums;
using sales_portal.domain.Models;
using sales_portal.infrastructure;
using Xunit;

namespace SalesPortal.Tests;

public class UserServiceTests
{
    private static UserService CreateService(TestDatabase db) => new(new Repository<User>(db.Context));

    private static CreateUserDto NewUser(string username = "jdoe", UserRole? role = UserRole.Sales) => new()
    {
        Username = username,
        FullName = "Jane Doe",
        Initials = "jd",
        Email = "jane@example.com",
        Role = role,
        Password = "correct-horse"
    };

    private static User AddUser(TestDatabase db, string username, UserRole role, bool isActive = true)
    {
        var user = new User
        {
            Username = username,
            FullName = username,
            Initials = "XX",
            PasswordHash = "x",
            Role = role,
            IsActive = isActive
        };
        db.Context.Users.Add(user);
        db.Context.SaveChanges();
        return user;
    }

    private static UpdateUserDto UpdateFor(User user, UserRole role, bool? isActive = null) => new()
    {
        FullName = user.FullName,
        Initials = user.Initials,
        Role = role,
        IsActive = isActive
    };

    [Fact]
    public async Task Create_hashes_the_password_and_normalises_the_fields()
    {
        using var db = new TestDatabase();
        var service = CreateService(db);

        var created = await service.CreateAsync(NewUser(), CancellationToken.None);

        Assert.Equal("JD", created.Initials);
        Assert.Equal(UserRole.Sales, created.Role);
        Assert.True(created.IsActive);

        var stored = db.Context.Users.Single(u => u.Id == created.Id);
        Assert.NotEqual("correct-horse", stored.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify("correct-horse", stored.PasswordHash));
    }

    [Fact]
    public async Task Create_rejects_a_duplicate_username_ignoring_case()
    {
        using var db = new TestDatabase();
        var service = CreateService(db);
        await service.CreateAsync(NewUser("jdoe"), CancellationToken.None);

        var error = await Assert.ThrowsAsync<ValidationException>(
            () => service.CreateAsync(NewUser("JDoe"), CancellationToken.None));

        Assert.NotNull(error.FieldErrors);
        Assert.True(error.FieldErrors!.ContainsKey("username"));
    }

    [Fact]
    public async Task Create_requires_a_role_so_a_missing_one_never_becomes_admin()
    {
        using var db = new TestDatabase();
        var service = CreateService(db);

        var error = await Assert.ThrowsAsync<ValidationException>(
            () => service.CreateAsync(NewUser(role: null), CancellationToken.None));

        Assert.True(error.FieldErrors!.ContainsKey("role"));
        Assert.Empty(db.Context.Users);
    }

    [Theory]
    [InlineData("short")]
    [InlineData("")]
    public async Task Create_rejects_a_weak_password(string password)
    {
        using var db = new TestDatabase();
        var service = CreateService(db);
        var dto = NewUser();
        dto.Password = password;

        var error = await Assert.ThrowsAsync<ValidationException>(
            () => service.CreateAsync(dto, CancellationToken.None));

        Assert.True(error.FieldErrors!.ContainsKey("password"));
    }

    [Fact]
    public async Task Create_rejects_initials_that_cannot_be_part_of_an_offer_reference()
    {
        using var db = new TestDatabase();
        var service = CreateService(db);
        var dto = NewUser();
        dto.Initials = "J/D";

        var error = await Assert.ThrowsAsync<ValidationException>(
            () => service.CreateAsync(dto, CancellationToken.None));

        Assert.True(error.FieldErrors!.ContainsKey("initials"));
    }

    [Fact]
    public async Task Update_cannot_demote_the_last_active_admin()
    {
        using var db = new TestDatabase();
        var service = CreateService(db);
        var onlyAdmin = AddUser(db, "admin", UserRole.Admin);

        await Assert.ThrowsAsync<ConflictException>(
            () => service.UpdateAsync(onlyAdmin.Id, UpdateFor(onlyAdmin, UserRole.Sales), Guid.NewGuid(), CancellationToken.None));

        Assert.Equal(UserRole.Admin, db.Context.Users.Single(u => u.Id == onlyAdmin.Id).Role);
    }

    [Fact]
    public async Task Update_cannot_deactivate_or_demote_yourself()
    {
        using var db = new TestDatabase();
        var service = CreateService(db);
        var me = AddUser(db, "me", UserRole.Admin);
        AddUser(db, "other", UserRole.Admin);

        await Assert.ThrowsAsync<ConflictException>(
            () => service.UpdateAsync(me.Id, UpdateFor(me, UserRole.Admin, isActive: false), me.Id, CancellationToken.None));
        await Assert.ThrowsAsync<ConflictException>(
            () => service.UpdateAsync(me.Id, UpdateFor(me, UserRole.Sales), me.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Update_can_demote_an_admin_when_another_active_admin_exists()
    {
        using var db = new TestDatabase();
        var service = CreateService(db);
        var actor = AddUser(db, "actor", UserRole.Admin);
        var target = AddUser(db, "target", UserRole.Admin);

        var updated = await service.UpdateAsync(
            target.Id, UpdateFor(target, UserRole.Accountant), actor.Id, CancellationToken.None);

        Assert.Equal(UserRole.Accountant, updated.Role);
    }

    [Fact]
    public async Task Update_keeps_the_active_flag_when_it_is_omitted()
    {
        using var db = new TestDatabase();
        var service = CreateService(db);
        var inactive = AddUser(db, "sleeper", UserRole.Sales, isActive: false);

        var updated = await service.UpdateAsync(
            inactive.Id, UpdateFor(inactive, UserRole.Sales), Guid.NewGuid(), CancellationToken.None);

        Assert.False(updated.IsActive);
    }

    [Fact]
    public async Task Reset_password_replaces_the_hash()
    {
        using var db = new TestDatabase();
        var service = CreateService(db);
        var created = await service.CreateAsync(NewUser(), CancellationToken.None);

        await service.ResetPasswordAsync(
            created.Id, new ResetPasswordDto { NewPassword = "a-brand-new-one" }, CancellationToken.None);

        var stored = db.Context.Users.Single(u => u.Id == created.Id);
        Assert.True(BCrypt.Net.BCrypt.Verify("a-brand-new-one", stored.PasswordHash));
        Assert.False(BCrypt.Net.BCrypt.Verify("correct-horse", stored.PasswordHash));
    }

    [Fact]
    public async Task Paged_list_filters_by_search_role_and_status()
    {
        using var db = new TestDatabase();
        var service = CreateService(db);
        AddUser(db, "amira", UserRole.Sales);
        AddUser(db, "hassan", UserRole.Accountant);
        AddUser(db, "layla", UserRole.Sales, isActive: false);

        var all = await service.GetPagedAsync(null, null, null, 1, 10, CancellationToken.None);
        var sales = await service.GetPagedAsync(null, UserRole.Sales, null, 1, 10, CancellationToken.None);
        var activeSales = await service.GetPagedAsync(null, UserRole.Sales, true, 1, 10, CancellationToken.None);
        var search = await service.GetPagedAsync("HASS", null, null, 1, 10, CancellationToken.None);
        var secondPage = await service.GetPagedAsync(null, null, null, 2, 2, CancellationToken.None);

        Assert.Equal(3, all.TotalCount);
        Assert.Equal(2, sales.TotalCount);
        Assert.Equal("amira", Assert.Single(activeSales.Items).Username);
        Assert.Equal("hassan", Assert.Single(search.Items).Username);
        Assert.Single(secondPage.Items);
        Assert.Equal(2, secondPage.TotalPages);
    }
}
