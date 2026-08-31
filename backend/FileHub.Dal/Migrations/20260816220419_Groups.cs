using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dal.Migrations
{
    /// <inheritdoc />
    public partial class Groups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Groups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false, collation: "NOCASE")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Groups", x => x.Id);
                });

            // Hand-written rather than AddColumn + AddForeignKey, because SQLite cannot add a
            // constraint to an existing table: EF emulates it by rebuilding Shares, and a rebuild
            // is bracketed with "PRAGMA foreign_keys = 0", which cannot run inside the migration's
            // transaction. That left the whole migration outside one, so an interrupted upgrade
            // would need unpicking by hand. SQLite does allow a REFERENCES clause on ADD COLUMN as
            // long as the default is NULL, which this column's is, so the same schema arrives in
            // one statement that the transaction covers. The constraint is unnamed, as every
            // SQLite foreign key written inline is.
            migrationBuilder.Sql(
                """
                ALTER TABLE "Shares"
                ADD COLUMN "AudienceGroupId" TEXT NULL
                REFERENCES "Groups" ("Id") ON DELETE CASCADE;
                """);

            migrationBuilder.CreateTable(
                name: "BasePathGroupAccesses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    BasePathId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GroupId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BasePathGroupAccesses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BasePathGroupAccesses_BasePaths_BasePathId",
                        column: x => x.BasePathId,
                        principalTable: "BasePaths",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BasePathGroupAccesses_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GroupMemberships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    GroupId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupMemberships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupMemberships_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GroupMemberships_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Shares_AudienceGroupId",
                table: "Shares",
                column: "AudienceGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_BasePathGroupAccesses_BasePathId_GroupId",
                table: "BasePathGroupAccesses",
                columns: new[] { "BasePathId", "GroupId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BasePathGroupAccesses_GroupId",
                table: "BasePathGroupAccesses",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupMemberships_GroupId_UserId",
                table: "GroupMemberships",
                columns: new[] { "GroupId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupMemberships_UserId",
                table: "GroupMemberships",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Groups_Name",
                table: "Groups",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Shares_Groups_AudienceGroupId",
                table: "Shares");

            migrationBuilder.DropTable(
                name: "BasePathGroupAccesses");

            migrationBuilder.DropTable(
                name: "GroupMemberships");

            migrationBuilder.DropTable(
                name: "Groups");

            migrationBuilder.DropIndex(
                name: "IX_Shares_AudienceGroupId",
                table: "Shares");

            migrationBuilder.DropColumn(
                name: "AudienceGroupId",
                table: "Shares");
        }
    }
}
