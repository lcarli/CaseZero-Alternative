namespace CaseZeroApi.Services
{
    #region Prompt Library
    internal static class PromptLibrary
    {
        public static string SystemArchitect(CaseSeed seed) =>
            """
            Você é um arquiteto narrativo forense. Gere um ÚNICO JSON VÁLIDO do **Sistema Objeto Caso** (ver OBJETO_CASO.md, DATABASE_SCHEMA.md, API.md) — sem comentários, sem markdown, apenas JSON.
            REGRAS GERAIS:
            - IDs formais: CASE-*, DOC-*, EVD-###, ANL-###, SUS-###, WIT-###.
            - Datas/times: ISO 8601 COM offset, timezone padrão America/Toronto (projeto usa esse timezone).
            - Tudo fictício e verossímil, sem nomes reais.
            - Nunca conclua culpabilidade. Documentos derivam deste objeto.
            - Consistência: timeline monotônica; evidências citadas existem e têm cadeia de custódia.
            """;

        public static string UserArchitect(CaseSeed seed)
        {
            var tz = seed.Timezone ?? "America/Toronto";
            return $@"Crie o case.json com os seguintes campos:
- caseId, title, description, locale, timezone, difficulty, durationMinutes, incidentDateTime
- persons: suspects[] e witnesses[] com IDs, bios (objetivas), motivos, álibis, fragilidades
- locations[] (com mapa de salas/externos se aplicável)
- evidences[] (EVD-###) com: name, type, category (Foto, Vídeo, Documento, Log, Áudio, Perícia), path relativo sugerido, collectedBy, storedAt, integrity.sha256(simule), chainOfCustody[], unlockRequirements, riskOfContamination
- forensicAnalyses[] (ANL-###) com: objetivo, método, equipamento, incerteza, limitações, anexos
- timeline[] (eventos encadeados, minuto-a-minuto quando necessário) ligando pessoas, locais e evidências
- documents[] (rótulos e caminhos esperados para interrogatórios, relatórios, laudos)

Semente mínima do caso:
Título: {seed.Title}
Local: {seed.Location}
Data/Hora do incidente: {seed.IncidentDateTime:o} ({tz})
Pitch: {seed.Pitch}
Twist: {seed.Twist}
Restrições: {seed.Constraints}
Nível/Duração alvo: {seed.Difficulty} / {seed.TargetDurationMinutes} min

Saída: JSON único e válido. IDs únicos. Nada de markdown.";
        }

        public static string SystemForense() =>
            """
            Você é redator forense oficial. Estilo burocrático, neutro, sem conclusões sobre culpa. Use padrões do projeto (OBJETO_CASO.md, API.md).
            """;

        public static string SystemPerito() =>
            """
            Você é perito criminal oficial. Redija laudos técnicos completos, com método, equipamento, cadeia de custódia, limitações e incerteza. Sem opiniões sobre culpabilidade.
            """;

        public static string SystemDiretorArte() =>
            """
            Você é diretor de arte forense. Sua tarefa é gerar uma LISTA JSON de prompts de IMAGEM extremamente detalhados (rich captions), cada item com:
            {
              "evidenceId": "EVD-### ou null",
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
- suspectId: {suspectId}
- timezone: America/Toronto
- dadosDoCaso: extraia do case.json abaixo (mantenha nomes/IDs/horários)

REQUISITOS:
- Front matter YAML com metadados (docType, caseId, documentId, evidenceId opcional, jurisdiction, subject, session, participants, legal, recording, links, chainOfCustody, version, author, changes)
- TRANSCRIÇÃO LONGA com MÍNIMO de 150 turnos (2.500–6.000 palavras), timestamps [HH:MM:SS], eventos não verbais [pausa], [inaudível], etc.
- Log de evidências exibidas e reações; Log de intervalos; Encerramento e assinaturas; Anexos com hashes simulados.
- Use apenas fatos do case.json; tudo citado deve existir (EVD/ANL).

CASE.JSON:
{Truncate(ctx.CaseJson, 14000)}";
        }

        public static string UserRelatorio(CaseContext ctx)
        {
            return $@"Redija um ÚNICO RELATÓRIO INVESTIGATIVO oficial do caso {ctx.CaseId} em Markdown:
- Capa com metadados; Sumário executivo (factual, 10–15 linhas); Histórico do caso; Metodologia; Achados organizados por fonte (CFTV, controle de acesso, bilhete, depoimentos); Lacunas e pendências; Diligências solicitadas;
- Tabela de evidências (EVD-###) com estado (bloqueada/desbloqueada), cadeia de custódia, integridade, local de guarda; Referências cruzadas (ANL-###).
- Estilo burocrático, neutro. NÃO inferir culpa.

CASE.JSON:
{Truncate(ctx.CaseJson, 14000)}";
        }

        public static string UserLaudo(CaseContext ctx, string laudoId, string tema)
        {
            return $@"Emita o LAUDO PERICIAL {laudoId} (Markdown) — Tema: {tema}
- Cabeçalho com metadados (caseId {ctx.CaseId}, laudoId, perito, laboratório, equipamento, datas/horas com timezone America/Toronto)
- Cadeia de custódia dos itens analisados (EVD-###), com hashes e transferências
- Método (protocolos, calibração, controle de qualidade, parâmetros)
- Resultados (tabelas, medições, frames/descrições, logs), com incerteza e limitações
- Discussão técnica (alternativas, vieses, P-erro)
- Conclusão técnica (o que foi estabelecido/descartado) SEM atribuir culpa
- Anexos (lista EVD/figuras) com hashes simulados

Use EXCLUSIVAMENTE dados do case.json abaixo; tudo referenciado deve existir.
CASE.JSON:
{Truncate(ctx.CaseJson, 12000)}";
        }

        public static string UserEvidences(CaseContext ctx)
        {
            return $@"Gere a TABELA-MESTRA DE EVIDÊNCIAS em Markdown para o caso {ctx.CaseId}:
| EVD | Nome | Tipo | Origem | Coletado por | Local de guarda | Integridade (sha256) | Cadeia de custódia (resumo) | Estado |
|-----|------|------|--------|--------------|------------------|----------------------|------------------------------|--------|
Inclua TODAS as evidências do case.json, consistentes, com paths relativos. Depois, liste um bloco 'Regras de desbloqueio' e outro 'Riscos de contaminação'.
CASE.JSON:
{Truncate(ctx.CaseJson, 12000)}";
        }

        public static string UserImagePrompts(CaseContext ctx)
        {
            return $@"A partir do case.json, gere uma LISTA JSON de prompts de imagem RICOS (rich captions) para:
- cenas (fachada, corredor lateral, sala técnica, recepção, cofre)
- evidências (foto técnica de lacre, close do quadro de chaves, captura simulada de CFTV, print de planilha, log do controle de acesso)
- documentos (recibo, extrato simplificado de bilhete, etiqueta de evidência)
Cada prompt deve incluir descrição minuciosa de: ambiente, composição, enquadramento, lente, distância focal, profundidade de campo, iluminação, horário, condições climáticas, texturas, marcas/ausências, metadados EXIF fictícios, restrições (sem rostos identificáveis, sem logomarcas reais), variações possíveis e NOTAS para o artista.
Devolva APENAS JSON, seguindo este shape:
[
  {{
    ""evidenceId"": ""EVD-019"",
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
CASE.JSON:
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