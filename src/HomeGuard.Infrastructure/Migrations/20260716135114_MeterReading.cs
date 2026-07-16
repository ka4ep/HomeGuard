using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeGuard.Infrastructure.Migrations;

/// <summary>
/// Adds the MeterReadings table: standalone meter/odometer readings independent of service
/// events, fed by manual entry or an external ingestor. Additive, non-breaking.
/// </summary>
public partial class MeterReading : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "MeterReadings",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                EquipmentId = table.Column<Guid>(type: "TEXT", nullable: false),
                ReadingDate = table.Column<string>(type: "TEXT", nullable: false),
                Value = table.Column<decimal>(type: "TEXT", nullable: false),
                Source = table.Column<int>(type: "INTEGER", nullable: false),
                Note = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MeterReadings", x => x.Id);
                table.ForeignKey(
                    name: "FK_MeterReadings_Equipment_EquipmentId",
                    column: x => x.EquipmentId,
                    principalTable: "Equipment",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_MeterReadings_EquipmentId_ReadingDate",
            table: "MeterReadings",
            columns: ["EquipmentId", "ReadingDate"]);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "MeterReadings");
    }
}
