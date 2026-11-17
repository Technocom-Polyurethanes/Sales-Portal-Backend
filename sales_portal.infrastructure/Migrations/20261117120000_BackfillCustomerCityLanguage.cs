using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using sales_portal.infrastructure.Contexts;

#nullable disable

namespace sales_portal.infrastructure.Migrations
{
    /// <summary>
    /// City used to be a single free-text field. Move Arabic values into city_ar and leave
    /// English values in city, so the two language-specific fields start from the old data.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261117120000_BackfillCustomerCityLanguage")]
    public partial class BackfillCustomerCityLanguage : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE customers
                SET city_ar = COALESCE(NULLIF(city_ar, ''), city),
                    city = NULL
                WHERE city IS NOT NULL
                  AND city ~ '[ء-ي]';
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE customers
                SET city = city_ar
                WHERE city IS NULL
                  AND city_ar IS NOT NULL;
                """);
        }
    }
}
