using CaseGen.Functions.Models.CaseV2;
using CaseGen.Functions.Services;
using CaseGen.Functions.Services.CaseV2;
using CaseGen.Functions.Services.CaseV2.Rendering;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CaseGen.Functions.Tests.CaseV2;

public class RenderedEvidenceTests
{
    private static readonly string[] Layouts =
    [
        "GeneralReport", "PoliceReport", "WitnessStatement", "InterviewTranscript", "AudioTranscript",
        "ForensicReport", "MedicalReport", "EvidenceLog", "Memo", "CustodyForm",
        "NewspaperClipping", "MagazineArticle", "HandwrittenNote", "DiaryEntry", "WorkSchedule",
        "PersonalLetter", "Receipt", "SearchWarrant", "DispatchLog", "CaseMap", "Calendar"
    ];

    public static IEnumerable<object[]> Materials() =>
        Layouts.SelectMany(layout => new[] { "pt-BR", "en-US", "es-ES", "fr-FR" }
            .Select(language => new object[] { layout, language }));

    [Theory]
    [MemberData(nameof(Materials))]
    public async Task ActualPdfPreservesAllAuthoredContentAndAttribution(string layout, string language)
    {
        using var services = Services();
        var renderer = services.GetRequiredService<IAssetRenderingService>();
        var asset = Asset(layout, language);
        renderer.PrepareForReview([asset]);
        var texts = await renderer.ReadPreparedDocumentsAsync([asset]);
        var text = Assert.Single(texts).Value;
        Assert.Contains(RenderedEvidenceText.Normalize(asset.Support[0].Excerpt),
            RenderedEvidenceText.Normalize(text), StringComparison.Ordinal);
        Assert.True(asset.PreparedForReview);
    }

    [Fact]
    public void ChangedAuthoredContentCannotBeApprovedUsingAnOlderPdf()
    {
        using var services = Services();
        var asset = Asset("GeneralReport", "en-US");
        var bytes = services.GetRequiredService<IEvidenceDocumentRenderer>().Render(asset.BodyDoc!);
        asset.BodyDoc!.Sections[0].Text += " This later statement was never rendered.";
        var error = Assert.Throws<TaskContractException>(() => RenderedEvidenceText.ExtractAndValidate(asset, bytes));
        Assert.Contains("omits authored content", error.Message);
    }

    [Fact]
    public void UnsupportedQuotationCannotBeApprovedUsingUnrelatedPdfText()
    {
        using var services = Services();
        var asset = Asset("GeneralReport", "en-US");
        var bytes = services.GetRequiredService<IEvidenceDocumentRenderer>().Render(asset.BodyDoc!);
        asset.Support[0].Excerpt = "The clerk positively identified a different person.";
        var error = Assert.Throws<TaskContractException>(() => RenderedEvidenceText.ExtractAndValidate(asset, bytes));
        Assert.Contains("omits support", error.Message);
    }

    [Fact]
    public void NativeExtractionPreservesRecordTokensAndCompleteAttribution()
    {
        using var services = Services();
        var asset = Asset("GeneralReport", "en-US");
        var bytes = services.GetRequiredService<IEvidenceDocumentRenderer>().Render(asset.BodyDoc!);
        var text = RenderedEvidenceText.Extract(bytes);
        var error = Record.Exception(() => RenderedEvidenceText.ExtractAndValidate(asset, bytes));
        Assert.True(error is null, $"{error?.Message}\nExtracted fixture:\n{text}");
    }

