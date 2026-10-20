using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.MigrationsPostgres
{
    /// <inheritdoc />
    public partial class ReviewAuthorUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reviews_OrderId_AuthorId_TargetUserId",
                table: "Reviews");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_OrderId_AuthorId",
                table: "Reviews",
                columns: new[] { "OrderId", "AuthorId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reviews_OrderId_AuthorId",
                table: "Reviews");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_OrderId_AuthorId_TargetUserId",
                table: "Reviews",
                columns: new[] { "OrderId", "AuthorId", "TargetUserId" },
                unique: true);
        }
    }
}
