# 📋 Backlog Completo — CaseZero v1.0

> **⚠️ ESTRATÉGIA DE IMPLEMENTAÇÃO:**  
> Priorizar **refatoração** do código existente ao invés de recriar do zero.  
> **Legenda:** 🟢 Criar novo | 🟡 Adaptar existente | 🔵 Refatorar

> **🔒 PRINCÍPIO DE SEGURANÇA FUNDAMENTAL:**  
> O usuário NUNCA pode ter acesso a: solution, culpritId, assets/emails hidden não desbloqueados, ou regras do sistema.

---

## A) Contrato do Caso (Case Definition)

1. ✅ 🟡 **Adaptar** case.json v1 a partir do modelo existente (OBJETO_CASO.md):
   - ✅ Manter: metadata, suspects (já funcionais)
   - ✅ Renomear: evidences → assets
   - ✅ Adicionar: emails (integrar EMAIL_SYSTEM), rules, forensicsDefaults
   - ✅ Remover: unlockLogic antigo (substituir por visibility system)
   - ✅ **Implementado:** CASE_JSON_V1_SPEC.md completo, case_001 criado
2. ✅ 🟢 Definir regra de visibilidade: `visibility: initial | hidden` (sem "locked")
3. ✅ 🟡 **Adaptar** "email do chefe" do sistema atual (já gera briefing) como `visibility: initial` + anexos iniciais
   - **Implementado**: email.briefing_001 com visibility="initial" e attachments (briefing_doc, victim_photo)
4. ✅ 🟡 **Padronizar** IDs existentes (asset.*, email.*, rule.*, suspect.*) - já tem convenção parcial
5. ✅ 🟢 Criar `case.schema.json` (JSON Schema) para validar case.json v1
6. ✅ 🟡 **Converter** um caso existente para `case.sample.json` v1 - **case_001 criado**
7. ✅ 🟢 Implementar "case linter" local (valida IDs únicos, paths, attachments, regras) - **case-linter.js criado**

**Blob Storage Integration (Extra):**
- ✅ Implementado ICaseV1StorageService + CaseV1StorageService
- ✅ Criado CasesV1Controller com 5 endpoints REST
- ✅ Frontend adapter CaseV1 → CaseData
- ✅ Upload script (upload-case-to-blob.js)
- ✅ case_001 testado e funcionando no blob

---

## B) Persistência de Sessão (SQL)

8. ✅ 🔵 **Refatorar** CaseSessions existente (já tem UserId, CaseId, GameTime):
   - ✅ Adicionar: Status enum (active, paused, completed)
   - ✅ Já tem: GameTimeAtStart, GameTimeAtEnd (manter)
9. ✅ 🟢 Criar tabela `CaseSessionVisibleAssets` (UserId, CaseId, AssetId)
10. ✅ 🟢 Criar tabela `CaseSessionVisibleEmails` (UserId, CaseId, EmailId)
11. ✅ 🟢 Criar tabela `CaseSessionEmailState` (UserId, CaseId, EmailId, ReadAt, OpenCount)
12. ✅ 🟡 **Adaptar** ForensicAnalysis existente → ForensicsRequests:
    - ✅ Já tem: RequestId, UserId, CaseId, EvidenceId, AnalysisType, Status, RequestedAt, CompletedAt
    - ✅ Adicionar: ResultEmailId (referência ao email gerado)
    - ✅ Renomear: EvidenceId → InputAssetId, EvidenceName → InputAssetName
    - **Implementado**: Modelo refatorado, migration aplicada
13. ✅ 🟢 Criar tabela `EmailAttachmentsDownloaded` (UserId, CaseId, EmailId, AssetId, DownloadedAt)
    - **Implementado**: Tabela criada com foreign key para User, migration aplicada
14. ✅ 🟡 **Estender** migrations EF existentes e rodar local

---

## C) API — Casos e Sessões

15. ✅ **Refatorar** endpoint GET /api/case (já retorna lista):
    - Remover dados desnecessários (enviar só metadados)
    - **Implementado**: GET /api/cases retorna lista de casos com metadados
16. ✅ **Refatorar** endpoint GET /api/case/{caseId}:
    - Adicionar lógica "canAccess" baseada em User.Rank
    - **Implementado**: CaseAccessService filtra casos por rank do usuário
17. ✅ **Refatorar** POST /api/casesession/start existente:
    - ✅ Já cria sessão com GameTimeAtStart
    - ✅ Campo Status = SessionStatus.Active implementado
18. ✅ **Adicionar** lógica ao start:
    - 18a: ✅ Adaptar loader existente de case.json (CaseV1StorageService)
    - 18b: ✅ Criar `VisibilityService.ApplyInitialRules()`
    - 18c: ✅ Inserir em CaseSessionVisibleAssets/Emails
    - **Implementado**: VisibilityService filtra assets/emails com visibility="initial" e insere nas tabelas de visibilidade
19. ✅ Criar endpoint GET /api/cases/{caseId}/session (retorna estado completo)
    - **Implementado**: Retorna sessão atual/última + assets visíveis + emails visíveis + estados dos emails
20. ✅ **Refatorar** POST /api/casesession/end existente:
    - ✅ Já salva GameTimeAtEnd
    - ✅ Status = SessionStatus.Paused implementado
21. ✅ Criar endpoint POST /api/cases/{caseId}/resume (marca status active)
    - **Implementado**: Busca sessão pausada mais recente e marca como Active

