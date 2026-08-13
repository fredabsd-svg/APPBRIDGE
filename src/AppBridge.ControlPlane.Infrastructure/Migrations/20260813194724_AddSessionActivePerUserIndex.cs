using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppBridge.ControlPlane.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionActivePerUserIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_session_active_per_user",
                table: "session",
                columns: new[] { "tenant_id", "session_host_id", "user_account_id" },
                unique: true,
                filter: "ended_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_session_active_per_user",
                table: "session");
        }
    }
}
