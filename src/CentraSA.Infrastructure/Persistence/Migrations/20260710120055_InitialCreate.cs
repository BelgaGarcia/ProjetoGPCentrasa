using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CentraSA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActivityHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EntityType = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    EntityId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ActionType = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Summary = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ChangesJson = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityHistories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UserName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: true),
                    SecurityStamp = table.Column<string>(type: "TEXT", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "TEXT", nullable: true),
                    PhoneNumber = table.Column<string>(type: "TEXT", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Scope = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ColorToken = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                    table.CheckConstraint("CK_Category_SortOrder", "SortOrder >= 0");
                });

            migrationBuilder.CreateTable(
                name: "DailyMeetings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MeetingDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FinishedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    GeneralNotes = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ArchivedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Version = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyMeetings", x => x.Id);
                    table.CheckConstraint("CK_DailyMeeting_Version", "Version >= 1");
                });

            migrationBuilder.CreateTable(
                name: "StatusDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Scope = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LifecycleState = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    ColorToken = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatusDefinitions", x => x.Id);
                    table.CheckConstraint("CK_StatusDefinition_SortOrder", "SortOrder >= 0");
                });

            migrationBuilder.CreateTable(
                name: "TeamAreas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    ColorToken = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamAreas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ClaimType = table.Column<string>(type: "TEXT", nullable: true),
                    ClaimValue = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "TEXT", nullable: false),
                    ProviderKey = table.Column<string>(type: "TEXT", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "TEXT", nullable: true),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LoginProvider = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "People",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    TeamAreaId = table.Column<Guid>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_People", x => x.Id);
                    table.ForeignKey(
                        name: "FK_People_TeamAreas_TeamAreaId",
                        column: x => x.TeamAreaId,
                        principalTable: "TeamAreas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PendingTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    ResponsiblePersonId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ResponsibleAreaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StatusDefinitionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Priority = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    DueDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    CategoryId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Origin = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    PresentationOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ArchivedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Version = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingTasks", x => x.Id);
                    table.CheckConstraint("CK_PendingTask_PresentationOrder", "PresentationOrder >= 0");
                    table.CheckConstraint("CK_PendingTask_Version", "Version >= 1");
                    table.ForeignKey(
                        name: "FK_PendingTasks_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PendingTasks_People_ResponsiblePersonId",
                        column: x => x.ResponsiblePersonId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PendingTasks_StatusDefinitions_StatusDefinitionId",
                        column: x => x.StatusDefinitionId,
                        principalTable: "StatusDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PendingTasks_TeamAreas_ResponsibleAreaId",
                        column: x => x.ResponsibleAreaId,
                        principalTable: "TeamAreas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Smuds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    NormalizedCode = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    ResponsiblePersonId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ResponsibleAreaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StatusDefinitionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Priority = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    OpenedOn = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    DueDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    RequiredAction = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ArchivedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Version = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Smuds", x => x.Id);
                    table.CheckConstraint("CK_Smud_Version", "Version >= 1");
                    table.ForeignKey(
                        name: "FK_Smuds_People_ResponsiblePersonId",
                        column: x => x.ResponsiblePersonId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Smuds_StatusDefinitions_StatusDefinitionId",
                        column: x => x.StatusDefinitionId,
                        principalTable: "StatusDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Smuds_TeamAreas_ResponsibleAreaId",
                        column: x => x.ResponsibleAreaId,
                        principalTable: "TeamAreas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupportTickets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TicketNumber = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    NormalizedNumber = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    ResponsiblePersonId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ResponsibleAreaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StatusDefinitionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CategoryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Priority = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    OpenedOn = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    PendingAction = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ArchivedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Version = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportTickets", x => x.Id);
                    table.CheckConstraint("CK_SupportTicket_Version", "Version >= 1");
                    table.ForeignKey(
                        name: "FK_SupportTickets_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupportTickets_People_ResponsiblePersonId",
                        column: x => x.ResponsiblePersonId,
                        principalTable: "People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupportTickets_StatusDefinitions_StatusDefinitionId",
                        column: x => x.StatusDefinitionId,
                        principalTable: "StatusDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupportTickets_TeamAreas_ResponsibleAreaId",
                        column: x => x.ResponsibleAreaId,
                        principalTable: "TeamAreas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DailyMeetingItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DailyMeetingId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PendingTaskId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SmudId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SupportTicketId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Section = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    PresentationNotes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    WasPresented = table.Column<bool>(type: "INTEGER", nullable: false),
                    SnapshotTitle = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SnapshotStatus = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    SnapshotDueDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    SnapshotResponsible = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyMeetingItems", x => x.Id);
                    table.CheckConstraint("CK_DailyMeetingItem_ExactlyOneSource", "(PendingTaskId IS NOT NULL AND SmudId IS NULL AND SupportTicketId IS NULL) OR (PendingTaskId IS NULL AND SmudId IS NOT NULL AND SupportTicketId IS NULL) OR (PendingTaskId IS NULL AND SmudId IS NULL AND SupportTicketId IS NOT NULL)");
                    table.CheckConstraint("CK_DailyMeetingItem_SortOrder", "SortOrder >= 0");
                    table.ForeignKey(
                        name: "FK_DailyMeetingItems_DailyMeetings_DailyMeetingId",
                        column: x => x.DailyMeetingId,
                        principalTable: "DailyMeetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DailyMeetingItems_PendingTasks_PendingTaskId",
                        column: x => x.PendingTaskId,
                        principalTable: "PendingTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DailyMeetingItems_Smuds_SmudId",
                        column: x => x.SmudId,
                        principalTable: "Smuds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DailyMeetingItems_SupportTickets_SupportTicketId",
                        column: x => x.SupportTicketId,
                        principalTable: "SupportTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkItemReferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PendingTaskId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SmudId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SupportTicketId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItemReferences", x => x.Id);
                    table.CheckConstraint("CK_WorkItemReference_ExactlyOneTarget", "(SmudId IS NOT NULL AND SupportTicketId IS NULL) OR (SmudId IS NULL AND SupportTicketId IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_WorkItemReferences_PendingTasks_PendingTaskId",
                        column: x => x.PendingTaskId,
                        principalTable: "PendingTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkItemReferences_Smuds_SmudId",
                        column: x => x.SmudId,
                        principalTable: "Smuds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkItemReferences_SupportTickets_SupportTicketId",
                        column: x => x.SupportTicketId,
                        principalTable: "SupportTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityHistories_EntityType_EntityId_OccurredAtUtc",
                table: "ActivityHistories",
                columns: new[] { "EntityType", "EntityId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityHistories_OccurredAtUtc",
                table: "ActivityHistories",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Scope_Code",
                table: "Categories",
                columns: new[] { "Scope", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Scope_IsActive_SortOrder",
                table: "Categories",
                columns: new[] { "Scope", "IsActive", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_DailyMeetingItems_DailyMeetingId_PendingTaskId",
                table: "DailyMeetingItems",
                columns: new[] { "DailyMeetingId", "PendingTaskId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DailyMeetingItems_DailyMeetingId_Section_SortOrder",
                table: "DailyMeetingItems",
                columns: new[] { "DailyMeetingId", "Section", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_DailyMeetingItems_DailyMeetingId_SmudId",
                table: "DailyMeetingItems",
                columns: new[] { "DailyMeetingId", "SmudId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DailyMeetingItems_DailyMeetingId_SupportTicketId",
                table: "DailyMeetingItems",
                columns: new[] { "DailyMeetingId", "SupportTicketId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DailyMeetingItems_PendingTaskId",
                table: "DailyMeetingItems",
                column: "PendingTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyMeetingItems_SmudId",
                table: "DailyMeetingItems",
                column: "SmudId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyMeetingItems_SupportTicketId",
                table: "DailyMeetingItems",
                column: "SupportTicketId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyMeetings_ArchivedAtUtc",
                table: "DailyMeetings",
                column: "ArchivedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_DailyMeetings_MeetingDate_Status",
                table: "DailyMeetings",
                columns: new[] { "MeetingDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PendingTasks_ArchivedAtUtc_StatusDefinitionId_DueDate",
                table: "PendingTasks",
                columns: new[] { "ArchivedAtUtc", "StatusDefinitionId", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PendingTasks_CategoryId",
                table: "PendingTasks",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_PendingTasks_ResponsibleAreaId_PresentationOrder",
                table: "PendingTasks",
                columns: new[] { "ResponsibleAreaId", "PresentationOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_PendingTasks_ResponsiblePersonId",
                table: "PendingTasks",
                column: "ResponsiblePersonId");

            migrationBuilder.CreateIndex(
                name: "IX_PendingTasks_StatusDefinitionId",
                table: "PendingTasks",
                column: "StatusDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_People_IsActive",
                table: "People",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_People_NormalizedName",
                table: "People",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_People_TeamAreaId",
                table: "People",
                column: "TeamAreaId");

            migrationBuilder.CreateIndex(
                name: "IX_Smuds_ArchivedAtUtc_StatusDefinitionId_DueDate",
                table: "Smuds",
                columns: new[] { "ArchivedAtUtc", "StatusDefinitionId", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Smuds_NormalizedCode",
                table: "Smuds",
                column: "NormalizedCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Smuds_ResponsibleAreaId",
                table: "Smuds",
                column: "ResponsibleAreaId");

            migrationBuilder.CreateIndex(
                name: "IX_Smuds_ResponsiblePersonId",
                table: "Smuds",
                column: "ResponsiblePersonId");

            migrationBuilder.CreateIndex(
                name: "IX_Smuds_StatusDefinitionId",
                table: "Smuds",
                column: "StatusDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_StatusDefinitions_Scope_Code",
                table: "StatusDefinitions",
                columns: new[] { "Scope", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StatusDefinitions_Scope_IsActive_SortOrder",
                table: "StatusDefinitions",
                columns: new[] { "Scope", "IsActive", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_ArchivedAtUtc_StatusDefinitionId_DueDate",
                table: "SupportTickets",
                columns: new[] { "ArchivedAtUtc", "StatusDefinitionId", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_CategoryId_ResponsibleAreaId",
                table: "SupportTickets",
                columns: new[] { "CategoryId", "ResponsibleAreaId" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_NormalizedNumber",
                table: "SupportTickets",
                column: "NormalizedNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_ResponsibleAreaId",
                table: "SupportTickets",
                column: "ResponsibleAreaId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_ResponsiblePersonId",
                table: "SupportTickets",
                column: "ResponsiblePersonId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_StatusDefinitionId",
                table: "SupportTickets",
                column: "StatusDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamAreas_IsActive",
                table: "TeamAreas",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_TeamAreas_Kind_NormalizedName",
                table: "TeamAreas",
                columns: new[] { "Kind", "NormalizedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemReferences_PendingTaskId_SmudId",
                table: "WorkItemReferences",
                columns: new[] { "PendingTaskId", "SmudId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemReferences_PendingTaskId_SupportTicketId",
                table: "WorkItemReferences",
                columns: new[] { "PendingTaskId", "SupportTicketId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemReferences_SmudId",
                table: "WorkItemReferences",
                column: "SmudId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemReferences_SupportTicketId",
                table: "WorkItemReferences",
                column: "SupportTicketId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityHistories");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "DailyMeetingItems");

            migrationBuilder.DropTable(
                name: "WorkItemReferences");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "DailyMeetings");

            migrationBuilder.DropTable(
                name: "PendingTasks");

            migrationBuilder.DropTable(
                name: "Smuds");

            migrationBuilder.DropTable(
                name: "SupportTickets");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "People");

            migrationBuilder.DropTable(
                name: "StatusDefinitions");

            migrationBuilder.DropTable(
                name: "TeamAreas");
        }
    }
}
