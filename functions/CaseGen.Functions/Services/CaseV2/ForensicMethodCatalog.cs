using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Text.Json.Serialization;
using CaseGen.Functions.Models.CaseV2;

namespace CaseGen.Functions.Services.CaseV2;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ForensicInputObjectType
{
    Document,
    DigitalFile,
    DeviceImage,
    SessionRecord,
    AccountRecord,
    FinancialRecord,
    AccessRecord,
    CommunicationRecord,
    LocationTrack,
    TextCorpus,
    HandwritingSample,
    Image
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ForensicObservationProperty
{
    FileMetadata,
    DeviceCorrelation,
    SessionTimeline,
    TimestampCorrelation,
    AccountOwnership,
    TransactionCorrelation,
    AccessCorrelation,
    DocumentAuthenticity,
    HandwritingSimilarity,
    LinguisticSimilarity,
    VisibleImageFeature,
    SpatialMeasurement
}

public sealed record ForensicMethodLocalization(
    string Name,
    string Description,
    IReadOnlyDictionary<string, string> Labels,
    IReadOnlyDictionary<string, string> Limitations);

public sealed record ForensicMethodDefinition(
    string Id,
    ImmutableHashSet<ForensicInputObjectType> AcceptedInputObjectTypes,
    ImmutableHashSet<string> AcceptedAssetTypes,
    ImmutableHashSet<ForensicObservationProperty> ProducibleProperties,
    ImmutableArray<string> LimitationKeys,
    bool RequiresReferenceSample,
    bool RequiresChainOfCustody,
    ImmutableHashSet<string> ValidResultLayouts,
    ImmutableHashSet<DerivationRule> AllowedDerivationRules,
    IReadOnlyDictionary<string, ForensicMethodLocalization> Localizations)
{
    public ForensicMethodLocalization Localize(string? language) =>
        Localizations[ForensicMethodCatalog.NormalizeLanguage(language)];
}

public static class ForensicMethodCatalog
{
    private static readonly Lazy<ImmutableArray<ForensicMethodDefinition>> Catalog = new(() =>
    [
        Method(
            "DigitalForensics",
            [ForensicInputObjectType.DigitalFile, ForensicInputObjectType.DeviceImage, ForensicInputObjectType.SessionRecord],
            ["digital"],
            [ForensicObservationProperty.FileMetadata, ForensicObservationProperty.DeviceCorrelation, ForensicObservationProperty.SessionTimeline],
            ["operator_not_proven", "deleted_data_may_be_incomplete"],
            false,
            true,
            [DerivationRule.DeviceAssignment, DerivationRule.SessionOrigin, DerivationRule.TimelineCorrelation]),
        Method(
            "MetadataAnalysis",
            [ForensicInputObjectType.Document, ForensicInputObjectType.DigitalFile, ForensicInputObjectType.Image],
            ["digital", "document", "pdf", "photo"],
            [ForensicObservationProperty.FileMetadata, ForensicObservationProperty.TimestampCorrelation, ForensicObservationProperty.DeviceCorrelation],
            ["operator_not_proven", "metadata_can_be_modified"],
            false,
            true,
            [DerivationRule.DeviceAssignment, DerivationRule.SessionOrigin, DerivationRule.TimelineCorrelation]),
        Method(
            "TimelineCorrelation",
            [ForensicInputObjectType.SessionRecord, ForensicInputObjectType.AccessRecord, ForensicInputObjectType.CommunicationRecord, ForensicInputObjectType.LocationTrack, ForensicInputObjectType.FinancialRecord],
            ["digital", "document", "pdf"],
            [ForensicObservationProperty.SessionTimeline, ForensicObservationProperty.TimestampCorrelation, ForensicObservationProperty.AccessCorrelation, ForensicObservationProperty.TransactionCorrelation],
            ["clock_sources_may_drift", "correlation_not_identity"],
            false,
            false,
            [DerivationRule.TimelineCorrelation, DerivationRule.AccessOpportunity, DerivationRule.AlibiContradiction]),
        Method(
            "AccountAttribution",
            [ForensicInputObjectType.AccountRecord, ForensicInputObjectType.FinancialRecord, ForensicInputObjectType.AccessRecord],
            ["digital", "document", "pdf"],
            [ForensicObservationProperty.AccountOwnership, ForensicObservationProperty.TransactionCorrelation, ForensicObservationProperty.AccessCorrelation],
            ["ownership_not_intent", "shared_access_possible"],
            false,
            true,
            [DerivationRule.AccountOwnership, DerivationRule.AccessOpportunity, DerivationRule.BenefitAttribution]),
        Method(
            "DocumentExamination",
            [ForensicInputObjectType.Document, ForensicInputObjectType.DigitalFile],
            ["document", "pdf", "digital"],
            [ForensicObservationProperty.DocumentAuthenticity, ForensicObservationProperty.FileMetadata],
            ["authenticity_not_authorship", "copy_limits_examination"],
            false,
            true,
            [DerivationRule.IdentityMatch, DerivationRule.TimelineCorrelation]),
        Method(
            "HandwritingComparison",
            [ForensicInputObjectType.Document, ForensicInputObjectType.HandwritingSample],
            ["document", "pdf"],
            [ForensicObservationProperty.HandwritingSimilarity],
            ["similarity_not_absolute_identity", "sample_quality_limits_confidence"],
            true,
            true,
            [DerivationRule.IdentityMatch]),
        Method(
            "LinguisticAnalysis",
            [ForensicInputObjectType.Document, ForensicInputObjectType.TextCorpus, ForensicInputObjectType.CommunicationRecord],
            ["document", "pdf", "digital"],
            [ForensicObservationProperty.LinguisticSimilarity],
            ["style_not_unique_identity", "editing_translation_affect_style"],
            true,
            false,
            [DerivationRule.IdentityMatch]),
        Method(
            "ImageAnalysis",
            [ForensicInputObjectType.Image],
            ["photo"],
            [ForensicObservationProperty.VisibleImageFeature, ForensicObservationProperty.TimestampCorrelation],
            ["unreadable_details_cannot_be_recovered", "image_context_may_be_incomplete"],
            false,
            true,
            [DerivationRule.TimelineCorrelation, DerivationRule.AccessOpportunity]),
        Method(
            "Photogrammetry",
            [ForensicInputObjectType.Image],
            ["photo"],
            [ForensicObservationProperty.SpatialMeasurement, ForensicObservationProperty.VisibleImageFeature],
            ["scale_and_perspective_limit_accuracy", "occluded_features_remain_unknown"],
            true,
            true,
            [DerivationRule.AccessOpportunity, DerivationRule.TimelineCorrelation])
    ]);

