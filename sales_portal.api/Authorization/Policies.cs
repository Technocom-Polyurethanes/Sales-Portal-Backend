using Microsoft.AspNetCore.Authorization;
using sales_portal.domain.Enums;

namespace sales_portal.api.Authorization;

/// <summary>
/// The role matrix, in one place. Every protected endpoint names one of these policies,
/// so changing what a role may do is a one-line change here.
///
/// Admin       - everything.
/// Sales       - create offers; view customers (with history), materials and bank accounts and create new ones.
/// Accountant  - data import and every catalogue feature; offers are read only.
/// </summary>
public static class Policies
{
    /// <summary>Users screen and user management API.</summary>
    public const string ManageUsers = nameof(ManageUsers);

    /// <summary>Create, edit, revise and change the status of offers.</summary>
    public const string OffersWrite = nameof(OffersWrite);

    /// <summary>Create a customer, material or bank account.</summary>
    public const string CatalogCreate = nameof(CatalogCreate);

    /// <summary>Edit an existing customer, material or bank account.</summary>
    public const string CatalogEdit = nameof(CatalogEdit);

    /// <summary>Data import: templates, preview, commit and history.</summary>
    public const string Import = nameof(Import);

    /// <summary>Company settings.</summary>
    public const string SettingsWrite = nameof(SettingsWrite);

    public static void Register(AuthorizationOptions options)
    {
        Add(options, ManageUsers, UserRole.Admin);
        Add(options, OffersWrite, UserRole.Admin, UserRole.Sales);
        Add(options, CatalogCreate, UserRole.Admin, UserRole.Sales, UserRole.Accountant);
        Add(options, CatalogEdit, UserRole.Admin, UserRole.Accountant);
        Add(options, Import, UserRole.Admin, UserRole.Accountant);
        Add(options, SettingsWrite, UserRole.Admin);
    }

    private static void Add(AuthorizationOptions options, string name, params UserRole[] roles)
    {
        options.AddPolicy(name, policy => policy
            .RequireAuthenticatedUser()
            .RequireRole(roles.Select(role => role.ToString())));
    }
}
