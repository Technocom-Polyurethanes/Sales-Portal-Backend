using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace sales_portal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOfferType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "offer_type",
                table: "offers",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "Loc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "offer_type",
                table: "offers");
        }
    }
}