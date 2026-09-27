using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoFlow.Web.Migrations
{
    public partial class FixCustomerImageColumn : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "Customers"
                ADD COLUMN IF NOT EXISTS "ImageUrl" character varying(500);
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "Customers"
                DROP COLUMN IF EXISTS "ImageUrl";
                """);
        }
    }
}