---

## D) API — File Viewer (somente visível)

22. ✅ 🔵 **Refatorar** endpoint GET /api/evidence/{caseId}:
    - ✅ Renomear rota: GET /api/cases/{caseId}/assets
    - ✅ Filtrar por CaseSessionVisibleAssets (🔒 CRÍTICO)
    - ✅ Já retorna metadata (manter formato)
    - **Implementado**: AssetsController criado, retorna apenas assets visíveis na sessão ativa
23. ✅ 🟡 **Adaptar** endpoint de download de evidências:
    - ✅ Renomear: GET /api/cases/{caseId}/assets/{assetId}/download
    - ✅ Já usa Blob Storage (manter)
    - **Implementado**: Download com validação de visibilidade antes do stream
24. ✅ 🟢 **Adicionar** middleware de autorização:
    - ✅ Verificar se assetId está em CaseSessionVisibleAssets antes do download
    - **Implementado**: Validação integrada no endpoint de download (linha 109-115 AssetsController)
25. ✅ 🟢 Implementar cache in-memory do case.json (IMemoryCache do .NET)
    - **Implementado**: Cache no CaseV1StorageService.GetCaseRawAsync
    - Sliding expiration: 30 minutos
    - Absolute expiration: 2 horas
    - Cache key: case_v1_{caseId}

---

## E) API — Email App (somente visível)

26. ✅ 🟢 Criar endpoint GET /api/cases/{caseId}/emails:
    - ✅ Integrar com EMAIL_SYSTEM (modelo Email do CaseV1 usado)
    - ✅ Filtrar por CaseSessionVisibleEmails (🔒 CRÍTICO)
    - **Implementado**: EmailsController retorna emails visíveis com estados (lido, aberturas)
27. ✅ 🟢 Criar endpoint POST /api/cases/{caseId}/emails/{emailId}/open:
    - ✅ Inserir/atualizar CaseSessionEmailState (ReadAt, OpenCount)
    - **Implementado**: Marca primeira leitura e incrementa contagem de aberturas
28. ✅ 🟢 Criar endpoint GET /api/cases/{caseId}/emails/{emailId}:
    - ✅ Retornar email completo + attachments (refs para assets)
    - **Implementado**: Retorna conteúdo, attachments, metadata e estado de leitura
29. ✅ 🟡 **Adaptar** lógica de download de attachments:
    - ✅ Rota: POST /api/cases/{caseId}/emails/{emailId}/attachments/{assetId}/download
    - ✅ Usar Blob Storage existente
    - **Implementado**: Download com stream direto do blob
30. ✅ 🟢 **Adicionar** hook pós-download:
    - ✅ Inserir em EmailAttachmentsDownloaded
    - ⏳ Chamar `RulesEngine.ApplyRule("reveal_asset", assetId)` (TODO: implementar RulesEngine)
    - **Implementado**: Registro de download criado, reveal_asset logado
31. ✅ 🟢 **Adicionar** validação:
    - ✅ Verificar emailId em CaseSessionVisibleEmails antes de retornar attachments
    - **Implementado**: Validação integrada no endpoint de download

---

## F) Forensics — Submissão e Processamento (assíncrono)

32. ✅ 🔵 **Refatorar** endpoint POST /api/forensicrequest:
    - ✅ Já aceita evidenceId + analysisType → Renomeado para inputAssetId
    - ✅ Manter cálculo de duração baseado em GameTime
    - **Implementado**: Endpoint POST /api/forensicrequest criado
33. ✅ 🟢 **Adicionar** validação:
    - ✅ Verificar se inputAssetId está em CaseSessionVisibleAssets (🔒 CRÍTICO)
    - **Implementado**: Validação integrada antes de criar request
34. ✅ 🔵 **Adaptar** criação de registro (ForensicRequest modelo):
    - ✅ Já cria com status pending
    - ✅ Adicionar campo: ResultEmailId (NULL inicialmente)
    - **Implementado**: ForensicRequest criado com todos os campos
35. ✅ 🟢 **Adicionar** enfileiramento em Azure Storage Queue:
    - ✅ Criar serviço IForensicQueueService
    - ✅ Implementar ForensicQueueService com QueueClient
    - ✅ Usar mesma connection string que BlobStorageService (Azurite ou Azure)
    - ✅ Queue name: "forensic-requests"
    - ✅ Integrar no endpoint: após criar registro, enfileirar mensagem
    - **Implementado**: Serviço registrado como Singleton, mensagens enviadas à fila
36. ✅ 🟢 **Criar** Azure Function com Queue Trigger:
    - ✅ Projeto já existe: functions/CaseGen.Functions
    - ✅ Adicionar extensão Microsoft.Azure.Functions.Worker.Extensions.Storage.Queues
    - ✅ Criar ForensicProcessorFunction com [QueueTrigger("forensic-requests")]
    - ✅ Deserializar mensagem da fila (RequestId, CaseId, UserId, InputAssetId, AnalysisType)
    - ✅ Placeholder para tasks 37-40 (load case.json, rules engine, reveal_email, completion)
    - **Implementado**: Function criada, compila com sucesso, pronta para receber mensagens
