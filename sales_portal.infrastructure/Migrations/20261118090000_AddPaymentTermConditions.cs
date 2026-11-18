using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using sales_portal.infrastructure.Contexts;

#nullable disable

namespace sales_portal.infrastructure.Migrations
{
    /// <summary>
    /// A payment term can carry optional conditions (English and Arabic) that are printed with it.
    /// The new instalment terms are added by the database initializer so fresh databases get them too.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261118090000_AddPaymentTermConditions")]
    public partial class AddPaymentTermConditions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "conditions",
                table: "payment_terms",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "conditions_ar",
                table: "payment_terms",
                type: "text",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "conditions", table: "payment_terms");
            migrationBuilder.DropColumn(name: "conditions_ar", table: "payment_terms");
        }
    }
}
