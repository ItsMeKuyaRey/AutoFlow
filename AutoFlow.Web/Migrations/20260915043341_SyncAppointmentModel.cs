using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoFlow.Web.Migrations
{
    public partial class SyncAppointmentModel : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CustomerId",
                table: "Appointments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "Appointments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE "Appointments" a
                SET "CustomerId" = v."CustomerId"
                FROM "Vehicles" v
                WHERE a."VehicleId" = v."VehicleId";
                """);

            migrationBuilder.Sql("""
                UPDATE "Appointments"
                SET "Reason" = COALESCE("CustomerConcern", '');
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_CustomerId",
                table: "Appointments",
                column: "CustomerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Appointments_Customers_CustomerId",
                table: "Appointments",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "CustomerId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.DropColumn(
                name: "CustomerConcern",
                table: "Appointments");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomerConcern",
                table: "Appointments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Appointments"
                SET "CustomerConcern" = NULLIF("Reason", '');
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Appointments_Customers_CustomerId",
                table: "Appointments");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_CustomerId",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "Appointments");
        }
    }
}