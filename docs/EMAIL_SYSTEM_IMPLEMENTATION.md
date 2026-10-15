# Sistema de Emails - Implementação Atual

## 📋 Visão Geral

Este documento descreve o estado **atual** do sistema de emails no repositório.

Hoje existem **dois fluxos distintos**:

1. **Case V2 / case.json**: emails investigativos do caso, gerados nas Azure Functions e expostos pela API v2.
2. **Inbox SQL legada**: entidade `Email` persistida no banco para mensagens internas do produto, seed inicial e notificações como promoção.

A implementação real **não** usa mais o plano antigo com `NormalizedEmail`, `GenerateEmailDesignsAsync()`, `ExpandEmailsAsync()`, `NormalizeEmailsAsync()`, `CaseFormatService` ou `EmailViewer`.

---

## ✅ Status Atual

### Implementado no código

- **Geração de emails no pipeline Case V2** já existe nas Functions.
- **Briefing email** é gerado no estágio `BriefingEmailTask`.
- **Follow-up emails** são gerados no estágio `InitialEmailsTask`.
- **Result emails** podem ser revelados depois por regras ligadas a forense.
- **Gating** de email existe, mas é feito por **visibilidade de sessão + regras**, e **não** por campos `gated`/`gatingRule`.
- **Frontend** já consome emails visíveis em `EmailApp.tsx`.
- **API** já possui endpoints para listar, abrir, detalhar e baixar anexos de emails do caso.

### Não existe no código atual

- `NormalizedEmail`
- `NormalizedCaseBundle.Emails`
- `NormalizerService` lendo pasta `emails/`
- `CaseFormatService` montando briefing a partir de bundle normalizado
- `frontend/src/components/apps/EmailViewer.tsx`
- `CaseEngine.generateEmailsFromCase()`
- `functions/CaseGen.Functions/Functions/TestGenerateEmails.cs`
- `tests/http-requests/casegen-functions/test-emails.http`

---

## 🧠 Arquitetura Atual

## 1. Geração de emails nas Functions (`CaseGen.Functions`)

A geração atual acontece dentro do pipeline V2 principal em `functions/CaseGen.Functions`.

### Estágios reais

- **Stage 4**: `BriefingEmailTask`
  - gera o email principal do chefe
- **Stage 6**: `InitialEmailsTask`
  - gera **0 a 3** emails iniciais adicionais
  - em casos não-rookie, roda em paralelo com outcomes forenses
  - em casos rookie, roda no fluxo `rookieInitialEvidence`
- **Stage 7**: `MechanicalRulesBuilder`
  - cria regras determinísticas como `forensics_complete -> reveal_email`
- **Stage 8**: `RulesTask`
  - adiciona regras narrativas extras
- **Assembly final**: `CaseV2GeneratorService`
  - monta `emails[]` diretamente dentro do `case.json` final

### Como os emails são montados no `case.json`

O `case.json` V2 usa o modelo real abaixo:

```json
{
  "id": "email.briefing",
  "from": "Chief of Police",
  "to": ["Alex Morgan <detective@citypolice.gov>"],
  "subject": "URGENT: Case Assignment",
  "body": "...",
  "sentAt": "2026-01-01T12:00:00Z",
  "priority": "urgent",
  "attachments": ["asset.casefile_001"],
  "visibility": "initial"
}
```

### Campos reais do email V2

- `id`
- `from`
- `to` (`string[]`, com compatibilidade para ler string única via converter)
- `subject`
- `body`
- `sentAt`
- `priority`
- `attachments`
- `visibility`
- `metadata` (opcional)

### Regras reais de visibilidade

Não existe `gated: true/false` no schema atual.

O desbloqueio é feito por:

- `visibility: "initial"` ou `"hidden"`
- registros em `CaseSessionVisibleEmails`
- regras em `case.json.rules`
- eventos temporais (`temporalEvents`)
- triggers disparados por abrir email, visualizar asset/suspeito e avançar tempo

### Observações importantes

- O email de briefing é sempre montado com **id fixo `email.briefing`**.
- O briefing recebe prioridade **`urgent`**.
- O briefing anexa os **dois primeiros assets iniciais** do caso.
- `InitialEmailsTask` força `visibility = "initial"` nos follow-ups iniciais.
- Emails de resultado forense entram como **`hidden`** até serem revelados.
- `BuildNoFindingsEmail()` cria template localizado para **pt-BR, es-ES, fr-FR e fallback em inglês**.

---

## 2. Backend API (`backend\CaseZeroApi`)

## Endpoints reais de email do caso

Controller: `backend/CaseZeroApi/Controllers/EmailsController.cs`

### Listar emails visíveis

```http
GET /api/cases/{caseId}/emails
```

Retorna apenas os emails presentes em `CaseSessionVisibleEmails` para o usuário/sessão atuais.

### Abrir email

```http
POST /api/cases/{caseId}/emails/{emailId}/open
```

