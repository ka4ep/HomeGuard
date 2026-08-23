using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeGuard.Infrastructure.Migrations;

/// <summary>
/// Contracts, their versioned payment plans and the individual payments: four tables
/// plus two for the owned collections. Money columns are TEXT — SQLite has no decimal
/// type and REAL would quietly round amounts the household cares about.
/// </summary>
public partial class Contracts : Migration
{
    // В полях, а не в вызовах: CA1861 запрещает константные массивы
    // в повторяющихся вызовах.
    private static readonly string[] PaymentsByContractAndDueDate = ["ContractId", "DueDate"];
    private static readonly string[] PaymentsByStatusAndDueDate = ["Status", "DueDate"];
    private static readonly string[] RevisionsByContractAndVersion = ["ContractId", "Version"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Contracts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                EquipmentId = table.Column<Guid>(type: "TEXT", nullable: true),
                Kind = table.Column<int>(type: "INTEGER", nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                Provider = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                ContractNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                StartDate = table.Column<string>(type: "TEXT", nullable: false),
                EndDate = table.Column<string>(type: "TEXT", nullable: true),
                Renewal = table.Column<int>(type: "INTEGER", nullable: false),
                CancellationNoticeDays = table.Column<int>(type: "INTEGER", nullable: true),
                Status = table.Column<int>(type: "INTEGER", nullable: false),
                PreviousContractId = table.Column<Guid>(type: "TEXT", nullable: true),
                Currency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                SummaryMarkdown = table.Column<string>(type: "TEXT", nullable: true),
                Notes = table.Column<string>(type: "TEXT", nullable: true),
                CoverageAmount = table.Column<decimal>(type: "TEXT", nullable: true),
                Deductible = table.Column<decimal>(type: "TEXT", nullable: true),
                OpeningAsOfDate = table.Column<string>(type: "TEXT", nullable: true),
                OpeningInstallmentsPaid = table.Column<int>(type: "INTEGER", nullable: true),
                OpeningAmountPaid = table.Column<decimal>(type: "TEXT", nullable: true),
                OpeningRemainingBalance = table.Column<decimal>(type: "TEXT", nullable: true),
                Tags = table.Column<string>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Contracts", x => x.Id);
                table.ForeignKey(
                    name: "FK_Contracts_Equipment_EquipmentId",
                    column: x => x.EquipmentId,
                    principalTable: "Equipment",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "Contracts_NotificationRules",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Offset = table.Column<int>(type: "INTEGER", nullable: false),
                IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                ContractId = table.Column<Guid>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Contracts_NotificationRules", x => x.Id);
                table.ForeignKey(
                    name: "FK_Contracts_NotificationRules_Contracts_ContractId",
                    column: x => x.ContractId,
                    principalTable: "Contracts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "Payments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                ContractId = table.Column<Guid>(type: "TEXT", nullable: false),
                PlanRevisionId = table.Column<Guid>(type: "TEXT", nullable: true),
                InstallmentNo = table.Column<int>(type: "INTEGER", nullable: true),
                Kind = table.Column<int>(type: "INTEGER", nullable: false),
                Status = table.Column<int>(type: "INTEGER", nullable: false),
                DueDate = table.Column<string>(type: "TEXT", nullable: false),
                AmountDue = table.Column<decimal>(type: "TEXT", nullable: false),
                PaidDate = table.Column<string>(type: "TEXT", nullable: true),
                AmountPaid = table.Column<decimal>(type: "TEXT", nullable: true),
                PrincipalPart = table.Column<decimal>(type: "TEXT", nullable: true),
                InterestPart = table.Column<decimal>(type: "TEXT", nullable: true),
                Note = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Payments", x => x.Id);
                table.ForeignKey(
                    name: "FK_Payments_Contracts_ContractId",
                    column: x => x.ContractId,
                    principalTable: "Contracts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "PlanRevisions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                ContractId = table.Column<Guid>(type: "TEXT", nullable: false),
                Version = table.Column<int>(type: "INTEGER", nullable: false),
                EffectiveFrom = table.Column<string>(type: "TEXT", nullable: false),
                Reason = table.Column<int>(type: "INTEGER", nullable: false),
                FirstDueDate = table.Column<string>(type: "TEXT", nullable: false),
                IntervalMonths = table.Column<int>(type: "INTEGER", nullable: false),
                InstallmentCount = table.Column<int>(type: "INTEGER", nullable: true),
                InstallmentAmount = table.Column<decimal>(type: "TEXT", nullable: false),
                RemainingPrincipal = table.Column<decimal>(type: "TEXT", nullable: true),
                AnnualInterestRate = table.Column<decimal>(type: "TEXT", nullable: true),
                ResidualAmount = table.Column<decimal>(type: "TEXT", nullable: true),
                ResidualDueDate = table.Column<string>(type: "TEXT", nullable: true),
                Note = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PlanRevisions", x => x.Id);
                table.ForeignKey(
                    name: "FK_PlanRevisions_Contracts_ContractId",
                    column: x => x.ContractId,
                    principalTable: "Contracts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "PlanRevisions_Adjustments",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                Amount = table.Column<decimal>(type: "TEXT", nullable: false),
                PlanRevisionId = table.Column<Guid>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PlanRevisions_Adjustments", x => x.Id);
                table.ForeignKey(
                    name: "FK_PlanRevisions_Adjustments_PlanRevisions_PlanRevisionId",
                    column: x => x.PlanRevisionId,
                    principalTable: "PlanRevisions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Contracts_EquipmentId",
            table: "Contracts",
            column: "EquipmentId");

        migrationBuilder.CreateIndex(
            name: "IX_Contracts_Status",
            table: "Contracts",
            column: "Status");

        migrationBuilder.CreateIndex(
            name: "IX_Contracts_NotificationRules_ContractId",
            table: "Contracts_NotificationRules",
            column: "ContractId");

        migrationBuilder.CreateIndex(
            name: "IX_Payments_ContractId_DueDate",
            table: "Payments",
            columns: PaymentsByContractAndDueDate);

        migrationBuilder.CreateIndex(
            name: "IX_Payments_Status_DueDate",
            table: "Payments",
            columns: PaymentsByStatusAndDueDate);

        migrationBuilder.CreateIndex(
            name: "IX_PlanRevisions_ContractId_Version",
            table: "PlanRevisions",
            columns: RevisionsByContractAndVersion,
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_PlanRevisions_Adjustments_PlanRevisionId",
            table: "PlanRevisions_Adjustments",
            column: "PlanRevisionId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Contracts_NotificationRules");

        migrationBuilder.DropTable(
            name: "Payments");

        migrationBuilder.DropTable(
            name: "PlanRevisions_Adjustments");

        migrationBuilder.DropTable(
            name: "PlanRevisions");

        migrationBuilder.DropTable(
            name: "Contracts");
    }
}
