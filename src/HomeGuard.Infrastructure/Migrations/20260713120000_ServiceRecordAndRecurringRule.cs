using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeGuard.Infrastructure.Migrations;

/// <summary>
/// Adds RecurringRule + ServiceStatus and replaces ServiceRecord.OdometerReading/NextServiceDate
/// with MeterReading/Status/RecurringRuleId/OriginalPredictedDate.
///
/// Safe on a live database with existing rows:
///  1. All new columns/tables are added first, additive and nullable (or with a safe default).
///  2. Legacy data is copied forward — OdometerReading into MeterReading where it parses cleanly
///     as a plain decimal, everything else (unparseable OdometerReading, and NextServiceDate,
///     which has no direct column in the new model) is appended to Notes instead of discarded.
///  3. Only then are the superseded columns dropped.
/// No existing row's information is deleted by this migration.
/// </summary>
public partial class ServiceRecordAndRecurringRule : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ── 1. Additive schema changes ────────────────────────────────────────────

        migrationBuilder.AddColumn<string>(
            name: "MeterUnit",
            table: "Equipment",
            type: "TEXT",
            maxLength: 20,
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "MeterReading",
            table: "ServiceRecords",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "Status",
            table: "ServiceRecords",
            type: "INTEGER",
            nullable: false,
            // ServiceStatus.Completed = 0. Every pre-existing row represents a service that
            // already happened, so Completed is the only correct value for legacy data.
            defaultValue: 0);

        migrationBuilder.AddColumn<Guid>(
            name: "RecurringRuleId",
            table: "ServiceRecords",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "OriginalPredictedDate",
            table: "ServiceRecords",
            type: "TEXT",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "RecurringRules",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                EquipmentId = table.Column<Guid>(type: "TEXT", nullable: false),
                Title = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                IntervalDays = table.Column<int>(type: "INTEGER", nullable: true),
                IntervalMeter = table.Column<decimal>(type: "TEXT", nullable: true),
                MaterializeDaysAhead = table.Column<int>(type: "INTEGER", nullable: false),
                PredictionsAhead = table.Column<int>(type: "INTEGER", nullable: false),
                IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RecurringRules", x => x.Id);
                table.ForeignKey(
                    name: "FK_RecurringRules_Equipment_EquipmentId",
                    column: x => x.EquipmentId,
                    principalTable: "Equipment",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_RecurringRules_EquipmentId",
            table: "RecurringRules",
            column: "EquipmentId");

        migrationBuilder.CreateIndex(
            name: "IX_ServiceRecords_RecurringRuleId",
            table: "ServiceRecords",
            column: "RecurringRuleId");

        migrationBuilder.AddForeignKey(
            name: "FK_ServiceRecords_RecurringRules_RecurringRuleId",
            table: "ServiceRecords",
            column: "RecurringRuleId",
            principalTable: "RecurringRules",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);

        // ── 2. Carry legacy data forward — nothing is discarded ───────────────────
        //
        // "Clean numeric" = only digits/dot, at least one digit, at most one dot.
        // Anything that qualifies is copied into MeterReading; anything that doesn't
        // (free-text odometer notes, unit suffixes, etc.) is preserved verbatim in Notes.

        migrationBuilder.Sql(@"
UPDATE ServiceRecords
SET MeterReading = TRIM(OdometerReading)
WHERE OdometerReading IS NOT NULL
  AND TRIM(OdometerReading) <> ''
  AND TRIM(OdometerReading) GLOB '*[0-9]*'
  AND TRIM(OdometerReading) NOT GLOB '*[^0-9.]*'
  AND (LENGTH(TRIM(OdometerReading)) - LENGTH(REPLACE(TRIM(OdometerReading), '.', ''))) <= 1;
");

        migrationBuilder.Sql(@"
UPDATE ServiceRecords
SET Notes = CASE
    WHEN Notes IS NULL OR TRIM(Notes) = '' THEN 'Legacy odometer reading: ' || OdometerReading
    ELSE Notes || char(10) || 'Legacy odometer reading: ' || OdometerReading
    END
WHERE OdometerReading IS NOT NULL
  AND TRIM(OdometerReading) <> ''
  AND NOT (
    TRIM(OdometerReading) GLOB '*[0-9]*'
    AND TRIM(OdometerReading) NOT GLOB '*[^0-9.]*'
    AND (LENGTH(TRIM(OdometerReading)) - LENGTH(REPLACE(TRIM(OdometerReading), '.', ''))) <= 1
  );
");

        // NextServiceDate has no 1:1 replacement column in the new model (prediction is
        // now driven by RecurringRule) — preserve the value as text so it isn't lost.
        migrationBuilder.Sql(@"
UPDATE ServiceRecords
SET Notes = CASE
    WHEN Notes IS NULL OR TRIM(Notes) = '' THEN 'Legacy next service date: ' || NextServiceDate
    ELSE Notes || char(10) || 'Legacy next service date: ' || NextServiceDate
    END
WHERE NextServiceDate IS NOT NULL;
");

        // ── 3. Only now is it safe to drop the superseded columns ─────────────────

        migrationBuilder.DropColumn(
            name: "OdometerReading",
            table: "ServiceRecords");

        migrationBuilder.DropColumn(
            name: "NextServiceDate",
            table: "ServiceRecords");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "OdometerReading",
            table: "ServiceRecords",
            type: "TEXT",
            maxLength: 50,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "NextServiceDate",
            table: "ServiceRecords",
            type: "TEXT",
            nullable: true);

        migrationBuilder.DropForeignKey(
            name: "FK_ServiceRecords_RecurringRules_RecurringRuleId",
            table: "ServiceRecords");

        migrationBuilder.DropIndex(
            name: "IX_ServiceRecords_RecurringRuleId",
            table: "ServiceRecords");

        migrationBuilder.DropTable(
            name: "RecurringRules");

        migrationBuilder.DropColumn(
            name: "MeterReading",
            table: "ServiceRecords");

        migrationBuilder.DropColumn(
            name: "Status",
            table: "ServiceRecords");

        migrationBuilder.DropColumn(
            name: "RecurringRuleId",
            table: "ServiceRecords");

        migrationBuilder.DropColumn(
            name: "OriginalPredictedDate",
            table: "ServiceRecords");

        migrationBuilder.DropColumn(
            name: "MeterUnit",
            table: "Equipment");
    }
}