Efeitos reais:

- valida se o email está visível
- cria/atualiza `CaseSessionEmailState`
- marca `ReadAt`
- incrementa `OpenCount`
- grava audit log
- dispara regras com trigger `email_opened`

### Buscar detalhes completos

```http
GET /api/cases/{caseId}/emails/{emailId}
```

Retorna:

- `emailId`
- `from`
- `to`
- `subject`
- `sentAt`
- `priority`
- `content`
- `attachments`
- `metadata`
- `isRead`
- `readAt`
- `openCount`

### Baixar anexo

```http
POST /api/cases/{caseId}/emails/{emailId}/attachments/{assetId}/download
```

Comportamento real:

- valida visibilidade do email
- valida se `assetId` está em `email.Attachments`
- resolve o blob usando `asset.Uri`
- grava `EmailAttachmentDownloaded`
- chama `UnlockAssetAsync()` para tornar o asset visível na sessão
- faz stream do arquivo

> Observação: embora `RulesEngineService` suporte o trigger `attachment_download`, o fluxo atual de download do controller **não** chama `EvaluateAndApplyAsync()` para esse trigger. O efeito implementado hoje é o desbloqueio direto do asset baixado.

---

## 3. Sessão, visibilidade e gating real

### Sessão automática ao carregar caso

`backend/CaseZeroApi/Controllers/CasesController.cs` chama `EnsureSessionAndInitialVisibilityAsync()` durante `GET /api/cases/{caseId}`.

Isso garante automaticamente:

- criação de `CaseSession` quando necessário
- seed de `CaseSessionVisibleAssets`
- seed de `CaseSessionVisibleEmails`
- seed de suspeitos revelados na sessão

### Regra especial para Rookie

Se `metadata.unlockMode == "all_initial"` **ou** o `requiredRank` for `Rookie`, o backend usa `unlockAll` e libera assets, emails e suspeitos do payload mesmo quando vierem marcados como `hidden`.

### Sanitização para o cliente

`CaseV2SanitizerService` entrega ao frontend apenas:

- assets iniciais ou desbloqueados
- emails iniciais ou desbloqueados
- suspeitos iniciais ou revelados

Também aplica:

- overrides de anexos por sessão (`EmailAttachmentOverrides`)
- inclusão de `SyntheticEmails` quando existirem

---

## 4. Modelos e persistência

## Modelos SQL relacionados a email

### Entidade legada de inbox

Arquivo: `backend/CaseZeroApi/Models/Email.cs`

Campos principais:

- `CaseId`
- `ToUserId`
- `FromUserId`
- `Subject`
- `Content`
- `Preview`
- `SentAt`
- `IsRead`
- `ReadAt`
- `Priority`
- `Type`
- `Attachments`
- `IsSystemGenerated`
- `MetadataJson`

Enums atuais:

- `EmailPriority`: `Low`, `Normal`, `High`, `Urgent`
- `EmailType`: `General`, `CaseAssignment`, `CaseBriefing`, `ForensicResults`, `EvidenceNotification`, `SystemNotification`, `CaseUpdate`, `PromotionNotice`

### Tabelas de sessão usadas pelo email V2

Arquivos:

- `backend/CaseZeroApi/Models/CaseSessionVisibleEmail.cs`
- `backend/CaseZeroApi/Models/CaseSessionEmailState.cs`
- `backend/CaseZeroApi/Models/EmailAttachmentDownloaded.cs`

Função de cada uma:

- `CaseSessionVisibleEmail`: controla quais emails estão liberados
- `CaseSessionEmailState`: controla leitura e contagem de abertura
- `EmailAttachmentDownloaded`: audita download de anexo por usuário/caso/email/asset

### DbContext real

`backend/CaseZeroApi/Data/ApplicationDbContext.cs` expõe:

- `DbSet<Email> Emails`
- `DbSet<CaseSessionVisibleEmail> CaseSessionVisibleEmails`
- `DbSet<CaseSessionEmailState> CaseSessionEmailStates`
- `DbSet<EmailAttachmentDownloaded> EmailAttachmentsDownloaded`

---

## 5. Frontend (`frontend\src`)

## Componentes reais

### Interface principal do caso

Arquivo: `frontend/src/components/apps/EmailApp.tsx`

Comportamento atual:

- recebe `emails` e `caseId`
- hidrata estado de leitura via `emailsApi.getEmails(caseId)`
- ao clicar em um email:
  1. faz `POST /open`
  2. busca `GET /emails/{emailId}`
  3. renderiza conteúdo completo
- renderiza o corpo com `dangerouslySetInnerHTML`
- permite baixar anexos
- após download, chama `refreshCase()` para atualizar assets visíveis

### Context e engine

Arquivos:

- `frontend/src/contexts/CaseContext.tsx`
- `frontend/src/engine/CaseEngine.ts`
- `frontend/src/services/api.ts`
- `frontend/src/types/caseV2.ts`

