using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ListingQuantityAndSupportAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Quantity",
                table: "Listings",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<Guid>(
                name: "AssignedAdminId",
                table: "Chats",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Chats_AssignedAdminId",
                table: "Chats",
                column: "AssignedAdminId");

            migrationBuilder.AddForeignKey(
                name: "FK_Chats_AspNetUsers_AssignedAdminId",
                table: "Chats",
                column: "AssignedAdminId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.Sql("UPDATE [Listings] SET [Quantity] = 1 WHERE [Quantity] < 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Chats_AspNetUsers_AssignedAdminId",
                table: "Chats");

            migrationBuilder.DropIndex(
                name: "IX_Chats_AssignedAdminId",
                table: "Chats");

            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "AssignedAdminId",
                table: "Chats");
        }
    }
}