37. ✅ 🔵 **Carregar** case.json e aplicar regras:
    - ✅ Injetar IStorageService na Function
    - ✅ LoadCaseJsonAsync: carregar case.json do blob storage (container "cases")
    - ✅ Deserializar com JsonSerializer
    - ✅ FindMatchingRule: buscar em rules.forensics[] por inputAssetId + analysisType
    - ✅ Logging detalhado de matching rule ou warning se não encontrar
    - **Implementado**: Function carrega case.json e encontra regra matching
38. ✅ 🟢 **Implementar** ação reveal_email:
    - ✅ Adicionar EF Core 9.0 ao projeto de Functions (compatível com .NET 9)
    - ✅ Criar modelos CaseSessionVisibleEmails e ForensicRequest (matching backend schema)
    - ✅ Criar ApplicationDbContext com DbSets e configuração de tabelas
    - ✅ Registrar DbContext em Program.cs com SQL Server provider + retry logic
    - ✅ Adicionar ConnectionStrings:DefaultConnection em local.settings.json
    - ✅ Injetar ApplicationDbContext em ForensicProcessorFunction
    - ✅ Implementar RevealEmailAsync: inserir emailId em CaseSessionVisibleEmails
    - ✅ Chamar RevealEmailAsync quando matchingRule.Action == "reveal_email"
    - **Implementado**: Function tem acesso SQL, insere email visível para o usuário
39. ✅ 🔵 **Adaptar** conclusão:
    - ✅ Já marca CompletedAt
    - ✅ Adicionar: Status = completed, ResultEmailId
    - ✅ Implementar UpdateForensicRequestStatusAsync
    - ✅ Buscar registro no banco, atualizar campos, salvar
    - **Implementado**: ForensicRequest atualizado com status completo após processamento
40. ✅ 🟢 Integrar SignalR notification para notificar cliente:
    - ✅ Adicionar Microsoft.Azure.Functions.Worker.Extensions.SignalRService
    - ✅ Implementar SendSignalRNotificationAsync method
    - ✅ Chamar após atualizar ForensicRequest status
    - ✅ Logging de notificação (evento ForensicResultReady)
    - 📝 TODO: Configurar Azure SignalR Service connection string
    - 📝 TODO: Implementar SignalR output binding (quando Azure SignalR Service estiver configurado)
    - 📝 TODO: Frontend conectar ao SignalR para receber notificações em tempo real
    - **Implementado**: Estrutura de notificação pronta, logs de evento, pendente configuração Azure SignalR Service

---

## G) Rules Engine v1 (mínimo viável)

41. ✅ 🟢 Criar `RulesEngineService`:
    - ✅ Interface IRulesEngineService criada
    - ✅ Método: `EvaluateForensicRuleAsync(caseId, inputAssetId, analysisType)` → ForensicRule | null
    - ✅ Carregar rules[] do case.json (usa ICaseV1StorageService.GetCaseJsonAsync)
    - ✅ Registrado como Scoped no Program.cs
    - **Implementado**: Serviço centralizado para avaliação de regras
42. ✅ 🟢 Implementar fallback:
    - ✅ Se EvaluateForensicRuleAsync retorna null → gerar email padrão "no findings"
    - ✅ Método: `GenerateNoFindingsEmailAsync(caseId, userId, inputAssetId, analysisType)`
    - ✅ Email salvo no blob storage em emails/{emailId}.json
    - ✅ Formato HTML com informações da análise
    - **Implementado**: Email "no findings" gerado automaticamente quando não há regra
43. ✅ 🟢 Implementar action `reveal_email`:
    - ✅ Método: `ApplyRevealEmailActionAsync(userId, caseId, emailId)`
    - ✅ INSERT INTO CaseSessionVisibleEmails se não existir
    - ✅ Verifica duplicatas antes de inserir
    - ✅ Logging detalhado de cada ação
    - **Implementado**: Revela emails para usuários conforme regras
44. ✅ 🟢 Implementar action `reveal_asset`:
    - ✅ Método: `ApplyRevealAssetActionAsync(userId, caseId, assetId)`
    - ✅ INSERT INTO CaseSessionVisibleAssets se não existir
    - ✅ Verifica duplicatas antes de inserir
    - ✅ Logging detalhado de cada ação
    - **Implementado**: Revela assets para usuários conforme regras
45. ✅ 🟢 Implementar action `add_email_attachment`:
    - ✅ Método: `ApplyAddEmailAttachmentActionAsync(caseId, emailId, assetId)`
    - ✅ Carrega email JSON do blob storage
    - ✅ Adiciona assetId ao array attachments[]
    - ✅ Salva email atualizado de volta ao blob storage
    - ✅ Verifica duplicatas antes de adicionar
    - **Implementado**: Adiciona anexos dinamicamente a emails existentes

**Métodos auxiliares adicionados ao ICaseV1StorageService:**
- ✅ `GetCaseJsonAsync(caseId)` - retorna case.json raw como string
- ✅ `GetEmailAsync(caseId, emailId)` - retorna email JSON como string
- ✅ `SaveEmailAsync(caseId, emailId, content)` - salva email JSON atualizado

---

## H) Frontend — Integração (Desktop)

46. ✅ 🔵 **Refatorar** lógica de start case:
    - ✅ Já chama /api/casesession/start (mantido)
    - ✅ Já redireciona pra /desktop (mantido)
    - ✅ Aguarda resposta do /session antes de renderizar (isLoadingSession state)
    - ✅ Loading screen enquanto inicializa sessão
    - **Implementado**: DesktopPage.tsx já contém toda a lógica necessária
