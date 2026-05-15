# 04 — Estrutura de Caso

> **Canônico v2.** A forma técnica do `case.json` v2 é especificada em [`docs/CASE_JSON_V2_SPEC.md`](../docs/CASE_JSON_V2_SPEC.md). Este capítulo descreve apenas o **design de jogo** por trás dessa forma. Qualquer documento mais antigo que use `evidences[]`, `unlockLogic`, `documents[]` ou `forensicReports[]` está obsoleto e deve ser ignorado.

## 4.1 Visão geral

Cada caso é um único arquivo `case.json` (v2) acompanhado de assets binários (PDFs, fotos, áudios) em uma pasta `cases/<caseId>/assets/`. Toda a **lógica do caso** vive no JSON: o que está visível inicialmente, o que se desbloqueia quando, qual o culpado, quais perguntas fazer ao jogador, quanto vale cada acerto.

Princípios:

- **Auto-suficiência.** O caso roda no site sem código auxiliar. O backend não tem "lógica de caso" hard-coded.
- **Server-only sensível.** `rules`, `forensicOutcomes`, `solution`, `temporalEvents` e `gameMetadata.generation` nunca chegam ao cliente.
- **Idempotência.** Toda regra/evento temporal dispara no máximo uma vez por sessão.
- **Reproducibilidade.** Dois jogadores no mesmo caso devem ver os mesmos pontos-de-virada na mesma ordem (sujeito ao que o jogador escolher investigar).

## 4.2 Blocos do caso

| Bloco | Papel no jogo | Cliente vê? |
|-------|---------------|-------------|
| `metadata` | Pitch, dificuldade, rank exigido, modo de desbloqueio | sim |
| `assets[]` | Tudo que o detetive consegue **abrir no FileViewer** (PDF, foto, áudio) | só os visíveis |
| `emails[]` | Inbox do detetive — briefing, lab, testemunhas | só os visíveis |
| `suspects[]` | Fichas com motivo, álibi, status | só os visíveis |
| `timeline[]` | Linha do tempo narrativa visível ao jogador | sim |
| `temporalEvents[]` | Memos/alertas que disparam por tempo de jogo | resultado sim, definição não |
| `rules[]` | "Quando X acontece, faça Y" | **não** |
| `forensicsDefaults` | Tipos de análise disponíveis e durações | parcial |
| `forensicOutcomes[]` | Resultado canônico de cada análise sobre cada asset | **não** |
| `solution{}` | Culpado, evidências exigidas, perguntas, pontuação | só perguntas (sem gabarito) |
| `gameMetadata` | Tags, content warnings, info de geração | sim (sem `generation`) |

## 4.3 Modo de desbloqueio (`unlockMode`)

- **`gated`** (default) — só os itens com `visibility: "initial"` aparecem no início; o resto é desbloqueado por `rules[]`.
- **`all_initial`** — tudo aparece desde o início. Usado em casos Rookie e em playtests.
- **Auto-rule:** quando `metadata.requiredRank == "Rookie"`, o backend força `unlockMode = "all_initial"` independente do JSON.

## 4.4 Triggers e Actions

Gatilhos que o servidor reconhece:

| Trigger | Disparado quando |
|---------|------------------|
| `forensics_complete` | Uma análise forense `(inputAssetId, analysisType)` termina |
| `email_opened` | Detetive abre um email pela primeira vez |
| `attachment_download` | Detetive baixa um anexo |
| `asset_viewed` | Detetive abre um asset no FileViewer |
| `suspect_viewed` | Detetive abre a ficha de um suspeito |
| `time_elapsed` | Tempo de jogo chega em `atMinutes` |
| `multiple_conditions` | Composição AND/OR de outros triggers |

Ações que uma regra pode aplicar:

| Action | Efeito |
|--------|--------|
| `reveal_email` / `reveal_asset` / `reveal_suspect` | Torna entidade visível |
| `add_email_attachment` | Adiciona anexo a email já visível |
| `send_notification` | Notificação em tela (info/warn/critical) |
| `update_suspect_status` | `suspect → cleared` ou `confirmed_culprit` |
| `mark_alibi_verified` | Marca álibi como verificado |

Toda regra é **idempotente** por `(ruleId, sessionId)`.

## 4.5 Perícia (`forensicsDefaults` + `forensicOutcomes`)

Quando o detetive solicita uma análise:

1. Backend verifica que o `analysisType` é compatível com o `Asset.type` via `forensicsDefaults.analysisTypes[*].availableFor`.
2. Cria um `ForensicRequest` e aguarda a duração definida.
3. Ao concluir:
   - Procura em `forensicOutcomes[]` a entrada de `(inputAssetId, analysisType)`.
   - Se houver e `findings == true`, revela `resultEmailId`/`resultAssetId`.
   - Se não houver ou `findings == false`, envia o email `forensicsDefaults.noFindingsEmail`.
4. Em qualquer caso, dispara `forensics_complete` no rules engine — regras adicionais podem reagir.

## 4.6 Solução (`solution`)

A solução é avaliada server-side e tem quatro componentes pontuáveis:

| Componente | Peso default | Como pontua |
|------------|--------------|-------------|
| `culpritId` | 0.4 | Tudo ou nada |
| `requiredEvidenceIds` | 0.2 | Proporcional à interseção |
| `requiredAnalysisIds` | 0.2 | Idem |
| `questions[]` | 0.2 | Soma ponderada de `weight × acerto` |

`partialCreditRules` define os pesos (devem somar 1.0). `minimumScore` é o limiar para `correct = true`. `maxAttempts` limita as tentativas por `(usuário, caso)`. Após acertar ou esgotar as tentativas, o jogador recebe `explanation` em markdown.

O cliente recebe `solution.questions[]` **sem `correctOptionId`** e sem nenhuma das listas obrigatórias — só o formulário.

## 4.7 Eventos temporais (`temporalEvents`)

Cada `tevt.*` dispara em `triggerAtMinutes` (game time desde `openedAt`). O servidor mantém um registro de quais já dispararam por sessão para garantir que cada um aconteça uma única vez. Os payloads viram emails dinâmicos, memos no desktop ou notificações.

## 4.8 Boas práticas de design

1. **O primeiro email deve abrir caminho para a primeira análise.** Nada deve estar bloqueado no início sem ser explicado.
2. **Toda regra deve ter um "porquê" narrativo.** Não use `reveal_*` como atalho — é o resultado de uma descoberta.
3. **Cada caso tem 3-5 suspeitos.** Menos vira óbvio; mais vira confuso.
4. **A solução deve exigir pelo menos uma perícia e uma evidência.** Senão o jogo vira "adivinha o culpado".
5. **Use `partialCreditRules` para reconhecer raciocínio parcial.** Acertar o culpado mas não as evidências ainda merece pontos.

## 4.9 Para gerar um caso

A pipeline de geração mora em `functions/CaseGen.Functions/` (Azure Functions, .NET 9). Ela deve emitir um `case.json` que valide contra `schemas/case.schema.json`. A migração da pipeline para emitir v2 está no roadmap (capítulo 12).
