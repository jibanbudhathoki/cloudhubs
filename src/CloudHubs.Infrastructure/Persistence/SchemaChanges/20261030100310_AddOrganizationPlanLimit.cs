using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CloudHubs.Infrastructure.Persistence.SchemaChanges
{
    /// <inheritdoc />
    public partial class AddOrganizationPlanLimit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxOrganizations",
                table: "SubscriptionPlans",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                "UPDATE \"SubscriptionPlans\" SET \"MaxOrganizations\" = 2 WHERE lower(\"Name\") = 'basic';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxOrganizations",
                table: "SubscriptionPlans");
        }
    }
}
