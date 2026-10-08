using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetQuest.App.Migrations
{
    /// <inheritdoc />
    public partial class InitialGameDb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Players",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExternalSubject = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Nickname = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    AvatarId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CertificateName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    XpTotal = table.Column<int>(type: "INTEGER", nullable: false),
                    Gems = table.Column<int>(type: "INTEGER", nullable: false),
                    StreakCurrent = table.Column<int>(type: "INTEGER", nullable: false),
                    StreakBest = table.Column<int>(type: "INTEGER", nullable: false),
                    LastPlayedUtc = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    SettingsJson = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    OnboardingComplete = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Players", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Achievements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PlayerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AchievementId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    UnlockedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Achievements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Achievements_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Certificates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PlayerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorldId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    IssuedToName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    XpAtIssue = table.Column<int>(type: "INTEGER", nullable: false),
                    IssuedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Certificates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Certificates_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CodeSubmissions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PlayerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExerciseId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Passed = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodeSubmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CodeSubmissions_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DailyActivity",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PlayerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DateUtc = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    XpEarned = table.Column<int>(type: "INTEGER", nullable: false),
                    LessonsCompleted = table.Column<int>(type: "INTEGER", nullable: false),
                    CorrectAnswers = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyActivity", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DailyActivity_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExerciseAttempts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PlayerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorldId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    LessonId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ExerciseId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Correct = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExerciseAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExerciseAttempts_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LessonProgress",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PlayerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorldId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    LessonId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Stars = table.Column<int>(type: "INTEGER", nullable: false),
                    Attempts = table.Column<int>(type: "INTEGER", nullable: false),
                    Mistakes = table.Column<int>(type: "INTEGER", nullable: false),
                    XpEarned = table.Column<int>(type: "INTEGER", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LessonProgress", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LessonProgress_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Achievements_PlayerId_AchievementId",
                table: "Achievements",
                columns: new[] { "PlayerId", "AchievementId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Certificates_PlayerId_WorldId",
                table: "Certificates",
                columns: new[] { "PlayerId", "WorldId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CodeSubmissions_PlayerId",
                table: "CodeSubmissions",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyActivity_PlayerId_DateUtc",
                table: "DailyActivity",
                columns: new[] { "PlayerId", "DateUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExerciseAttempts_PlayerId_CreatedAtUtc",
                table: "ExerciseAttempts",
                columns: new[] { "PlayerId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LessonProgress_PlayerId_WorldId_LessonId",
                table: "LessonProgress",
                columns: new[] { "PlayerId", "WorldId", "LessonId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Players_ExternalSubject",
                table: "Players",
                column: "ExternalSubject",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Players_Nickname",
                table: "Players",
                column: "Nickname",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Achievements");

            migrationBuilder.DropTable(
                name: "Certificates");

            migrationBuilder.DropTable(
                name: "CodeSubmissions");

            migrationBuilder.DropTable(
                name: "DailyActivity");

            migrationBuilder.DropTable(
                name: "ExerciseAttempts");

            migrationBuilder.DropTable(
                name: "LessonProgress");

            migrationBuilder.DropTable(
                name: "Players");
        }
    }
}
