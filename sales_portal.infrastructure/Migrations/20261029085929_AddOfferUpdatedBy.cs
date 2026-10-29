using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace sales_portal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOfferUpdatedBy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "updated_by_user_id",
                table: "offers",
                type: "uuid",
                nullable: true);

            // Existing offers were last touched by their creator; backfill so the list
            // can always display a last modifier.
            migrationBuilder.Sql(
                "UPDATE offers SET updated_by_user_id = created_by_user_id WHERE updated_by_user_id IS NULL;");

            migrationBuilder.CreateIndex(
                name: "ix_offers_updated_by_user_id",
                table: "offers",
                column: "updated_by_user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_offers_users_updated_by_user_id",
                table: "offers",
                column: "updated_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_offers_users_updated_by_user_id",
                table: "offers");

            migrationBuilder.DropIndex(
                name: "ix_offers_updated_by_user_id",
                table: "offers");

            migrationBuilder.DropColumn(
                name: "updated_by_user_id",
                table: "offers");
        }
    }
}
