using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace User0332.Backup.Server.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Chunks",
                columns: table => new
                {
                    IdentityHash = table.Column<uint>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FilePath = table.Column<string>(type: "TEXT", nullable: false),
                    RefCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Chunks", x => x.IdentityHash);
                });

            migrationBuilder.CreateTable(
                name: "Snapshots",
                columns: table => new
                {
                    SnapshotId = table.Column<string>(type: "TEXT", nullable: false),
                    ClientId = table.Column<string>(type: "TEXT", nullable: false),
                    JobId = table.Column<string>(type: "TEXT", nullable: false),
                    EncryptedManifest = table.Column<byte[]>(type: "BLOB", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Snapshots", x => x.SnapshotId);
                });

            migrationBuilder.CreateTable(
                name: "ChunkRecordSnapshotRecord",
                columns: table => new
                {
                    ChunksIdentityHash = table.Column<uint>(type: "INTEGER", nullable: false),
                    SnapshotsSnapshotId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChunkRecordSnapshotRecord", x => new { x.ChunksIdentityHash, x.SnapshotsSnapshotId });
                    table.ForeignKey(
                        name: "FK_ChunkRecordSnapshotRecord_Chunks_ChunksIdentityHash",
                        column: x => x.ChunksIdentityHash,
                        principalTable: "Chunks",
                        principalColumn: "IdentityHash",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChunkRecordSnapshotRecord_Snapshots_SnapshotsSnapshotId",
                        column: x => x.SnapshotsSnapshotId,
                        principalTable: "Snapshots",
                        principalColumn: "SnapshotId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChunkRecordSnapshotRecord_SnapshotsSnapshotId",
                table: "ChunkRecordSnapshotRecord",
                column: "SnapshotsSnapshotId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChunkRecordSnapshotRecord");

            migrationBuilder.DropTable(
                name: "Chunks");

            migrationBuilder.DropTable(
                name: "Snapshots");
        }
    }
}
