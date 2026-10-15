namespace CaseGen.Functions.Services.CaseV2.Rendering;

/// <summary>
/// Strongly-typed keys for every chrome string that appears in a rendered
/// PDF (page numbering, header labels, classification bands, signature
/// captions, etc.). Layouts use <see cref="DocumentChrome.Get"/> with these
/// keys so we never hardcode user-visible strings inside renderer code.
/// </summary>
public enum ChromeKey
{
    // Generic
    PageOf,                          // "Page {0} of {1}"
    Confidential,                    // CONFIDENTIAL · INTERNAL USE ONLY
    GeneratedAt,                     // "Generated at"
    CaseRef,                         // "Case Ref"
    ExhibitRef,                      // "Exhibit Ref"
    PreparedBy,                      // "Prepared by"
    Date,                            // "Date"
    Time,                            // "Time"
    Page,                            // "Page" (standalone label)

    // Section dividers
    SectionPrefix,                   // "§"

    // PoliceReport
    PoliceLetterhead,                // METROPOLITAN POLICE DEPARTMENT
    CaseFileNo,                      // "Case File No."
    ReportNo,                        // "Report No."
    ReportingOfficer,                // "Reporting Officer"
    Station,                         // "Station"
    IncidentDate,                    // "Incident Date"
    PoliceReportTitle,               // POLICE REPORT
    PoliceSupplementalTitle,         // SUPPLEMENTAL POLICE REPORT
    FirstResponderTitle,             // FIRST RESPONDING OFFICER NARRATIVE

    // Witness Statement
    StatementOf,                     // STATEMENT OF
    TakenOn,                         // "Taken on"
    TakenAt,                         // "at"
    TakenBy,                         // "by"
    OathBlock,                       // I, {NAME}, declare under penalty of perjury that the foregoing is true and correct.
    SignatureWitness,                // "Witness signature"
    SignatureOfficer,                // "Officer signature"

    // Interview Transcript
    InterviewRecord,                 // INTERVIEW RECORD
    InterviewOf,                     // "Interview of"
    ConductedBy,                     // "Conducted by"
    RecordingRef,                    // "Recording Ref"
    Duration,                        // "Duration"

    // Audio Transcript (recording transcribed from an audio file/dispatch tape)
    AudioRecordingTranscript,        // AUDIO RECORDING TRANSCRIPT
    RecordingDevice,                 // "Recording Device"
    TranscribedBy,                   // "Transcribed by"

    // Forensic Report
    ForensicLetterhead,              // FORENSIC LABORATORY · CASE EXAMINATION REPORT
    EvidenceIntegrityVerified,       // EVIDENCE INTEGRITY VERIFIED
    EvidenceIntegrityCompromised,    // EVIDENCE INTEGRITY COMPROMISED
    SubmissionDate,                  // "Submission Date"
    Examiner,                        // "Examiner"
    LabId,                           // "Lab ID"
    ItemsSubmitted,                  // 1. ITEMS SUBMITTED
    Methodology,                     // 2. METHODOLOGY
    Findings,                        // 3. FINDINGS
    Conclusions,                     // 4. CONCLUSIONS
    Limitations,                     // 5. LIMITATIONS
    ChainOfCustody,                  // CHAIN OF CUSTODY

    // Medical Report
    MedicalExaminerOffice,           // OFFICE OF THE MEDICAL EXAMINER
    UrgentCare,                      // URGENT CARE VISIT RECORD
    PatientRef,                      // "Patient Ref"
    DecedentRef,                     // "Decedent Ref"
    History,                         // HISTORY
    ExternalExamination,             // EXTERNAL EXAMINATION
    MedicalFindings,                 // FINDINGS
    CauseOfDeath,                    // CAUSE
    Diagnosis,                       // DIAGNOSIS
    Disposition,                     // DISPOSITION
    AttendingPhysician,              // "Attending Physician"
    MedicalExaminer,                 // "Medical Examiner"

    // Evidence Log
    EvidenceLogTitle,                // EVIDENCE LOG / CHAIN OF CUSTODY
    Item,                            // "Item #"
    Description,                     // "Description"
    CollectedBy,                     // "Collected By"
    Location,                        // "Location"
    Container,                       // "Container"
    Seal,                            // "Seal #"
    Transferred,                     // "Transferred"