47. ✅ 🔵 **Refatorar** Desktop.tsx (já existe):
    - ✅ Adicionar chamadas: GET /session, GET /assets, GET /emails no useEffect
    - ✅ Carregamento paralelo com Promise.all()
    - ✅ State management com useState (assets, emails, forensics)
    - ✅ Loading state e error handling
    - **Implementado**: Desktop agora busca dados automaticamente ao montar
48. ✅ 🔵 **Refatorar** FileViewer existente:
    - ✅ Trocar source: /api/evidence → /api/cases/{caseId}/assets
    - ✅ Remover lógica de items "locked" (deletados 500+ linhas de dados hardcoded)
    - ✅ Recebe assets via props do Desktop
    - ✅ Desktop wrapper injeta assets em FileViewer windows
    - ✅ UI simplificada: lista de assets com seleção e detalhes
    - **Implementado**: FileViewer usa API real e recebe dados via props
49. ✅ 🟢 Criar EmailApp component:
    - ✅ Lista emails de GET /emails (recebe via props do Desktop)
    - ✅ Ao clicar: POST /emails/{emailId}/open → GET /emails/{emailId}
    - ✅ Mostra from, to, subject, timestamp
    - ✅ Marca emails não lidos com borda azul
    - ✅ Renderiza body HTML com dangerouslySetInnerHTML
    - ✅ Mostra attachments com botão Download
    - **Implementado**: EmailApp completo com UI inbox/reader
50. ✅ 🟢 **Adicionar** lógica no EmailApp:
    - ✅ Botão "Download Attachment" → POST /attachments/{assetId}/download
    - ✅ Após success: refetch GET /assets (Desktop.refetchAssets())
    - ✅ Desktop wrapper injeta emails, caseId, onRefetchAssets
    - ✅ Loading states e disabled button durante download
    - **Implementado**: Download de attachments funcional com refetch automático
51. ✅ 🔵 **Adaptar** ForensicsQueue existente (já existe):
    - ✅ Adicionar polling: GET /forensicrequest/{caseId}/pending (30s)
    - ✅ Quando completar: refetch GET /emails
    - ✅ Implementado no Desktop.tsx useEffect com setInterval
    - **Implementado**: Polling automático a cada 30s, refetch emails quando status=completed
52. ✅ 🟡 (Opcional) SignalR Hub: adaptar se existe, criar se não
    - ✅ Integrado forensicsSignalR service existente
    - ✅ Conecta ao hub /hubs/forensics com token
    - ✅ Escuta evento 'ForensicCompleted'
    - ✅ Refetch forensics + emails ao receber notificação
    - ✅ Fallback automático para polling se SignalR falhar
    - ✅ Cleanup adequado (disconnect no unmount)
    - **Implementado**: Real-time updates via SignalR com fallback para polling

**Section H - Frontend Integration: COMPLETA ✅**
- Todas as tasks implementadas (46-52)
- Desktop carrega dados automaticamente (assets, emails, forensics)
- FileViewer usa API /assets com props injection
- EmailApp completo com inbox/reader e download de attachments
- Real-time updates via SignalR ou polling fallback

**Notas de implementação Tasks 47-48-51:**
- Desktop.tsx agora é o centro de controle de dados
- Três useEffect: mount (desktop-mode class), data loading, forensics polling
- Estado: assets[], emails[], forensics[], loading
- handleOpenWindow() wrapper para injetar props em componentes (FileViewer recebe assets)
- Polling inteligente: detecta forensic completed → refetch emails
- API services criados: assetsApi, emailsApi, forensicsApi com DTOs completos

---

## I) Entrar/Sair e Persistência de UI

53. 🟡 **Verificar** se WindowContext já persiste posição:
    - Se sim: manter. Se não: adicionar localStorage
54. 🟢 **Adicionar** persistência de apps abertos:
    - Salvar em localStorage: `caseApps_${caseId}` = ["FileViewer", "EmailApp"]
    - Restaurar no Desktop.tsx ao carregar
55. 🔵 **Garantir** comportamento de /casesession/end:
    - Já salva GameTimeAtEnd (manter)
    - Já não deleta sessão (verificar)
    - Adicionar: Status = paused

---

## J) Observabilidade e Segurança

56. Logging: logar (UserId, CaseId, Action) para openEmail, download, forensics submit
57. Proteger downloads com autorização + check de visibilidade
58. Definir limites (rate limit) para forensics submit (anti-spam)
59. Telemetria básica: tempo no caso, emails abertos, downloads

---

## K) Testes (Funcionalidade + Segurança)

### Testes Funcionais Básicos
60. ✅ Teste: start case cria sessão com email do chefe visível
    - **Implementado**: SecurityIntegrationTests.StartCase_CreatesSessionWithInitialEmail
    - Verifica se sessão é criada com Status=Active
    - Valida que email.briefing está visível após start
61. ✅ Teste: asset hidden não aparece em /assets
    - **Implementado**: SecurityIntegrationTests.GetAssets_HiddenAsset_NotReturned
    - Adiciona apenas asset visível
    - Verifica que assets hidden não aparecem na lista
62. ✅ Teste: baixar attachment revela asset
    - **Implementado**: SecurityIntegrationTests.DownloadAttachment_RevealsAsset
    - Verifica que download é registrado em EmailAttachmentsDownloaded
    - Hook pós-download implementado no EmailsController
    - 📝 TODO: Integrar RulesEngine.ApplyRule("reveal_asset") no endpoint
