using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MarketplaceCartCheckout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CartItems_UserId",
                table: "CartItems");

            migrationBuilder.AddColumn<string>(
                name: "ListingPrimaryImageUrlSnapshot",
                table: "Orders",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ListingTitleSnapshot",
                table: "Orders",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<Guid>(
                name: "ProductId",
                table: "CartItems",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "CartItems",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "IsSelected",
                table: "CartItems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ListingId",
                table: "CartItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CartItems_ListingId",
                table: "CartItems",
                column: "ListingId");

            migrationBuilder.CreateIndex(
                name: "IX_CartItems_UserId_ListingId",
                table: "CartItems",
                columns: new[] { "UserId", "ListingId" },
                unique: true,
                filter: "[ListingId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_CartItems_Listings_ListingId",
                table: "CartItems",
                column: "ListingId",
                principalTable: "Listings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CartItems_Listings_ListingId",
                table: "CartItems");

            migrationBuilder.DropIndex(
                name: "IX_CartItems_ListingId",
                table: "CartItems");

            migrationBuilder.DropIndex(
                name: "IX_CartItems_UserId_ListingId",
                table: "CartItems");

            migrationBuilder.DropColumn(
                name: "ListingPrimaryImageUrlSnapshot",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ListingTitleSnapshot",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "CartItems");

            migrationBuilder.DropColumn(
                name: "IsSelected",
                table: "CartItems");

            migrationBuilder.DropColumn(
                name: "ListingId",
                table: "CartItems");

            migrationBuilder.AlterColumn<Guid>(
                name: "ProductId",
                table: "CartItems",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CartItems_UserId",
                table: "CartItems",
                column: "UserId");
        }
    }
}
