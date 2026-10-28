using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyDevPlatform.Infrastructure.Persistence.SchemaChanges
{
    /// <inheritdoc />
    public partial class AddSuperuserLoginId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Users",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AddColumn<string>(
                name: "LoginId",
                table: "Users",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_LoginId",
                table: "Users",
                column: "LoginId",
                unique: true,
                filter: "\"LoginId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_LoginId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LoginId",
                table: "Users");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Users",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);
        }
    }
}
