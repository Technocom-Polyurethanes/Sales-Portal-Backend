using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace sales_portal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AlignDomainWithFrontend : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_offer_delivery_allocations_materials_material_id",
                table: "offer_delivery_allocations");

            migrationBuilder.DropForeignKey(
                name: "fk_offer_delivery_allocations_offer_delivery_batches_delivery_1",
                table: "offer_delivery_allocations");

            migrationBuilder.DropTable(
                name: "offer_payment_milestones");

            migrationBuilder.DropIndex(
                name: "ix_offers_reference",
                table: "offers");

            migrationBuilder.DropIndex(
                name: "ix_offer_delivery_allocations_delivery_batch_id1",
                table: "offer_delivery_allocations");

            migrationBuilder.DropIndex(
                name: "ix_offer_delivery_allocations_material_id",
                table: "offer_delivery_allocations");

            migrationBuilder.DropColumn(
                name: "delivery_batch_id1",
                table: "offer_delivery_allocations");

            migrationBuilder.AddColumn<string>(
                name: "tax_id",
                table: "offers",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "weight_per_package_kg",
                table: "offer_line_packagings",
                type: "numeric(18,3)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AddColumn<Guid>(
                name: "offer_line_id",
                table: "offer_delivery_allocations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "offer_line_packaging_id",
                table: "offer_delivery_allocations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "package_type_id",
                table: "offer_delivery_allocations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "package_type_name",
                table: "offer_delivery_allocations",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "package_type_name_ar",
                table: "offer_delivery_allocations",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "total_weight_kg",
                table: "offer_delivery_allocations",
                type: "numeric(18,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "weight_per_package_kg",
                table: "offer_delivery_allocations",
                type: "numeric(18,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "default_package_type_id",
                table: "materials",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "default_weight_kg",
                table: "materials",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "tax_id",
                table: "customers",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_offers_reference_version",
                table: "offers",
                columns: new[] { "reference", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_offer_delivery_allocations_offer_line_id",
                table: "offer_delivery_allocations",
                column: "offer_line_id");

            migrationBuilder.CreateIndex(
                name: "ix_offer_delivery_allocations_offer_line_packaging_id",
                table: "offer_delivery_allocations",
                column: "offer_line_packaging_id");

            migrationBuilder.CreateIndex(
                name: "ix_materials_default_package_type_id",
                table: "materials",
                column: "default_package_type_id");

            migrationBuilder.AddForeignKey(
                name: "fk_materials_package_types_default_package_type_id",
                table: "materials",
                column: "default_package_type_id",
                principalTable: "package_types",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_offer_delivery_allocations_offer_line_packagings_offer_line",
                table: "offer_delivery_allocations",
                column: "offer_line_packaging_id",
                principalTable: "offer_line_packagings",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_offer_delivery_allocations_offer_lines_offer_line_id",
                table: "offer_delivery_allocations",
                column: "offer_line_id",
                principalTable: "offer_lines",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_materials_package_types_default_package_type_id",
                table: "materials");

            migrationBuilder.DropForeignKey(
                name: "fk_offer_delivery_allocations_offer_line_packagings_offer_line",
                table: "offer_delivery_allocations");

            migrationBuilder.DropForeignKey(
                name: "fk_offer_delivery_allocations_offer_lines_offer_line_id",
                table: "offer_delivery_allocations");

            migrationBuilder.DropIndex(
                name: "ix_offers_reference_version",
                table: "offers");

            migrationBuilder.DropIndex(
                name: "ix_offer_delivery_allocations_offer_line_id",
                table: "offer_delivery_allocations");

            migrationBuilder.DropIndex(
                name: "ix_offer_delivery_allocations_offer_line_packaging_id",
                table: "offer_delivery_allocations");

            migrationBuilder.DropIndex(
                name: "ix_materials_default_package_type_id",
                table: "materials");

            migrationBuilder.DropColumn(
                name: "tax_id",
                table: "offers");

            migrationBuilder.DropColumn(
                name: "offer_line_id",
                table: "offer_delivery_allocations");

            migrationBuilder.DropColumn(
                name: "offer_line_packaging_id",
                table: "offer_delivery_allocations");

            migrationBuilder.DropColumn(
                name: "package_type_name",
                table: "offer_delivery_allocations");

            migrationBuilder.DropColumn(
                name: "package_type_name_ar",
                table: "offer_delivery_allocations");

            migrationBuilder.DropColumn(
                name: "total_weight_kg",
                table: "offer_delivery_allocations");

            migrationBuilder.DropColumn(
                name: "weight_per_package_kg",
                table: "offer_delivery_allocations");

            migrationBuilder.DropColumn(
                name: "default_package_type_id",
                table: "materials");

            migrationBuilder.DropColumn(
                name: "default_weight_kg",
                table: "materials");

            migrationBuilder.DropColumn(
                name: "tax_id",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "package_type_id",
                table: "offer_delivery_allocations");

            migrationBuilder.AddColumn<Guid>(
                name: "delivery_batch_id1",
                table: "offer_delivery_allocations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<decimal>(
                name: "weight_per_package_kg",
                table: "offer_line_packagings",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,3)");

            migrationBuilder.CreateTable(
                name: "offer_payment_milestones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    offer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount_due = table.Column<decimal>(type: "numeric", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    due_date_or_event = table.Column<string>(type: "text", nullable: true),
                    percentage_or_fixed = table.Column<decimal>(type: "numeric", nullable: false),
                    phase_description = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_offer_payment_milestones", x => x.id);
                    table.ForeignKey(
                        name: "fk_offer_payment_milestones_offers_offer_id",
                        column: x => x.offer_id,
                        principalTable: "offers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_offers_reference",
                table: "offers",
                column: "reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_offer_delivery_allocations_delivery_batch_id1",
                table: "offer_delivery_allocations",
                column: "delivery_batch_id1");

            migrationBuilder.CreateIndex(
                name: "ix_offer_delivery_allocations_material_id",
                table: "offer_delivery_allocations",
                column: "material_id");

            migrationBuilder.CreateIndex(
                name: "ix_offer_payment_milestones_offer_id",
                table: "offer_payment_milestones",
                column: "offer_id");

            migrationBuilder.AddForeignKey(
                name: "fk_offer_delivery_allocations_materials_material_id",
                table: "offer_delivery_allocations",
                column: "material_id",
                principalTable: "materials",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_offer_delivery_allocations_offer_delivery_batches_delivery_1",
                table: "offer_delivery_allocations",
                column: "delivery_batch_id1",
                principalTable: "offer_delivery_batches",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
