using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MyDevPlatform.Infrastructure.Persistence.SchemaChanges
{
    /// <inheritdoc />
    public partial class AddPlatformSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Subscriptions",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "MonthlyPrice",
                table: "Subscriptions",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "PlanName",
                table: "Subscriptions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "PlatformSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlatformName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SupportEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PublicUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SessionTimeoutMinutes = table.Column<int>(type: "integer", nullable: false),
                    JwtLifetimeMinutes = table.Column<int>(type: "integer", nullable: false),
                    MinimumPasswordLength = table.Column<int>(type: "integer", nullable: false),
                    RequirePasswordDigit = table.Column<bool>(type: "boolean", nullable: false),
                    AllowOrganizationCreation = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultOrganizationStatus = table.Column<int>(type: "integer", nullable: false),
                    DefaultTrialDays = table.Column<int>(type: "integer", nullable: false),
                    DefaultTemplateId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    DefaultPlanName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BillingCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    MonthlyPrice = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    NotifyOnOrganizationCreated = table.Column<bool>(type: "boolean", nullable: false),
                    NotificationEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformSettings", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "PlatformSettings",
                columns: new[]
                {
                    "Id", "PlatformName", "SupportEmail", "PublicUrl", "SessionTimeoutMinutes",
                    "JwtLifetimeMinutes", "MinimumPasswordLength", "RequirePasswordDigit",
                    "AllowOrganizationCreation", "DefaultOrganizationStatus", "DefaultTrialDays",
                    "DefaultTemplateId", "DefaultPlanName", "BillingCurrency", "MonthlyPrice",
                    "NotifyOnOrganizationCreated", "NotificationEmail", "UpdatedAt"
                },
                values: new object[]
                {
                    1, "CloudHubs", "", "", 480, 15, 12, true, true, 1, 30,
                    "default", "Standard", "USD", 0m, false, "", new DateTime(2026, 10, 28, 0, 0, 0, DateTimeKind.Utc)
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlatformSettings");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "MonthlyPrice",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "PlanName",
                table: "Subscriptions");
        }
    }
}
