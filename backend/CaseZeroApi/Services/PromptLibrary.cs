namespace CaseZeroApi.Services
{
    #region Prompt Library
    internal static class PromptLibrary
    {
        public static string SystemArchitect(CaseSeed seed) =>
            """
            Você é um arquiteto narrativo forense. Gere um ÚNICO JSON VÁLIDO do **case.json v1.0** (ver CASE_JSON_V1_SPEC.md, DATABASE_SCHEMA.md) — sem comentários, sem markdown, apenas JSON.
            REGRAS GERAIS v1.0:
            - IDs formais: case_xxx, asset.nome_descritivo, email.nome_descritivo, suspect.nome_completo, rule.nome_descritivo
            - Datas/times: ISO 8601 COM offset, timezone padrão America/Toronto
            - Tudo fictício e verossímil, sem nomes reais
            - Nunca conclua culpabilidade
            - Sistema de visibilidade: "initial" (disponível no início) ou "hidden" (desbloqueado por rules)
            - Rules server-side para progressão narrativa (forensics_complete, asset_viewed, email_opened, time_elapsed)
            - Assets unificados (evidências, documentos, mídia) com checksums SHA256
            - Emails dinâmicos com attachments (assetIds)
            """;

        public static string UserArchitect(CaseSeed seed)
        {
            var tz = seed.Timezone ?? "America/Toronto";
            return $@"Crie o case.json v1.0 COMPLETO com a seguinte estrutura:

{{
  ""version"": ""1.0"",
  ""caseId"": ""case_xxx"" (ex: case_001, case_missing_heir),
  ""metadata"": {{
    ""title"": ""{seed.Title}"",
    ""description"": ""(1-2 frases descritivas)"",
    ""difficulty"": ""Rookie|Intermediate|Advanced|Expert"",
    ""estimatedTimeMinutes"": {seed.TargetDurationMinutes},
    ""requiredRank"": ""Detective"",
    ""location"": ""{seed.Location}"",
    ""incidentDate"": ""{seed.IncidentDateTime:o}"",
    ""category"": ""Murder|Theft|Missing Person|Fraud|Assault"",
    ""briefing"": ""(briefing inicial completo, 200-500 palavras, contexto do caso)"",
    ""victim"": {{ ""name"": ""..."", ""age"": X, ""occupation"": ""..."", ""lastSeen"": ""..."" }}
  }},
  ""assets"": [
    // Assets = evidências + documentos + mídia unificados
    // MÍNIMO 8-15 assets variados
    {{
      ""assetId"": ""asset.briefing_doc"" (formato: asset.nome_descritivo),
      ""name"": ""Initial Case Briefing"",
      ""type"": ""document|image|video|audio|physical|digital"",
      ""category"": ""Document|Digital|Physical|Biological|Communication|Technical"",
      ""description"": ""(descrição detalhada 50-200 palavras)"",
      ""filePath"": ""/cases/case_xxx/assets/briefing.pdf"",
      ""visibility"": ""initial|hidden"",
      ""checksum"": ""abc123...(sha256 fictício 64 chars)"",
      ""metadata"": {{ /* campos específicos do tipo */ }}
    }}
  ],
  ""emails"": [
    // MÍNIMO 3-5 emails (briefing inicial + updates + resultados forenses)
    {{
      ""emailId"": ""email.briefing"" (formato: email.nome_descritivo),
      ""from"": ""Chief Inspector <chief@forensics.gov>"",
      ""to"": ""Detective <you@forensics.gov>"",
      ""subject"": ""Case Assignment: {seed.Title}"",
      ""sentAt"": ""{seed.IncidentDateTime:o}"",
      ""priority"": ""normal|high|urgent"",
      ""visibility"": ""initial|hidden"",
      ""content"": ""(corpo do email 100-500 palavras)"",
      ""attachments"": [""asset.briefing_doc""],
      ""metadata"": {{}}
    }}
  ],
  ""suspects"": [
    // MÍNIMO 3-5 suspeitos
    {{
      ""suspectId"": ""suspect.john_doe"" (formato: suspect.nome_completo),
      ""name"": ""John Doe"",
      ""age"": 35,
      ""occupation"": ""..."",
      ""relationship"": ""(relação com vítima/caso)"",
      ""motive"": ""(possível motivo)"",
      ""alibi"": ""(álibi alegado)"",
      ""alibiVerified"": false,
      ""background"": ""(background detalhado 200-500 palavras)"",
      ""linkedAssets"": [""asset.xxx"", ""asset.yyy""],
      ""visibility"": ""initial|hidden""
    }}
  ],
  ""rules"": [
    // Rules para progressão narrativa (reveal emails, assets, suspects)
    // MÍNIMO 5-8 rules para criar fluxo dinâmico
    {{
      ""ruleId"": ""rule.reveal_dna_results"" (formato: rule.nome_descritivo),
      ""trigger"": {{
        ""type"": ""forensics_complete|attachment_download|asset_viewed|email_opened|time_elapsed"",
        ""inputAssetId"": ""asset.blood_sample"" (se forensics_complete),
        ""analysisType"": ""DNA"" (se forensics_complete),
        ""gameTimeMinutes"": 120 (se time_elapsed)
      }},
      ""actions"": [
        {{ ""type"": ""reveal_email"", ""emailId"": ""email.dna_results"" }},
        {{ ""type"": ""reveal_asset"", ""assetId"": ""asset.dna_report"" }}
      ]
    }}
  ],
  ""forensicsDefaults"": {{
    ""analysisTypes"": [
      {{ ""type"": ""DNA"", ""durationMinutes"": 180, ""availableFor"": [""physical"", ""biological""] }},
      {{ ""type"": ""Fingerprint"", ""durationMinutes"": 120, ""availableFor"": [""physical""] }},
      {{ ""type"": ""DigitalForensics"", ""durationMinutes"": 240, ""availableFor"": [""digital""] }},
      {{ ""type"": ""Ballistics"", ""durationMinutes"": 150, ""availableFor"": [""physical""] }},
      {{ ""type"": ""Toxicology"", ""durationMinutes"": 360, ""availableFor"": [""biological""] }}
    ],
    ""noFindingsEmail"": {{
      ""template"": ""No significant findings for {{{{assetName}}}}. Analysis inconclusive."",
      ""from"": ""Forensics Lab <lab@forensics.gov>"",
      ""subject"": ""Analysis Results: {{{{assetName}}}}""
    }}
  }}
}}

Semente do caso:
Pitch: {seed.Pitch}
Twist: {seed.Twist}
Restrições: {seed.Constraints}
Dificuldade: {seed.Difficulty}
Timezone: {tz}

REQUISITOS CRÍTICOS:
✅ version = ""1.0""
✅ Todos os IDs únicos no formato especificado (asset.*, email.*, suspect.*, rule.*)
✅ FilePaths no formato /cases/case_xxx/assets/arquivo.ext
✅ Checksums SHA256 fictícios (64 chars hex)
✅ Datas ISO 8601 com offset ({tz})
✅ Visibility system (initial/hidden) + rules para reveal
✅ LinkedAssets nos suspects apontam para assetIds válidos
✅ Email attachments apontam para assetIds válidos
✅ Rules com triggers válidos e actions que referenciam IDs existentes
✅ ForensicsDefaults com todos os analysis types
✅ Briefing completo no metadata.briefing (não separado)

Saída: JSON único válido, sem markdown, sem comentários.";
        }

        public static string SystemForense() =>
            """
            Você é redator forense oficial. Estilo burocrático, neutro, sem conclusões sobre culpa. Use padrões do case.json v1.0 (CASE_JSON_V1_SPEC.md).
            """;

        public static string SystemPerito() =>
            """
            Você é perito criminal oficial. Redija laudos técnicos completos, com método, equipamento, cadeia de custódia, limitações e incerteza. Sem opiniões sobre culpabilidade.
            """;

        public static string SystemDiretorArte() =>
            """
            Você é diretor de arte forense. Sua tarefa é gerar uma LISTA JSON de prompts de IMAGEM extremamente detalhados (rich captions), cada item com:
            {
              "assetId": "asset.nome_descritivo ou null" (formato v1.0),
              "title": "curto",
              "intendedUse": "cenário|evidência|documento|frame_CFTV|foto_técnica",
              "prompt": "descrição hyper-detalhada em pt-BR",
              "negativePrompt": "o que evitar",
              "constraints": { "lighting": "…", "camera": "…", "style": "foto realista|variante…", "guidelines": ["sem rostos identificáveis", "sem marcas"] }
            }
            Não use markdown; devolva APENAS JSON (lista).
            """;

        public static string UserInterrogatorio(CaseContext ctx, string suspectId)
        {
            return $@"Você é um redator forense especializado em casos de polícia. Gere UM ÚNICO documento oficial de INTERROGATÓRIO (ou DEPOIMENTO), em Markdown, completo e verossímil, SEM resumo.
PARAMS:
- caseId: {ctx.CaseId}
- suspectId: {suspectId} (formato: suspect.nome_completo)
- timezone: America/Toronto
- dadosDoCaso: extraia do case.json v1.0 abaixo (mantenha nomes/IDs/horários)

REQUISITOS:
- Front matter YAML com metadados (docType, caseId, documentId, assetId opcional, jurisdiction, subject, session, participants, legal, recording, links, chainOfCustody, version, author, changes)
- TRANSCRIÇÃO LONGA com MÍNIMO de 150 turnos (2.500–6.000 palavras), timestamps [HH:MM:SS], eventos não verbais [pausa], [inaudível], etc.
- Log de assets exibidos e reações; Log de intervalos; Encerramento e assinaturas; Anexos com checksums simulados.
- Use apenas fatos do case.json v1.0; tudo citado deve existir (assetIds válidos).

CASE.JSON v1.0:
{Truncate(ctx.CaseJson, 14000)}";
        }

        public static string UserRelatorio(CaseContext ctx)
        {
            return $@"Redija um ÚNICO RELATÓRIO INVESTIGATIVO oficial do caso {ctx.CaseId} em Markdown:
- Capa com metadados; Sumário executivo (factual, 10–15 linhas); Histórico do caso; Metodologia; Achados organizados por fonte (CFTV, controle de acesso, documentos, depoimentos); Lacunas e pendências; Diligências solicitadas;
- Tabela de assets com visibility (initial/hidden), checksums, integridade, referências cruzadas.
- Estilo burocrático, neutro. NÃO inferir culpa.

CASE.JSON v1.0:
{Truncate(ctx.CaseJson, 14000)}";
        }

        public static string UserLaudo(CaseContext ctx, string laudoId, string tema)
        {
            return $@"Emita o LAUDO PERICIAL {laudoId} (Markdown) — Tema: {tema}
- Cabeçalho com metadados (caseId {ctx.CaseId}, laudoId, perito, laboratório, equipamento, datas/horas com timezone America/Toronto)
- Assets analisados (assetIds formato asset.nome_descritivo), com checksums SHA256 e cadeia de custódia
- Método (protocolos, calibração, controle de qualidade, parâmetros)
- Resultados (tabelas, medições, frames/descrições, logs), com incerteza e limitações
- Discussão técnica (alternativas, vieses, P-erro)
- Conclusão técnica (o que foi estabelecido/descartado) SEM atribuir culpa
- Anexos (lista assetIds/figuras) com checksums

Use EXCLUSIVAMENTE dados do case.json v1.0 abaixo; tudo referenciado deve existir.
CASE.JSON v1.0:
{Truncate(ctx.CaseJson, 12000)}";
        }

        public static string UserEvidences(CaseContext ctx)
        {
            return $@"Gere a TABELA-MESTRA DE ASSETS em Markdown para o caso {ctx.CaseId}:
| AssetId | Nome | Tipo | Categoria | Visibility | FilePath | Checksum (sha256) | Estado |
|---------|------|------|-----------|------------|----------|-------------------|--------|
Inclua TODOS os assets do case.json v1.0, consistentes, com paths relativos /cases/case_xxx/assets/. Depois, liste um bloco 'Rules de Revelação' baseado no rules[].
CASE.JSON v1.0:
{Truncate(ctx.CaseJson, 12000)}";
        }

        public static string UserImagePrompts(CaseContext ctx)
        {
            return $@"A partir do case.json v1.0, gere uma LISTA JSON de prompts de imagem RICOS (rich captions) para:
- cenas (fachada, corredor lateral, sala técnica, recepção, cofre)
- assets tipo image (foto técnica, captura CFTV, documentos digitalizados)
- assets tipo physical (evidências físicas fotografadas)
Cada prompt deve incluir descrição minuciosa de: ambiente, composição, enquadramento, lente, distância focal, profundidade de campo, iluminação, horário, condições climáticas, texturas, marcas/ausências, metadados EXIF fictícios, restrições (sem rostos identificáveis, sem logomarcas reais), variações possíveis e NOTAS para o artista.
Devolva APENAS JSON, seguindo este shape:
[
  {{
    ""assetId"": ""asset.cctv_corridor"" (formato v1.0),
    ""title"": ""Frame CFTV — corredor lateral"",
    ""intendedUse"": ""frame_CFTV"",
    ""prompt"": ""…"",
    ""negativePrompt"": ""…"",
    ""constraints"": {{
      ""lighting"": ""…"",
      ""camera"": ""sensor CMOS 1080p, low-light, 24fps"",
      ""style"": ""câmera de segurança/grão sutil"",
      ""guidelines"": [""sem rostos legíveis"", ""perspectiva alta, canto do teto""]
    }}
  }}
]
CASE.JSON v1.0:
{Truncate(ctx.CaseJson, 11000)}";
        }

        internal static string Truncate(string txt, int max)
        {
            if (string.IsNullOrWhiteSpace(txt)) return txt;
            return txt.Length <= max ? txt : txt.Substring(0, max) + "\n[truncado para prompt]";
        }
    }
    #endregion
}