Fluxo atual:

- `CaseEngine.loadCase()` confia no payload sanitizado do backend
- `visibleEmails` vem diretamente de `caseData.emails`
- `applyReveal('email', id)` adiciona email já presente no estado sanitizado
- `emailsApi` encapsula os endpoints específicos de email

### O que o frontend **não** faz hoje

- não existe `EmailViewer.tsx`
- não existe UI de “email bloqueado” com cadeado
- não existe `isLocked`, `gated` ou `gatingRule` no tipo `Email`

No fluxo atual, emails bloqueados simplesmente **não aparecem** até serem revelados pelo backend.

---

## 6. Inbox SQL legada / notificações internas

Além do fluxo Case V2, o repositório ainda mantém a entidade SQL `Email` para outros cenários.

### Seeds de emails iniciais

`backend/CaseZeroApi/Services/DataSeedingService.cs` cria emails de briefing iniciais no banco usando `EmailType.CaseBriefing`.

### Email de promoção

`backend/CaseZeroApi/Services/PromotionService.cs` cria `EmailType.PromotionNotice` com `MetadataJson` para a UI localizar o conteúdo.

Isso é separado do fluxo `case.json` V2.

---

## 7. Como validar o comportamento atual

## Fluxo mínimo recomendado

1. Carregar um caso V2 com `GET /api/cases/{caseId}`
   - isso já garante sessão e visibilidade inicial
2. Listar emails com `GET /api/cases/{caseId}/emails`
3. Abrir um email com `POST /api/cases/{caseId}/emails/{emailId}/open`
4. Ler conteúdo completo com `GET /api/cases/{caseId}/emails/{emailId}`
5. Baixar anexo com `POST /api/cases/{caseId}/emails/{emailId}/attachments/{assetId}/download`
6. Disparar reveals adicionais via:
   - `POST /api/cases/{caseId}/assets/{assetId}/view`
   - `POST /api/cases/{caseId}/suspects/{suspectId}/view`
   - `POST /api/cases/{caseId}/time`

## Geração de casos

A geração atual não usa `TestGenerateEmails`.

O fluxo real passa pelo durable orchestrator em:

- `functions/CaseGen.Functions/Functions/CaseV2/CaseV2GenerationOrchestrator.cs`

---

## 8. Suposições antigas removidas deste documento

As afirmações abaixo estavam desatualizadas e foram removidas/corrigidas:

- pipeline `design -> expand -> normalize` para emails
- pasta `emails/` normalizada sendo lida pelo backend da API
- modelo `NormalizedEmail`
- rota de teste `TestGenerateEmails`
- ideia de que o frontend extrai briefing das 3 primeiras linhas de `police_report`
- uso de `CaseFormatService` para montar emails do jogo
- `gated/gatingRule` como contrato de email
- componente `EmailViewer.tsx` para mostrar placeholders bloqueados

---

## 🔗 Links locais validados

- [CASE_JSON_V2_SPEC.md](CASE_JSON_V2_SPEC.md)

Nenhum outro link Markdown local era necessário neste arquivo após a correção.

---

## 📚 Arquivos-chave

### Backend

- `backend/CaseZeroApi/Controllers/EmailsController.cs`
- `backend/CaseZeroApi/Controllers/CasesController.cs`
- `backend/CaseZeroApi/Controllers/CaseTriggersController.cs`
- `backend/CaseZeroApi/Services/CaseV2SanitizerService.cs`
- `backend/CaseZeroApi/Services/VisibilityService.cs`
- `backend/CaseZeroApi/Services/RulesEngineService.cs`
- `backend/CaseZeroApi/Data/ApplicationDbContext.cs`
- `backend/CaseZeroApi/Models/Email.cs`
- `backend/CaseZeroApi/Models/CaseSessionVisibleEmail.cs`
- `backend/CaseZeroApi/Models/CaseSessionEmailState.cs`
- `backend/CaseZeroApi/Models/EmailAttachmentDownloaded.cs`

### Frontend

- `frontend/src/components/apps/EmailApp.tsx`
- `frontend/src/contexts/CaseContext.tsx`
- `frontend/src/engine/CaseEngine.ts`
- `frontend/src/services/api.ts`
- `frontend/src/types/caseV2.ts`

### Functions

- `functions/CaseGen.Functions/Functions/CaseV2/CaseV2GenerationOrchestrator.cs`
- `functions/CaseGen.Functions/Services/CaseV2/CaseV2GeneratorService.cs`
- `functions/CaseGen.Functions/Services/CaseV2/Tasks/EmailRuleTasks.cs`
- `functions/CaseGen.Functions/Services/CaseV2/MechanicalRulesBuilder.cs`
- `functions/CaseGen.Functions/Models/CaseV2/CaseV2GenerationModels.cs`

---

**Data de atualização**: 2026-07-28

**Status**: ✅ Alinhado ao código atual
