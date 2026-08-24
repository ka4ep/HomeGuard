using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeGuard.Infrastructure.Migrations;

/// <inheritdoc />
public partial class WarrantyCost : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "Cost",
            table: "Warranties",
            type: "TEXT",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Cost",
            table: "Warranties");
    }
}
