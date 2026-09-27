using Microsoft.EntityFrameworkCore.Migrations;
using Infrastructure.DbContextt;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace Infrastructure.MigrationsPostgres;

[DbContext(typeof(ProductDbContext))]
[Migration("20260926170000_SupportChatWorkflow")]
public partial class SupportChatWorkflow : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "SupportStatus",
            table: "Chats",
            type: "integer",
            nullable: false,
            defaultValue: 1);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "SupportStatus",
            table: "Chats");
    }
}
