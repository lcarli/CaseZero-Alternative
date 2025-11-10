# Sistema de Emails - Plano de Implementação

## 📋 Visão Geral

Este documento descreve o plano completo para implementar emails gerados por IA no sistema CaseZero. Atualmente, o email inicial do caso é extraído estaticamente das primeiras 3 linhas do `police_report`. O objetivo é substituir isso por um sistema dinâmico onde a IA gera emails contextuais durante a pipeline de geração de casos.

## 🎯 Objetivos

1. **Substituir extração estática** por geração dinâmica de emails via IA
2. **Adicionar suporte a gating** para emails bloqueados até certas condições
3. **Integrar na pipeline existente** seguindo o padrão `design → expand → normalize`
4. **Manter backward compatibility** com casos já gerados

## 📊 Status Atual

### ✅ Concluído
- **Tarefa 1**: Estrutura de modelo `NormalizedEmail` criada
  - Records em `CaseGen.Functions/Models/CaseGenerationModels.cs`
  - Classes em `CaseZeroApi/Models/NormalizedCaseBundle.cs`
  - Campos: `emailId`, `from`, `to`, `subject`, `content`, `sentAt`, `priority`, `attachments`, `gated`, `gatingRule`, `metadata`

- **Tarefa 2**: Suporte básico no `NormalizerService`
  - `CreateNormalizedBundle` retorna `Emails = Array.Empty<NormalizedEmail>()`
  - TODO comment para implementar leitura de `emails/` folder

- **Tarefa 3**: Método `GenerateEmailDesignsAsync()` criado (⚠️ PRECISA SER RE-IMPLEMENTADO)
  - Gera especificações de emails via IA
  - Salva em `design/emails/`
  - **NOTA**: Código foi perdido, precisa ser recriado

- **Tarefa 4**: Método `ExpandEmailsAsync()` criado (⚠️ PRECISA SER RE-IMPLEMENTADO)
  - Expande designs em conteúdo completo
  - Salva em `expand/emails/`
  - **NOTA**: Código foi perdido, precisa ser recriado

