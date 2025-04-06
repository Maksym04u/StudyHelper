using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace studyhelper.Migrations
{
    /// <inheritdoc />
    public partial class AddGradeModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Score",
                table: "Grades");

            migrationBuilder.AddColumn<DateTime>(
                name: "Date",
                table: "Grades",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<double>(
                name: "Grade",
                table: "Grades",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Date",
                table: "Grades");

            migrationBuilder.DropColumn(
                name: "Grade",
                table: "Grades");

            migrationBuilder.AddColumn<int>(
                name: "Score",
                table: "Grades",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }
    }
}
