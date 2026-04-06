using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeGuard.Infrastructure.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Equipment",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                Category = table.Column<int>(type: "INTEGER", nullable: false),
                Brand = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                Model = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                SerialNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                PurchaseDate = table.Column<string>(type: "TEXT", nullable: false),
                PurchasePrice = table.Column<decimal>(type: "TEXT", nullable: true),
                Notes = table.Column<string>(type: "TEXT", nullable: true),
                Tags = table.Column<string>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Equipment", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "ProcessedOperations",
            columns: table => new
            {
                ClientOperationId = table.Column<Guid>(type: "TEXT", nullable: false),
                UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                OperationType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                AckJson = table.Column<string>(type: "TEXT", nullable: false),
                ProcessedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProcessedOperations", x => x.ClientOperationId);
            });

        migrationBuilder.CreateTable(
            name: "PushSubscriptions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                Endpoint = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                P256dh = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                Auth = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PushSubscriptions", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "ScheduledJobs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                JobType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                PayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                RunAfter = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                Status = table.Column<int>(type: "INTEGER", nullable: false),
                AttemptCount = table.Column<int>(type: "INTEGER", nullable: false),
                LastAttemptAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                LastError = table.Column<string>(type: "TEXT", nullable: true),
                CorrelationKey = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ScheduledJobs", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Users",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                DisplayName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Users", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "ServiceRecords",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                EquipmentId = table.Column<Guid>(type: "TEXT", nullable: false),
                Title = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                ServiceDate = table.Column<string>(type: "TEXT", nullable: false),
                NextServiceDate = table.Column<string>(type: "TEXT", nullable: true),
                Cost = table.Column<decimal>(type: "TEXT", nullable: true),
                ServiceProvider = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                Notes = table.Column<string>(type: "TEXT", nullable: true),
                OdometerReading = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                GoogleCalendarEventId = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ServiceRecords", x => x.Id);
                table.ForeignKey(
                    name: "FK_ServiceRecords_Equipment_EquipmentId",
                    column: x => x.EquipmentId,
                    principalTable: "Equipment",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "Warranties",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                EquipmentId = table.Column<Guid>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                PeriodStart = table.Column<string>(type: "TEXT", nullable: false),
                PeriodEnd = table.Column<string>(type: "TEXT", nullable: false),
                Provider = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                ContractNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                Notes = table.Column<string>(type: "TEXT", nullable: true),
                GoogleCalendarEventId = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Warranties", x => x.Id);
                table.ForeignKey(
                    name: "FK_Warranties_Equipment_EquipmentId",
                    column: x => x.EquipmentId,
                    principalTable: "Equipment",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "Credentials",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                CredentialId = table.Column<byte[]>(type: "BLOB", nullable: false),
                PublicKey = table.Column<byte[]>(type: "BLOB", nullable: false),
                DeviceName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                SignCount = table.Column<uint>(type: "INTEGER", nullable: false),
                LastUsedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Credentials", x => x.Id);
                table.ForeignKey(
                    name: "FK_Credentials_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ServiceRecords_NotificationRules",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Offset = table.Column<int>(type: "INTEGER", nullable: false),
                IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                ServiceRecordId = table.Column<Guid>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ServiceRecords_NotificationRules", x => x.Id);
                table.ForeignKey(
                    name: "FK_ServiceRecords_NotificationRules_ServiceRecords_ServiceRecordId",
                    column: x => x.ServiceRecordId,
                    principalTable: "ServiceRecords",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "BlobEntries",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                OwnerEntityId = table.Column<Guid>(type: "TEXT", nullable: false),
                OwnerEntityType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                FileName = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                ContentType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                SizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                LocalPath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                NextCloudPath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                SyncStatus = table.Column<int>(type: "INTEGER", nullable: false),
                ServiceRecordId = table.Column<Guid>(type: "TEXT", nullable: true),
                WarrantyId = table.Column<Guid>(type: "TEXT", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_BlobEntries", x => x.Id);
                table.ForeignKey(
                    name: "FK_BlobEntries_Equipment_OwnerEntityId",
                    column: x => x.OwnerEntityId,
                    principalTable: "Equipment",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_BlobEntries_ServiceRecords_ServiceRecordId",
                    column: x => x.ServiceRecordId,
                    principalTable: "ServiceRecords",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_BlobEntries_Warranties_WarrantyId",
                    column: x => x.WarrantyId,
                    principalTable: "Warranties",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateTable(
            name: "Warranties_NotificationRules",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Offset = table.Column<int>(type: "INTEGER", nullable: false),
                IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                WarrantyId = table.Column<Guid>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Warranties_NotificationRules", x => x.Id);
                table.ForeignKey(
                    name: "FK_Warranties_NotificationRules_Warranties_WarrantyId",
                    column: x => x.WarrantyId,
                    principalTable: "Warranties",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_BlobEntries_OwnerEntityId_OwnerEntityType",
            table: "BlobEntries",
            columns: ["OwnerEntityId", "OwnerEntityType"]);

        migrationBuilder.CreateIndex(
            name: "IX_BlobEntries_ServiceRecordId",
            table: "BlobEntries",
            column: "ServiceRecordId");

        migrationBuilder.CreateIndex(
            name: "IX_BlobEntries_SyncStatus",
            table: "BlobEntries",
            column: "SyncStatus");

        migrationBuilder.CreateIndex(
            name: "IX_BlobEntries_WarrantyId",
            table: "BlobEntries",
            column: "WarrantyId");

        migrationBuilder.CreateIndex(
            name: "IX_Credentials_CredentialId",
            table: "Credentials",
            column: "CredentialId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Credentials_UserId",
            table: "Credentials",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_ProcessedOperations_ProcessedAt",
            table: "ProcessedOperations",
            column: "ProcessedAt");

        migrationBuilder.CreateIndex(
            name: "IX_PushSubscriptions_UserId",
            table: "PushSubscriptions",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_ScheduledJobs_CorrelationKey",
            table: "ScheduledJobs",
            column: "CorrelationKey");

        migrationBuilder.CreateIndex(
            name: "IX_ScheduledJobs_Status_RunAfter",
            table: "ScheduledJobs",
            columns: ["Status", "RunAfter"]);

        migrationBuilder.CreateIndex(
            name: "IX_ServiceRecords_EquipmentId",
            table: "ServiceRecords",
            column: "EquipmentId");

        migrationBuilder.CreateIndex(
            name: "IX_ServiceRecords_NotificationRules_ServiceRecordId",
            table: "ServiceRecords_NotificationRules",
            column: "ServiceRecordId");

        migrationBuilder.CreateIndex(
            name: "IX_Warranties_EquipmentId",
            table: "Warranties",
            column: "EquipmentId");

        migrationBuilder.CreateIndex(
            name: "IX_Warranties_NotificationRules_WarrantyId",
            table: "Warranties_NotificationRules",
            column: "WarrantyId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "BlobEntries");

        migrationBuilder.DropTable(
            name: "Credentials");

        migrationBuilder.DropTable(
            name: "ProcessedOperations");

        migrationBuilder.DropTable(
            name: "PushSubscriptions");

        migrationBuilder.DropTable(
            name: "ScheduledJobs");

        migrationBuilder.DropTable(
            name: "ServiceRecords_NotificationRules");

        migrationBuilder.DropTable(
            name: "Warranties_NotificationRules");

        migrationBuilder.DropTable(
            name: "Users");

        migrationBuilder.DropTable(
            name: "ServiceRecords");

        migrationBuilder.DropTable(
            name: "Warranties");

        migrationBuilder.DropTable(
            name: "Equipment");
    }
}