    private static readonly Lazy<FrozenDictionary<string, ForensicMethodDefinition>> MethodsById =
        new(() => All.ToFrozenDictionary(method => method.Id, StringComparer.Ordinal));

    public static ImmutableArray<ForensicMethodDefinition> All => Catalog.Value;

    public static ForensicMethodDefinition? Find(string? id) =>
        id is not null && MethodsById.Value.TryGetValue(id, out var method) ? method : null;

    public static ForensicMethodDefinition Get(string id) =>
        Find(id) ?? throw new KeyNotFoundException($"Unknown forensic method '{id}'.");

    public static ForensicsAnalysisType CreateAnalysisType(string methodId, int durationMinutes)
    {
        var method = Get(methodId);
        return new ForensicsAnalysisType
        {
            Type = method.Id,
            DurationMinutes = Math.Clamp(durationMinutes, 1, 1440),
            AvailableFor = method.AcceptedAssetTypes.Order(StringComparer.Ordinal).ToList()
        };
    }

    public static IReadOnlyList<ForensicsAnalysisType> MaterializeAnalysisTypes(
        IEnumerable<ForensicsAnalysisType> proposed,
        IEnumerable<ForensicOutcomeStub> outcomes)
    {
        var proposedById = proposed
            .GroupBy(type => type.Type, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        return outcomes
            .Select(outcome => outcome.AnalysisType)
            .Distinct(StringComparer.Ordinal)
            .Select(methodId => Find(methodId) is null
                ? proposedById.GetValueOrDefault(methodId)
                : CreateAnalysisType(methodId, proposedById.GetValueOrDefault(methodId)?.DurationMinutes ?? 60))
            .Where(type => type is not null)
            .Select(type => type!)
            .OrderBy(type => type.Type, StringComparer.Ordinal)
            .ToList();
    }

    public static string NormalizeLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return "en-US";
        if (language.StartsWith("pt", StringComparison.OrdinalIgnoreCase))
            return "pt-BR";
        if (language.StartsWith("es", StringComparison.OrdinalIgnoreCase))
            return "es-ES";
        if (language.StartsWith("fr", StringComparison.OrdinalIgnoreCase))
            return "fr-FR";
        return "en-US";
    }

