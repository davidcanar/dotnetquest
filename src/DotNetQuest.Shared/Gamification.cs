using DotNetQuest.Shared.Content;

namespace DotNetQuest.Shared;

/// <summary>Snapshot of a player's lifetime counters, used for achievement checks.</summary>
public record PlayerStats(
    int LessonsCompleted,
    int PerfectLessons,
    int BossesDefeated,
    int QuizzesPassed,
    int MaxCombo,
    int CodeRunsPassed,
    int PlaygroundRuns,
    int DailyChallengesDone,
    int LightningRuns,
    int PracticeRuns,
    int StreakBest,
    int XpTotal,
    int Level,
    int StreakFreezeCount,
    int GemsEarnedTotal);

public record AchievementDef(
    string Id,
    string Icon,
    Loc Title,
    Loc Desc,
    int XpReward,
    int GemReward,
    Func<PlayerStats, bool> IsUnlocked);

/// <summary>The 34 launch achievements. Server evaluates; client renders from the same definitions.</summary>
public static class Achievements
{
    public static readonly AchievementDef[] All =
    [
        new("first-lesson", "📚", new Loc("First Steps", "Primeros pasos"),
            new Loc("Complete your first lesson", "Completa tu primera lección"), 10, 5,
            s => s.LessonsCompleted >= 1),
        new("lessons-5", "📗", new Loc("Bookworm", "Ratón de biblioteca"),
            new Loc("Complete 5 lessons", "Completa 5 lecciones"), 15, 5,
            s => s.LessonsCompleted >= 5),
        new("lessons-10", "📘", new Loc("Getting Serious", "Poniéndome serio"),
            new Loc("Complete 10 lessons", "Completa 10 lecciones"), 20, 10,
            s => s.LessonsCompleted >= 10),
        new("lessons-25", "📙", new Loc("Marathoner", "Maratonista"),
            new Loc("Complete 25 lessons", "Completa 25 lecciones"), 30, 15,
            s => s.LessonsCompleted >= 25),
        new("lessons-50", "📕", new Loc("Half Century", "Medio siglo"),
            new Loc("Complete 50 lessons", "Completa 50 lecciones"), 50, 25,
            s => s.LessonsCompleted >= 50),
        new("perfect-1", "💎", new Loc("Flawless", "Impecable"),
            new Loc("Finish a lesson with zero mistakes", "Termina una lección sin errores"), 15, 5,
            s => s.PerfectLessons >= 1),
        new("perfect-10", "🔮", new Loc("Perfectionist", "Perfeccionista"),
            new Loc("Get 3 stars in 10 lessons", "Consigue 3 estrellas en 10 lecciones"), 30, 10,
            s => s.PerfectLessons >= 10),
        new("perfect-25", "✨", new Loc("Untouchable", "Intocable"),
            new Loc("Get 3 stars in 25 lessons", "Consigue 3 estrellas en 25 lecciones"), 60, 20,
            s => s.PerfectLessons >= 25),
        new("combo-5", "🔥", new Loc("On Fire", "En racha"),
            new Loc("Get a 5-answer combo", "Logra un combo de 5 respuestas"), 10, 5,
            s => s.MaxCombo >= 5),
        new("combo-10", "🌋", new Loc("Unstoppable", "Imparable"),
            new Loc("Get a 10-answer combo", "Logra un combo de 10 respuestas"), 25, 10,
            s => s.MaxCombo >= 10),
        new("boss-1", "⚔️", new Loc("Boss Slayer", "Matajefes"),
            new Loc("Defeat your first boss battle", "Vence tu primera batalla de jefe"), 20, 10,
            s => s.BossesDefeated >= 1),
        new("boss-5", "🛡️", new Loc("Guardian Hunter", "Cazaguardianes"),
            new Loc("Defeat 5 boss battles", "Vence 5 batallas de jefe"), 40, 20,
            s => s.BossesDefeated >= 5),
        new("quiz-5", "🧠", new Loc("Quiz Kid", "Cerebrito"),
            new Loc("Pass 5 unit quizzes", "Supera 5 exámenes de unidad"), 20, 10,
            s => s.QuizzesPassed >= 5),
        new("streak-3", "🔥", new Loc("Warming Up", "Calentando motores"),
            new Loc("Reach a 3-day streak", "Alcanza una racha de 3 días"), 10, 10,
            s => s.StreakBest >= 3),
        new("streak-7", "📅", new Loc("One Week Wonder", "Maravilla semanal"),
            new Loc("Reach a 7-day streak", "Alcanza una racha de 7 días"), 30, 25,
            s => s.StreakBest >= 7),
        new("streak-14", "🗓️", new Loc("Fortnight Force", "Fuerza quincenal"),
            new Loc("Reach a 14-day streak", "Alcanza una racha de 14 días"), 50, 40,
            s => s.StreakBest >= 14),
        new("streak-30", "🏆", new Loc("Monthly Machine", "Máquina mensual"),
            new Loc("Reach a 30-day streak", "Alcanza una racha de 30 días"), 100, 80,
            s => s.StreakBest >= 30),
        new("xp-100", "⚡", new Loc("Charged Up", "Cargado"),
            new Loc("Earn 100 XP", "Gana 100 PX"), 10, 5,
            s => s.XpTotal >= 100),
        new("xp-500", "🔋", new Loc("Power Plant", "Central eléctrica"),
            new Loc("Earn 500 XP", "Gana 500 PX"), 25, 15,
            s => s.XpTotal >= 500),
        new("xp-2000", "🏭", new Loc("XP Factory", "Fábrica de PX"),
            new Loc("Earn 2000 XP", "Gana 2000 PX"), 50, 30,
            s => s.XpTotal >= 2000),
        new("xp-5000", "🌠", new Loc("Legend", "Leyenda"),
            new Loc("Earn 5000 XP", "Gana 5000 PX"), 100, 60,
            s => s.XpTotal >= 5000),
        new("level-3", "🥉", new Loc("Level 3", "Nivel 3"),
            new Loc("Reach level 3", "Alcanza el nivel 3"), 10, 5,
            s => s.Level >= 3),
        new("level-5", "🥈", new Loc("Level 5", "Nivel 5"),
            new Loc("Reach level 5", "Alcanza el nivel 5"), 20, 10,
            s => s.Level >= 5),
        new("level-10", "🥇", new Loc("Level 10", "Nivel 10"),
            new Loc("Reach level 10", "Alcanza el nivel 10"), 40, 20,
            s => s.Level >= 10),
        new("code-1", "⌨️", new Loc("Hello, Runner!", "Hello, Runner!"),
            new Loc("Pass your first code exercise", "Supera tu primer ejercicio de código"), 10, 5,
            s => s.CodeRunsPassed >= 1),
        new("code-25", "💾", new Loc("Code Crusher", "Trituracódigo"),
            new Loc("Pass 25 code exercises", "Supera 25 ejercicios de código"), 30, 15,
            s => s.CodeRunsPassed >= 25),
        new("code-100", "🖥️", new Loc("Keyboard Warrior", "Guerrero del teclado"),
            new Loc("Pass 100 code exercises", "Supera 100 ejercicios de código"), 60, 30,
            s => s.CodeRunsPassed >= 100),
        new("playground-5", "🧪", new Loc("Mad Scientist", "Científico loco"),
            new Loc("Run code 5 times in the Playground", "Ejecuta código 5 veces en el Laboratorio"), 10, 5,
            s => s.PlaygroundRuns >= 5),
        new("challenge-1", "🎯", new Loc("Daily Dev", "Dev diario"),
            new Loc("Complete Bit's Daily Challenge", "Completa el Desafío diario de Bit"), 10, 5,
            s => s.DailyChallengesDone >= 1),
        new("challenge-7", "📌", new Loc("Challenge Streak", "Racha de desafíos"),
            new Loc("Complete 7 daily challenges", "Completa 7 desafíos diarios"), 40, 20,
            s => s.DailyChallengesDone >= 7),
        new("lightning-1", "⛈️", new Loc("Speed Demon", "Demonio de la velocidad"),
            new Loc("Play a Lightning round", "Juega una ronda de Relámpago"), 10, 5,
            s => s.LightningRuns >= 1),
        new("lightning-10", "🌩️", new Loc("Storm Chaser", "Perseguidor de tormentas"),
            new Loc("Play 10 Lightning rounds", "Juega 10 rondas de Relámpago"), 30, 15,
            s => s.LightningRuns >= 10),
        new("practice-1", "🔁", new Loc("Review Time", "Hora de repasar"),
            new Loc("Finish a practice session", "Termina una sesión de práctica"), 10, 5,
            s => s.PracticeRuns >= 1),
        new("practice-10", "♻️", new Loc("Spaced Out", "Repetición espaciada"),
            new Loc("Finish 10 practice sessions", "Termina 10 sesiones de práctica"), 30, 15,
            s => s.PracticeRuns >= 10),
        new("gems-100", "💚", new Loc("Gem Collector", "Coleccionista de gemas"),
            new Loc("Earn 100 gems in total", "Gana 100 gemas en total"), 20, 0,
            s => s.GemsEarnedTotal >= 100)
    ];

