using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreateProductImagesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProductImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ImagePath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductImages_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "ProductImages",
                columns: new[] { "Id", "ImagePath", "ProductId" },
                values: new object[,]
                {
                    { new Guid("a1b2c3d4-e5f6-4a7b-8c9d-111111111111"), "uploads/product-photos/tshirt1.jpg", new Guid("a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d") },
                    { new Guid("a1b2c3d4-e5f6-4a7b-8c9d-222222222222"), "uploads/product-photos/tshirt2.jpg", new Guid("a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d") },
                    { new Guid("b2c3d4e5-f6a7-4b8c-9d0e-333333333333"), "uploads/product-photos/boots1.jpg", new Guid("b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e") },
                    { new Guid("c3d4e5f6-a7b8-4c9d-0e1f-444444444444"), "uploads/product-photos/watch1.jpg", new Guid("c3d4e5f6-a7b8-4c9d-0e1f-2a3b4c5d6e7f") }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductImages_ProductId",
                table: "ProductImages",
                column: "ProductId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductImages");
        }
    }
}
