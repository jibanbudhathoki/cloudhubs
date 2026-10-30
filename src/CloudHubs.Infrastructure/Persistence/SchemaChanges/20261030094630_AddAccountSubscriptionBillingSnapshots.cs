using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CloudHubs.Infrastructure.Persistence.SchemaChanges
{
    /// <inheritdoc />
    public partial class AddAccountSubscriptionBillingSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BillingInterval",
                table: "SubscriptionPurchases",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "SubscriptionPurchases",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PlanName",
                table: "SubscriptionPurchases",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "Price",
                table: "SubscriptionPurchases",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "BillingInterval",
                table: "AccountSubscriptions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "AccountSubscriptions",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PlanName",
                table: "AccountSubscriptions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "Price",
                table: "AccountSubscriptions",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BillingInterval",
                table: "SubscriptionPurchases");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "SubscriptionPurchases");

            migrationBuilder.DropColumn(
                name: "PlanName",
                table: "SubscriptionPurchases");

            migrationBuilder.DropColumn(
                name: "Price",
                table: "SubscriptionPurchases");

            migrationBuilder.DropColumn(
                name: "BillingInterval",
                table: "AccountSubscriptions");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "AccountSubscriptions");

            migrationBuilder.DropColumn(
                name: "PlanName",
                table: "AccountSubscriptions");

            migrationBuilder.DropColumn(
                name: "Price",
                table: "AccountSubscriptions");
        }
    }
}