63. ✅ Teste: forensics sem regra gera email "no findings"
    - **Implementado**: SecurityIntegrationTests.ForensicsWithoutRule_GeneratesNoFindingsEmail
    - Cria case sem regras de forensics (rules: [])
    - Chama RulesEngineService.GenerateNoFindingsEmailAsync
    - Verifica que email com ID "no-findings-{guid}" é criado
    - Verifica que email é salvo em blob storage ({caseId}/emails/{emailId}.json)
    - Valida conteúdo: from="forensics@casezero.system", subject contém "No Findings"
64. ✅ Teste: forensics com regra gera email com attachment
    - **Implementado**: SecurityIntegrationTests.ForensicsWithRule_GeneratesEmailWithAttachment
    - Cria case COM regra de forensics que revela email quando análise completa
    - Email no case.json tem attachment (dna_results.pdf)
    - Simula processamento chamando ApplyRevealEmailActionAsync
    - Verifica que email foi adicionado a CaseSessionVisibleEmails
    - Valida estrutura do email no case.json com attachments
    - 📝 TODO: Integrar avaliação automática de regras na Azure Function
65. ✅ Teste: usuário não consegue baixar asset não visível
    - **Implementado**: SecurityIntegrationTests.DownloadAsset_InvisibleAsset_ReturnsForbidden
    - Tenta baixar asset que NÃO está em CaseSessionVisibleAssets
    - Verifica retorno 403 Forbidden

### Testes de Segurança (CRÍTICOS) 🔒
105. ✅ **Teste anti-spoiler**:
     - **Implementado**: SecurityIntegrationTests.GetAssets_NeverExposeSensitiveData
     - GET /api/cases/{caseId}/assets sem filtro → só visíveis
     - Verifica que response NÃO contém: "solution", "culprit", "rules", "answer", "hidden"
     - Valida que apenas assets visíveis retornam
     - Acessar asset.hidden_xxx diretamente → 403
106. ✅ **Teste sanitização**:
     - **Implementado**: SecurityIntegrationTests.GetCaseSession_NeverExposeSolution
     - Buscar "solution" ou "culpritId" em response → NUNCA deve aparecer
     - Verificar que rules[] não são expostas
107. ✅ **Teste autorização**:
     - **Implementado**: SecurityIntegrationTests.GetAssets_DifferentUser_ReturnsForbidden
     - User A tenta acessar sessão de User B → 403
     - User sem rank tenta caso avançado → 403
108. 🟡 **Teste rate limiting**:
     - 11 forensics requests em 1h → 11ª retorna 429
     - 101 email opens em 1h → 101ª retorna 429
109. 🟡 **Teste integridade**:
     - Modificar asset no Blob → checksum deve falhar
     - Enviar assetId malformado → 400
110. 🟡 **Teste concurrency**:
     - Abrir caso em 2 tabs → apenas 1 "primária"
     - Forensics simultâneos → apenas 1 processa

**NOVO: Testes de Segurança Implementados** (30/12/2025)
- ✅ Arquivo criado: `backend/CaseZeroApi.IntegrationTests/SecurityIntegrationTests.cs`
- ✅ Framework: xUnit + WebApplicationFactory + InMemory Database
- ✅ 6 testes críticos implementados (Tasks 60, 61, 65, 105-107)
- ✅ Compilação: Sucesso
- ⏳ Pendente: Rodar testes contra API real e verificar falhas
- ⏳ Próximo passo: Implementar correções de segurança se testes falharem

---

## L) Infraestrutura e Deploy

66. Definir estrutura de pastas no Blob: `/cases/{caseId}/assets/{assetId}`
67. Criar Azure Resources (App Service, Storage, SQL, Queue)
68. Setup de CI/CD (build + deploy)
69. Configuração de ambientes (dev/staging/prod)

---

## M) Error Handling e Resilience

70. Implementar retry policy na Queue Function (exponential backoff)
71. Timeout para forensics (15min) → auto-fail com email padrão
72. Validação robusta de case.json no load (try-catch + logging)
73. Fallback se asset não existe no Blob (404 gracioso)

---

## N) Cache Strategy

74. Definir TTL e invalidation strategy para case.json cache
75. Implementar cache de sessões (Redis ou in-memory)
76. Cache de "assets visíveis" por sessão (evitar query SQL repetida)

---

## O) Monitoring e Alerting

77. Application Insights com custom metrics (forensics duration, email opens)
78. Alert: forensics pendente > 15min
79. Alert: caso corrompido (parse fail)
80. Dashboard no Azure com KPIs (casos ativos, completion rate)

---

## P) 🔒 SEGURANÇA E ANTI-SPOILER (CRÍTICO)

### P81-P85: Segurança Básica (Sprint 0 - OBRIGATÓRIO antes de qualquer dev)

81. 🟢 **Criar middleware AuthorizationFilter para TODOS os endpoints**:
    ```csharp
    // Verificar JWT válido
    // Verificar UserId do token vs. UserId na sessão
    // Retornar 403 Forbidden se não autorizado
    ```

82. 🟢 **Implementar VisibilityGuard antes de QUALQUER retorno de dados**:
    ```csharp
    // Assets: Verificar AssetId em CaseSessionVisibleAssets
    // Emails: Verificar EmailId em CaseSessionVisibleEmails
    // Forensics: Verificar inputAssetId visível antes de aceitar request
    // NUNCA retornar listas completas - SEMPRE filtrar por sessão
    ```

