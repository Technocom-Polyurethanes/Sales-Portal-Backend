using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace sales_portal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_offers_root_offer_id_version",
                table: "offers");

            migrationBuilder.DropIndex(
                name: "ix_materials_item_code",
                table: "materials");

            migrationBuilder.DropIndex(
                name: "ix_customers_customer_code",
                table: "customers");

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "offers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "materials",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "customers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "bank_accounts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_offers_root_offer_id_version",
                table: "offers",
                columns: new[] { "root_offer_id", "version" },
                unique: true,
                filter: "\"is_deleted\" = false");

            migrationBuilder.CreateIndex(
                name: "ix_materials_item_code",
                table: "materials",
                column: "item_code",
                unique: true,
                filter: "\"is_deleted\" = false");

            migrationBuilder.CreateIndex(
                name: "ix_customers_customer_code",
                table: "customers",
                column: "customer_code",
                unique: true,
                filter: "\"is_deleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_offers_root_offer_id_version",
                table: "offers");

            migrationBuilder.DropIndex(
                name: "ix_materials_item_code",
                table: "materials");

            migrationBuilder.DropIndex(
                name: "ix_customers_customer_code",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "users");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "offers");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "materials");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "bank_accounts");

            migrationBuilder.CreateIndex(
                name: "ix_offers_root_offer_id_version",
                table: "offers",
                columns: new[] { "root_offer_id", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_materials_item_code",
                table: "materials",
                column: "item_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_customers_customer_code",
                table: "customers",
                column: "customer_code",
                unique: true);
        }
    }
}
