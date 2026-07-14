using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeGuard.Infrastructure.Migrations;

/// <summary>
/// Adds RecurringRule.AnchorToPurchaseDate. Additive, non-breaking: defaults to true for
/// every existing rule (matches the new default — most tracked items are assumed to have
/// existed since the equipment was purchased unless a user later says otherwise).
/// </summary>
public partial class RecurringRuleAnchorToPurchaseDate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "AnchorToPurchaseDate",
            table: "RecurringRules",
            type: "INTEGER",
            nullable: false,
            defaultValue: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "AnchorToPurchaseDate",
            table: "RecurringRules");
    }
}