83. 🟢 **Sanitizar case.json antes de enviar ao cliente** (🔒 CRÍTICO):
    ```csharp
    // Criar método: SanitizeCaseForClient(case, sessionId)
    // REMOVER OBRIGATORIAMENTE:
    // - solution, solutionStub, culpritId, qualquer campo com "answer"
    // - Todos os assets/emails com visibility:hidden não desbloqueados
    // - rules[] completas (processar server-side APENAS)
    ```

84. 🟢 **Implementar rate limiting específico anti-brute-force**:
    ```
    - Forensics submission: max 10 requests/hora por usuário
    - Email open: max 100 opens/hora
    - Asset download: max 50 downloads/hora
    - Solution submission: max 3 tentativas por caso
    ```

85. 🟢 **Validar integridade de IDs antes de queries**:
    ```csharp
    // Verificar formato: asset.xxx, email.xxx, suspect.xxx
    // Prevenir SQL injection via parametrização EF
    // Rejeitar IDs malformados com 400 Bad Request
    ```

### P86-P90: Segurança Avançada (Sprint 1)

86. 🟢 **Implementar audit log para ações sensíveis**:
    ```sql
    CREATE TABLE AuditLog (
        Timestamp, UserId, CaseId, 
        Action, ResourceId, IpAddress
    )
    -- Logar: asset_download, email_open, forensics_submit, solution_submit
    -- Detectar padrões suspeitos (100 requests em 1 min = ataque)
    ```

87. 🟢 **Adicionar checksum validation no download de assets**:
    ```csharp
    // Salvar hash SHA256 de cada asset no case.json
    // Verificar integridade antes de servir (prevenir tampering)
    ```

88. 🟢 **Implementar CORS restritivo**:
    ```csharp
    // Dev: localhost:3000
    // Prod: domínio específico
    // Bloquear wildcards (*) em produção
    ```

89. 🟢 **Proteger endpoints de administração**:
    ```csharp
    [Authorize(Roles = "Admin")] // Geração de caso
    // Verificar rank mínimo para casos específicos
    // Bloqueio de casos até aprovação administrativa
    ```

90. 🟢 **Implementar Content Security Policy headers**:
    ```csharp
    // Verificar headers existentes funcionando
    // Adicionar: X-Case-Sanitized: true em respostas com case.json
    ```

---

## Q) Performance e Cache Strategy (Responde Perguntas Técnicas)

91. 🟢 **Definir TTL para cache de case.json**:
    ```csharp
    // TTL: 30 minutos (casos raramente mudam durante jogo)
    // Usar IMemoryCache do .NET com sliding expiration
    // Key pattern: case_{caseId}_json
    ```

92. 🟢 **Implementar cache invalidation strategy**:
    ```csharp
    // Quando case.json atualizado: _cache.Remove($"case_{caseId}_json")
    // Endpoint admin: POST /api/admin/cache/invalidate/{caseId}
    // Auto-invalidação ao detectar ETag diferente no Blob
    ```

93. 🟢 **Implementar cache de sessões ativas**:
    ```csharp
    // Cache in-memory de CaseSession por (UserId, CaseId)
    // TTL: 5 minutos (dados mudam com frequência)
    // Invalidar ao: start, end, resume, visibility change
    ```

94. 🟢 **Implementar cache de visibilidade**:
    ```csharp
    // Cache: visible_assets_{userId}_{caseId} e visible_emails_{userId}_{caseId}
    // TTL: 2 minutos (pode mudar com forensics/downloads)
    // Invalidar ao: reveal_asset, reveal_email, attachment_download
    ```

95. 🟢 **Otimizar queries SQL com índices**:
    ```sql
    -- Índice composto: (UserId, CaseId) em tabelas de sessão
    -- Índice: EmailId, AssetId em tabelas de visibilidade
    -- Índice: Status em ForensicsRequests (queries de pending)
    ```

96. 🟢 **Implementar batching de queries**:
    ```csharp
    // Ao carregar Desktop: 1 query assets + 1 query emails (evitar N+1)
    // Usar Include() do EF para carregar relacionamentos
    ```

97. 🟢 **Comprimir respostas HTTP**:
    ```csharp
    // Habilitar Gzip/Brotli para JSON responses
    // Especialmente importante para case.json (pode ser grande)
    ```

---

## R) Concurrency e State Sync (Multi-tab/Multi-device)

98. 🟢 **Implementar locking pessimista para ações críticas**:
    ```csharp
    // Ao enviar forensics: verificar se já não há request pendente
    // com mesmo (inputAssetId, analysisType)
    // Usar transaction isolation level ReadCommitted
    ```

99. 🟢 **Adicionar versioning para detectar conflitos**:
    ```csharp
    // Campo Version (rowversion) em CaseSession
    // Ao atualizar: verificar se Version não mudou desde leitura
    // Se mudou: retornar 409 Conflict com dados atualizados
    ```

100. 🟢 **Implementar heartbeat para detecção de sessões ativas**:
     ```csharp
     // Endpoint: POST /api/cases/{caseId}/heartbeat (chamar cada 30s)
     // Atualizar CaseSession.LastHeartbeat
     // Frontend: detectar se LastHeartbeat outra tab < 1min
     ```

