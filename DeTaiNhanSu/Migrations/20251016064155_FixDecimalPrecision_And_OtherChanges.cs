using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeTaiNhanSu.Migrations
{
    /// <inheritdoc />
    public partial class FixDecimalPrecision_And_OtherChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "LastExpiryNotifyDate",
                table: "Contracts",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastExpiryNotifyDate",
                table: "Contracts");
        }
    }
}
