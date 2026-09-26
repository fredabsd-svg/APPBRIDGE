using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppBridge.ControlPlane.Migrations
{
    /// <inheritdoc />
    public partial class SessionPendingBinding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "backend_session_id",
                table: "session",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Sessões ainda sem vínculo não têm identificador no RDS; o esquema anterior exigia texto.
            migrationBuilder.Sql("UPDATE session SET backend_session_id = '' WHERE backend_session_id IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "backend_session_id",
                table: "session",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
