using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using sales_portal.infrastructure.Contexts;

#nullable disable

namespace sales_portal.infrastructure.Migrations
{
    /// <summary>
    /// Versions of an offer are chained by a stable <c>root_offer_id</c> instead of by the reference
    /// text, so changing the reference no longer detaches a version from its history.
    /// Also stores the offer validity in days.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261103120000_AddOfferRootAndValidity")]
    public partial class AddOfferRootAndValidity : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_offers_reference_version",
                table: "offers");

            migrationBuilder.AddColumn<Guid>(
                name: "root_offer_id",
                table: "offers",
                type: "uuid",
                nullable: false,
                defaultValue: Guid.Empty);

            migrationBuilder.AddColumn<int>(
                name: "validity_days",
                table: "offers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Every version points at the first version of its chain, found by following the parent links.
            migrationBuilder.Sql(@"
WITH RECURSIVE chain AS (
    SELECT id, id AS root_id FROM offers WHERE parent_offer_id IS NULL
    UNION ALL
    SELECT o.id, c.root_id FROM offers o JOIN chain c ON o.parent_offer_id = c.id
)
UPDATE offers SET root_offer_id = chain.root_id FROM chain WHERE offers.id = chain.id;");

            migrationBuilder.Sql(
                "UPDATE offers SET root_offer_id = id WHERE root_offer_id = '00000000-0000-0000-0000-000000000000';");

            // Versions that shared a reference were one offer before, keep them together.
            migrationBuilder.Sql(@"
UPDATE offers o SET root_offer_id = head.root_offer_id
FROM (
    SELECT DISTINCT ON (reference) reference, root_offer_id
    FROM offers
    ORDER BY reference, version, created_at
) head
WHERE o.reference = head.reference AND o.root_offer_id <> head.root_offer_id;");

            migrationBuilder.CreateIndex(
                name: "ix_offers_reference",
                table: "offers",
                column: "reference");

            migrationBuilder.CreateIndex(
                name: "ix_offers_root_offer_id_version",
                table: "offers",
                columns: new[] { "root_offer_id", "version" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_offers_root_offer_id_version",
                table: "offers");

            migrationBuilder.DropIndex(
                name: "ix_offers_reference",
                table: "offers");

            migrationBuilder.DropColumn(
                name: "validity_days",
                table: "offers");

            migrationBuilder.DropColumn(
                name: "root_offer_id",
                table: "offers");

            migrationBuilder.CreateIndex(
                name: "ix_offers_reference_version",
                table: "offers",
                columns: new[] { "reference", "version" },
                unique: true);
        }
    }
}
