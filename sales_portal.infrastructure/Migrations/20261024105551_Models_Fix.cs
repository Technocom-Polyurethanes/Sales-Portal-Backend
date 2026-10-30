using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace sales_portal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Models_Fix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_customers_payment_terms_default_payment_term_id",
                table: "customers");

            migrationBuilder.AddColumn<Guid>(
                name: "delivery_batch_id1",
                table: "offer_delivery_allocations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "ix_offer_delivery_allocations_delivery_batch_id1",
                table: "offer_delivery_allocations",
                column: "delivery_batch_id1");

            migrationBuilder.AddForeignKey(
                name: "fk_customers_payment_terms_default_payment_term_id",
                table: "customers",
                column: "default_payment_term_id",
                principalTable: "payment_terms",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_offer_delivery_allocations_offer_delivery_batches_delivery_1",
                table: "offer_delivery_allocations",
                column: "delivery_batch_id1",
                principalTable: "offer_delivery_batches",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_customers_payment_terms_default_payment_term_id",
                table: "customers");

            migrationBuilder.DropForeignKey(
                name: "fk_offer_delivery_allocations_offer_delivery_batches_delivery_1",
                table: "offer_delivery_allocations");

            migrationBuilder.DropIndex(
                name: "ix_offer_delivery_allocations_delivery_batch_id1",
                table: "offer_delivery_allocations");

            migrationBuilder.DropColumn(
                name: "delivery_batch_id1",
                table: "offer_delivery_allocations");

            migrationBuilder.AddForeignKey(
                name: "fk_customers_payment_terms_default_payment_term_id",
                table: "customers",
                column: "default_payment_term_id",
                principalTable: "payment_terms",
                principalColumn: "id");
        }
    }
}
