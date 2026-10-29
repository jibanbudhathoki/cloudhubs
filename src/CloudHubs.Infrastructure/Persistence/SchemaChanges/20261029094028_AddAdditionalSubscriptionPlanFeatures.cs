using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CloudHubs.Infrastructure.Persistence.SchemaChanges
{
    /// <inheritdoc />
    public partial class AddAdditionalSubscriptionPlanFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasAuditLogs",
                table: "SubscriptionPlans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasAutomatedBackups",
                table: "SubscriptionPlans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasCustomRoles",
                table: "SubscriptionPlans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasIntegrations",
                table: "SubscriptionPlans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasSingleSignOn",
                table: "SubscriptionPlans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "SupportResponseHours",
                table: "SubscriptionPlans",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasAuditLogs",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "HasAutomatedBackups",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "HasCustomRoles",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "HasIntegrations",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "HasSingleSignOn",
                table: "SubscriptionPlans");

            migrationBuilder.DropColumn(
                name: "SupportResponseHours",
                table: "SubscriptionPlans");
        }
    }
}
