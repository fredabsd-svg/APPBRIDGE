using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using AppBridge.ControlPlane.Infrastructure.Persistence;

#nullable disable

namespace AppBridge.ControlPlane.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(AppBridgeDbContext))]
    partial class AppBridgeDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.0")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("AppBridge.ControlPlane.Core.Entities.Application", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id")
                        .HasDefaultValueSql("gen_random_uuid()");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamptz")
                        .HasColumnName("created_at");

                    b.Property<DateTime?>("DeletedAt")
                        .HasColumnType("timestamptz")
                        .HasColumnName("deleted_at");

                    b.Property<string>("Description")
                        .HasMaxLength(1000)
                        .HasColumnType("character varying(1000)")
                        .HasColumnName("description");

                    b.Property<string>("DisplayName")
                        .IsRequired()
                        .HasMaxLength(255)
                        .HasColumnType("character varying(255)")
                        .HasColumnName("display_name");

                    b.Property<string>("IconUrl")
                        .HasMaxLength(2048)
                        .HasColumnType("character varying(2048)")
                        .HasColumnName("icon_url");

                    b.Property<string>("Identifier")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)")
                        .HasColumnName("identifier");

                    b.Property<bool>("IsPublished")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("boolean")
                        .HasDefaultValue(false)
                        .HasColumnName("is_published");

                    b.Property<string>("RemoteAppName")
                        .IsRequired()
                        .HasMaxLength(255)
                        .HasColumnType("character varying(255)")
                        .HasColumnName("remote_app_name");

                    b.Property<Guid>("TenantId")
                        .HasColumnType("uuid")
                        .HasColumnName("tenant_id");

                    b.Property<DateTime>("UpdatedAt")
                        .HasColumnType("timestamptz")
                        .HasColumnName("updated_at");

                    b.HasKey("Id")
                        .HasName("PK_applications");

                    b.HasIndex("TenantId", "Identifier")
                        .IsUnique()
                        .HasDatabaseName("IX_applications_tenant_id_identifier");

                    b.ToTable("applications", (string)null);
                });

            modelBuilder.Entity("AppBridge.ControlPlane.Core.Entities.ApplicationUserPermission", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id")
                        .HasDefaultValueSql("gen_random_uuid()");

                    b.Property<Guid>("ApplicationId")
                        .HasColumnType("uuid")
                        .HasColumnName("application_id");

                    b.Property<DateTime>("GrantedAt")
                        .HasColumnType("timestamptz")
                        .HasColumnName("granted_at");

                    b.Property<DateTime?>("ExpiresAt")
                        .HasColumnType("timestamptz")
                        .HasColumnName("expires_at");

                    b.Property<string>("Reason")
                        .HasMaxLength(500)
                        .HasColumnType("character varying(500)")
                        .HasColumnName("reason");

                    b.Property<DateTime?>("RevokedAt")
                        .HasColumnType("timestamptz")
                        .HasColumnName("revoked_at");

                    b.Property<string>("RevocationReason")
                        .HasMaxLength(500)
                        .HasColumnType("character varying(500)")
                        .HasColumnName("revocation_reason");

                    b.Property<Guid>("TenantId")
                        .HasColumnType("uuid")
                        .HasColumnName("tenant_id");

                    b.Property<DateTime>("UpdatedAt")
                        .HasColumnType("timestamptz")
                        .HasColumnName("updated_at");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid")
                        .HasColumnName("user_id");

                    b.HasKey("Id")
                        .HasName("PK_application_user_permissions");

                    b.HasIndex("TenantId", "ApplicationId", "UserId")
                        .IsUnique()
                        .HasDatabaseName("IX_application_user_permissions_tenant_id_application_id_user_id");

                    b.ToTable("application_user_permissions", (string)null);
                });

            modelBuilder.Entity("AppBridge.ControlPlane.Core.Entities.AuditLog", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id")
                        .HasDefaultValueSql("gen_random_uuid()");

                    b.Property<Guid?>("ActorUserId")
                        .HasColumnType("uuid")
                        .HasColumnName("actor_user_id");

                    b.Property<string>("ActorIdentifier")
                        .IsRequired()
                        .HasMaxLength(256)
                        .HasColumnType("character varying(256)")
                        .HasColumnName("actor_identifier");

                    b.Property<string>("Action")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)")
                        .HasColumnName("action");

                    b.Property<string>("Category")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("category");

                    b.Property<string>("ChainHash")
                        .HasMaxLength(256)
                        .HasColumnType("character varying(256)")
                        .HasColumnName("chain_hash");

                    b.Property<string>("Description")
                        .IsRequired()
                        .HasMaxLength(1000)
                        .HasColumnType("character varying(1000)")
                        .HasColumnName("description");

                    b.Property<string>("Details")
                        .HasColumnType("jsonb")
                        .HasColumnName("details");

                    b.Property<string>("FailureReason")
                        .HasMaxLength(500)
                        .HasColumnType("character varying(500)")
                        .HasColumnName("failure_reason");

                    b.Property<DateTime>("LoggedAt")
                        .HasColumnType("timestamptz")
                        .HasColumnName("logged_at");

                    b.Property<DateTime>("OccurredAt")
                        .HasColumnType("timestamptz")
                        .HasColumnName("occurred_at");

                    b.Property<Guid?>("PreviousLogId")
                        .HasColumnType("uuid")
                        .HasColumnName("previous_log_id");

                    b.Property<string>("ResourceId")
                        .IsRequired()
                        .HasMaxLength(36)
                        .HasColumnType("character varying(36)")
                        .HasColumnName("resource_id");

                    b.Property<string>("ResourceType")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)")
                        .HasColumnName("resource_type");

                    b.Property<string>("Result")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("result");

                    b.Property<string>("SourceIp")
                        .HasMaxLength(45)
                        .HasColumnType("character varying(45)")
                        .HasColumnName("source_ip");

                    b.Property<Guid>("TenantId")
                        .HasColumnType("uuid")
                        .HasColumnName("tenant_id");

                    b.Property<string>("UserAgent")
                        .HasMaxLength(500)
                        .HasColumnType("character varying(500)")
                        .HasColumnName("user_agent");

                    b.HasKey("Id")
                        .HasName("PK_audit_logs");

                    b.HasIndex("TenantId", "Category", "OccurredAt")
                        .IsDescending(false, false, true)
                        .HasDatabaseName("IX_audit_logs_tenant_id_category_occurred_at");

                    b.HasIndex("TenantId", "ResourceType", "ResourceId", "OccurredAt")
                        .IsDescending(false, false, false, true)
                        .HasDatabaseName("IX_audit_logs_tenant_id_resource_type_resource_id_occurred_at");

                    b.HasIndex("TenantId", "Result", "OccurredAt")
                        .IsDescending(false, false, true)
                        .HasDatabaseName("IX_audit_logs_tenant_id_result_occurred_at");

                    b.ToTable("audit_logs", (string)null);
                });

            modelBuilder.Entity("AppBridge.ControlPlane.Core.Entities.Session", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id")
                        .HasDefaultValueSql("gen_random_uuid()");

                    b.Property<Guid>("ApplicationId")
                        .HasColumnType("uuid")
                        .HasColumnName("application_id");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamptz")
                        .HasColumnName("created_at");

                    b.Property<DateTime?>("EndedAt")
                        .HasColumnType("timestamptz")
                        .HasColumnName("ended_at");

                    b.Property<string>("RdpFileSignature")
                        .HasMaxLength(2000)
                        .HasColumnType("character varying(2000)")
                        .HasColumnName("rdp_file_signature");

                    b.Property<string>("SessionHostId")
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)")
                        .HasColumnName("session_host_id");

                    b.Property<string>("State")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("state");

                    b.Property<DateTime?>("StartedAt")
                        .HasColumnType("timestamptz")
                        .HasColumnName("started_at");

                    b.Property<Guid>("TenantId")
                        .HasColumnType("uuid")
                        .HasColumnName("tenant_id");

                    b.Property<string>("TerminationReason")
                        .HasMaxLength(500)
                        .HasColumnType("character varying(500)")
                        .HasColumnName("termination_reason");

                    b.Property<DateTime>("UpdatedAt")
                        .HasColumnType("timestamptz")
                        .HasColumnName("updated_at");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid")
                        .HasColumnName("user_id");

                    b.HasKey("Id")
                        .HasName("PK_sessions");

                    b.HasIndex("TenantId", "State", "CreatedAt")
                        .IsDescending(false, false, true)
                        .HasDatabaseName("IX_sessions_tenant_id_state_created_at");

                    b.ToTable("sessions", (string)null);
                });

            modelBuilder.Entity("AppBridge.ControlPlane.Core.Entities.Tenant", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id")
                        .HasDefaultValueSql("gen_random_uuid()");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamptz")
                        .HasColumnName("created_at");

                    b.Property<DateTime?>("DeletedAt")
                        .HasColumnType("timestamptz")
                        .HasColumnName("deleted_at");

                    b.Property<string>("Description")
                        .HasMaxLength(1000)
                        .HasColumnType("character varying(1000)")
                        .HasColumnName("description");

                    b.Property<string>("Identifier")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)")
                        .HasColumnName("identifier");

                    b.Property<bool>("IsActive")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("boolean")
                        .HasDefaultValue(true)
                        .HasColumnName("is_active");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(255)
                        .HasColumnType("character varying(255)")
                        .HasColumnName("name");

                    b.Property<DateTime>("UpdatedAt")
                        .HasColumnType("timestamptz")
                        .HasColumnName("updated_at");

                    b.HasKey("Id")
                        .HasName("PK_tenants");

                    b.HasIndex("Identifier")
                        .IsUnique()
                        .HasDatabaseName("IX_tenants_identifier");

                    b.ToTable("tenants", (string)null);
                });

            modelBuilder.Entity("AppBridge.ControlPlane.Core.Entities.User", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid")
                        .HasColumnName("id")
                        .HasDefaultValueSql("gen_random_uuid()");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamptz")
                        .HasColumnName("created_at");

                    b.Property<DateTime?>("DeletedAt")
                        .HasColumnType("timestamptz")
                        .HasColumnName("deleted_at");

                    b.Property<string>("DisplayName")
                        .IsRequired()
                        .HasMaxLength(255)
                        .HasColumnType("character varying(255)")
                        .HasColumnName("display_name");

                    b.Property<string>("Email")
                        .IsRequired()
                        .HasMaxLength(256)
                        .HasColumnType("character varying(256)")
                        .HasColumnName("email");

                    b.Property<string>("Identifier")
                        .IsRequired()
                        .HasMaxLength(256)
                        .HasColumnType("character varying(256)")
                        .HasColumnName("identifier");

                    b.Property<bool>("IsActive")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("boolean")
                        .HasDefaultValue(true)
                        .HasColumnName("is_active");

                    b.Property<DateTime?>("LastLoginAt")
                        .HasColumnType("timestamptz")
                        .HasColumnName("last_login_at");

                    b.Property<string>("PasswordHash")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("password_hash");

                    b.Property<Guid>("TenantId")
                        .HasColumnType("uuid")
                        .HasColumnName("tenant_id");

                    b.Property<DateTime>("UpdatedAt")
                        .HasColumnType("timestamptz")
                        .HasColumnName("updated_at");

                    b.HasKey("Id")
                        .HasName("PK_users");

                    b.HasIndex("TenantId", "Identifier")
                        .IsUnique()
                        .HasDatabaseName("IX_users_tenant_id_identifier");

                    b.ToTable("users", (string)null);
                });
#pragma warning restore 612, 618
        }
    }
}