    /// <summary>Evaluate all achievements and return the defs newly unlocked (not in alreadyUnlocked).</summary>
    public static List<AchievementDef> Evaluate(PlayerStats stats, HashSet<string> alreadyUnlocked) =>
        All.Where(a => !alreadyUnlocked.Contains(a.Id) && a.IsUnlocked(stats)).ToList();
}

// ---------- Daily quests ----------

public enum QuestMetric
{
    XpToday,
    LessonsToday,
    CorrectToday,
    PerfectToday,
    CodeRunsToday
}

public record QuestDef(
    string Id,
    QuestMetric Metric,
    int Target,
    Loc Title,
    int XpReward,
    int GemReward);

public static class DailyQuests
{
    public static readonly QuestDef[] Pool =
    [
        new("q-xp-30", QuestMetric.XpToday, 30,
            new Loc("Earn 30 XP", "Gana 30 PX"), 10, 5),
        new("q-xp-60", QuestMetric.XpToday, 60,
            new Loc("Earn 60 XP", "Gana 60 PX"), 15, 8),
        new("q-lessons-1", QuestMetric.LessonsToday, 1,
            new Loc("Complete 1 lesson", "Completa 1 lección"), 10, 5),
        new("q-lessons-2", QuestMetric.LessonsToday, 2,
            new Loc("Complete 2 lessons", "Completa 2 lecciones"), 15, 8),
        new("q-correct-10", QuestMetric.CorrectToday, 10,
            new Loc("Answer 10 exercises correctly", "Responde 10 ejercicios bien"), 10, 5),
        new("q-correct-20", QuestMetric.CorrectToday, 20,
            new Loc("Answer 20 exercises correctly", "Responde 20 ejercicios bien"), 20, 10),
        new("q-perfect-1", QuestMetric.PerfectToday, 1,
            new Loc("Get 3 stars in a lesson", "Consigue 3 estrellas en una lección"), 15, 8),
        new("q-code-2", QuestMetric.CodeRunsToday, 2,
            new Loc("Run code 2 times", "Ejecuta código 2 veces"), 15, 8)
    ];

