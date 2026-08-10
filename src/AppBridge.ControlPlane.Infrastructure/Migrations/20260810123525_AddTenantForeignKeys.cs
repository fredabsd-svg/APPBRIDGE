using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AppBridge.ControlPlane.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "uq_user_account_tenant",
                table: "user_account",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddUniqueConstraint(
                name: "uq_session_host_tenant",
                table: "session_host",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddUniqueConstraint(
                name: "uq_session_tenant",
                table: "session",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddUniqueConstraint(
                name: "uq_host_pool_tenant",
                table: "host_pool",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddUniqueConstraint(
                name: "uq_group_tenant",
                table: "group",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddUniqueConstraint(
                name: "uq_application_tenant",
                table: "application",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_user_group_membership_tenant_id_group_id",
                table: "user_group_membership",
                columns: new[] { "tenant_id", "group_id" });

            migrationBuilder.CreateIndex(
                name: "IX_session_host_tenant_id_host_pool_id",
                table: "session_host",
                columns: new[] { "tenant_id", "host_pool_id" });

            migrationBuilder.CreateIndex(
                name: "IX_session_tenant_id_user_account_id",
                table: "session",
                columns: new[] { "tenant_id", "user_account_id" });

            migrationBuilder.CreateIndex(
                name: "IX_redirection_policy_tenant_id_application_id",
                table: "redirection_policy",
                columns: new[] { "tenant_id", "application_id" });

            migrationBuilder.CreateIndex(
                name: "IX_purge_run_tenant_id",
                table: "purge_run",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_launch_tenant_id_application_id",
                table: "launch",
                columns: new[] { "tenant_id", "application_id" });

            migrationBuilder.CreateIndex(
                name: "IX_launch_tenant_id_session_id",
                table: "launch",
                columns: new[] { "tenant_id", "session_id" });

            migrationBuilder.CreateIndex(
                name: "IX_launch_tenant_id_user_account_id",
                table: "launch",
                columns: new[] { "tenant_id", "user_account_id" });

            migrationBuilder.CreateIndex(
                name: "IX_application_permission_tenant_id_granted_by",
                table: "application_permission",
                columns: new[] { "tenant_id", "granted_by" });

            migrationBuilder.CreateIndex(
                name: "IX_application_permission_tenant_id_group_id",
                table: "application_permission",
                columns: new[] { "tenant_id", "group_id" });

            migrationBuilder.CreateIndex(
                name: "IX_application_permission_tenant_id_revoked_by",
                table: "application_permission",
                columns: new[] { "tenant_id", "revoked_by" });

            migrationBuilder.CreateIndex(
                name: "IX_application_tenant_id_host_pool_id",
                table: "application",
                columns: new[] { "tenant_id", "host_pool_id" });

            migrationBuilder.CreateIndex(
                name: "IX_access_event_tenant_id_user_account_id",
                table: "access_event",
                columns: new[] { "tenant_id", "user_account_id" });

            migrationBuilder.AddForeignKey(
                name: "fk_access_event_tenant",
                table: "access_event",
                column: "tenant_id",
                principalTable: "tenant",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_access_event_user_account",
                table: "access_event",
                columns: new[] { "tenant_id", "user_account_id" },
                principalTable: "user_account",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_application_host_pool",
                table: "application",
                columns: new[] { "tenant_id", "host_pool_id" },
                principalTable: "host_pool",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_application_tenant",
                table: "application",
                column: "tenant_id",
                principalTable: "tenant",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_application_permission_tenant",
                table: "application_permission",
                column: "tenant_id",
                principalTable: "tenant",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_permission_application",
                table: "application_permission",
                columns: new[] { "tenant_id", "application_id" },
                principalTable: "application",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_permission_granted_by",
                table: "application_permission",
                columns: new[] { "tenant_id", "granted_by" },
                principalTable: "user_account",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_permission_group",
                table: "application_permission",
                columns: new[] { "tenant_id", "group_id" },
                principalTable: "group",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_permission_revoked_by",
                table: "application_permission",
                columns: new[] { "tenant_id", "revoked_by" },
                principalTable: "user_account",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_group_tenant",
                table: "group",
                column: "tenant_id",
                principalTable: "tenant",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_host_pool_tenant",
                table: "host_pool",
                column: "tenant_id",
                principalTable: "tenant",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_launch_application",
                table: "launch",
                columns: new[] { "tenant_id", "application_id" },
                principalTable: "application",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_launch_session",
                table: "launch",
                columns: new[] { "tenant_id", "session_id" },
                principalTable: "session",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_launch_tenant",
                table: "launch",
                column: "tenant_id",
                principalTable: "tenant",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_launch_user_account",
                table: "launch",
                columns: new[] { "tenant_id", "user_account_id" },
                principalTable: "user_account",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_purge_run_tenant",
                table: "purge_run",
                column: "tenant_id",
                principalTable: "tenant",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_redirection_policy_application",
                table: "redirection_policy",
                columns: new[] { "tenant_id", "application_id" },
                principalTable: "application",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_redirection_policy_tenant",
                table: "redirection_policy",
                column: "tenant_id",
                principalTable: "tenant",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_retention_policy_tenant",
                table: "retention_policy",
                column: "tenant_id",
                principalTable: "tenant",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_session_session_host",
                table: "session",
                columns: new[] { "tenant_id", "session_host_id" },
                principalTable: "session_host",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_session_tenant",
                table: "session",
                column: "tenant_id",
                principalTable: "tenant",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_session_user_account",
                table: "session",
                columns: new[] { "tenant_id", "user_account_id" },
                principalTable: "user_account",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_session_host_host_pool",
                table: "session_host",
                columns: new[] { "tenant_id", "host_pool_id" },
                principalTable: "host_pool",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_session_host_tenant",
                table: "session_host",
                column: "tenant_id",
                principalTable: "tenant",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_user_account_tenant",
                table: "user_account",
                column: "tenant_id",
                principalTable: "tenant",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_user_group_membership_group",
                table: "user_group_membership",
                columns: new[] { "tenant_id", "group_id" },
                principalTable: "group",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_user_group_membership_tenant",
                table: "user_group_membership",
                column: "tenant_id",
                principalTable: "tenant",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_user_group_membership_user_account",
                table: "user_group_membership",
                columns: new[] { "tenant_id", "user_account_id" },
                principalTable: "user_account",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_access_event_tenant",
                table: "access_event");

            migrationBuilder.DropForeignKey(
                name: "fk_access_event_user_account",
                table: "access_event");

            migrationBuilder.DropForeignKey(
                name: "fk_application_host_pool",
                table: "application");

            migrationBuilder.DropForeignKey(
                name: "fk_application_tenant",
                table: "application");

            migrationBuilder.DropForeignKey(
                name: "fk_application_permission_tenant",
                table: "application_permission");

            migrationBuilder.DropForeignKey(
                name: "fk_permission_application",
                table: "application_permission");

            migrationBuilder.DropForeignKey(
                name: "fk_permission_granted_by",
                table: "application_permission");

            migrationBuilder.DropForeignKey(
                name: "fk_permission_group",
                table: "application_permission");

            migrationBuilder.DropForeignKey(
                name: "fk_permission_revoked_by",
                table: "application_permission");

            migrationBuilder.DropForeignKey(
                name: "fk_group_tenant",
                table: "group");

            migrationBuilder.DropForeignKey(
                name: "fk_host_pool_tenant",
                table: "host_pool");

            migrationBuilder.DropForeignKey(
                name: "fk_launch_application",
                table: "launch");

            migrationBuilder.DropForeignKey(
                name: "fk_launch_session",
                table: "launch");

            migrationBuilder.DropForeignKey(
                name: "fk_launch_tenant",
                table: "launch");

            migrationBuilder.DropForeignKey(
                name: "fk_launch_user_account",
                table: "launch");

            migrationBuilder.DropForeignKey(
                name: "fk_purge_run_tenant",
                table: "purge_run");

            migrationBuilder.DropForeignKey(
                name: "fk_redirection_policy_application",
                table: "redirection_policy");

            migrationBuilder.DropForeignKey(
                name: "fk_redirection_policy_tenant",
                table: "redirection_policy");

            migrationBuilder.DropForeignKey(
                name: "fk_retention_policy_tenant",
                table: "retention_policy");

            migrationBuilder.DropForeignKey(
                name: "fk_session_session_host",
                table: "session");

            migrationBuilder.DropForeignKey(
                name: "fk_session_tenant",
                table: "session");

            migrationBuilder.DropForeignKey(
                name: "fk_session_user_account",
                table: "session");

            migrationBuilder.DropForeignKey(
                name: "fk_session_host_host_pool",
                table: "session_host");

            migrationBuilder.DropForeignKey(
                name: "fk_session_host_tenant",
                table: "session_host");

            migrationBuilder.DropForeignKey(
                name: "fk_user_account_tenant",
                table: "user_account");

            migrationBuilder.DropForeignKey(
                name: "fk_user_group_membership_group",
                table: "user_group_membership");

            migrationBuilder.DropForeignKey(
                name: "fk_user_group_membership_tenant",
                table: "user_group_membership");

            migrationBuilder.DropForeignKey(
                name: "fk_user_group_membership_user_account",
                table: "user_group_membership");

            migrationBuilder.DropIndex(
                name: "IX_user_group_membership_tenant_id_group_id",
                table: "user_group_membership");

            migrationBuilder.DropUniqueConstraint(
                name: "uq_user_account_tenant",
                table: "user_account");

            migrationBuilder.DropUniqueConstraint(
                name: "uq_session_host_tenant",
                table: "session_host");

            migrationBuilder.DropIndex(
                name: "IX_session_host_tenant_id_host_pool_id",
                table: "session_host");

            migrationBuilder.DropUniqueConstraint(
                name: "uq_session_tenant",
                table: "session");

            migrationBuilder.DropIndex(
                name: "IX_session_tenant_id_user_account_id",
                table: "session");

            migrationBuilder.DropIndex(
                name: "IX_redirection_policy_tenant_id_application_id",
                table: "redirection_policy");

            migrationBuilder.DropIndex(
                name: "IX_purge_run_tenant_id",
                table: "purge_run");

            migrationBuilder.DropIndex(
                name: "IX_launch_tenant_id_application_id",
                table: "launch");

            migrationBuilder.DropIndex(
                name: "IX_launch_tenant_id_session_id",
                table: "launch");

            migrationBuilder.DropIndex(
                name: "IX_launch_tenant_id_user_account_id",
                table: "launch");

            migrationBuilder.DropUniqueConstraint(
                name: "uq_host_pool_tenant",
                table: "host_pool");

            migrationBuilder.DropUniqueConstraint(
                name: "uq_group_tenant",
                table: "group");

            migrationBuilder.DropIndex(
                name: "IX_application_permission_tenant_id_granted_by",
                table: "application_permission");

            migrationBuilder.DropIndex(
                name: "IX_application_permission_tenant_id_group_id",
                table: "application_permission");

            migrationBuilder.DropIndex(
                name: "IX_application_permission_tenant_id_revoked_by",
                table: "application_permission");

            migrationBuilder.DropUniqueConstraint(
                name: "uq_application_tenant",
                table: "application");

            migrationBuilder.DropIndex(
                name: "IX_application_tenant_id_host_pool_id",
                table: "application");

            migrationBuilder.DropIndex(
                name: "IX_access_event_tenant_id_user_account_id",
                table: "access_event");
        }
    }
}
