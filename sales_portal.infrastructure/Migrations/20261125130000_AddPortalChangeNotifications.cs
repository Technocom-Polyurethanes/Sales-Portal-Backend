using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using sales_portal.infrastructure.Contexts;

#nullable disable

namespace sales_portal.infrastructure.Migrations
{
    /// <summary>
    /// Announces direct database writes (AdminNeo, psql, scripts) on the channel the API listens to.
    /// Statement-level triggers send only the table name; never row data.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261125130000_AddPortalChangeNotifications")]
    public partial class AddPortalChangeNotifications : Migration
    {
        private const string TriggerName = "trg_portal_changes_notify";

        // Every table behind a live topic, from AppDbContext and the snake_case naming convention.
        private static readonly string[] Tables =
        [
            "users",
            "customers",
            "package_types",
            "materials",
            "bank_accounts",
            "payment_terms",
            "company_settings",
            "workspace_settings",
            "offers",
            "offer_lines",
            "offer_line_packagings",
            "offer_delivery_batches",
            "offer_delivery_allocations",
            "import_batches"
        ];

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // CREATE TRIGGER needs SHARE ROW EXCLUSIVE; fail fast instead of blocking writers.
            migrationBuilder.Sql("SET LOCAL lock_timeout = '5s'; SET LOCAL statement_timeout = '30s';");

            migrationBuilder.Sql($@"
CREATE OR REPLACE FUNCTION portal_changes_notify()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    PERFORM pg_notify('portal_changes', TG_TABLE_NAME::text);
    RETURN NULL; -- ignored by AFTER ... FOR EACH STATEMENT triggers
END;
$$;");

            foreach (var table in Tables)
            {
                migrationBuilder.Sql($@"
DROP TRIGGER IF EXISTS {TriggerName} ON {table};
CREATE TRIGGER {TriggerName}
AFTER INSERT OR UPDATE OR DELETE OR TRUNCATE ON {table}
FOR EACH STATEMENT
EXECUTE FUNCTION portal_changes_notify();");
            }
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("SET LOCAL lock_timeout = '5s'; SET LOCAL statement_timeout = '30s';");

            foreach (var table in Tables)
            {
                migrationBuilder.Sql($"DROP TRIGGER IF EXISTS {TriggerName} ON {table};");
            }

            migrationBuilder.Sql("DROP FUNCTION IF EXISTS portal_changes_notify();");
        }
    }
}
