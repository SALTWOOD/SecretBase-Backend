using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddGuestComments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_comments_users_AuthorId",
                table: "comments");

            migrationBuilder.AlterColumn<int>(
                name: "AuthorId",
                table: "comments",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<string>(
                name: "Metadata",
                table: "comments",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<int>(
                name: "ReviewStatus",
                table: "comments",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_comments_ReviewStatus",
                table: "comments",
                column: "ReviewStatus");

            migrationBuilder.AddForeignKey(
                name: "FK_comments_users_AuthorId",
                table: "comments",
                column: "AuthorId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_comments_users_AuthorId",
                table: "comments");

            migrationBuilder.DropIndex(
                name: "IX_comments_ReviewStatus",
                table: "comments");

            migrationBuilder.DropColumn(
                name: "Metadata",
                table: "comments");

            migrationBuilder.DropColumn(
                name: "ReviewStatus",
                table: "comments");

            migrationBuilder.AlterColumn<int>(
                name: "AuthorId",
                table: "comments",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_comments_users_AuthorId",
                table: "comments",
                column: "AuthorId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
