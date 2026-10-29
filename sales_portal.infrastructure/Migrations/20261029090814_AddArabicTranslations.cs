using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace sales_portal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddArabicTranslations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "attention_ar",
                table: "offers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "address_ar",
                table: "customers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "city_ar",
                table: "customers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "contact_person_ar",
                table: "customers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "account_holder_name_ar",
                table: "bank_accounts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "bank_ar",
                table: "bank_accounts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "branch_ar",
                table: "bank_accounts",
                type: "text",
                nullable: true);

            // AccountHolderName is the English holder from this migration on. Move any
            // existing Arabic value to the new Arabic column and seed the English one.
            migrationBuilder.Sql(
                """
                UPDATE bank_accounts
                SET account_holder_name_ar = account_holder_name,
                    account_holder_name = 'Technocom for Commercial Agencies'
                WHERE account_holder_name_ar IS NULL
                  AND account_holder_name ~ '[ء-ي]';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE bank_accounts
                SET account_holder_name = account_holder_name_ar
                WHERE account_holder_name_ar IS NOT NULL
                  AND account_holder_name = 'Technocom for Commercial Agencies';
                """);

            migrationBuilder.DropColumn(
                name: "attention_ar",
                table: "offers");

            migrationBuilder.DropColumn(
                name: "address_ar",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "city_ar",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "contact_person_ar",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "account_holder_name_ar",
                table: "bank_accounts");

            migrationBuilder.DropColumn(
                name: "bank_ar",
                table: "bank_accounts");

            migrationBuilder.DropColumn(
                name: "branch_ar",
                table: "bank_accounts");
        }
    }
}
