using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotNetQuest.App.Migrations
{
    /// <inheritdoc />
    public partial class Phase2Gamification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BossesDefeated",
                table: "Players",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CodeRunsPassed",
                table: "Players",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DailyChallengesDone",
                table: "Players",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DailyGoalXp",
                table: "Players",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "GemsEarnedTotal",
                table: "Players",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Hearts",
                table: "Players",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "HeartsRegenAtUtc",
                table: "Players",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "LastLoginBonusDateUtc",
                table: "Players",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LessonsCompleted",
                table: "Players",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LightningRuns",
                table: "Players",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LoginBonusDay",
                table: "Players",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxCombo",
                table: "Players",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PerfectLessons",
                table: "Players",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PlaygroundRuns",
                table: "Players",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PracticeRuns",
                table: "Players",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "QuizzesPassed",
                table: "Players",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StreakFreezeCount",
                table: "Players",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "ChallengeDone",
                table: "DailyActivity",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "CodeRuns",
                table: "DailyActivity",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "GoalReached",
                table: "DailyActivity",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "LightningRuns",
                table: "DailyActivity",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PerfectLessons",
                table: "DailyActivity",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PracticeRuns",
                table: "DailyActivity",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "DailyQuests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PlayerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DateUtc = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    QuestId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Progress = table.Column<int>(type: "INTEGER", nullable: false),
                    Claimed = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyQuests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DailyQuests_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailyQuests_PlayerId_DateUtc_QuestId",
                table: "DailyQuests",
                columns: new[] { "PlayerId", "DateUtc", "QuestId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyQuests");

            migrationBuilder.DropColumn(
                name: "BossesDefeated",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "CodeRunsPassed",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "DailyChallengesDone",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "DailyGoalXp",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "GemsEarnedTotal",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "Hearts",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "HeartsRegenAtUtc",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "LastLoginBonusDateUtc",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "LessonsCompleted",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "LightningRuns",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "LoginBonusDay",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "MaxCombo",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "PerfectLessons",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "PlaygroundRuns",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "PracticeRuns",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "QuizzesPassed",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "StreakFreezeCount",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "ChallengeDone",
                table: "DailyActivity");

            migrationBuilder.DropColumn(
                name: "CodeRuns",
                table: "DailyActivity");

            migrationBuilder.DropColumn(
                name: "GoalReached",
                table: "DailyActivity");

            migrationBuilder.DropColumn(
                name: "LightningRuns",
                table: "DailyActivity");

            migrationBuilder.DropColumn(
                name: "PerfectLessons",
                table: "DailyActivity");

            migrationBuilder.DropColumn(
                name: "PracticeRuns",
                table: "DailyActivity");
        }
    }
}