    /// <summary>Deterministically pick 3 different quests for a given player + day.</summary>
    public static List<QuestDef> PickForDay(Guid playerId, DateOnly day)
    {
        var seed = HashCode.Combine(playerId, day.DayNumber);
        var rng = new Random(seed);
        return Pool.OrderBy(_ => rng.Next()).Take(3).ToList();
    }
}

// ---------- Shop & login bonus ----------

public record ShopItem(string Id, string Icon, int Cost, Loc Name, Loc Desc);

public static class Shop
{
    public const int HeartRefillCost = 100;
    public const int StreakFreezeCost = 200;
    public const int HintCost = 10;
    public const int MaxStreakFreezes = 2;

    public static readonly ShopItem[] Items =
    [
        new("heart-refill", "❤️", HeartRefillCost,
            new Loc("Heart refill", "Relleno de corazones"),
            new Loc("Instantly restore all 5 hearts.", "Recupera al instante tus 5 corazones.")),
        new("streak-freeze", "🧊", StreakFreezeCost,
            new Loc("Streak freeze", "Congelación de racha"),
            new Loc("Protects your streak for one missed day (max 2).", "Protege tu racha un día perdido (máx. 2).")),
        new("hint", "💡", HintCost,
            new Loc("Bit hint", "Pista de Bit"),
            new Loc("One extra hint from Bit after the 3 free ones.", "Una pista extra de Bit tras las 3 gratis."))
    ];

    public static readonly (int Gems, int Xp)[] LoginBonusDays =
    {
        (5, 5), (10, 10), (10, 10), (15, 15), (15, 15), (20, 20), (50, 50)
    };

    public const int DailyChallengeXp = 15;
    public const int DailyChallengeGems = 5;
}
