using System.Collections.Frozen;
using System.Globalization;
using System.Text.RegularExpressions;

namespace CaseGen.Functions.Services.CaseV2;

public sealed record LocaleProfile(
    string Language,
    IReadOnlySet<string> TimeZoneIds,
    string DatePattern,
    string DateTimePattern,
    IReadOnlyDictionary<string, string> Terminology,
    IReadOnlySet<string> NeutralDomains,
    IReadOnlyDictionary<string, string> ReportHeadings,
    string AgencyNamePattern,
    string PersonNamePattern,
    IReadOnlyDictionary<string, string> ActionLabels);

public static class LocaleProfileCatalog
{
    public static readonly IReadOnlyDictionary<string, LocaleProfile> All =
        new[]
        {
            Profile(
                "en-US",
                ["America/New_York", "America/Chicago", "America/Denver", "America/Los_Angeles", "UTC"],
                "MM/dd/yyyy", "MM/dd/yyyy h:mm tt",
                Terms("Police Department", "detective", "evidence", "case file"),
                Headings("Items Submitted", "Findings", "Conclusions", "Limitations"),
                @"\b(Police|Sheriff|Marshal|Public Safety)\b",
                Actions("Inspect asset", "Compare records", "Request analysis", "Review result", "Accuse suspect", "Answer question")),
            Profile(
                "pt-BR",
                ["America/Sao_Paulo", "America/Manaus", "America/Recife", "America/Fortaleza", "UTC"],
                "dd/MM/yyyy", "dd/MM/yyyy HH:mm",
                Terms("Polícia Civil", "investigador", "evidência", "inquérito"),
                Headings("Itens submetidos", "Constatações", "Conclusões", "Limitações"),
                @"\b(Polícia|Delegacia|Secretaria de Segurança)\b",
                Actions("Inspecionar evidência", "Comparar registros", "Solicitar análise", "Revisar resultado", "Acusar suspeito", "Responder pergunta")),
            Profile(
                "es-ES",
                ["Europe/Madrid", "Atlantic/Canary", "UTC"],
                "dd/MM/yyyy", "dd/MM/yyyy HH:mm",
                Terms("Policía Nacional", "inspector", "prueba", "expediente"),
                Headings("Elementos presentados", "Hallazgos", "Conclusiones", "Limitaciones"),
                @"\b(Policía|Guardia Civil|Comisaría)\b",
                Actions("Inspeccionar prueba", "Comparar registros", "Solicitar análisis", "Revisar resultado", "Acusar sospechoso", "Responder pregunta")),
            Profile(
                "fr-FR",
                ["Europe/Paris", "Europe/Brussels", "Europe/Monaco", "UTC"],
                "dd/MM/yyyy", "dd/MM/yyyy HH:mm",
                Terms("Police nationale", "enquêteur", "élément de preuve", "dossier"),
                Headings("Éléments soumis", "Constatations", "Conclusions", "Limites"),
                @"\b(Police|Gendarmerie|Commissariat)\b",
                Actions("Examiner la preuve", "Comparer les dossiers", "Demander une analyse", "Examiner le résultat", "Accuser un suspect", "Répondre à la question"))
        }.ToFrozenDictionary(profile => profile.Language, StringComparer.Ordinal);

    public static LocaleProfile Get(string? language) =>
        All[ForensicMethodCatalog.NormalizeLanguage(language)];

    public static string ActionLabel(string? language, PlayerActionKind action) =>
        Get(language).ActionLabels[action.ToString()];

    public static StageValidationReport Validate(CaseDraft draft)
    {
        var report = new StageValidationReport();
        var profile = Get(draft.Request.Language);
        var locale = draft.Blueprint.Locale;
        if (!profile.TimeZoneIds.Contains(locale.TimeZoneId))
            report.Errors.Add($"locale node 'locale.timeZoneId' value '{locale.TimeZoneId}' is not valid for {profile.Language}");
        if (!string.IsNullOrWhiteSpace(locale.UtcOffset)
            && !Regex.IsMatch(locale.UtcOffset, @"^[+-](0\d|1[0-4]):[0-5]\d$", RegexOptions.CultureInvariant))
            report.Errors.Add($"locale node 'locale.utcOffset' value '{locale.UtcOffset}' is invalid");
        if (!string.IsNullOrWhiteSpace(locale.PoliceAgency)
            && !Regex.IsMatch(locale.PoliceAgency, profile.AgencyNamePattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            report.Errors.Add($"locale node 'locale.policeAgency' value '{locale.PoliceAgency}' does not match {profile.Language} agency naming");
        if (!string.IsNullOrWhiteSpace(locale.InvestigatorName)
            && !Regex.IsMatch(locale.InvestigatorName, profile.PersonNamePattern, RegexOptions.CultureInvariant))
            report.Errors.Add($"locale node 'locale.investigatorName' value '{locale.InvestigatorName}' has an invalid naming pattern");
        if (!string.IsNullOrWhiteSpace(locale.InvestigatorEmail))
        {
            var domain = locale.InvestigatorEmail.Split('@').LastOrDefault() ?? string.Empty;
            if (domain.EndsWith(".example", StringComparison.OrdinalIgnoreCase)
                || domain.Equals("example.com", StringComparison.OrdinalIgnoreCase))
                report.Errors.Add($"locale node 'locale.investigatorEmail' uses a non-neutral placeholder domain '{domain}'");
        }

        ValidateTimestamp("metadata.incidentDate", draft.Metadata.IncidentDate, report);
        ValidateTimestamp("metadata.openedAt", draft.Metadata.OpenedAt, report);
        return report;
    }

    public static string FormatDateTime(DateTimeOffset value, string? language)
    {
        var profile = Get(language);
        return value.ToString(profile.DateTimePattern, CultureInfo.GetCultureInfo(profile.Language));
    }

    private static LocaleProfile Profile(
        string language,
        IEnumerable<string> timeZones,
        string datePattern,
        string dateTimePattern,
        IReadOnlyDictionary<string, string> terminology,
        IReadOnlyDictionary<string, string> headings,
        string agencyPattern,
        IReadOnlyDictionary<string, string> actions) =>
        new(
            language,
            timeZones.ToFrozenSet(StringComparer.Ordinal),
            datePattern,
            dateTimePattern,
            terminology,
            new[] { "casezero.local", "example.invalid", "localhost" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
            headings,
            agencyPattern,
            @"^[\p{L}][\p{L}\p{M} .'\-]{1,79}$",
            actions);

    private static IReadOnlyDictionary<string, string> Terms(
        string agency,
        string investigator,
        string evidence,
        string caseFile) =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["agency"] = agency,
            ["investigator"] = investigator,
            ["evidence"] = evidence,
            ["caseFile"] = caseFile
        }.ToFrozenDictionary(StringComparer.Ordinal);

    private static IReadOnlyDictionary<string, string> Headings(
        string items,
        string findings,
        string conclusions,
        string limitations) =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["itemsSubmitted"] = items,
            ["findings"] = findings,
            ["conclusions"] = conclusions,
            ["limitations"] = limitations
        }.ToFrozenDictionary(StringComparer.Ordinal);

    private static IReadOnlyDictionary<string, string> Actions(
        string inspect,
        string compare,
        string request,
        string review,
        string accuse,
        string answer) =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [PlayerActionKind.InspectAsset.ToString()] = inspect,
            [PlayerActionKind.CompareRecords.ToString()] = compare,
            [PlayerActionKind.RequestAnalysis.ToString()] = request,
            [PlayerActionKind.ReviewResult.ToString()] = review,
            [PlayerActionKind.AccuseSuspect.ToString()] = accuse,
            [PlayerActionKind.AnswerQuestion.ToString()] = answer
        }.ToFrozenDictionary(StringComparer.Ordinal);

