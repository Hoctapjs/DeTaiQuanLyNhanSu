using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DeTaiNhanSu.Migrations
{
    /// <inheritdoc />
    public partial class Init_TrainingMinimal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EndDate",
                table: "TrainingRecords");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "TrainingRecords");

            migrationBuilder.DropColumn(
                name: "Hours",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "Provider",
                table: "Courses");

            migrationBuilder.AddColumn<string>(
                name: "ClassCode",
                table: "Courses",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                defaultValue: "");

            // unique filtered index
            //migrationBuilder.Sql(@"
            //    CREATE UNIQUE INDEX [IX_Courses_ClassCode]
            //    ON [Courses]([ClassCode])
            //    WHERE [ClassCode] IS NOT NULL AND [ClassCode] <> ''
            //    ");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Courses",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AddColumn<int>(
                name: "PassThreshold",
                table: "Courses",
                type: "int",
                nullable: false,
                defaultValue: 70);

            migrationBuilder.CreateTable(
                name: "CourseQuestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    A = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    B = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    C = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    D = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Correct = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourseQuestions_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CourseResults",
                columns: table => new
                {
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CourseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Chosen = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    IsCorrect = table.Column<bool>(type: "bit", nullable: false),
                    AnsweredAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseResults", x => new { x.EmployeeId, x.CourseId, x.QuestionId });
                    table.ForeignKey(
                        name: "FK_CourseResults_CourseQuestions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "CourseQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CourseResults_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CourseQuestions_CourseId",
                table: "CourseQuestions",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseResults_CourseId",
                table: "CourseResults",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseResults_QuestionId",
                table: "CourseResults",
                column: "QuestionId");

            //migrationBuilder.CreateIndex(
            //    name: "IX_Courses_ClassCode",
            //    table: "Courses",
            //    column: "ClassCode",
            //    unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CourseResults");

            migrationBuilder.DropTable(
                name: "CourseQuestions");

            migrationBuilder.DropColumn(
                name: "ClassCode",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "PassThreshold",
                table: "Courses");

            migrationBuilder.AddColumn<DateTime>(
                name: "EndDate",
                table: "TrainingRecords",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "StartDate",
                table: "TrainingRecords",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Hours",
                table: "Courses",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Provider",
                table: "Courses",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