101. 🟢 **Criar estratégia de sincronização entre tabs**:
     ```javascript
     // Usar BroadcastChannel API (navegador) para comunicação local
     // Tab A faz action: broadcast para outras tabs do mesmo caso
     // Tabs B/C recebem: refetch dados (assets, emails, forensics)
     ```

102. 🟢 **Adicionar avisos de conflito no frontend**:
     ```javascript
     // Se detectar outra tab ativa: toast "Caso aberto em outra aba"
     // Desabilitar ações se não for tab "primária"
     // Opção: "Assumir controle" (torna esta tab primária)
     ```

103. 🟢 **Prevenir race conditions em forensics**:
     ```csharp
     // Queue idempotente (verificar se email já revelado antes de revelar)
     // Se 2 requests simultâneos: segundo detecta já processado
     ```

104. 🟢 **Implementar soft-locking de assets durante download**:
     ```csharp
     // Prevenir download simultâneo do mesmo asset (double-reveal)
     // Lock por 30s durante download
     // Outro request chega: aguardar ou 429 Too Many Requests
     ```

---

## 🚀 Priorização por Sprints

### Sprint 0 - Spike/Proof of Concept + Segurança Básica
- **Task 0**: Validar arquitetura end-to-end
  - Case.json mínimo
  - 1 email inicial visível
  - 1 forensics request que revela 1 novo email
  - Provar ciclo completo funciona
- **P81-P85**: Implementar segurança básica (🔒 CRÍTICO antes de qualquer dev)

### Sprint 1 - Foundation (Adaptar Base Existente)
- A1-A7 (contrato + validação) - 🟡 Maioria adaptações
- B8-B14 (SQL schema) - 🔵 Refatorar tabelas existentes + 4 novas
- L66-L67 (infra) - 🟡 Blob Storage já existe, adicionar Queue
- **P86-P90**: Completar segurança + anti-spoiler
- **Q91-Q94**: Implementar cache strategy

### Sprint 2 - Core API (Refatorar Controllers Existentes)
- C15-C21 (casos e sessões) - 🔵 70% refatoração, 30% novo
- D22-D25 (file viewer) - 🔵 Adaptar endpoints de Evidence
- M72 (error handling básico) - 🟢 Novo
- **Q95-Q97**: Otimizações de performance
- **R98-R100**: Locking e versioning

### Sprint 3 - Email + Forensics (Estender Sistema Existente)
- E26-E31 (email app) - 🟢 Novo (integrar EMAIL_SYSTEM_IMPLEMENTATION)
- F32-F40 (forensics completo) - 🔵 Estender ForensicAnalysis + Queue
- G41-G45 (rules engine) - 🟢 Novo service
- **R103-R104**: Anti-race conditions

### Sprint 4 - Frontend + Polish (Adaptar Componentes Existentes)
- H46-H52 (integração) - 🔵 80% adaptar Desktop/FileViewer/ForensicsQueue
- K60-K65 (testes funcionais) - 🟢 Novo
- J56-J59 (observabilidade) - 🟡 Estender logging
- **R101-R102**: Sincronização multi-tab

### Sprint 5 - Segurança Final + Testes de Penetração (OBRIGATÓRIO)
- **K105-K110**: Testes de segurança (🔒 CRÍTICO antes de produção)
- **Penetration testing**: OWASP ZAP ou auditoria externa
- **Load testing**: 100 usuários simultâneos (verificar rate limiting)
- **Validação manual**: Buscar "solution", "culpritId", "rules" em responses

---

## 📌 Dependências Críticas

```
P81-P85 → TUDO               Segurança básica ANTES de qualquer endpoint
A → B,C,D,E,F                Todas APIs dependem do case.json finalizado
B → C,D,E,F                  Todas APIs dependem do schema SQL
P83 → C,D,E,F                Sanitização ANTES de enviar dados ao cliente
C,D,E → H                    Frontend depende das APIs funcionando
E → F                        Forensics depende de emails funcionando
F → G                        Processamento depende do rules engine
G → P83                      Rules Engine server-side APENAS (nunca expor)
Q91-Q94 → Performance        Cache ANTES de testes de carga
R98-R104 → Produção          Concurrency ANTES de múltiplos usuários
L66-67 → Tudo                Infraestrutura ANTES de deploy
```

---

## ⚠️ Pontos de Atenção (RESOLVIDOS)

### Error Handling
✅ **Resolvido em M70-M73**: Retry logic, timeout, validação robusta

### Performance
✅ **Resolvido em Q91**: Cache case.json 30min  
✅ **Resolvido em Q92**: Invalidação via ETag + endpoint admin  
✅ **Resolvido em Q93-Q94**: Cache sessões (5min) e visibilidade (2min)

### Concurrency
✅ **Resolvido em R100-R102**: Heartbeat + BroadcastChannel multi-tab  
✅ **Resolvido em R98-R99**: Locking pessimista + versioning (409 Conflict)

### Blob Storage
✅ **Estrutura**: `/cases/{caseId}/assets/{assetId}` (padrão CaseGen)  
✅ **Nomenclatura**: Extensões originais + hash SHA256 (P87)

### State Management
✅ **CaseEngine permanece** (adaptar, não trocar)  
✅ **Sincronização**: BroadcastChannel (R101) + polling inteligente

---

## 🔄 Código Existente que Será Reutilizado

