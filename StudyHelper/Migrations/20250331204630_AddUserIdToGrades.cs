using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace studyhelper.Migrations
{
    /// <inheritdoc />
    public partial class AddUserIdToGrades : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "Grades",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Grades");
        }
    }
}
