using Microsoft.EntityFrameworkCore;
using sales_portal.core.Exceptions;
using sales_portal.domain.Enums;
using sales_portal.domain.Models;
using sales_portal.infrastructure;
using sales_portal.infrastructure.Contexts;
using Xunit;

namespace SalesPortal.Tests;

public class BootstrapAdminTests
{
    [Fact]
    public async Task BootstrapAdmin_creates_admin_when_users_table_is_empty_and_variables_are_set()
    {
        using var db = new TestDatabase();

        // Set required environment variables
        Environment.SetEnvironmentVariable("BOOTSTRAP_ADMIN_USERNAME", "bootstrap_admin");
        Environment.SetEnvironmentVariable("BOOTSTRAP_ADMIN_PASSWORD", "SecurePassword123");
        Environment.SetEnvironmentVariable("BOOTSTRAP_ADMIN_FULL_NAME", "Bootstrap Admin");
        Environment.SetEnvironmentVariable("BOOTSTRAP_ADMIN_INITIALS", "BA");
        Environment.SetEnvironmentVariable("BOOTSTRAP_ADMIN_EMAIL", "bootstrap@example.com");

        try
        {
            await DbInitializer.BootstrapAdminAsync(db.Context);

            var user = await db.Context.Users.SingleAsync();
            Assert.Equal("bootstrap_admin", user.Username);
            Assert.Equal("Bootstrap Admin", user.FullName);
            Assert.Equal("BA", user.Initials);
            Assert.Equal("bootstrap@example.com", user.Email);
            Assert.Equal(UserRole.Admin, user.Role);
            Assert.True(user.IsActive);
            Assert.True(BCrypt.Net.BCrypt.Verify("SecurePassword123", user.PasswordHash));
        }
        finally
        {
            // Clean up environment variables
            Environment.SetEnvironmentVariable("BOOTSTRAP_ADMIN_USERNAME", null);
            Environment.SetEnvironmentVariable("BOOTSTRAP_ADMIN_PASSWORD", null);
            Environment.SetEnvironmentVariable("BOOTSTRAP_ADMIN_FULL_NAME", null);
            Environment.SetEnvironmentVariable("BOOTSTRAP_ADMIN_INITIALS", null);
            Environment.SetEnvironmentVariable("BOOTSTRAP_ADMIN_EMAIL", null);
        }
    }

    [Fact]
    public async Task BootstrapAdmin_skips_when_user_already_exists()
    {
        using var db = new TestDatabase();

        // Pre-seed a user
        db.Context.Users.Add(new User
        {
            Username = "existing",
            FullName = "Existing User",
            Initials = "EU",
            PasswordHash = "hash",
            Role = UserRole.Admin,
            IsActive = true
        });
        await db.Context.SaveChangesAsync();

        Environment.SetEnvironmentVariable("BOOTSTRAP_ADMIN_USERNAME", "bootstrap_admin");
        Environment.SetEnvironmentVariable("BOOTSTRAP_ADMIN_PASSWORD", "SecurePassword123");
        Environment.SetEnvironmentVariable("BOOTSTRAP_ADMIN_FULL_NAME", "Bootstrap Admin");
        Environment.SetEnvironmentVariable("BOOTSTRAP_ADMIN_INITIALS", "BA");

        try
        {
            await DbInitializer.BootstrapAdminAsync(db.Context);

            // Should still have only the original user
            var users = await db.Context.Users.ToListAsync();
            Assert.Single(users);
            Assert.Equal("existing", users[0].Username);
        }
        finally
        {
            Environment.SetEnvironmentVariable("BOOTSTRAP_ADMIN_USERNAME", null);
            Environment.SetEnvironmentVariable("BOOTSTRAP_ADMIN_PASSWORD", null);
            Environment.SetEnvironmentVariable("BOOTSTRAP_ADMIN_FULL_NAME", null);
            Environment.SetEnvironmentVariable("BOOTSTRAP_ADMIN_INITIALS", null);
        }
    }

    [Fact]
    public async Task BootstrapAdmin_skips_when_variables_are_missing()
    {
        using var db = new TestDatabase();

        // Don't set any bootstrap variables

        await DbInitializer.BootstrapAdminAsync(db.Context);

        // No users should be created
        var count = await db.Context.Users.CountAsync();
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task BootstrapAdmin_skips_when_some_variables_are_missing()
    {
        using var db = new TestDatabase();

        // Set only some variables
        Environment.SetEnvironmentVariable("BOOTSTRAP_ADMIN_USERNAME", "bootstrap_admin");
        Environment.SetEnvironmentVariable("BOOTSTRAP_ADMIN_PASSWORD", "SecurePassword123");
        // Missing FULL_NAME and INITIALS

        try
        {
            await DbInitializer.BootstrapAdminAsync(db.Context);

            var count = await db.Context.Users.CountAsync();
            Assert.Equal(0, count);
        }
        finally
        {
            Environment.SetEnvironmentVariable("BOOTSTRAP_ADMIN_USERNAME", null);
            Environment.SetEnvironmentVariable("BOOTSTRAP_ADMIN_PASSWORD", null);
        }
    }

    [Fact]
    public async Task BootstrapAdmin_rejects_short_password()
    {
        using var db = new TestDatabase();

        Environment.SetEnvironmentVariable("BOOTSTRAP_ADMIN_USERNAME", "bootstrap_admin");
        Environment.SetEnvironmentVariable("BOOTSTRAP_ADMIN_PASSWORD", "Short1"); // Only 8 chars
        Environment.SetEnvironmentVariable("BOOTSTRAP_ADMIN_FULL_NAME", "Bootstrap Admin");
        Environment.SetEnvironmentVariable("BOOTSTRAP_ADMIN_INITIALS", "BA");

        try
        {
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                DbInitializer.BootstrapAdminAsync(db.Context));

            Assert.Contains("at least 12 characters", exception.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("BOOTSTRAP_ADMIN_USERNAME", null);
            Environment.SetEnvironmentVariable("BOOTSTRAP_ADMIN_PASSWORD", null);
            Environment.SetEnvironmentVariable("BOOTSTRAP_ADMIN_FULL_NAME", null);
            Environment.SetEnvironmentVariable("BOOTSTRAP_ADMIN_INITIALS", null);
        }
    }

    [Fact]
    public async Task BootstrapAdmin_email_is_optional()
    {
        using var db = new TestDatabase();

        Environment.SetEnvironmentVariable("BOOTSTRAP_ADMIN_USERNAME", "bootstrap_admin");
        Environment.SetEnvironmentVariable("BOOTSTRAP_ADMIN_PASSWORD", "SecurePassword123");
        Environment.SetEnvironmentVariable("BOOTSTRAP_ADMIN_FULL_NAME", "Bootstrap Admin");
        Environment.SetEnvironmentVariable("BOOTSTRAP_ADMIN_INITIALS", "BA");
        // No EMAIL set

        try
        {
            await DbInitializer.BootstrapAdminAsync(db.Context);

            var user = await db.Context.Users.SingleAsync();
            Assert.Null(user.Email);
        }
        finally
        {
            Environment.SetEnvironmentVariable("BOOTSTRAP_ADMIN_USERNAME", null);
            Environment.SetEnvironmentVariable("BOOTSTRAP_ADMIN_PASSWORD", null);
            Environment.SetEnvironmentVariable("BOOTSTRAP_ADMIN_FULL_NAME", null);
            Environment.SetEnvironmentVariable("BOOTSTRAP_ADMIN_INITIALS", null);
        }
    }
}