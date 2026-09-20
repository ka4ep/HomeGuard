using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeGuard.Infrastructure.Migrations;

/// <inheritdoc />
public partial class FixBlobEntryOwnerPolymorphism : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_BlobEntries_Equipment_OwnerEntityId",
            table: "BlobEntries");

        migrationBuilder.DropForeignKey(
            name: "FK_BlobEntries_ServiceRecords_ServiceRecordId",
            table: "BlobEntries");

        migrationBuilder.DropForeignKey(
            name: "FK_BlobEntries_Warranties_WarrantyId",
            table: "BlobEntries");

        migrationBuilder.DropIndex(
            name: "IX_BlobEntries_ServiceRecordId",
            table: "BlobEntries");

        migrationBuilder.DropIndex(
            name: "IX_BlobEntries_WarrantyId",
            table: "BlobEntries");

        migrationBuilder.DropColumn(
            name: "ServiceRecordId",
            table: "BlobEntries");

        migrationBuilder.DropColumn(
            name: "WarrantyId",
            table: "BlobEntries");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "ServiceRecordId",
            table: "BlobEntries",
            type: "TEXT",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "WarrantyId",
            table: "BlobEntries",
            type: "TEXT",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_BlobEntries_ServiceRecordId",
            table: "BlobEntries",
            column: "ServiceRecordId");

        migrationBuilder.CreateIndex(
            name: "IX_BlobEntries_WarrantyId",
            table: "BlobEntries",
            column: "WarrantyId");

        migrationBuilder.AddForeignKey(
            name: "FK_BlobEntries_Equipment_OwnerEntityId",
            table: "BlobEntries",
            column: "OwnerEntityId",
            principalTable: "Equipment",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_BlobEntries_ServiceRecords_ServiceRecordId",
            table: "BlobEntries",
            column: "ServiceRecordId",
            principalTable: "ServiceRecords",
            principalColumn: "Id");

        migrationBuilder.AddForeignKey(
            name: "FK_BlobEntries_Warranties_WarrantyId",
            table: "BlobEntries",
            column: "WarrantyId",
            principalTable: "Warranties",
            principalColumn: "Id");
    }
}
