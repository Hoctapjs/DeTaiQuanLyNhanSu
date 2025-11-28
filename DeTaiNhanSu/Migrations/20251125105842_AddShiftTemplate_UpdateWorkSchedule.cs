using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeTaiNhanSu.Migrations
{
    /// <inheritdoc />
    public partial class AddShiftTemplate_UpdateWorkSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EndTime",
                table: "WorkSchedules");

            migrationBuilder.DropColumn(
                name: "Shift",
                table: "WorkSchedules");

            migrationBuilder.DropColumn(
                name: "StartTime",
                table: "WorkSchedules");

            migrationBuilder.AddColumn<Guid>(
                name: "ShiftTemplateId",
                table: "WorkSchedules",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "ShiftTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    BreakDurationMinutes = table.Column<int>(type: "int", nullable: false),
                    TotalWorkingHours = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftTemplates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkSchedules_ShiftTemplateId",
                table: "WorkSchedules",
                column: "ShiftTemplateId");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkSchedules_ShiftTemplates_ShiftTemplateId",
                table: "WorkSchedules",
                column: "ShiftTemplateId",
                principalTable: "ShiftTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkSchedules_ShiftTemplates_ShiftTemplateId",
                table: "WorkSchedules");

            migrationBuilder.DropTable(
                name: "ShiftTemplates");

            migrationBuilder.DropIndex(
                name: "IX_WorkSchedules_ShiftTemplateId",
                table: "WorkSchedules");

            migrationBuilder.DropColumn(
                name: "ShiftTemplateId",
                table: "WorkSchedules");

            migrationBuilder.AddColumn<TimeOnly>(
                name: "EndTime",
                table: "WorkSchedules",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Shift",
                table: "WorkSchedules",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "StartTime",
                table: "WorkSchedules",
                type: "time",
                nullable: true);
        }
    }
}
