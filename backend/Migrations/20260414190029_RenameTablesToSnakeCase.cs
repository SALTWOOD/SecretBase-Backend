using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class RenameTablesToSnakeCase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FileShares_users_OwnerId",
                table: "FileShares");

            migrationBuilder.DropForeignKey(
                name: "FK_Shortcodes_users_CreatedByUserId",
                table: "Shortcodes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Shortcodes",
                table: "Shortcodes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_FileShares",
                table: "FileShares");

            migrationBuilder.RenameTable(
                name: "Shortcodes",
                newName: "shortcodes");

            migrationBuilder.RenameTable(
                name: "FileShares",
                newName: "file_shares");

            migrationBuilder.RenameIndex(
                name: "IX_Shortcodes_Name",
                table: "shortcodes",
                newName: "IX_shortcodes_Name");

            migrationBuilder.RenameIndex(
                name: "IX_Shortcodes_CreatedByUserId",
                table: "shortcodes",
                newName: "IX_shortcodes_CreatedByUserId");

            migrationBuilder.RenameIndex(
                name: "IX_FileShares_OwnerId",
                table: "file_shares",
                newName: "IX_file_shares_OwnerId");

            migrationBuilder.RenameIndex(
                name: "IX_FileShares_CreatedAt",
                table: "file_shares",
                newName: "IX_file_shares_CreatedAt");

            migrationBuilder.AddPrimaryKey(
                name: "PK_shortcodes",
                table: "shortcodes",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_file_shares",
                table: "file_shares",
                column: "ShortId");

            migrationBuilder.AddForeignKey(
                name: "FK_file_shares_users_OwnerId",
                table: "file_shares",
                column: "OwnerId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_shortcodes_users_CreatedByUserId",
                table: "shortcodes",
                column: "CreatedByUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_file_shares_users_OwnerId",
                table: "file_shares");

            migrationBuilder.DropForeignKey(
                name: "FK_shortcodes_users_CreatedByUserId",
                table: "shortcodes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_shortcodes",
                table: "shortcodes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_file_shares",
                table: "file_shares");

            migrationBuilder.RenameTable(
                name: "shortcodes",
                newName: "Shortcodes");

            migrationBuilder.RenameTable(
                name: "file_shares",
                newName: "FileShares");

            migrationBuilder.RenameIndex(
                name: "IX_shortcodes_Name",
                table: "Shortcodes",
                newName: "IX_Shortcodes_Name");

            migrationBuilder.RenameIndex(
                name: "IX_shortcodes_CreatedByUserId",
                table: "Shortcodes",
                newName: "IX_Shortcodes_CreatedByUserId");

            migrationBuilder.RenameIndex(
                name: "IX_file_shares_OwnerId",
                table: "FileShares",
                newName: "IX_FileShares_OwnerId");

            migrationBuilder.RenameIndex(
                name: "IX_file_shares_CreatedAt",
                table: "FileShares",
                newName: "IX_FileShares_CreatedAt");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Shortcodes",
                table: "Shortcodes",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_FileShares",
                table: "FileShares",
                column: "ShortId");

            migrationBuilder.AddForeignKey(
                name: "FK_FileShares_users_OwnerId",
                table: "FileShares",
                column: "OwnerId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Shortcodes_users_CreatedByUserId",
                table: "Shortcodes",
                column: "CreatedByUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