### ❌ Pendente
- Tarefas 5-15 (veja seção [Tarefas Detalhadas](#-tarefas-detalhadas))

## 🔧 Tarefas Detalhadas

### **Tarefa 3 (RE-FAZER)**: Criar método GenerateEmailDesigns

**Arquivo**: `backend/CaseGen.Functions/Services/CaseGenerationService.cs`

**Localização**: Adicionar após o método `DesignMediaTypeAsync()` (aproximadamente linha 1534)

**O que fazer**:
```csharp
public async Task<string> GenerateEmailDesignsAsync(string caseId, CancellationToken cancellationToken = default)
```

**Implementação**:
1. Carregar contextos: `plan/core`, `plan/suspects`, `expand/timeline`
2. Extrair difficulty, título do caso, nome da vítima
3. Criar prompt IA para gerar especificações de emails:
   - **Briefing email** (obrigatório): Email do Chief of Police atribuindo o caso
   - **Update email** (opcional, para difficulty Advanced/Expert): Email de acompanhamento
4. Validar resposta JSON (deve ter array `emails`)
5. Salvar em `design/emails/` via `_contextManager.SaveContextAsync()`
6. Retry logic (3 tentativas)

**Campos do design**:
- `emailId`: `email_briefing_001`, `email_update_002`
- `from`: `"Chief Sarah Mitchell <chief.police@cityofportland.gov>"`
- `to`: `"Detective Alex Morgan <detective@cityofportland.gov>"`
- `subject`: Texto do assunto
- `contentDesign`: **Instruções** para geração de conteúdo (não o corpo final)
- `sentAt`: ISO-8601 timestamp
- `priority`: `"normal"`, `"high"`, `"urgent"`
- `attachments`: Array de refs `@documents/`
- `gated`: boolean
- `gatingRule`: Se gated=true, condições de unlock
- `metadata`: Dict com `tone`, `purpose`, etc.

**Quantidade de emails**:
- Rookie/Intermediate: 1 briefing email
- Advanced/Expert: 1 briefing + 1 update email (pode ser gated)

---

### **Tarefa 4 (RE-FAZER)**: Criar método ExpandEmails

**Arquivo**: `backend/CaseGen.Functions/Services/CaseGenerationService.cs`

**Localização**: Logo após `GenerateEmailDesignsAsync()`

**O que fazer**:
```csharp
public async Task<string> ExpandEmailsAsync(string caseId, CancellationToken cancellationToken = default)
```

**Implementação**:
1. Ler `design/emails/` via `LoadContextAsync()`
2. Carregar contextos: `plan/core`, `plan/suspects`, `plan/evidence`, `expand/timeline`
3. Para cada email no design:
   - Criar prompt IA com instruções do `contentDesign`
   - Gerar corpo completo do email (300-600 palavras)
   - Incluir saudação e assinatura apropriadas
   - Manter tom profissional
4. Preservar todos os campos (from, to, subject, sentAt, priority, attachments, gated, gatingRule, metadata)
5. Salvar em `expand/emails/`

**Tom dos emails**:
- Briefing: Authoritative, clear, supportive
- Update: Urgent, focused, pode transmitir pressão externa

---

### **Tarefa 5**: Criar método NormalizeEmails

**Arquivo**: `backend/CaseGen.Functions/Services/CaseGenerationService.cs`

**Localização**: Logo após `ExpandEmailsAsync()`

**O que fazer**:
```csharp
public async Task<string> NormalizeEmailsAsync(string caseId, CancellationToken cancellationToken = default)
```

**Implementação**:
1. Ler `expand/emails/` via `LoadContextAsync()`
2. Para cada email expandido:
   - Converter para formato `NormalizedEmail`
   - Validar campos required: `emailId`, `from`, `to`, `subject`, `content`, `sentAt`
   - Garantir ISO-8601 timestamps com timezone
   - Vincular attachments (refs `@documents/`)
3. Salvar cada email como JSON individual em `emails/` folder
4. Exemplo: `emails/email_briefing_001.json`, `emails/email_update_002.json`

**Validações**:
- `emailId` não vazio
- `from`/`to` formato válido (`Name <email>`)
- `sentAt` ISO-8601 válido
- `priority` um dos valores: `normal`, `high`, `urgent`
- Se `gated=true`, `gatingRule` não pode ser null

---

### **Tarefa 6**: Integrar email generation na pipeline principal

**Arquivos**:
- `backend/CaseGen.Functions/Services/CaseGenerationService.cs` (método de orquestração principal)
- `backend/CaseGen.Functions/Services/NormalizerService.cs`

**O que fazer**:

#### 6.1. Adicionar chamadas na pipeline
Encontrar o método de orquestração principal (provavelmente em alguma Function que orquestra a geração completa). Adicionar após document generation e antes de gating graph creation:

```csharp
// Após document generation...
await _caseGenerationService.GenerateEmailDesignsAsync(caseId, cancellationToken);
await _caseGenerationService.ExpandEmailsAsync(caseId, cancellationToken);
await _caseGenerationService.NormalizeEmailsAsync(caseId, cancellationToken);
// Antes de gating graph creation...
```

#### 6.2. Atualizar NormalizerService
**Arquivo**: `backend/CaseGen.Functions/Services/NormalizerService.cs`

Modificar `CreateNormalizedBundle` (linha ~909):
- Remover: `Emails = Array.Empty<NormalizedEmail>()`
- Adicionar lógica para ler de `emails/` folder no blob storage
- Listar todos os arquivos `email_*.json`
- Deserializar cada um para `NormalizedEmail`
- Retornar array de emails

```csharp
// Pseudocódigo
var emailFiles = await _storageService.ListFilesAsync(container, $"{caseId}/emails/");
var emails = new List<NormalizedEmail>();
foreach (var file in emailFiles)
{
    var json = await _storageService.GetFileAsync(container, file);
    var email = JsonSerializer.Deserialize<NormalizedEmail>(json);
    emails.Add(email);
}
return new NormalizedCaseBundle { Emails = emails.ToArray(), ... };
```

---

### **Tarefa 7**: Atualizar CaseFormatService para ler emails

**Arquivo**: `backend/CaseZeroApi/Services/CaseFormatService.cs`

**O que fazer**:

1. **Remover** método `ExtractBriefingFromDocuments()` (atualmente extrai email das 3 primeiras linhas do police_report)

2. **Adicionar** lógica para ler emails do `NormalizedCaseBundle`:
```csharp
// No método que converte para game format
var briefingEmail = normalizedBundle.Emails.FirstOrDefault(e => e.EmailId == "email_briefing_001");
if (briefingEmail != null)
{
    gameFormat.InitialEmail = new Email
    {
        Id = briefingEmail.EmailId,
        From = briefingEmail.From,
        To = briefingEmail.To,
        Subject = briefingEmail.Subject,
        Content = briefingEmail.Content,
        Timestamp = briefingEmail.SentAt,
        Priority = briefingEmail.Priority,
        Attachments = briefingEmail.Attachments,
        IsLocked = briefingEmail.Gated
    };
}
```

3. **Incluir** todos os emails na resposta da API `GetCase`:
```csharp
case.Emails = normalizedBundle.Emails.Select(e => new EmailDto
{
    EmailId = e.EmailId,
    From = e.From,
    To = e.To,
    Subject = e.Subject,
    Content = e.Content,
    SentAt = e.SentAt,
    Priority = e.Priority,
    Attachments = e.Attachments,
    Gated = e.Gated,
    GatingRule = e.GatingRule,
    Metadata = e.Metadata
}).ToList();
```

---

### **Tarefa 8**: Atualizar interfaces TypeScript no frontend

**Arquivo**: `frontend/src/services/api.ts` (ou `frontend/src/types/`)

**O que fazer**:

1. Criar interface `EmailItem`:
```typescript
export interface EmailItem {
  emailId: string;
  from: string;
  to: string;
  subject: string;
  content: string;
  sentAt: string; // ISO-8601
  priority: 'normal' | 'high' | 'urgent';
  attachments: string[]; // @documents/ refs
  gated: boolean;
  gatingRule?: {
    requiredNodeIds: string[];
    unlockCondition: string;
  };
  metadata: Record<string, any>;
}
```

2. Atualizar interface `NormalizedCaseBundle`:
```typescript
export interface NormalizedCaseBundle {
  caseId: string;
  // ... campos existentes ...
  emails: EmailItem[]; // ADICIONAR
}
```

---

### **Tarefa 9**: Modificar CaseEngine para ler emails do state

**Arquivo**: `frontend/src/services/CaseEngine.ts`

**O que fazer**:

Modificar método `generateEmailsFromCase()`:

**ANTES** (extrai do police_report):
```typescript
generateEmailsFromCase(): Email[] {
  const policeReport = this.state.caseData.documents.find(d => d.id === 'doc_police_report_001');
  const briefing = policeReport?.content.split('\n').slice(0, 3).join('\n');
  // ...
}
```

**DEPOIS** (lê de state.caseData.emails):
```typescript
generateEmailsFromCase(): Email[] {
  if (!this.state.caseData.emails) return [];
  
  return this.state.caseData.emails.map(email => ({
    id: email.emailId,
    from: email.from,
    to: email.to,
    subject: email.subject,
    content: email.content,
    timestamp: new Date(email.sentAt),
    priority: email.priority,
    attachments: email.attachments,
    isRead: false,
    isLocked: this.isEmailLocked(email) // Aplicar gating rules
  }));
}

private isEmailLocked(email: EmailItem): boolean {
  if (!email.gated) return false;
  
  // Verificar se todas as condições de unlock foram satisfeitas
  const gatingRule = email.gatingRule;
  if (!gatingRule) return false;
  
  // Exemplo: verificar se todos os documentos requeridos foram lidos
  const allDocsRead = gatingRule.requiredNodeIds.every(nodeId => 
    this.state.unlockedNodes.includes(nodeId)
  );
  
  return !allDocsRead;
}
```

---

### **Tarefa 10**: Adicionar UI de email bloqueado no frontend

**Arquivo**: `frontend/src/components/apps/EmailViewer.tsx`

**O que fazer**:

Modificar componente para mostrar emails bloqueados:

```tsx
const EmailViewer: React.FC = () => {
  const { emails } = useCaseEngine();
  
  return (
    <div className="email-list">
      {emails.map(email => (
        <div key={email.id} className={`email-item ${email.isLocked ? 'locked' : ''}`}>
          {email.isLocked ? (
            <div className="email-locked">
              <LockIcon className="lock-icon" />
              <h3>{email.subject}</h3>
              <p className="locked-message">
                🔒 Complete requirements to unlock this email
              </p>
              {email.gatingRule && (
                <div className="unlock-requirements">
                  <p>Required: Read all related documents</p>
                  <ul>
                    {email.gatingRule.requiredNodeIds.map(nodeId => (
                      <li key={nodeId}>{nodeId}</li>
                    ))}
                  </ul>
                </div>
              )}
            </div>
          ) : (
            <div className="email-content">
              <div className="email-header">
                <strong>From:</strong> {email.from}
                <strong>To:</strong> {email.to}
                <strong>Priority:</strong> {email.priority}
              </div>
              <h3>{email.subject}</h3>
              <div className="email-body">
                {email.content}
              </div>
              {email.attachments.length > 0 && (
                <div className="email-attachments">
                  <strong>Attachments:</strong>
                  {email.attachments.map(att => (
                    <button key={att} onClick={() => openDocument(att)}>
                      {att}
                    </button>
                  ))}
                </div>
              )}
            </div>
          )}
        </div>
      ))}
    </div>
  );
};
```

**CSS** (adicionar estilos para emails bloqueados):
```css
.email-item.locked {
  opacity: 0.6;
  background: #f5f5f5;
  border: 2px dashed #999;
}

.email-locked {
  padding: 20px;
  text-align: center;
}

.lock-icon {
  font-size: 48px;
  color: #999;
  margin-bottom: 10px;
}

.locked-message {
  font-style: italic;
  color: #666;
}

.unlock-requirements {
  margin-top: 15px;
  text-align: left;
  background: #fff;
  padding: 10px;
  border-radius: 4px;
}
```

---

### **Tarefa 11**: Criar endpoint de teste TestGenerateEmails

**Arquivo**: `backend/CaseGen.Functions/Functions/TestGenerateEmails.cs` (CRIAR NOVO)

**O que fazer**:

Criar Azure Function HTTP para testar geração isolada de emails:

```csharp
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using CaseGen.Functions.Services;
using System.Net;

namespace CaseGen.Functions.Functions;

public class TestGenerateEmails
{
    private readonly ICaseGenerationService _caseGenService;
    private readonly ILogger<TestGenerateEmails> _logger;

    public TestGenerateEmails(
        ICaseGenerationService caseGenService,
        ILogger<TestGenerateEmails> logger)
    {
        _caseGenService = caseGenService;
        _logger = logger;
    }

    [Function("TestGenerateEmails")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("TEST-GENERATE-EMAILS: Starting test");

        try
        {
            // Ler caseId do body
            var body = await req.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(body ?? "{}");
            
            if (!data.TryGetValue("caseId", out var caseId))
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await errorResponse.WriteStringAsync("Missing caseId in request body");
                return errorResponse;
            }

            _logger.LogInformation("TEST-GENERATE-EMAILS: Testing email generation for case {CaseId}", caseId);

            // Executar pipeline de emails
            var designResult = await _caseGenService.GenerateEmailDesignsAsync(caseId, cancellationToken);
            _logger.LogInformation("✓ Design step completed");

            var expandResult = await _caseGenService.ExpandEmailsAsync(caseId, cancellationToken);
            _logger.LogInformation("✓ Expand step completed");

            var normalizeResult = await _caseGenService.NormalizeEmailsAsync(caseId, cancellationToken);
            _logger.LogInformation("✓ Normalize step completed");

            // Retornar resultado
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                success = true,
                caseId,
                design = JsonSerializer.Deserialize<object>(designResult),
                expand = JsonSerializer.Deserialize<object>(expandResult),
                normalize = JsonSerializer.Deserialize<object>(normalizeResult)
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TEST-GENERATE-EMAILS: Failed");
            
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new
            {
                success = false,
                error = ex.Message,
                stackTrace = ex.StackTrace
            });

            return errorResponse;
        }
    }
}
```

---

### **Tarefa 12**: Testar geração de emails com caso existente

**Arquivo**: `tests/http-requests/casegen-functions/test-emails.http` (CRIAR NOVO)

**O que fazer**:

Criar arquivo HTTP request para testar:

```http
### Test Email Generation
POST {{casegen_host}}/api/TestGenerateEmails
Content-Type: application/json

{
  "caseId": "CASE-20251109-a9bb62da"
}
```

**Checklist de validação**:
- [ ] `design/emails/` foi criado no blob storage
- [ ] `expand/emails/` foi criado no blob storage  
- [ ] `emails/` foi criado com JSONs válidos (`email_briefing_001.json`, etc.)
- [ ] Attachments vinculam corretamente a `@documents/` (ex: `@documents/doc_police_report_001`)
- [ ] Timestamps em formato ISO-8601 com timezone (ex: `2024-01-15T09:00:00-08:00`)
- [ ] Se email gated, `gatingRule` está presente e válido
- [ ] Campos `from`/`to` no formato `Name <email>`
- [ ] `priority` é um dos valores: `normal`, `high`, `urgent`

---

### **Tarefa 13**: Gerar caso completo novo com emails

**O que fazer**:

Executar pipeline completa de geração de caso novo e verificar que emails são gerados automaticamente.

**Passos**:
1. Criar novo caso via `GenerateCaseFromPDF` ou similar
2. Verificar logs para confirmar chamadas:
   - `DESIGN-EMAILS: Generating email designs`
   - `EXPAND-EMAILS: Expanding email designs`
   - `NORMALIZE-EMAILS: Normalizing emails`
3. Verificar estrutura de pastas no blob storage:
   ```
   {caseId}/
   ├── plan/
   │   ├── core.json
   │   ├── suspects.json
   │   ├── timeline.json
   │   └── evidence.json
   ├── expand/
   │   ├── timeline.json
   │   └── emails/  ← NOVO
   │       └── emails.json
   ├── design/
   │   ├── documents/
   │   ├── media/
   │   └── emails/  ← NOVO
   │       └── emails.json
   ├── emails/  ← NOVO
   │   ├── email_briefing_001.json
   │   └── email_update_002.json
   └── normalizedCaseBundle.json
   ```
4. Abrir `normalizedCaseBundle.json` e verificar que contém array `emails[]`

**Validação da ordem da pipeline**:
```
Plan → Expand → Design (docs + media + emails) → Generate → Normalize
```

---

### **Tarefa 14**: Testar frontend display e gating de emails

**O que fazer**:

Testar interface do usuário completamente:

**Checklist**:
- [ ] Abrir caso no frontend
- [ ] Email Viewer exibe emails do backend corretamente
- [ ] Briefing email sempre visível (não gated)
- [ ] Update email (se gated) mostra:
  - [ ] Ícone de cadeado 🔒
  - [ ] Mensagem "Complete requirements to unlock this email"
  - [ ] Lista de requisitos (documentos que precisam ser lidos)
- [ ] Attachments (`@documents/`) são clicáveis e abrem o documento correspondente
- [ ] Timestamps exibidos em formato legível (ex: "Jan 15, 2024 9:00 AM PST")
- [ ] Prioridade visual (high = ícone ⚠️, urgent = ícone 🚨)
- [ ] Após ler documentos requeridos, email gated é desbloqueado automaticamente
- [ ] Ao clicar em email desbloqueado, conteúdo completo é exibido

**Casos de teste**:
1. Caso Rookie → apenas 1 email (briefing)
2. Caso Advanced → 2 emails (briefing + update gated)
3. Desbloquear email: ler todos os documentos requeridos → email deve aparecer desbloqueado

---

### **Tarefa 15**: Atualizar documentação do sistema de emails

**Arquivo**: `docs/CASE_GENERATION_PIPELINE.md`

**O que adicionar**:

```markdown
## Email Generation

### Overview

Emails are generated dynamically by AI during case generation, replacing the previous static extraction from police reports. The email generation follows the same three-phase pattern as documents and media: **design → expand → normalize**.

### Email Generation Flow

```
plan/core, plan/suspects, expand/timeline
             ↓
    [IA: GenerateEmailDesigns]
             ↓
      design/emails/
             ↓
    [IA: ExpandEmails]
             ↓
      expand/emails/
             ↓
    [NormalizeEmails]
             ↓
         emails/
  (email_briefing_001.json,
   email_update_002.json)
             ↓
   [NormalizerService]
             ↓
  normalizedCaseBundle.json
```

### Email Types

1. **Briefing Email** (Required)
   - From: Chief of Police
   - To: Detective
   - Purpose: Initial case assignment
   - Gated: Always `false` (available at case start)
   - Content: Case overview, assignment rationale, urgency, available resources

2. **Update Email** (Optional, difficulty-dependent)
   - From: Chief of Police or Supervisor
   - To: Detective
   - Purpose: Progress check, external pressure (media, family, politics)
   - Gated: May be `true` (locked until detective makes progress)
   - Content: Reference to case progress, pressure for results

### NormalizedEmail Structure

```json
{
  "emailId": "email_briefing_001",
  "from": "Chief Sarah Mitchell <chief.police@cityofportland.gov>",
  "to": "Detective Alex Morgan <detective@cityofportland.gov>",
  "subject": "Re: Cold Case Assignment - [Case Title]",
  "content": "Detective Morgan,\n\nI'm assigning you to reopen the cold case...",
  "sentAt": "2024-01-13T08:30:00-08:00",
  "priority": "high",
  "attachments": ["@documents/doc_police_report_001"],
  "gated": false,
  "gatingRule": null,
  "metadata": {
    "tone": "professional, authoritative",
    "purpose": "case_assignment"
  }
}
```

### Gating Rules

Emails can be locked until the detective completes certain actions:

```json
{
  "emailId": "email_update_002",
  "gated": true,
  "gatingRule": {
    "requiredNodeIds": ["doc_police_report_001", "doc_forensics_001"],
    "unlockCondition": "read_all"
  }
}
```

**Unlock Conditions**:
- `read_all`: All required documents must be read
- `unlock_evidence`: Specific evidence must be unlocked
- Custom conditions can be added

### AI Prompts Used

#### Design Phase Prompt
```
You are a police department communications specialist designing email specifications.

EMAILS TO GENERATE:
1. Briefing Email (REQUIRED)
   - Initial case assignment
   - Priority: high/urgent
   - Gated: false

2. Update Email (OPTIONAL for Advanced/Expert)
   - Progress check or pressure
   - May be gated

OUTPUT FORMAT: JSON with email specifications
```

#### Expand Phase Prompt
```
You are writing investigative emails.

TASK: Generate COMPLETE EMAIL BODY based on design instructions.

TONE:
- Briefing: Authoritative, clear, supportive
- Update: Urgent, focused, may convey pressure

LENGTH: 300-600 words (briefing), 200-400 (update)
```

### Quantity by Difficulty

- **Rookie/Intermediate**: 1 email (briefing only)
- **Advanced/Expert**: 2 emails (briefing + gated update)

### Example: email_briefing_001.json

```json
{
  "emailId": "email_briefing_001",
  "from": "Chief Sarah Mitchell <chief.police@cityofportland.gov>",
  "to": "Detective Alex Morgan <detective@cityofportland.gov>",
  "subject": "Re: Cold Case Assignment - The Riverside Incident",
  "content": "Detective Morgan,\n\nI'm assigning you to reopen the cold case known as 'The Riverside Incident.' This case has remained unsolved for several years, and recent developments have brought it back to our attention.\n\n**Case Overview:**\nThe victim, [Victim Name], was found deceased under suspicious circumstances. Initial investigation yielded limited leads, but new forensic techniques may provide breakthrough evidence.\n\n**Your Assignment:**\n1. Review all existing case files and evidence\n2. Re-interview key witnesses using updated protocols\n3. Submit forensic samples for re-analysis\n4. Provide weekly progress reports\n\n**Resources Available:**\n- Full access to forensic lab\n- Archives department support\n- Budget approval for expert consultations\n\n**Timeline:**\nI expect an initial assessment within 2 weeks. Given the age of this case and the potential for closure, this investigation is designated as high priority.\n\nThe attached police report contains the original investigation details. Please begin your review immediately.\n\nBest regards,\nChief Sarah Mitchell\nPortland Police Department",
  "sentAt": "2024-01-13T08:30:00-08:00",
  "priority": "high",
  "attachments": ["@documents/doc_police_report_001"],
  "gated": false,
  "gatingRule": null,
  "metadata": {
    "tone": "professional, authoritative",
    "purpose": "case_assignment",
    "wordCount": 234
  }
}
```

### Integration Points

1. **Backend API**: `CaseFormatService` reads emails from `NormalizedCaseBundle`
2. **Frontend**: `CaseEngine.generateEmailsFromCase()` maps to game format
3. **UI**: `EmailViewer` component displays with gating support
4. **Gating**: `GatingGraph` tracks unlock conditions for gated emails

### Testing

Use `TestGenerateEmails` HTTP function to test email generation in isolation:

```http
POST /api/TestGenerateEmails
Content-Type: application/json

{
  "caseId": "CASE-20251109-a9bb62da"
}
```

Verify:
- [ ] `design/emails/` created
- [ ] `expand/emails/` created
- [ ] `emails/email_briefing_001.json` created
- [ ] Attachments link correctly
- [ ] Timestamps ISO-8601 with timezone
```

---

## 🚧 Erros Conhecidos

### Erro de Compilação Pré-Existente (NÃO relacionado a emails)

**Arquivo**: `CaseGenerationService.cs` linha 3852
**Erro**: `Argument 2: cannot convert from 'string' to 'EventId'`

**Causa**: `mediaFiles.Count` deveria ser `mediaFiles.Count()` (método LINQ, não propriedade)

**Correção**:
```csharp
// ANTES
_logger.LogInformation("PACKAGE: Found {MediaFilesCount} media files in storage for case {CaseId}", mediaFiles.Count, caseId);

// DEPOIS
_logger.LogInformation("PACKAGE: Found {MediaFilesCount} media files in storage for case {CaseId}", mediaFiles.Count(), caseId);
```

**Nota**: Este erro existe ANTES da implementação de emails e precisa ser corrigido para o build passar.

---

## 📁 Estrutura de Arquivos Esperada

Após implementação completa, a estrutura de um caso será:

```
cases/
└── {caseId}/
    ├── plan/
    │   ├── core.json
    │   ├── suspects.json
    │   ├── timeline.json
    │   └── evidence.json
    ├── expand/
    │   ├── timeline.json
    │   ├── suspects/
    │   │   ├── S001.json
    │   │   └── S002.json
    │   ├── evidence/
    │   │   ├── EV001.json
    │   │   └── EV002.json
    │   └── emails/          ← NOVO
    │       └── emails.json
    ├── design/
    │   ├── documents/
    │   │   ├── police_report.json
    │   │   └── interview.json
    │   ├── media/
    │   │   ├── crime_scene_photo.json
    │   │   └── evidence_photo.json
    │   └── emails/          ← NOVO
    │       └── emails.json
    ├── documents/
    │   └── @documents/
    │       ├── doc_police_report_001.json
    │       └── doc_interview_001.json
    ├── media/
    │   └── @media/
    │       ├── ev_crime_scene_001.png
    │       └── ev_evidence_001.png
    ├── emails/              ← NOVO
    │   ├── email_briefing_001.json
    │   └── email_update_002.json
    └── normalizedCaseBundle.json
```

---

## 🧪 Como Testar

### Teste Isolado (Tarefa 12)
```bash
# 1. Corrigir erro de build na linha 3852
# 2. Implementar Tarefas 3-5
# 3. Compilar projeto
cd backend/CaseGen.Functions
dotnet build

# 4. Executar Function
func start

# 5. Chamar endpoint de teste
POST http://localhost:7071/api/TestGenerateEmails
Content-Type: application/json

{
  "caseId": "CASE-20251109-a9bb62da"
}
```

### Teste Integrado (Tarefa 13)
```bash
# Gerar caso completo novo
POST http://localhost:7071/api/GenerateCaseFromPDF
# (com arquivo PDF)

# Verificar que emails foram gerados
# Verificar logs: DESIGN-EMAILS, EXPAND-EMAILS, NORMALIZE-EMAILS
# Verificar blob storage: design/emails/, expand/emails/, emails/
# Verificar normalizedCaseBundle.json contém array emails[]
```

### Teste Frontend (Tarefa 14)
```bash
# 1. Iniciar backend
cd backend/CaseZeroApi
dotnet run

# 2. Iniciar frontend
cd frontend
npm run dev

# 3. Abrir http://localhost:5173
# 4. Carregar caso com emails
# 5. Verificar Email Viewer exibe corretamente
# 6. Testar unlock de email gated
```

---

## 📚 Referências

- **Arquivos Modificados**: `CaseGenerationService.cs`, `NormalizerService.cs`, `CaseFormatService.cs`, `CaseEngine.ts`, `EmailViewer.tsx`
- **Modelos**: `NormalizedEmail` (record e class)
- **Pipeline**: Design → Expand → Normalize
- **Gating**: `GatingGraph`, `GatingRule`
- **Backward Compatibility**: `Array.Empty<NormalizedEmail>()` para casos antigos

---

## 💡 Notas Importantes

1. **Backward Compatibility**: Casos gerados antes da implementação retornarão `emails: []` vazio
2. **Timestamps**: SEMPRE usar ISO-8601 com timezone offset (ex: `-08:00` para PST)
3. **Attachments**: Usar referências `@documents/` no formato `@documents/doc_police_report_001`
4. **Gating**: Emails gated devem ter `gatingRule` não-null
5. **Retry Logic**: Todos os métodos IA devem ter 3 tentativas em caso de erro
6. **Logging**: Usar prefixos `DESIGN-EMAILS`, `EXPAND-EMAILS`, `NORMALIZE-EMAILS` para facilitar debug
7. **Validation**: Sempre validar campos required antes de salvar

---

## 🎯 Ordem Recomendada de Implementação

1. **Corrigir erro pré-existente** (linha 3852)
2. **Tarefas 3-5**: Implementar métodos de geração de emails no backend
3. **Tarefa 6**: Integrar na pipeline principal
4. **Tarefa 11**: Criar endpoint de teste
5. **Tarefa 12**: Testar geração isolada
6. **Tarefa 7**: Atualizar CaseFormatService
7. **Tarefas 8-10**: Implementar no frontend
8. **Tarefa 13**: Testar caso completo
9. **Tarefa 14**: Testar interface do usuário
10. **Tarefa 15**: Atualizar documentação

---

## ❓ Dúvidas ou Problemas

Se encontrar problemas durante a implementação:

1. Verificar logs no Azure Functions (prefixos `DESIGN-EMAILS`, `EXPAND-EMAILS`, `NORMALIZE-EMAILS`)
2. Verificar estrutura de pastas no Azure Blob Storage
3. Validar JSON gerado com schema esperado
4. Testar endpoint `TestGenerateEmails` isoladamente
5. Verificar se `NormalizerService.CreateNormalizedBundle` está lendo corretamente de `emails/` folder

---

**Data de Criação**: 2025-11-09  
**Versão**: 1.0  
**Status**: 🚧 Em Planejamento
