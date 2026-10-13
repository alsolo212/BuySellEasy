using Infrastructure.DbContextt;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.MigrationsPostgres;

public partial class StripeFeeResolution : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "StripeCustomerId", table: "AspNetUsers", type: "character varying(128)", maxLength: 128, nullable: true);
        migrationBuilder.AddColumn<string>(name: "StripeConnectAccountId", table: "AspNetUsers", type: "character varying(128)", maxLength: 128, nullable: true);
        migrationBuilder.AddColumn<int>(name: "PaymentState", table: "Orders", type: "integer", nullable: false, defaultValue: 1);
        migrationBuilder.AddColumn<decimal>(name: "StripeFeeAmount", table: "Orders", type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<bool>(name: "StripeFeeCalculated", table: "Orders", type: "boolean", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<string>(name: "StripeCheckoutSessionId", table: "Orders", type: "character varying(128)", maxLength: 128, nullable: true);
        migrationBuilder.AddColumn<string>(name: "StripePaymentIntentId", table: "Orders", type: "character varying(128)", maxLength: 128, nullable: true);
        migrationBuilder.AddColumn<string>(name: "StripeTransferId", table: "Orders", type: "character varying(128)", maxLength: 128, nullable: true);
        migrationBuilder.AddColumn<string>(name: "StripeRefundId", table: "Orders", type: "character varying(128)", maxLength: 128, nullable: true);
        migrationBuilder.AddColumn<DateTime>(name: "SellerDecisionDeadlineUtc", table: "Orders", type: "timestamp with time zone", nullable: false, defaultValue: new DateTime(2099, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        migrationBuilder.AddColumn<DateTime>(name: "ReadyForPickupAtUtc", table: "Orders", type: "timestamp with time zone", nullable: true);
        migrationBuilder.AddColumn<DateTime>(name: "DeliveredAtUtc", table: "Orders", type: "timestamp with time zone", nullable: true);

        migrationBuilder.CreateTable(
            name: "PaymentSessions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                BuyerId = table.Column<Guid>(type: "uuid", nullable: false),
                StripeCheckoutSessionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                StripePaymentIntentId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                Status = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                DraftJson = table.Column<string>(type: "text", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ProcessedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PaymentSessions", x => x.Id);
                table.ForeignKey(name: "FK_PaymentSessions_AspNetUsers_BuyerId", x => x.BuyerId, principalTable: "AspNetUsers", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(name: "IX_PaymentSessions_BuyerId", table: "PaymentSessions", column: "BuyerId");
        migrationBuilder.CreateIndex(name: "IX_PaymentSessions_StripeCheckoutSessionId", table: "PaymentSessions", column: "StripeCheckoutSessionId", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "PaymentSessions");
        migrationBuilder.DropColumn(name: "StripeCustomerId", table: "AspNetUsers");
        migrationBuilder.DropColumn(name: "StripeConnectAccountId", table: "AspNetUsers");
        migrationBuilder.DropColumn(name: "PaymentState", table: "Orders");
        migrationBuilder.DropColumn(name: "StripeFeeAmount", table: "Orders");
        migrationBuilder.DropColumn(name: "StripeFeeCalculated", table: "Orders");
        migrationBuilder.DropColumn(name: "StripeCheckoutSessionId", table: "Orders");
        migrationBuilder.DropColumn(name: "StripePaymentIntentId", table: "Orders");
        migrationBuilder.DropColumn(name: "StripeTransferId", table: "Orders");
        migrationBuilder.DropColumn(name: "StripeRefundId", table: "Orders");
        migrationBuilder.DropColumn(name: "SellerDecisionDeadlineUtc", table: "Orders");
        migrationBuilder.DropColumn(name: "ReadyForPickupAtUtc", table: "Orders");
        migrationBuilder.DropColumn(name: "DeliveredAtUtc", table: "Orders");
    }
}
