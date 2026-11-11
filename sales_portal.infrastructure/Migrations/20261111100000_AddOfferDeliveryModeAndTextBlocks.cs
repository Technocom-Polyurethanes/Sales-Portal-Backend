using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using sales_portal.infrastructure.Contexts;

#nullable disable

namespace sales_portal.infrastructure.Migrations
{
    /// <summary>
    /// Delivery can be described either as dated batches or as free text, and an offer now carries
    /// a delivery place and extra payment conditions. Existing offers keep their batches.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261111100000_AddOfferDeliveryModeAndTextBlocks")]
    public partial class AddOfferDeliveryModeAndTextBlocks : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "delivery_mode",
                table: "offers",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "Batches");

            migrationBuilder.AddColumn<string>(
                name: "delivery_text",
                table: "offers",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "delivery_place",
                table: "offers",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "payment_terms_text",
                table: "offers",
                type: "character varying(800)",
                maxLength: 800,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "payment_terms_text", table: "offers");
            migrationBuilder.DropColumn(name: "delivery_place", table: "offers");
            migrationBuilder.DropColumn(name: "delivery_text", table: "offers");
            migrationBuilder.DropColumn(name: "delivery_mode", table: "offers");
        }
    }
}