    private static ForensicMethodDefinition Method(
        string id,
        IEnumerable<ForensicInputObjectType> objectTypes,
        IEnumerable<string> assetTypes,
        IEnumerable<ForensicObservationProperty> properties,
        IEnumerable<string> limitationKeys,
        bool referenceSample,
        bool chainOfCustody,
        IEnumerable<DerivationRule> allowedRules)
    {
        var keys = limitationKeys.ToImmutableArray();
        return new ForensicMethodDefinition(
            id,
            objectTypes.ToImmutableHashSet(),
            assetTypes.ToImmutableHashSet(StringComparer.OrdinalIgnoreCase),
            properties.ToImmutableHashSet(),
            keys,
            referenceSample,
            chainOfCustody,
            ["ForensicReport"],
            allowedRules.ToImmutableHashSet(),
            BuildLocalizations(id, keys));
    }

    private static IReadOnlyDictionary<string, ForensicMethodLocalization> BuildLocalizations(
        string id,
        IReadOnlyCollection<string> limitationKeys)
    {
        var methodText = MethodText[id];
        return new[] { "en-US", "pt-BR", "es-ES", "fr-FR" }
            .ToFrozenDictionary(
                language => language,
                language => new ForensicMethodLocalization(
                    methodText[language].Name,
                    methodText[language].Description,
                    Labels[language],
                    limitationKeys.ToFrozenDictionary(
                        key => key,
                        key => LimitationText[key][language],
                        StringComparer.Ordinal)),
                StringComparer.Ordinal);
    }

