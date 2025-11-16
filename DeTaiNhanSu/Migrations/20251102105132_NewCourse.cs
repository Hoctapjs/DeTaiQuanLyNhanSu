using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeTaiNhanSu.Migrations
{
    /// <inheritdoc />
    public partial class NewCourse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "IsCorrect",
                table: "CourseResults",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldComputedColumnSql: "CASE WHEN [Chosen] = [CorrectAtSubmit] THEN CONVERT(bit,1) ELSE CONVERT(bit,0) END");

            migrationBuilder.AlterColumn<DateTime>(
                name: "AnsweredAt",
                table: "CourseResults",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()",
                oldClrType: typeof(DateTime),
                oldType: "datetime2");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "AnsweredAt",
                table: "CourseResults",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AlterColumn<bool>(
                name: "IsCorrect",
                table: "CourseResults",
                type: "bit",
                nullable: false,
                computedColumnSql: "CASE WHEN [Chosen] = [CorrectAtSubmit] THEN CONVERT(bit,1) ELSE CONVERT(bit,0) END",
                stored: true,
                oldClrType: typeof(bool),
                oldType: "bit");
        }
    }
}
