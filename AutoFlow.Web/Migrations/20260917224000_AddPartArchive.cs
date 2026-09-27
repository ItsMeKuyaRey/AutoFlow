using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoFlow.Web.Migrations
{
    public partial class AddPartArchive : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Parts",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Parts");
        }
    }
}
