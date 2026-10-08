using System.Reflection;
using DotNetQuest.App.Services;
using Xunit;

namespace DotNetQuest.Tests;

public class CertificateServiceTests
{
    [Theory]
    [InlineData("en", false)]
    [InlineData("es", false)]
    [InlineData("en", true)]
    public void Generates_valid_pdf(string language, bool grand)
    {
        var pdf = CertificateService.Generate(new CertificateData(
            PlayerName: "Test Pilot",
            WorldTitle: grand ? "DevQuest" : "C# Fundamentals",
            Language: language,
            Stars: 63,
            StarsTotal: 75,
            XpEarned: 1234,
            LessonsTotal: 25,
            CompletedOn: new DateOnly(2026, 10, 6),
            IsGrand: grand));

        Assert.True(pdf.Length > 3_000, $"PDF too small: {pdf.Length} bytes");
        Assert.Equal('%', (char)pdf[0]); // %PDF header
        Assert.Equal('P', (char)pdf[1]);
    }
}
