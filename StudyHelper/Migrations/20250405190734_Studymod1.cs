using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace studyhelper.Migrations
{
    /// <inheritdoc />
    public partial class Studymod1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Author",
                table: "Studies");

            migrationBuilder.DropColumn(
                name: "Content",
                table: "Studies");

            migrationBuilder.RenameColumn(
                name: "LastModifiedDate",
                table: "Studies",
                newName: "Tags");

            migrationBuilder.RenameColumn(
                name: "FilePath",
                table: "Studies",
                newName: "Category");

            migrationBuilder.RenameColumn(
                name: "CreatedDate",
                table: "Studies",
                newName: "CreatedAt");

            migrationBuilder.CreateTable(
                name: "StudyFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OriginalName = table.Column<string>(type: "TEXT", nullable: false),
                    StoragePath = table.Column<string>(type: "TEXT", nullable: false),
                    FileType = table.Column<string>(type: "TEXT", nullable: false),
                    FileSize = table.Column<long>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    UploadDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StudyId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudyFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudyFiles_Studies_StudyId",
                        column: x => x.StudyId,
                        principalTable: "Studies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Studies_UserId",
                table: "Studies",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudyFiles_StudyId",
                table: "StudyFiles",
                column: "StudyId");

            migrationBuilder.AddForeignKey(
                name: "FK_Studies_AspNetUsers_UserId",
                table: "Studies",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Studies_AspNetUsers_UserId",
                table: "Studies");

            migrationBuilder.DropTable(
                name: "StudyFiles");

            migrationBuilder.DropIndex(
                name: "IX_Studies_UserId",
                table: "Studies");

            migrationBuilder.RenameColumn(
                name: "Tags",
                table: "Studies",
                newName: "LastModifiedDate");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "Studies",
                newName: "CreatedDate");

            migrationBuilder.RenameColumn(
                name: "Category",
                table: "Studies",
                newName: "FilePath");

            migrationBuilder.AddColumn<string>(
                name: "Author",
                table: "Studies",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Content",
                table: "Studies",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }
    }
}
