using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeGuard.Infrastructure.Migrations;

/// <summary>
/// Adds AppUser.Language — the interface language, as an ISO 639-1 code.
/// It lives on the account rather than only in the browser because push notifications
/// and the calendar feed are rendered server-side, where there is no Accept-Language
/// to read. Existing rows default to "ru", the household's own language.
/// </summary>
public partial class AppUserLanguage : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Language",
            table: "Users",
            type: "TEXT",
            maxLength: 8,
            nullable: false,
            defaultValue: "ru");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Language",
            table: "Users");
    }
}
