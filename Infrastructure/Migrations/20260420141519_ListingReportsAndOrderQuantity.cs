using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ListingReportsAndOrderQuantity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PaymentCardMasked",
                table: "Orders",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Quantity",
                table: "Orders",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "ListingReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ListingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReporterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResolvedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListingReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ListingReports_AspNetUsers_ReporterId",
                        column: x => x.ReporterId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ListingReports_AspNetUsers_ResolvedById",
                        column: x => x.ResolvedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ListingReports_Listings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "Listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ListingReports_ListingId",
                table: "ListingReports",
                column: "ListingId");

            migrationBuilder.CreateIndex(
                name: "IX_ListingReports_ReporterId",
                table: "ListingReports",
                column: "ReporterId");

            migrationBuilder.CreateIndex(
                name: "IX_ListingReports_ResolvedById",
                table: "ListingReports",
                column: "ResolvedById");

            migrationBuilder.Sql("UPDATE [Orders] SET [Quantity] = 1 WHERE [Quantity] < 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ListingReports");

            migrationBuilder.DropColumn(
                name: "PaymentCardMasked",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "Orders");
        }
    }
}
