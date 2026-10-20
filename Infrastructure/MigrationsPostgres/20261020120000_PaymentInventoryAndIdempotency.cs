using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.MigrationsPostgres;

public partial class PaymentInventoryAndIdempotency : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "ReservedQuantity",
            table: "Listings",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<bool>(
            name: "InventoryReserved",
            table: "Orders",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        // Older payment processing could create more than one order for the same
        // payment intent and listing. Preserve every historical order, but keep
        // the payment intent attached to the most complete order so the new
        // idempotency index can be created without deleting business data.
        migrationBuilder.Sql("""
            WITH ranked_orders AS (
                SELECT
                    "Id",
                    ROW_NUMBER() OVER (
                        PARTITION BY "StripePaymentIntentId", "ListingId"
                        ORDER BY
                            CASE
                                WHEN "PaymentState" IN (2, 3, 4, 5) THEN 0
                                ELSE 1
                            END,
                            CASE "Status"
                                WHEN 6 THEN 0 -- Delivered
                                WHEN 7 THEN 1 -- ReadyForPickup
                                WHEN 5 THEN 2 -- Shipped
                                WHEN 2 THEN 3 -- Accepted
                                WHEN 1 THEN 4 -- Pending
                                ELSE 5
                            END,
                            "UpdatedAtUtc" DESC NULLS LAST,
                            "CreatedAtUtc",
                            "Id"
                    ) AS duplicate_rank
                FROM "Orders"
                WHERE "StripePaymentIntentId" IS NOT NULL
            )
            UPDATE "Orders" AS orders
            SET "StripePaymentIntentId" = NULL
            FROM ranked_orders
            WHERE orders."Id" = ranked_orders."Id"
              AND ranked_orders.duplicate_rank > 1;
            """);

        migrationBuilder.CreateIndex(
            name: "IX_PaymentSessions_StripePaymentIntentId",
            table: "PaymentSessions",
            column: "StripePaymentIntentId",
            unique: true,
            filter: "\"StripePaymentIntentId\" IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_Orders_StripePaymentIntentId_ListingId",
            table: "Orders",
            columns: new[] { "StripePaymentIntentId", "ListingId" },
            unique: true,
            filter: "\"StripePaymentIntentId\" IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_PaymentSessions_StripePaymentIntentId",
            table: "PaymentSessions");

        migrationBuilder.DropIndex(
            name: "IX_Orders_StripePaymentIntentId_ListingId",
            table: "Orders");

        migrationBuilder.DropColumn(
            name: "ReservedQuantity",
            table: "Listings");

        migrationBuilder.DropColumn(
            name: "InventoryReserved",
            table: "Orders");
    }
}
