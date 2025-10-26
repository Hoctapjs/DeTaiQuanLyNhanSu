using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeTaiNhanSu.Migrations
{
    /// <inheritdoc />
    public partial class AddIsFirstLoginProperty : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_first_login",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_first_login",
                table: "Users");
        }
    }
}
