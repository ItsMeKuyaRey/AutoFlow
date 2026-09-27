using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoFlow.Web.Migrations
{
    public partial class UpdateAppointmentsForScheduling : Migration
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
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "Appointments" a
                SET "CustomerId" = v."CustomerId",
                    "Reason" = COALESCE(a."CustomerConcern", '')
                FROM "Vehicles" v
                WHERE a."VehicleId" = v."VehicleId";
            """);

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                table: "Appointments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

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
                SET "CustomerConcern" = "Reason";
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
