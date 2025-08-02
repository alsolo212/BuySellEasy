using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SortOrderNewColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Создание таблицы ProductImages вручную (если она отсутствует)
            migrationBuilder.CreateTable(
                name: "ProductImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ImagePath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0)
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

            migrationBuilder.CreateIndex(
                name: "IX_ProductImages_ProductId",
                table: "ProductImages",
                column: "ProductId");

            // Удаление старых данных
            migrationBuilder.DeleteData(
                table: "ProductImages",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-e5f6-4a7b-8c9d-111111111111"));

            migrationBuilder.DeleteData(
                table: "ProductImages",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-e5f6-4a7b-8c9d-222222222222"));

            migrationBuilder.DeleteData(
                table: "ProductImages",
                keyColumn: "Id",
                keyValue: new Guid("b2c3d4e5-f6a7-4b8c-9d0e-333333333333"));

            migrationBuilder.DeleteData(
                table: "ProductImages",
                keyColumn: "Id",
                keyValue: new Guid("c3d4e5f6-a7b8-4c9d-0e1f-444444444444"));

            migrationBuilder.DeleteData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d"));

            migrationBuilder.DeleteData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e"));

            migrationBuilder.DeleteData(
                table: "Products",
                keyColumn: "Id",
                keyValue: new Guid("c3d4e5f6-a7b8-4c9d-0e1f-2a3b4c5d6e7f"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Удаление таблицы ProductImages (на случай отката миграции)
            migrationBuilder.DropTable(
                name: "ProductImages");

            // Вставка данных обратно
            migrationBuilder.InsertData(
                table: "Products",
                columns: new[] { "Id", "CreatedAt", "Description", "Location", "ProductCondition", "ProductPrice", "Title" },
                values: new object[,]
                {
                    { new Guid("a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d"), new DateTime(2023, 11, 15, 10, 30, 0, 0, DateTimeKind.Unspecified), "Black T-shirt size L is suitable for men with a height of 180+ centimeters", "Kyiv", "Used", 123.0, "T-shirt" },
                    { new Guid("b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e"), new DateTime(2023, 11, 15, 10, 35, 0, 0, DateTimeKind.Unspecified), "Leather winter boots", "Lviv", "Open Box", 234.0, "Boots" },
                    { new Guid("c3d4e5f6-a7b8-4c9d-0e1f-2a3b4c5d6e7f"), new DateTime(2023, 11, 15, 10, 40, 0, 0, DateTimeKind.Unspecified), "Stylish men's wristwatch", "Odesa", "New", 345.0, "Watch" }
                });

            migrationBuilder.InsertData(
                table: "ProductImages",
                columns: new[] { "Id", "ImagePath", "ProductId", "SortOrder" },
                values: new object[,]
                {
                    { new Guid("a1b2c3d4-e5f6-4a7b-8c9d-111111111111"), "uploads/product-photos/tshirt1.jpg", new Guid("a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d"), 0 },
                    { new Guid("a1b2c3d4-e5f6-4a7b-8c9d-222222222222"), "uploads/product-photos/tshirt2.jpg", new Guid("a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d"), 1 },
                    { new Guid("b2c3d4e5-f6a7-4b8c-9d0e-333333333333"), "uploads/product-photos/boots1.jpg", new Guid("b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e"), 0 },
                    { new Guid("c3d4e5f6-a7b8-4c9d-0e1f-444444444444"), "uploads/product-photos/watch1.jpg", new Guid("c3d4e5f6-a7b8-4c9d-0e1f-2a3b4c5d6e7f"), 0 }
                });
        }
    }
}