    private static void ValidateTimestamp(string nodeId, string value, StageValidationReport report)
    {
        if (!string.IsNullOrWhiteSpace(value) && !DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _))
            report.Errors.Add($"locale node '{nodeId}' timestamp '{value}' is invalid");
    }
}

public static class PlayerMessageCatalog
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Messages =
        new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal)
        {
            ["en-US"] = Set(
                "Action accepted.", "Unknown player action.", "Source '{0}' is not available.",
                "Action '{0}' is not available.", "Action '{0}' prerequisites are not satisfied.",
                "Analysis '{0}' is not available.", "Suspect '{0}' is not available.",
                "Observation '{0}' was not inspected and cannot be cited.",
                "An accusation must cite exact evidence observations.",
                "Question or option '{0}' is unavailable."),
            ["pt-BR"] = Set(
                "Ação aceita.", "Ação do jogador desconhecida.", "A fonte '{0}' não está disponível.",
                "A ação '{0}' não está disponível.", "Os pré-requisitos da ação '{0}' não foram atendidos.",
                "A análise '{0}' não está disponível.", "O suspeito '{0}' não está disponível.",
                "A observação '{0}' não foi inspecionada e não pode ser citada.",
                "Uma acusação deve citar observações exatas das evidências.",
                "A pergunta ou opção '{0}' não está disponível."),
            ["es-ES"] = Set(
                "Acción aceptada.", "Acción del jugador desconocida.", "La fuente '{0}' no está disponible.",
                "La acción '{0}' no está disponible.", "No se cumplen los requisitos de la acción '{0}'.",
                "El análisis '{0}' no está disponible.", "El sospechoso '{0}' no está disponible.",
                "La observación '{0}' no fue inspeccionada y no puede citarse.",
                "Una acusación debe citar observaciones exactas de las pruebas.",
                "La pregunta u opción '{0}' no está disponible."),
            ["fr-FR"] = Set(
                "Action acceptée.", "Action du joueur inconnue.", "La source « {0} » n’est pas disponible.",
                "L’action « {0} » n’est pas disponible.", "Les prérequis de l’action « {0} » ne sont pas satisfaits.",
                "L’analyse « {0} » n’est pas disponible.", "Le suspect « {0} » n’est pas disponible.",
                "L’observation « {0} » n’a pas été examinée et ne peut pas être citée.",
                "Une accusation doit citer des observations précises.",
                "La question ou l’option « {0} » n’est pas disponible.")
        }.ToFrozenDictionary(StringComparer.Ordinal);

    public static string Get(string? language, string key, params object?[] args) =>
        string.Format(
            CultureInfo.GetCultureInfo(ForensicMethodCatalog.NormalizeLanguage(language)),
            Messages[ForensicMethodCatalog.NormalizeLanguage(language)][key],
            args);

    private static IReadOnlyDictionary<string, string> Set(
        string accepted,
        string unknown,
        string sourceUnavailable,
        string actionUnavailable,
        string prerequisite,
        string analysis,
        string suspect,
        string citation,
        string citationRequired,
        string answer) =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["accepted"] = accepted,
            ["unknownAction"] = unknown,
            ["sourceUnavailable"] = sourceUnavailable,
            ["actionUnavailable"] = actionUnavailable,
            ["prerequisiteMissing"] = prerequisite,
            ["analysisUnavailable"] = analysis,
            ["suspectUnavailable"] = suspect,
            ["citationUnavailable"] = citation,
            ["citationRequired"] = citationRequired,
            ["answerUnavailable"] = answer
        }.ToFrozenDictionary(StringComparer.Ordinal);
}