    [Fact]
    public void PublicInvestigationDoesNotExposeUninspectedTextPrivateKeysOrImagePrompts()
    {
        var asset = Asset("GeneralReport", "en-US");
        var draft = new CaseDraft
        {
            Request = new() { Language = "en-US", Difficulty = "Rookie" },
            Metadata = new() { Difficulty = "Rookie", RequiredRank = "Rookie" },
            CulpritId = "suspect.private_key",
            AssetFull =
            [
                asset,
                new() { Id = "asset.visual", Type = "photo", Title = "Photograph", Body = "PRIVATE_IMAGE_SPECIFICATION" }
            ],
            Questions =
            [
                new() { Id = "question.record", Prompt = "What does the record establish?",
                    CorrectOptionId = "option.private_key", Options = [new() { Id = "option.a", Label = "Unconfirmed identity" }] }
            ]
        };
        var simulator = new RuntimePlayerSimulator(draft, new Dictionary<string, string> { [asset.Id] = "READABLE_RECORD" });
        var json = System.Text.Json.JsonSerializer.Serialize(simulator.Projection());
        Assert.DoesNotContain("READABLE_RECORD", json);
        Assert.DoesNotContain("PRIVATE_IMAGE_SPECIFICATION", json);
        Assert.DoesNotContain("suspect.private_key", json);
        Assert.DoesNotContain("option.private_key", json);
        Assert.DoesNotContain("correctOptionId", json, StringComparison.OrdinalIgnoreCase);
        simulator.Apply($"inspect:{asset.Id}");
        Assert.Contains("READABLE_RECORD", System.Text.Json.JsonSerializer.Serialize(simulator.Projection()));
        simulator.Apply("inspect:asset.visual");
        Assert.Null(simulator.ReadableSource("asset.visual"));
    }

    private static ServiceProvider Services()
    {
        var configuration = new ConfigurationBuilder().Build();
        return new ServiceCollection().AddSingleton<IConfiguration>(configuration)
            .AddCaseGenApplicationServices(configuration).BuildServiceProvider();
    }

    private static EvidenceAsset Asset(string layout, string language)
    {
        var (title, subtitle, attribution, label, section) = language switch
        {
            "pt-BR" => ("Registro da ocorrência", "Cópia de arquivo", "Segundo o atendente, Jordan pode ter usado o terminal às 19:45; a identidade não foi confirmada.",
                "Campo", "Relato atribuído"),
            "es-ES" => ("Registro del incidente", "Copia de archivo", "Según el empleado, Jordan pudo usar el terminal a las 19:45; la identidad no se confirmó.",
                "Campo", "Relato atribuido"),
            "fr-FR" => ("Registre de l'incident", "Copie d'archive", "Selon l'employé, Jordan aurait utilisé le terminal à 19:45; son identité reste incertaine.",
                "Champ", "Déclaration attribuée"),
            _ => ("Incident record", "Archive copy", "According to the clerk, Jordan may have used the terminal at 19:45; identity remains unconfirmed.",
                "Field", "Attributed account")
        };
        var document = new EvidenceDocument
        {
            Layout = layout, Language = language, Title = title, Subtitle = subtitle, Classification = string.Empty,
            Header = Enumerable.Range(1, 5).Select(index => new EvidenceKeyValue
            {
                Label = $"{label} {index}", Value = $"RECORD-{index}"
            }).ToList(),
            Sections =
            [
                new() { Kind = "narrative", Heading = section, Text = attribution },
                new() { Kind = "table", Heading = "19:45", Table = new()
                {
                    Columns = [label, "ID"], Rows = [["Terminal", "TERM-7"]], Footnote = "LOG-045"
                } },
                new() { Kind = "keyvalue", Items = [new() { Label = "ID", Value = "REC-7" }] },
                new() { Kind = "transcript", Transcript = [new()
                {
                    Timestamp = "19:45", Speaker = "J. Clerk", Text = attribution
                }] },
                new() { Kind = "code", Code = new() { Content = "terminal=TERM-7; identity=unconfirmed" } }
            ],
            Signature = new() { Name = "J. Clerk", Title = subtitle, Badge = "REC-7", SignedAt = "2026-05-16 09:00" }
        };
        return new EvidenceAsset
        {
            Id = "asset.record", Type = "document", Title = title, BodyDoc = document, CanonicalContent = true,
            Support = [new() { ReferenceId = "observation.record", Excerpt = attribution }]
        };
    }
}
