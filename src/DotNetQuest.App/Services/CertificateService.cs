using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace DotNetQuest.App.Services;

public record CertificateData(
    string PlayerName,
    string WorldTitle,
    string Language,
    int Stars,
    int StarsTotal,
    int XpEarned,
    int LessonsTotal,
    DateOnly CompletedOn,
    bool IsGrand);

/// <summary>
/// Generates playful PDF completion certificates. Bit the robot is assembled
/// from QuestPDF layout primitives (rounded divs), so it renders identically
/// on every machine with zero image assets. A4 landscape: 841.89 x 595.28 pt.
/// </summary>
public static class CertificateService
{
    static CertificateService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        // Emoji (Bit's star, decorations) fall back to system fonts (Windows: Segoe UI Emoji).
        QuestPDF.Settings.UseSystemFonts = true;
    }

    private static string T(string en, string es, string language) =>
        language.StartsWith("es", StringComparison.OrdinalIgnoreCase) ? es : en;

    public static byte[] Generate(CertificateData data)
    {
        var badgeTitle = data.IsGrand
            ? T("GRAND GRADUATION", "GRADUACIÓN FINAL", data.Language)
            : T("WORLD COMPLETED", "MUNDO COMPLETADO", data.Language);
        var mainTitle = T("Certificate of Completion", "Certificado de Finalización", data.Language);
        var awardedTo = T("Proudly awarded to", "Otorgado con orgullo a", data.Language);
        var forCompleting = data.IsGrand
            ? T("for completing every available world of the DevQuest quest map", "por completar todos los mundos disponibles del mapa de DevQuest", data.Language)
            : T("for completing the world", "por completar el mundo", data.Language);
        var lessonsWord = T("lessons", "lecciones", data.Language);
        var xpWord = T("XP earned", "PX ganados", data.Language);
        var dateWord = T("Completed on", "Completado el", data.Language);
        var signature = T("Bit — your AI tutor", "Bit — tu tutor con IA", data.Language);
        var footer = T("Earned by playing DevQuest — learn C# and .NET by playing", "Ganado jugando DevQuest — aprende C# y .NET jugando", data.Language);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(841.89f, 595.28f, Unit.Point); // A4 landscape
                page.Margin(14);
                page.DefaultTextStyle(x => x.FontFamily("Lato").FontSize(13));

                page.Content().Border(3, Unit.Point).BorderColor("#5b5bd6")
                    .Padding(5)
                    .Border(1.5f, Unit.Point).BorderColor("#2dd4bf")
                    .Background("#fdfdff")
                    .Padding(26)
                    .Column(col =>
                    {
                        // Top ribbon.
                        col.Item().Background("#5b5bd6").PaddingVertical(7).PaddingHorizontal(12)
                            .Row(row =>
                            {
                                row.AutoItem().Text("DEVQUEST").FontSize(14).ExtraBold().FontColor(Colors.White);
                                row.RelativeItem();
                                row.AutoItem().Text(badgeTitle).FontSize(12).Bold().FontColor(Colors.White);
                            });

                        col.Spacing(20);

                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text(mainTitle).FontSize(36).ExtraBold().FontColor("#1e2340");
                                c.Spacing(10);
                                c.Item().Text(forCompleting).FontColor("#6b7191").FontSize(14);
                                c.Spacing(3);
                                c.Item().Text(data.WorldTitle).FontSize(25).Bold().FontColor("#5b5bd6");
                                c.Spacing(16);
                                c.Item().Text(awardedTo).FontColor("#6b7191");
                                c.Spacing(3);
                                c.Item().Text(data.PlayerName).FontSize(28).Bold().FontColor("#14b8a6");
                                c.Item().PaddingTop(3).PaddingRight(90).LineHorizontal(2, Unit.Point).LineColor("#14b8a6");
                            });

                            row.ConstantItem(180).Element(DrawBit);
                        });

                        col.Spacing(16);

                        // Stats chips + date.
                        col.Item().Row(row =>
                        {
                            row.AutoItem().Element(e => Chip(e, $"★ {data.Stars}/{data.StarsTotal}", "#f59e0b"));
                            row.ConstantItem(8);
                            row.AutoItem().Element(e => Chip(e, $"⚡ {data.XpEarned:n0} {xpWord}", "#5b5bd6"));
                            row.ConstantItem(8);
                            row.AutoItem().Element(e => Chip(e, $"📚 {data.LessonsTotal} {lessonsWord}", "#2dd4bf"));
                            row.RelativeItem();
                            row.AutoItem().AlignMiddle().Text($"{dateWord}: {data.CompletedOn:yyyy-MM-dd}").FontColor("#6b7191");
                        });

                        col.Item().PaddingTop(12).Row(row =>
                        {
                            row.RelativeItem();
                            row.ConstantItem(190).Column(c =>
                            {
                                c.Item().LineHorizontal(1, Unit.Point).LineColor("#c8cce4");
                                c.Item().AlignCenter().PaddingTop(4).Text(signature).FontColor("#6b7191").FontSize(11);
                            });
                        });

                        col.Item().PaddingTop(10).Text(footer).FontSize(9).FontColor("#9aa0c3");
                    });
            });
        }).GeneratePdf();
    }

    private static void Chip(IContainer container, string text, string color) => container
        .Background(color)
        .PaddingHorizontal(10).PaddingVertical(5)
        .DefaultTextStyle(x => x.FontSize(12).Bold().FontColor(Colors.White))
        .Text(text);

    private static IContainer Circle(IContainer c, float size, string color) => c
        .Width(size).Height(size)
        .Background(color)
        .CornerRadius(size / 2f, Unit.Point);

    /// <summary>Bit the robot (cheering) assembled from rounded divs.</summary>
    private static void DrawBit(IContainer container)
    {
        container.Column(col =>
        {
            col.Spacing(2);

            // Antenna star + stem.
            col.Item().AlignCenter().Text("⭐").FontSize(16);
            col.Item().AlignCenter().Width(6).Height(12).Background("#14b8a6").CornerRadius(3, Unit.Point);

            // Ears + head.
            col.Item().Row(row =>
            {
                row.ConstantItem(8).Height(22).Background("#14b8a6").CornerRadius(4, Unit.Point).AlignMiddle();
                row.AutoItem().Width(72).Column(head =>
                {
                    head.Item().Background("#2dd4bf").CornerRadius(14, Unit.Point).Padding(7)
                        .Background("#0f3d3a").CornerRadius(10, Unit.Point).Height(44).Column(face =>
                        {
                            face.Spacing(4);
                            face.Item().Row(eyes =>
                            {
                                eyes.AutoItem().Element(e => Circle(e, 11, "#f8fafc"));
                                eyes.RelativeItem();
                                eyes.AutoItem().Element(e => Circle(e, 11, "#f8fafc"));
                            });
                            face.Item().AlignCenter().Element(e => Circle(e, 12, "#f8fafc"));
                        });
                });
                row.ConstantItem(8).Height(22).Background("#14b8a6").CornerRadius(4, Unit.Point).AlignMiddle();
            });

            // Neck.
            col.Item().AlignCenter().Width(10).Height(7).Background("#14b8a6");

            // Body with raised arms.
            col.Item().Row(row =>
            {
                row.ConstantItem(28).Height(10).Background("#14b8a6").CornerRadius(6, Unit.Point)
                    .Rotate(-25).OffsetX(4, Unit.Point);
                row.ConstantItem(56).Column(mid =>
                {
                    mid.Item().AlignCenter().Width(10).Height(6).Background("#14b8a6");
                    mid.Item().Background("#2dd4bf").CornerRadius(12, Unit.Point).Height(46).AlignMiddle()
                        .AlignCenter().Element(e => Circle(e, 22, "#0f3d3a"));
                });
                row.ConstantItem(28).Height(10).Background("#14b8a6").CornerRadius(6, Unit.Point)
                    .Rotate(25).OffsetX(-4, Unit.Point);
            });

            // Legs.
            col.Item().Row(row =>
            {
                row.RelativeItem();
                row.ConstantItem(10).Height(12).Background("#14b8a6").CornerRadius(4, Unit.Point);
                row.ConstantItem(8);
                row.ConstantItem(10).Height(12).Background("#14b8a6").CornerRadius(4, Unit.Point);
                row.RelativeItem();
            });

            col.Item().AlignCenter().PaddingTop(6).Text("🤖 ⚡ 🎉").FontSize(13).FontColor("#9aa0c3");
        });
    }
}
