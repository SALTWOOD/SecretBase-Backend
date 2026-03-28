using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddFileShare : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FileShares",
                columns: table => new
                {
                    ShortId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Bucket = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Key = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    IsPublic = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    OwnerId = table.Column<int>(type: "integer", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileShares", x => x.ShortId);
                    table.ForeignKey(
                        name: "FK_FileShares_users_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FileShares_CreatedAt",
                table: "FileShares",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_FileShares_OwnerId",
                table: "FileShares",
                column: "OwnerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FileShares");
        }
    }
}