    private static readonly FrozenDictionary<string, IReadOnlyDictionary<string, (string Name, string Description)>> MethodText =
        new Dictionary<string, IReadOnlyDictionary<string, (string, string)>>(StringComparer.Ordinal)
        {
            ["DigitalForensics"] = Localized(
                ("Digital forensics", "Examines preserved digital files, device images, and session artifacts."),
                ("Perícia digital", "Examina arquivos digitais preservados, imagens de dispositivos e artefatos de sessão."),
                ("Informática forense", "Examina archivos digitales preservados, imágenes de dispositivos y artefactos de sesión."),
                ("Informatique légale", "Examine les fichiers numériques préservés, les images d’appareils et les artefacts de session.")),
            ["MetadataAnalysis"] = Localized(
                ("Metadata analysis", "Examines recorded metadata without inferring an unrecorded human operator."),
                ("Análise de metadados", "Examina metadados registrados sem inferir um operador humano não registrado."),
                ("Análisis de metadatos", "Examina metadatos registrados sin inferir un operador humano no registrado."),
                ("Analyse des métadonnées", "Examine les métadonnées enregistrées sans déduire un opérateur humain non consigné.")),
            ["TimelineCorrelation"] = Localized(
                ("Timeline correlation", "Compares timestamps from bounded records and exposes clock uncertainty."),
                ("Correlação temporal", "Compara horários de registros delimitados e explicita a incerteza dos relógios."),
                ("Correlación temporal", "Compara marcas de tiempo de registros delimitados y expone la incertidumbre de los relojes."),
                ("Corrélation chronologique", "Compare les horodatages de sources délimitées et expose l’incertitude des horloges.")),
            ["AccountAttribution"] = Localized(
                ("Account attribution", "Determines recorded account ownership or control without proving intent."),
                ("Atribuição de conta", "Determina a titularidade ou o controle registrado da conta sem provar intenção."),
                ("Atribución de cuenta", "Determina la titularidad o el control registrado de la cuenta sin probar intención."),
                ("Attribution de compte", "Détermine la propriété ou le contrôle enregistré du compte sans prouver l’intention.")),
            ["DocumentExamination"] = Localized(
                ("Document examination", "Examines document structure, production features, and authenticity indicators."),
                ("Exame documental", "Examina a estrutura, as características de produção e os indicadores de autenticidade do documento."),
                ("Examen documental", "Examina la estructura, las características de producción y los indicadores de autenticidad del documento."),
                ("Examen de document", "Examine la structure, les caractéristiques de production et les indices d’authenticité du document.")),
            ["HandwritingComparison"] = Localized(
                ("Handwriting comparison", "Compares questioned writing with an identified reference sample."),
                ("Comparação de escrita", "Compara a escrita questionada com uma amostra de referência identificada."),
                ("Comparación de escritura", "Compara la escritura cuestionada con una muestra de referencia identificada."),
                ("Comparaison d’écriture", "Compare l’écriture contestée à un échantillon de référence identifié.")),
            ["LinguisticAnalysis"] = Localized(
                ("Linguistic analysis", "Compares bounded language patterns with a documented reference corpus."),
                ("Análise linguística", "Compara padrões linguísticos delimitados com um corpus de referência documentado."),
                ("Análisis lingüístico", "Compara patrones lingüísticos delimitados con un corpus de referencia documentado."),
                ("Analyse linguistique", "Compare des schémas linguistiques délimités à un corpus de référence documenté.")),
            ["ImageAnalysis"] = Localized(
                ("Image analysis", "Examines visible image features without inventing unreadable detail."),
                ("Análise de imagem", "Examina características visíveis da imagem sem inventar detalhes ilegíveis."),
                ("Análisis de imagen", "Examina características visibles de la imagen sin inventar detalles ilegibles."),
                ("Analyse d’image", "Examine les éléments visibles de l’image sans inventer de détails illisibles.")),
            ["Photogrammetry"] = Localized(
                ("Photogrammetry", "Estimates spatial relationships from images with explicit scale and perspective limits."),
                ("Fotogrametria", "Estima relações espaciais em imagens com limites explícitos de escala e perspectiva."),
                ("Fotogrametría", "Estima relaciones espaciales en imágenes con límites explícitos de escala y perspectiva."),
                ("Photogrammétrie", "Estime les relations spatiales dans les images avec des limites explicites d’échelle et de perspective."))
        }.ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly FrozenDictionary<string, IReadOnlyDictionary<string, string>> Labels =
        new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal)
        {
            ["en-US"] = LabelSet("Items Submitted", "Methodology", "Findings", "Conclusions", "Limitations", "Reference Sample", "Chain of Custody"),
            ["pt-BR"] = LabelSet("Itens submetidos", "Metodologia", "Constatações", "Conclusões", "Limitações", "Amostra de referência", "Cadeia de custódia"),
            ["es-ES"] = LabelSet("Elementos presentados", "Metodología", "Hallazgos", "Conclusiones", "Limitaciones", "Muestra de referencia", "Cadena de custodia"),
            ["fr-FR"] = LabelSet("Éléments soumis", "Méthodologie", "Constatations", "Conclusions", "Limites", "Échantillon de référence", "Chaîne de conservation")
        }.ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly FrozenDictionary<string, IReadOnlyDictionary<string, string>> LimitationText =
        new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal)
        {
            ["operator_not_proven"] = Text("The artifact may correlate with a device or account but does not prove who operated it.", "O artefato pode se correlacionar com um dispositivo ou uma conta, mas não prova quem o operou.", "El artefacto puede correlacionarse con un dispositivo o una cuenta, pero no prueba quién lo operó.", "L’artefact peut être corrélé à un appareil ou à un compte sans prouver qui l’a utilisé."),
            ["deleted_data_may_be_incomplete"] = Text("Recovered or deleted data may be incomplete.", "Dados recuperados ou excluídos podem estar incompletos.", "Los datos recuperados o eliminados pueden estar incompletos.", "Les données récupérées ou supprimées peuvent être incomplètes."),
            ["metadata_can_be_modified"] = Text("Metadata can be changed by software, copying, or system processes.", "Metadados podem ser alterados por software, cópia ou processos do sistema.", "Los metadatos pueden cambiar por software, copia o procesos del sistema.", "Les métadonnées peuvent être modifiées par un logiciel, une copie ou des processus système."),
            ["clock_sources_may_drift"] = Text("Independent clock sources may drift or use different time zones.", "Fontes de relógio independentes podem divergir ou usar fusos horários diferentes.", "Las fuentes de reloj independientes pueden desviarse o usar zonas horarias distintas.", "Des horloges indépendantes peuvent dériver ou utiliser des fuseaux horaires différents."),
            ["correlation_not_identity"] = Text("Temporal correlation does not by itself establish identity.", "A correlação temporal, isoladamente, não estabelece identidade.", "La correlación temporal por sí sola no establece identidad.", "La corrélation temporelle ne suffit pas à établir une identité."),
            ["ownership_not_intent"] = Text("Recorded ownership does not establish intent or personal use.", "A titularidade registrada não estabelece intenção nem uso pessoal.", "La titularidad registrada no establece intención ni uso personal.", "La propriété enregistrée n’établit ni l’intention ni l’usage personnel."),
            ["shared_access_possible"] = Text("Delegated, shared, or compromised access remains possible.", "Acesso delegado, compartilhado ou comprometido continua possível.", "Sigue siendo posible un acceso delegado, compartido o comprometido.", "Un accès délégué, partagé ou compromis reste possible."),
            ["authenticity_not_authorship"] = Text("Document authenticity does not by itself establish authorship.", "A autenticidade do documento, isoladamente, não estabelece autoria.", "La autenticidad del documento por sí sola no establece autoría.", "L’authenticité du document ne suffit pas à établir son auteur."),
            ["copy_limits_examination"] = Text("A copy may omit physical features available in the original.", "Uma cópia pode omitir características físicas presentes no original.", "Una copia puede omitir características físicas del original.", "Une copie peut omettre des caractéristiques physiques présentes sur l’original."),
            ["similarity_not_absolute_identity"] = Text("Observed similarity supports comparison but is not absolute identification.", "A semelhança observada apoia a comparação, mas não é uma identificação absoluta.", "La similitud observada respalda la comparación, pero no es una identificación absoluta.", "La similitude observée étaye la comparaison sans constituer une identification absolue."),
            ["sample_quality_limits_confidence"] = Text("Reference sample quality and quantity limit confidence.", "A qualidade e a quantidade da amostra de referência limitam a confiança.", "La calidad y cantidad de la muestra de referencia limitan la confianza.", "La qualité et la quantité de l’échantillon de référence limitent le niveau de confiance."),
            ["style_not_unique_identity"] = Text("Writing style is not a unique biometric identifier.", "O estilo de escrita não é um identificador biométrico único.", "El estilo de escritura no es un identificador biométrico único.", "Le style rédactionnel n’est pas un identifiant biométrique unique."),
            ["editing_translation_affect_style"] = Text("Editing, translation, and collaboration can alter linguistic style.", "Edição, tradução e colaboração podem alterar o estilo linguístico.", "La edición, traducción y colaboración pueden alterar el estilo lingüístico.", "La révision, la traduction et la collaboration peuvent modifier le style linguistique."),
            ["unreadable_details_cannot_be_recovered"] = Text("Unreadable or occluded details cannot be invented or reliably recovered.", "Detalhes ilegíveis ou ocultos não podem ser inventados nem recuperados com confiabilidade.", "Los detalles ilegibles u ocultos no pueden inventarse ni recuperarse de forma fiable.", "Les détails illisibles ou masqués ne peuvent être inventés ni récupérés de façon fiable."),
            ["image_context_may_be_incomplete"] = Text("The image may omit events outside the frame or recording interval.", "A imagem pode omitir eventos fora do enquadramento ou do intervalo de gravação.", "La imagen puede omitir hechos fuera del encuadre o del intervalo de grabación.", "L’image peut omettre des événements hors champ ou hors de l’intervalle d’enregistrement."),
            ["scale_and_perspective_limit_accuracy"] = Text("Scale, lens, and perspective limit measurement accuracy.", "Escala, lente e perspectiva limitam a precisão das medições.", "La escala, lente y perspectiva limitan la precisión de las mediciones.", "L’échelle, l’objectif et la perspective limitent la précision des mesures."),
            ["occluded_features_remain_unknown"] = Text("Occluded spatial features remain unknown.", "Características espaciais ocultas permanecem desconhecidas.", "Las características espaciales ocultas permanecen desconocidas.", "Les caractéristiques spatiales masquées restent inconnues.")
        }.ToFrozenDictionary(StringComparer.Ordinal);

    private static IReadOnlyDictionary<string, (string Name, string Description)> Localized(
        (string Name, string Description) en,
        (string Name, string Description) pt,
        (string Name, string Description) es,
        (string Name, string Description) fr) =>
        new Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            ["en-US"] = en,
            ["pt-BR"] = pt,
            ["es-ES"] = es,
            ["fr-FR"] = fr
        }.ToFrozenDictionary(StringComparer.Ordinal);

    private static IReadOnlyDictionary<string, string> LabelSet(
        string items,
        string methodology,
        string findings,
        string conclusions,
        string limitations,
        string reference,
        string custody) =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["itemsSubmitted"] = items,
            ["methodology"] = methodology,
            ["findings"] = findings,
            ["conclusions"] = conclusions,
            ["limitations"] = limitations,
            ["referenceSample"] = reference,
            ["chainOfCustody"] = custody
        }.ToFrozenDictionary(StringComparer.Ordinal);

    private static IReadOnlyDictionary<string, string> Text(string en, string pt, string es, string fr) =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["en-US"] = en,
            ["pt-BR"] = pt,
            ["es-ES"] = es,
            ["fr-FR"] = fr
        }.ToFrozenDictionary(StringComparer.Ordinal);
}