    // Memo
    MemoLabel,                       // MEMORANDUM
    To,                              // "TO"
    From,                            // "FROM"
    Re,                              // "RE"
    ActionRequiredBy,                // "Action required by"

    // Custody Form
    Form,                            // "FORM"
    SignatureBlock,                  // "Signatures"
    Signed,                          // "Signed"
    Briefing,                        // CASE BRIEFING
    Objectives,                      // OBJECTIVES
    Suspects,                        // SUSPECTS
    KeyFacts,                        // KEY FACTS
}

/// <summary>
/// Per-language dictionary of every chrome string the PDF renderer writes.
/// All 4 supported languages MUST stay in sync — adding a key to one of the
/// dictionaries WITHOUT adding it to the others is a compile-time error
/// (covered by <see cref="Validate"/> in tests).
/// </summary>
public static class DocumentChrome
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<ChromeKey, string>> _byLang = BuildAll();

    public static string Get(ChromeKey key, string? language)
    {
        var lang = NormalizeLanguage(language);
        if (_byLang.TryGetValue(lang, out var dict) && dict.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v))
            return v;
        // Always fall back to English so we never render a blank label.
        return _byLang["en-US"][key];
    }

    public static string Format(ChromeKey key, string? language, params object[] args)
        => string.Format(Get(key, language), args);

    public static string NormalizeLanguage(string? lang)
    {
        if (string.IsNullOrWhiteSpace(lang)) return "en-US";
        // Accept "en", "en-US", "en_US", "pt-BR", "pt_br" etc.
        var l = lang.Replace('_', '-').Trim();
        if (l.StartsWith("pt", StringComparison.OrdinalIgnoreCase)) return "pt-BR";
        if (l.StartsWith("es", StringComparison.OrdinalIgnoreCase)) return "es-ES";
        if (l.StartsWith("fr", StringComparison.OrdinalIgnoreCase)) return "fr-FR";
        return "en-US";
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<ChromeKey, string>> BuildAll()
    {
        var en = new Dictionary<ChromeKey, string>
        {
            [ChromeKey.PageOf] = "Page {0} of {1}",
            [ChromeKey.Confidential] = "CONFIDENTIAL · INTERNAL USE ONLY",
            [ChromeKey.GeneratedAt] = "Generated at",
            [ChromeKey.CaseRef] = "Case Ref",
            [ChromeKey.ExhibitRef] = "Exhibit Ref",
            [ChromeKey.PreparedBy] = "Prepared by",
            [ChromeKey.Date] = "Date",
            [ChromeKey.Time] = "Time",
            [ChromeKey.Page] = "Page",
            [ChromeKey.SectionPrefix] = "§",
            [ChromeKey.PoliceLetterhead] = "METROPOLITAN POLICE DEPARTMENT",
            [ChromeKey.CaseFileNo] = "Case File No.",
            [ChromeKey.ReportNo] = "Report No.",
            [ChromeKey.ReportingOfficer] = "Reporting Officer",
            [ChromeKey.Station] = "Station",
            [ChromeKey.IncidentDate] = "Incident Date",
            [ChromeKey.PoliceReportTitle] = "POLICE REPORT",
            [ChromeKey.PoliceSupplementalTitle] = "SUPPLEMENTAL POLICE REPORT",
            [ChromeKey.FirstResponderTitle] = "FIRST RESPONDING OFFICER NARRATIVE",
            [ChromeKey.StatementOf] = "STATEMENT OF",
            [ChromeKey.TakenOn] = "Taken on",
            [ChromeKey.TakenAt] = "at",
            [ChromeKey.TakenBy] = "by",
            [ChromeKey.OathBlock] = "I, {0}, declare under penalty of perjury that the foregoing is true and correct.",
            [ChromeKey.SignatureWitness] = "Witness signature",
            [ChromeKey.SignatureOfficer] = "Officer signature",
            [ChromeKey.InterviewRecord] = "INTERVIEW RECORD",
            [ChromeKey.InterviewOf] = "Interview of",
            [ChromeKey.ConductedBy] = "Conducted by",
            [ChromeKey.RecordingRef] = "Recording Ref",
            [ChromeKey.Duration] = "Duration",
            [ChromeKey.AudioRecordingTranscript] = "AUDIO RECORDING TRANSCRIPT",
            [ChromeKey.RecordingDevice] = "Recording Device",
            [ChromeKey.TranscribedBy] = "Transcribed by",
            [ChromeKey.ForensicLetterhead] = "FORENSIC LABORATORY · CASE EXAMINATION REPORT",
            [ChromeKey.EvidenceIntegrityVerified] = "EVIDENCE INTEGRITY VERIFIED",
            [ChromeKey.EvidenceIntegrityCompromised] = "EVIDENCE INTEGRITY COMPROMISED",
            [ChromeKey.SubmissionDate] = "Submission Date",
            [ChromeKey.Examiner] = "Examiner",
            [ChromeKey.LabId] = "Lab ID",
            [ChromeKey.ItemsSubmitted] = "1. Items Submitted",
            [ChromeKey.Methodology] = "2. Methodology",
            [ChromeKey.Findings] = "3. Findings",
            [ChromeKey.Conclusions] = "4. Conclusions",
            [ChromeKey.Limitations] = "5. Limitations",
            [ChromeKey.ChainOfCustody] = "Chain of Custody",
            [ChromeKey.MedicalExaminerOffice] = "OFFICE OF THE MEDICAL EXAMINER",
            [ChromeKey.UrgentCare] = "URGENT CARE VISIT RECORD",
            [ChromeKey.PatientRef] = "Patient Ref",
            [ChromeKey.DecedentRef] = "Decedent Ref",
            [ChromeKey.History] = "History",
            [ChromeKey.ExternalExamination] = "External Examination",
            [ChromeKey.MedicalFindings] = "Findings",
            [ChromeKey.CauseOfDeath] = "Cause",
            [ChromeKey.Diagnosis] = "Diagnosis",
            [ChromeKey.Disposition] = "Disposition",
            [ChromeKey.AttendingPhysician] = "Attending Physician",
            [ChromeKey.MedicalExaminer] = "Medical Examiner",
            [ChromeKey.EvidenceLogTitle] = "EVIDENCE LOG · CHAIN OF CUSTODY",
            [ChromeKey.Item] = "Item #",
            [ChromeKey.Description] = "Description",
            [ChromeKey.CollectedBy] = "Collected By",
            [ChromeKey.Location] = "Location",
            [ChromeKey.Container] = "Container",
            [ChromeKey.Seal] = "Seal #",
            [ChromeKey.Transferred] = "Transferred",
            [ChromeKey.MemoLabel] = "MEMORANDUM",
            [ChromeKey.To] = "TO",
            [ChromeKey.From] = "FROM",
            [ChromeKey.Re] = "RE",
            [ChromeKey.ActionRequiredBy] = "Action required by",
            [ChromeKey.Form] = "FORM",
            [ChromeKey.SignatureBlock] = "Signatures",
            [ChromeKey.Signed] = "Signed",
            [ChromeKey.Briefing] = "CASE BRIEFING",
            [ChromeKey.Objectives] = "Objectives",
            [ChromeKey.Suspects] = "Suspects",
            [ChromeKey.KeyFacts] = "Key Facts",
        };

        var ptBR = new Dictionary<ChromeKey, string>
        {
            [ChromeKey.PageOf] = "Página {0} de {1}",
            [ChromeKey.Confidential] = "CONFIDENCIAL · USO INTERNO",
            [ChromeKey.GeneratedAt] = "Gerado em",
            [ChromeKey.CaseRef] = "Ref. do Caso",
            [ChromeKey.ExhibitRef] = "Ref. da Evidência",
            [ChromeKey.PreparedBy] = "Elaborado por",
            [ChromeKey.Date] = "Data",
            [ChromeKey.Time] = "Hora",
            [ChromeKey.Page] = "Página",
            [ChromeKey.SectionPrefix] = "§",
            [ChromeKey.PoliceLetterhead] = "DEPARTAMENTO METROPOLITANO DE POLÍCIA",
            [ChromeKey.CaseFileNo] = "N.º do Inquérito",
            [ChromeKey.ReportNo] = "N.º do Relatório",
            [ChromeKey.ReportingOfficer] = "Agente Relator",
            [ChromeKey.Station] = "Delegacia",
            [ChromeKey.IncidentDate] = "Data do Fato",
            [ChromeKey.PoliceReportTitle] = "BOLETIM DE OCORRÊNCIA",
            [ChromeKey.PoliceSupplementalTitle] = "BOLETIM DE OCORRÊNCIA SUPLEMENTAR",
            [ChromeKey.FirstResponderTitle] = "NARRATIVA DO PRIMEIRO AGENTE NO LOCAL",
            [ChromeKey.StatementOf] = "DECLARAÇÃO DE",
            [ChromeKey.TakenOn] = "Tomada em",
            [ChromeKey.TakenAt] = "às",
            [ChromeKey.TakenBy] = "por",
            [ChromeKey.OathBlock] = "Eu, {0}, declaro, sob as penas da lei, que o exposto acima é verdadeiro.",
            [ChromeKey.SignatureWitness] = "Assinatura da testemunha",
            [ChromeKey.SignatureOfficer] = "Assinatura do agente",
            [ChromeKey.InterviewRecord] = "REGISTRO DE OITIVA",
            [ChromeKey.InterviewOf] = "Oitiva de",
            [ChromeKey.ConductedBy] = "Conduzida por",
            [ChromeKey.RecordingRef] = "Ref. da Gravação",
            [ChromeKey.Duration] = "Duração",
            [ChromeKey.AudioRecordingTranscript] = "TRANSCRIÇÃO DE GRAVAÇÃO DE ÁUDIO",
            [ChromeKey.RecordingDevice] = "Dispositivo de Gravação",
            [ChromeKey.TranscribedBy] = "Transcrito por",
            [ChromeKey.ForensicLetterhead] = "LABORATÓRIO PERICIAL · LAUDO DE EXAME",
            [ChromeKey.EvidenceIntegrityVerified] = "INTEGRIDADE DA EVIDÊNCIA VERIFICADA",
            [ChromeKey.EvidenceIntegrityCompromised] = "INTEGRIDADE DA EVIDÊNCIA COMPROMETIDA",
            [ChromeKey.SubmissionDate] = "Data de Entrada",
            [ChromeKey.Examiner] = "Perito",
            [ChromeKey.LabId] = "ID do Laboratório",
            [ChromeKey.ItemsSubmitted] = "1. Itens Submetidos",
            [ChromeKey.Methodology] = "2. Metodologia",
            [ChromeKey.Findings] = "3. Achados",
            [ChromeKey.Conclusions] = "4. Conclusões",
            [ChromeKey.Limitations] = "5. Limitações",
            [ChromeKey.ChainOfCustody] = "Cadeia de Custódia",
            [ChromeKey.MedicalExaminerOffice] = "INSTITUTO MÉDICO-LEGAL",
            [ChromeKey.UrgentCare] = "REGISTRO DE PRONTO-ATENDIMENTO",
            [ChromeKey.PatientRef] = "Ref. do Paciente",
            [ChromeKey.DecedentRef] = "Ref. do Falecido",
            [ChromeKey.History] = "Histórico",
            [ChromeKey.ExternalExamination] = "Exame Externo",
            [ChromeKey.MedicalFindings] = "Achados",
            [ChromeKey.CauseOfDeath] = "Causa",
            [ChromeKey.Diagnosis] = "Diagnóstico",
            [ChromeKey.Disposition] = "Destinação",
            [ChromeKey.AttendingPhysician] = "Médico Atendente",
            [ChromeKey.MedicalExaminer] = "Perito Médico-Legista",
            [ChromeKey.EvidenceLogTitle] = "REGISTRO DE EVIDÊNCIAS · CADEIA DE CUSTÓDIA",
            [ChromeKey.Item] = "Item nº",
            [ChromeKey.Description] = "Descrição",
            [ChromeKey.CollectedBy] = "Coletado por",
            [ChromeKey.Location] = "Local",
            [ChromeKey.Container] = "Embalagem",
            [ChromeKey.Seal] = "Lacre nº",
            [ChromeKey.Transferred] = "Transferido",
            [ChromeKey.MemoLabel] = "MEMORANDO",
            [ChromeKey.To] = "PARA",
            [ChromeKey.From] = "DE",
            [ChromeKey.Re] = "REF",
            [ChromeKey.ActionRequiredBy] = "Ação necessária até",
            [ChromeKey.Form] = "FORMULÁRIO",
            [ChromeKey.SignatureBlock] = "Assinaturas",
            [ChromeKey.Signed] = "Assinado",
            [ChromeKey.Briefing] = "BRIEFING DO CASO",
            [ChromeKey.Objectives] = "Objetivos",
            [ChromeKey.Suspects] = "Suspeitos",
            [ChromeKey.KeyFacts] = "Fatos-chave",
        };

        var esES = new Dictionary<ChromeKey, string>
        {
            [ChromeKey.PageOf] = "Página {0} de {1}",
            [ChromeKey.Confidential] = "CONFIDENCIAL · USO INTERNO",
            [ChromeKey.GeneratedAt] = "Generado el",
            [ChromeKey.CaseRef] = "Ref. del caso",
            [ChromeKey.ExhibitRef] = "Ref. de la prueba",
            [ChromeKey.PreparedBy] = "Preparado por",
            [ChromeKey.Date] = "Fecha",
            [ChromeKey.Time] = "Hora",
            [ChromeKey.Page] = "Página",
            [ChromeKey.SectionPrefix] = "§",
            [ChromeKey.PoliceLetterhead] = "DEPARTAMENTO METROPOLITANO DE POLICÍA",
            [ChromeKey.CaseFileNo] = "N.º de expediente",
            [ChromeKey.ReportNo] = "N.º de informe",
            [ChromeKey.ReportingOfficer] = "Agente instructor",
            [ChromeKey.Station] = "Comisaría",
            [ChromeKey.IncidentDate] = "Fecha del hecho",
            [ChromeKey.PoliceReportTitle] = "INFORME POLICIAL",
            [ChromeKey.PoliceSupplementalTitle] = "INFORME POLICIAL SUPLEMENTARIO",
            [ChromeKey.FirstResponderTitle] = "NARRATIVA DEL PRIMER AGENTE EN EL LUGAR",
            [ChromeKey.StatementOf] = "DECLARACIÓN DE",
            [ChromeKey.TakenOn] = "Tomada el",
            [ChromeKey.TakenAt] = "a las",
            [ChromeKey.TakenBy] = "por",
            [ChromeKey.OathBlock] = "Yo, {0}, declaro bajo pena de perjurio que lo anterior es verdadero y correcto.",
            [ChromeKey.SignatureWitness] = "Firma del testigo",
            [ChromeKey.SignatureOfficer] = "Firma del agente",
            [ChromeKey.InterviewRecord] = "ACTA DE ENTREVISTA",
            [ChromeKey.InterviewOf] = "Entrevista a",
            [ChromeKey.ConductedBy] = "Realizada por",
            [ChromeKey.RecordingRef] = "Ref. de grabación",
            [ChromeKey.Duration] = "Duración",
            [ChromeKey.AudioRecordingTranscript] = "TRANSCRIPCIÓN DE GRABACIÓN DE AUDIO",
            [ChromeKey.RecordingDevice] = "Dispositivo de grabación",
            [ChromeKey.TranscribedBy] = "Transcrito por",
            [ChromeKey.ForensicLetterhead] = "LABORATORIO FORENSE · INFORME DE EXAMEN",
            [ChromeKey.EvidenceIntegrityVerified] = "INTEGRIDAD DE LA EVIDENCIA VERIFICADA",
            [ChromeKey.EvidenceIntegrityCompromised] = "INTEGRIDAD DE LA EVIDENCIA COMPROMETIDA",
            [ChromeKey.SubmissionDate] = "Fecha de ingreso",
            [ChromeKey.Examiner] = "Perito",
            [ChromeKey.LabId] = "ID del laboratorio",
            [ChromeKey.ItemsSubmitted] = "1. Elementos remitidos",
            [ChromeKey.Methodology] = "2. Metodología",
            [ChromeKey.Findings] = "3. Hallazgos",
            [ChromeKey.Conclusions] = "4. Conclusiones",
            [ChromeKey.Limitations] = "5. Limitaciones",
            [ChromeKey.ChainOfCustody] = "Cadena de custodia",
            [ChromeKey.MedicalExaminerOffice] = "OFICINA DEL FORENSE",
            [ChromeKey.UrgentCare] = "INFORME DE ATENCIÓN DE URGENCIAS",
            [ChromeKey.PatientRef] = "Ref. del paciente",
            [ChromeKey.DecedentRef] = "Ref. del fallecido",
            [ChromeKey.History] = "Antecedentes",
            [ChromeKey.ExternalExamination] = "Examen externo",
            [ChromeKey.MedicalFindings] = "Hallazgos",
            [ChromeKey.CauseOfDeath] = "Causa",
            [ChromeKey.Diagnosis] = "Diagnóstico",
            [ChromeKey.Disposition] = "Destino",
            [ChromeKey.AttendingPhysician] = "Médico tratante",
            [ChromeKey.MedicalExaminer] = "Médico forense",
            [ChromeKey.EvidenceLogTitle] = "REGISTRO DE EVIDENCIAS · CADENA DE CUSTODIA",
            [ChromeKey.Item] = "Ítem n.º",
            [ChromeKey.Description] = "Descripción",
            [ChromeKey.CollectedBy] = "Recolectado por",
            [ChromeKey.Location] = "Lugar",
            [ChromeKey.Container] = "Envase",
            [ChromeKey.Seal] = "Sello n.º",
            [ChromeKey.Transferred] = "Transferido",
            [ChromeKey.MemoLabel] = "MEMORÁNDUM",
            [ChromeKey.To] = "PARA",
            [ChromeKey.From] = "DE",
            [ChromeKey.Re] = "ASUNTO",
            [ChromeKey.ActionRequiredBy] = "Acción requerida antes del",
            [ChromeKey.Form] = "FORMULARIO",
            [ChromeKey.SignatureBlock] = "Firmas",
            [ChromeKey.Signed] = "Firmado",
            [ChromeKey.Briefing] = "INFORME PRELIMINAR DEL CASO",
            [ChromeKey.Objectives] = "Objetivos",
            [ChromeKey.Suspects] = "Sospechosos",
            [ChromeKey.KeyFacts] = "Hechos clave",
        };

        var frFR = new Dictionary<ChromeKey, string>
        {
            [ChromeKey.PageOf] = "Page {0} sur {1}",
            [ChromeKey.Confidential] = "CONFIDENTIEL · USAGE INTERNE",
            [ChromeKey.GeneratedAt] = "Généré le",
            [ChromeKey.CaseRef] = "Réf. du dossier",
            [ChromeKey.ExhibitRef] = "Réf. de la pièce",
            [ChromeKey.PreparedBy] = "Établi par",
            [ChromeKey.Date] = "Date",
            [ChromeKey.Time] = "Heure",
            [ChromeKey.Page] = "Page",
            [ChromeKey.SectionPrefix] = "§",
            [ChromeKey.PoliceLetterhead] = "PRÉFECTURE DE POLICE MÉTROPOLITAINE",
            [ChromeKey.CaseFileNo] = "N° de dossier",
            [ChromeKey.ReportNo] = "N° de rapport",
            [ChromeKey.ReportingOfficer] = "Agent rédacteur",
            [ChromeKey.Station] = "Poste",
            [ChromeKey.IncidentDate] = "Date des faits",
            [ChromeKey.PoliceReportTitle] = "PROCÈS-VERBAL",
            [ChromeKey.PoliceSupplementalTitle] = "PROCÈS-VERBAL COMPLÉMENTAIRE",
            [ChromeKey.FirstResponderTitle] = "RAPPORT DU PREMIER AGENT SUR LES LIEUX",
            [ChromeKey.StatementOf] = "DÉPOSITION DE",
            [ChromeKey.TakenOn] = "Reçue le",
            [ChromeKey.TakenAt] = "à",
            [ChromeKey.TakenBy] = "par",
            [ChromeKey.OathBlock] = "Je soussigné(e), {0}, déclare sous serment que les présentes sont véridiques et exactes.",
            [ChromeKey.SignatureWitness] = "Signature du témoin",
            [ChromeKey.SignatureOfficer] = "Signature de l'agent",
            [ChromeKey.InterviewRecord] = "PROCÈS-VERBAL D'AUDITION",
            [ChromeKey.InterviewOf] = "Audition de",
            [ChromeKey.ConductedBy] = "Menée par",
            [ChromeKey.RecordingRef] = "Réf. d'enregistrement",
            [ChromeKey.Duration] = "Durée",
            [ChromeKey.AudioRecordingTranscript] = "TRANSCRIPTION D'ENREGISTREMENT AUDIO",
            [ChromeKey.RecordingDevice] = "Appareil d'enregistrement",
            [ChromeKey.TranscribedBy] = "Transcrit par",
            [ChromeKey.ForensicLetterhead] = "LABORATOIRE DE POLICE SCIENTIFIQUE · RAPPORT D'EXAMEN",
            [ChromeKey.EvidenceIntegrityVerified] = "INTÉGRITÉ DES SCELLÉS VÉRIFIÉE",
            [ChromeKey.EvidenceIntegrityCompromised] = "INTÉGRITÉ DES SCELLÉS COMPROMISE",
            [ChromeKey.SubmissionDate] = "Date de dépôt",
            [ChromeKey.Examiner] = "Expert",
            [ChromeKey.LabId] = "ID du laboratoire",
            [ChromeKey.ItemsSubmitted] = "1. Pièces soumises",
            [ChromeKey.Methodology] = "2. Méthodologie",
            [ChromeKey.Findings] = "3. Constatations",
            [ChromeKey.Conclusions] = "4. Conclusions",
            [ChromeKey.Limitations] = "5. Limites",
            [ChromeKey.ChainOfCustody] = "Chaîne de possession",
            [ChromeKey.MedicalExaminerOffice] = "INSTITUT MÉDICO-LÉGAL",
            [ChromeKey.UrgentCare] = "FICHE DE CONSULTATION D'URGENCE",
            [ChromeKey.PatientRef] = "Réf. du patient",
            [ChromeKey.DecedentRef] = "Réf. du défunt",
            [ChromeKey.History] = "Antécédents",
            [ChromeKey.ExternalExamination] = "Examen externe",
            [ChromeKey.MedicalFindings] = "Constatations",
            [ChromeKey.CauseOfDeath] = "Cause",
            [ChromeKey.Diagnosis] = "Diagnostic",
            [ChromeKey.Disposition] = "Suites",
            [ChromeKey.AttendingPhysician] = "Médecin traitant",
            [ChromeKey.MedicalExaminer] = "Médecin légiste",
            [ChromeKey.EvidenceLogTitle] = "REGISTRE DES SCELLÉS · CHAÎNE DE POSSESSION",
            [ChromeKey.Item] = "Pièce n°",
            [ChromeKey.Description] = "Description",
            [ChromeKey.CollectedBy] = "Saisie par",
            [ChromeKey.Location] = "Lieu",
            [ChromeKey.Container] = "Contenant",
            [ChromeKey.Seal] = "Scellé n°",
            [ChromeKey.Transferred] = "Transféré",
            [ChromeKey.MemoLabel] = "NOTE DE SERVICE",
            [ChromeKey.To] = "À",
            [ChromeKey.From] = "DE",
            [ChromeKey.Re] = "OBJET",
            [ChromeKey.ActionRequiredBy] = "Action requise avant le",
            [ChromeKey.Form] = "FORMULAIRE",
            [ChromeKey.SignatureBlock] = "Signatures",
            [ChromeKey.Signed] = "Signé",
            [ChromeKey.Briefing] = "DOSSIER DE BRIEFING",
            [ChromeKey.Objectives] = "Objectifs",
            [ChromeKey.Suspects] = "Suspects",
            [ChromeKey.KeyFacts] = "Faits saillants",
        };

        // Validate completeness at type-init so a missing key crashes fast at startup.
        var enKeys = Enum.GetValues<ChromeKey>();
        foreach (var k in enKeys)
        {
            if (!en.ContainsKey(k)) throw new InvalidOperationException($"DocumentChrome en-US missing {k}");
            if (!ptBR.ContainsKey(k)) throw new InvalidOperationException($"DocumentChrome pt-BR missing {k}");
            if (!esES.ContainsKey(k)) throw new InvalidOperationException($"DocumentChrome es-ES missing {k}");
            if (!frFR.ContainsKey(k)) throw new InvalidOperationException($"DocumentChrome fr-FR missing {k}");
        }

        return new Dictionary<string, IReadOnlyDictionary<ChromeKey, string>>
        {
            ["en-US"] = en,
            ["pt-BR"] = ptBR,
            ["es-ES"] = esES,
            ["fr-FR"] = frFR,
        };
    }
}
