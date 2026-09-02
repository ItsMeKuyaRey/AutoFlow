using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoFlow.Web.Migrations
{
    /// <inheritdoc />
    public partial class SyncCurrentModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Parts_Supplier_SupplierId",
                table: "Parts");

            migrationBuilder.AddForeignKey(
                name: "FK_Parts_Supplier_SupplierId",
                table: "Parts",
                column: "SupplierId",
                principalTable: "Supplier",
                principalColumn: "SupplierId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Parts_Supplier_SupplierId",
                table: "Parts");

            migrationBuilder.AddForeignKey(
                name: "FK_Parts_Supplier_SupplierId",
                table: "Parts",
                column: "SupplierId",
                principalTable: "Supplier",
                principalColumn: "SupplierId");
        }
    }
}