### Backend (já funcional)
- ✅ CaseSession (tabela + controller + endpoints)
- ✅ ForensicAnalysis (tabela + controller + API completa)
- ✅ GameTime Engine (tempo acelerado + durações + persistência)
- ✅ Blob Storage Service (upload/download de assets)
- ✅ Case Service (carrega case.json do Blob)
- ✅ AspNetUsers + Identity (autenticação JWT + rank system)
- ✅ Evidence/Suspects (tabelas e modelos - renomear para Assets)

### Frontend (já funcional)
- ✅ Desktop.tsx (ambiente desktop com janelas)
- ✅ WindowContext (gerenciamento de janelas)
- ✅ TimeContext (game time ticker)
- ✅ CaseEngine (game state manager)
- ✅ ForensicsQueue (UI de análises forenses)
- ✅ FileViewer (listagem de evidências - adaptar para assets)
- ✅ AuthContext (login/logout/JWT)

### CaseGen.Functions (geração AI)
- ✅ Pipeline completa de 6 fases (plan → expand → design → generate → validate)
- ✅ PdfRenderingService (7 tipos de documentos)
- ✅ LLMService (GPT-4o integration)
- ✅ ImagesService (DALL-E 3)
- ✅ StorageService (Blob + Table Storage)
- ✅ EMAIL_SYSTEM (parcialmente implementado - completar tasks 3-15)

---

## 🔒 Checklist de Deploy (Segurança)

Antes de ir para produção:

- [ ] **P81-P85** implementados e testados (AuthorizationFilter, VisibilityGuard, Sanitização)
- [ ] **P86-P90** implementados (Audit log, Checksum, CORS, CSP)
- [ ] **K105-K110** passando 100% (Testes de segurança)
- [ ] Penetration testing realizado (OWASP ZAP ou auditoria)
- [ ] Load testing com 100 usuários (rate limiting funcionando)
- [ ] Validação manual: buscar "solution", "culpritId", "rules" em todas as responses
- [ ] CORS configurado (sem wildcards)
- [ ] Audit log gravando todas as ações sensíveis
- [ ] Checksum de assets validado
- [ ] Rate limiting testado em todos os endpoints críticos

---

## 🚨 Red Flags - NUNCA FAZER EM PRODUÇÃO

```csharp
// ❌ 1. NUNCA retornar lista completa sem filtro
return _db.Assets.Where(a => a.CaseId == caseId).ToList(); // ERRADO
return _db.Assets.Where(a => a.CaseId == caseId && 
    visibleAssets.Contains(a.Id)).ToList(); // CERTO

// ❌ 2. NUNCA enviar case.json bruto
return caseJson; // ERRADO
return SanitizeCaseForClient(caseJson, sessionId); // CERTO

// ❌ 3. NUNCA expor regras ao cliente
return new { case, rules }; // ERRADO
// Rules devem ficar 100% no backend

// ❌ 4. NUNCA confiar em dados do cliente
var assetId = request.AssetId;
return GetAsset(assetId); // ERRADO
if (IsVisible(userId, assetId)) return GetAsset(assetId); // CERTO

// ❌ 5. NUNCA retornar campos sensíveis
return new { 
    case.Metadata, 
    case.Solution, // PROIBIDO
    case.CulpritId // PROIBIDO
};
```

---

## 🎯 Diagrama de Segurança

```
┌─────────────────────────────────────────────────┐
│  Cliente (NUNCA confiar)                        │
│  ├─ Solicita asset "hidden_xxx"                 │
│  └─ Tenta acessar sessão de outro usuário      │
└──────────────────┬──────────────────────────────┘
                   │
                   ▼
┌─────────────────────────────────────────────────┐
│  AuthorizationFilter (P81)                      │
│  ├─ Valida JWT                                  │
│  ├─ Verifica UserId do token                   │
│  └─ 403 Forbidden se não autorizado            │
└──────────────────┬──────────────────────────────┘
                   │
                   ▼
┌─────────────────────────────────────────────────┐
│  VisibilityGuard (P82)                          │
│  ├─ Busca CaseSessionVisibleAssets             │
│  ├─ Verifica se "hidden_xxx" está na lista     │
│  └─ NÃO ESTÁ → 403 Forbidden                   │
└──────────────────┬──────────────────────────────┘
                   │
                   ▼
┌─────────────────────────────────────────────────┐
│  SanitizeCaseForClient (P83)                    │
│  ├─ Remove: solution, culpritId, rules[]       │
│  ├─ Remove: assets/emails hidden não revelados │
│  └─ Retorna: case.json SEGURO                  │
└─────────────────────────────────────────────────┘
                   │
                   ▼
         ✅ Cliente recebe APENAS
            dados autorizados
```

---

## 📊 Resumo Executivo

**Total de Tarefas:** 110  
**Novas:** ~35 (🟢)  
**Adaptações:** ~25 (🟡)  
**Refatorações:** ~20 (🔵)  
**Reutilização:** ~30 (código existente)

**Sprints Estimados:** 5  
**Prioridade #1:** Segurança (P81-P90, K105-K110)  
**Risco Crítico:** Exposição de solution/rules ao cliente

**Próximos Passos:**
1. Validar arquitetura com spike (Sprint 0)
2. Implementar P81-P85 (segurança básica)
3. Refatorar modelo de dados (Sprint 1)
4. Testes de segurança contínuos (K105-K110)
