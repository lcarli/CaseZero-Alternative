# Plano de Implementação: Contexto Hierárquico com Referências

## 🎯 Objetivo
Refatorar o pipeline de geração de casos para usar contexto granular e referências, reduzindo tokens em 80-90% e aumentando precisão.

---

## 📋 FASE 1: FUNDAÇÃO - Context Manager (1-2 dias)

### Task 1.1: Criar interface IContextManager
- [ ] Arquivo: `Services/IContextManager.cs`
- [ ] Métodos:
  - `SaveContextAsync<T>(caseId, path, data)`
  - `LoadContextAsync<T>(caseId, path)`
  - `QueryContextAsync<T>(caseId, query)`
  - `BuildSnapshotAsync(caseId, paths[])`
  - `DeleteContextAsync(caseId, path)`
- [ ] Adicionar modelos: `ContextSnapshot`, `ContextReference`

### Task 1.2: Implementar ContextManager com Azure Blob Storage
- [ ] Arquivo: `Services/ContextManager.cs`
- [ ] Estrutura de pastas no Blob:
  ```
  /{caseId}/context/
    ├─ plan/core.json
    ├─ plan/suspects.json
    ├─ plan/timeline.json
    ├─ plan/evidence.json
    ├─ expand/
    │   ├─ suspects/{suspectId}.json
    │   ├─ evidence/{evidenceId}.json
    │   └─ timeline.json
    ├─ design/
    │   ├─ documents/{docType}.json
    │   └─ media/{mediaType}.json
    ├─ entities/
    │   ├─ suspects/{id}.json
    │   ├─ evidence/{id}.json
    │   └─ witnesses/{id}.json
    └─ metadata.json (índice com todas as referências)
  ```
- [ ] Implementar serialização/deserialização JSON
- [ ] Implementar cache em memória (Dictionary com TTL)
- [ ] Error handling e retry logic

### Task 1.3: Criar modelos de referência
- [ ] Arquivo: `Models/ContextModels.cs`
- [ ] Classes:
  - `ContextReference` - "@suspects/S001"
  - `ContextSnapshot` - conjunto de dados carregados
  - `EntityReference` - referência tipada (Suspect, Evidence, etc)
  - `ContextMetadata` - índice de todas as entidades

### Task 1.4: Registrar serviço no DI
- [ ] Arquivo: `Program.cs`
- [ ] Adicionar `services.AddSingleton<IContextManager, ContextManager>()`
- [ ] Configurar Azure Blob connection string

### Task 1.5: Criar testes unitários
- [ ] Arquivo: `CaseGen.Functions.Tests/Services/ContextManagerTests.cs`
- [ ] Testar: Save, Load, Query, Snapshot, Delete
- [ ] Mock do BlobServiceClient

---

## 📋 FASE 2: REFATORAR PLAN (Dividir em 4 sub-steps) (2-3 dias)

### Task 2.1: Criar PlanCoreActivity
- [ ] Arquivo: `Functions/Activities/Plan/PlanCoreActivity.cs`
- [ ] Input: `CaseGenerationRequest` (difficulty, timezone)
- [ ] Output: Salvar em `/{caseId}/context/plan/core.json`
- [ ] Conteúdo: Crime, vítima, culpado, localização, data (200-300 tokens)
- [ ] Prompt: Focado apenas no núcleo do caso

### Task 2.2: Criar PlanSuspectsActivity
- [ ] Arquivo: `Functions/Activities/Plan/PlanSuspectsActivity.cs`
- [ ] Input: Contexto de `plan/core.json`
- [ ] Output: Salvar em `/{caseId}/context/plan/suspects.json`
- [ ] Conteúdo: Lista de suspeitos com relações básicas (300-400 tokens)
- [ ] Cada suspeito: id, nome, idade, relação com vítima, motivo potencial

### Task 2.3: Criar PlanTimelineActivity
- [ ] Arquivo: `Functions/Activities/Plan/PlanTimelineActivity.cs`
- [ ] Input: Contexto de `plan/core.json` + `plan/suspects.json`
- [ ] Output: Salvar em `/{caseId}/context/plan/timeline.json`
- [ ] Conteúdo: Timeline macro (antes/durante/depois do crime) (150-250 tokens)

### Task 2.4: Criar PlanEvidenceActivity
- [ ] Arquivo: `Functions/Activities/Plan/PlanEvidenceActivity.cs`
- [ ] Input: Contexto de `plan/core.json` + `plan/suspects.json`
- [ ] Output: Salvar em `/{caseId}/context/plan/evidence.json`
- [ ] Conteúdo: Lista de evidências-chave com tipo e relevância (200-300 tokens)

### Task 2.5: Atualizar Orchestrator - substituir PlanActivity
- [ ] Arquivo: `Functions/CaseGeneratorOrchestrator.cs`
- [ ] Remover: `CallActivityAsync("PlanActivity")`
- [ ] Adicionar sequência:
  ```csharp
  await context.CallActivityAsync("PlanCoreActivity", ...);
  await context.CallActivityAsync("PlanSuspectsActivity", ...);
  await context.CallActivityAsync("PlanTimelineActivity", ...);
  await context.CallActivityAsync("PlanEvidenceActivity", ...);
  ```

---

## 📋 FASE 3: REFATORAR EXPAND (Paralelização granular) (3-4 dias)

### Task 3.1: Criar ExpandSuspectActivity
- [ ] Arquivo: `Functions/Activities/Expand/ExpandSuspectActivity.cs`
- [ ] Input: `caseId`, `suspectId`
- [ ] Carrega: `plan/core.json` + `plan/suspects.json` (apenas este suspeito)
- [ ] Output: Salvar em `/{caseId}/context/expand/suspects/{suspectId}.json`
- [ ] Conteúdo expandido: Background, alibi, comportamento, testemunhos (300-500 tokens)
- [ ] Fan-out: Um activity por suspeito (paralelizado)

### Task 3.2: Criar ExpandEvidenceActivity
- [ ] Arquivo: `Functions/Activities/Expand/ExpandEvidenceActivity.cs`
- [ ] Input: `caseId`, `evidenceId`
- [ ] Carrega: `plan/core.json` + `plan/evidence.json` (apenas esta evidência)
- [ ] Output: Salvar em `/{caseId}/context/expand/evidence/{evidenceId}.json`
- [ ] Conteúdo expandido: Descrição detalhada, cadeia de custódia, análise (200-400 tokens)
- [ ] Fan-out: Um activity por evidência (paralelizado)

### Task 3.3: Criar ExpandTimelineActivity
- [ ] Arquivo: `Functions/Activities/Expand/ExpandTimelineActivity.cs`
- [ ] Input: `caseId`
- [ ] Carrega: `plan/timeline.json` + todos `expand/suspects/*.json`
- [ ] Output: Salvar em `/{caseId}/context/expand/timeline.json`
- [ ] Conteúdo: Timeline detalhada com horários específicos e ações (500-800 tokens)

### Task 3.4: Criar SynthesizeRelationsActivity
- [ ] Arquivo: `Functions/Activities/Expand/SynthesizeRelationsActivity.cs`
- [ ] Input: `caseId`
- [ ] Carrega: Metadata de todos suspects e evidence
- [ ] Output: Salvar em `/{caseId}/context/expand/relations.json`
- [ ] Conteúdo: Matriz de relações (quem conhece quem, quem tem acesso a qual evidência)

### Task 3.5: Atualizar Orchestrator - substituir ExpandActivity
- [ ] Remover: `CallActivityAsync("ExpandActivity")`
- [ ] Adicionar fan-out paralelo:
  ```csharp
  // Expandir suspeitos em paralelo
  var expandSuspectTasks = suspectIds.Select(id => 
    context.CallActivityAsync("ExpandSuspectActivity", new { caseId, suspectId = id })
  );
  await Task.WhenAll(expandSuspectTasks);
  
  // Expandir evidências em paralelo
  var expandEvidenceTasks = evidenceIds.Select(id => 
    context.CallActivityAsync("ExpandEvidenceActivity", new { caseId, evidenceId = id })
  );
  await Task.WhenAll(expandEvidenceTasks);
  
  // Timeline e relações (dependem dos anteriores)
  await context.CallActivityAsync("ExpandTimelineActivity", caseId);
  await context.CallActivityAsync("SynthesizeRelationsActivity", caseId);
  ```

---

## 📋 FASE 4: REFATORAR DESIGN (Por tipo de artefato) (2-3 dias)

### Task 4.1: Criar DesignDocumentTypeActivity
- [ ] Arquivo: `Functions/Activities/Design/DesignDocumentTypeActivity.cs`
- [ ] Input: `caseId`, `docType` (ex: "police_report", "interview", "forensic_report")
- [ ] Carrega: Apenas contexto relevante para este tipo
  - Police report: core + timeline
  - Interview: core + suspect específico
  - Forensic: core + evidence específica
- [ ] Output: Salvar em `/{caseId}/context/design/documents/{docType}.json`
- [ ] Conteúdo: Especificação do documento com campos e estrutura

### Task 4.2: Criar DesignMediaTypeActivity
- [ ] Arquivo: `Functions/Activities/Design/DesignMediaTypeActivity.cs`
- [ ] Input: `caseId`, `mediaType` (ex: "crime_scene_photo", "mugshot", "evidence_photo")
- [ ] Carrega: Apenas contexto relevante
  - Crime scene: core + timeline
  - Mugshot: suspect específico
  - Evidence: evidence específica
- [ ] Output: Salvar em `/{caseId}/context/design/media/{mediaType}.json`
- [ ] Conteúdo: Prompt para geração de imagem

### Task 4.3: Atualizar Orchestrator - substituir DesignActivity
- [ ] Remover: `CallActivityAsync("DesignActivity")`
- [ ] Adicionar fan-out por tipo:
  ```csharp
  var docTypes = new[] { "police_report", "interview", "forensic_report", ... };
  var designDocTasks = docTypes.Select(type => 
    context.CallActivityAsync("DesignDocumentTypeActivity", new { caseId, docType = type })
  );
  
  var mediaTypes = new[] { "crime_scene_photo", "mugshot", "evidence_photo", ... };
  var designMediaTasks = mediaTypes.Select(type => 
    context.CallActivityAsync("DesignMediaTypeActivity", new { caseId, mediaType = type })
  );
  
  await Task.WhenAll(designDocTasks.Concat(designMediaTasks));
  ```

---

## 📋 FASE 5: REFATORAR GENDOCS/GENMEDIA (Com referências) (3-4 dias)

### Task 5.1: Atualizar GenerateDocumentItemActivity
- [ ] Arquivo: `Functions/Activities/GenerateDocumentItemActivity.cs`
- [ ] Modificar input model:
  ```csharp
  public class GenerateDocumentItemInput {
    public string CaseId { get; set; }
    public string DocId { get; set; }
    public string DocType { get; set; }
    public string[] RequiredContext { get; set; } // ex: ["@suspects/S001", "@evidence/E003"]
  }
  ```
- [ ] Implementar carregamento seletivo via ContextManager
- [ ] Reduzir prompt para incluir apenas contexto carregado
- [ ] Output: JSON com referências ao invés de copiar dados

### Task 5.2: Atualizar GenerateMediaItemActivity
- [ ] Arquivo: `Functions/Activities/GenerateMediaItemActivity.cs`
- [ ] Modificar input model similar ao Task 5.1
- [ ] Carregar apenas contexto da evidência específica
- [ ] Gerar prompt focado

### Task 5.3: Criar helper para resolver referências
- [ ] Arquivo: `Services/ReferenceResolver.cs`
- [ ] Método: `ResolveReferencesAsync(caseId, contextPaths[])`
- [ ] Retorna: Objeto com todas as referências resolvidas
- [ ] Usado por activities que precisam de múltiplas referências

### Task 5.4: Atualizar Orchestrator - passar referências
- [ ] Modificar chamadas para `GenerateDocumentItemActivity`
- [ ] Passar apenas `RequiredContext` baseado no tipo de documento
- [ ] Exemplo:
  ```csharp
  var input = new GenerateDocumentItemInput {
    CaseId = caseId,
    DocId = "interview_S001",
    DocType = "interview",
    RequiredContext = new[] {
      "@plan/core",
      "@suspects/S001",
      "@expand/suspects/S001"
    }
  };
  ```

---

## 📋 FASE 6: REFATORAR NORMALIZE (Salvar granularmente) (2-3 dias)

### Task 6.1: Criar NormalizeEntitiesActivity
- [ ] Arquivo: `Functions/Activities/Normalize/NormalizeEntitiesActivity.cs`
- [ ] Input: `caseId`
- [ ] Carrega: Todos `expand/suspects/*.json` e `expand/evidence/*.json`
- [ ] Output: Normaliza e salva em `/{caseId}/context/entities/`
  - `suspects/{id}.json` - Formato final normalizado
  - `evidence/{id}.json` - Formato final normalizado
  - `witnesses/{id}.json` - Se existirem

### Task 6.2: Criar NormalizeDocumentsActivity
- [ ] Arquivo: `Functions/Activities/Normalize/NormalizeDocumentsActivity.cs`
- [ ] Input: `caseId`, array de `docIds`
- [ ] Carrega: Documentos gerados de Blob temporário
- [ ] Output: Salva em `/{caseId}/documents/{docId}.json`
- [ ] Adiciona referências às entidades mencionadas

### Task 6.3: Criar NormalizeManifestActivity
- [ ] Arquivo: `Functions/Activities/Normalize/NormalizeManifestActivity.cs`
- [ ] Input: `caseId`
- [ ] Carrega: Metadata de todas as entidades e documentos
- [ ] Output: Salva em `/{caseId}/manifest.json`
- [ ] Conteúdo: Índice completo com referências (NÃO copia conteúdo)
  ```json
  {
    "caseId": "CASE-2025-001",
    "suspects": ["@entities/suspects/S001", "@entities/suspects/S002"],
    "evidence": ["@entities/evidence/E001", "@entities/evidence/E002"],
    "documents": ["@documents/interview_S001", "@documents/forensic_E001"],
    "timeline": "@expand/timeline"
  }
  ```

### Task 6.4: Atualizar Orchestrator - substituir NormalizeActivity
- [ ] Remover: `CallActivityAsync("NormalizeActivity")`
- [ ] Adicionar sequência:
  ```csharp
  await context.CallActivityAsync("NormalizeEntitiesActivity", caseId);
  await context.CallActivityAsync("NormalizeDocumentsActivity", new { caseId, docIds });
  await context.CallActivityAsync("NormalizeManifestActivity", caseId);
  ```

---

## 📋 FASE 7: REFATORAR QA (Escopo dirigido) (4-5 dias)

### Task 7.1: Criar QA_ScanIssuesActivity
- [ ] Arquivo: `Functions/Activities/QA/QA_ScanIssuesActivity.cs`
- [ ] Input: `caseId`
- [ ] Carrega: Apenas `manifest.json` + `metadata.json` (lightweight)
- [ ] Usa LLM para scan rápido
- [ ] Output: Lista de áreas problemáticas
  ```json
  {
    "issues": [
      { "area": "suspect_S001_alibi", "severity": "high", "description": "..." },
      { "area": "evidence_E003_chain", "severity": "medium", "description": "..." }
    ]
  }
  ```

### Task 7.2: Criar QA_DeepDiveActivity
- [ ] Arquivo: `Functions/Activities/QA/QA_DeepDiveActivity.cs`
- [ ] Input: `caseId`, `issueArea`
- [ ] Carrega: APENAS a entidade específica + dependências diretas
- [ ] Exemplo: Para "suspect_S001_alibi", carrega:
  - `@entities/suspects/S001`
  - `@expand/timeline` (apenas eventos relacionados a S001)
  - Documentos que mencionam S001
- [ ] Output: Análise detalhada do problema específico

### Task 7.3: Criar FixEntityActivity (substitui FixActivity)
- [ ] Arquivo: `Functions/Activities/QA/FixEntityActivity.cs`
- [ ] Input: `caseId`, `entityId`, `issueDescription`
- [ ] Carrega: Apenas a entidade + dependências diretas
- [ ] Aplica correção cirúrgica
- [ ] Output: Entidade corrigida
- [ ] Salva de volta em `/{caseId}/context/entities/{entityId}.json`

### Task 7.4: Criar CheckCaseCleanActivityV2
- [ ] Arquivo: `Functions/Activities/QA/CheckCaseCleanActivityV2.cs`
- [ ] Input: `caseId`, lista de `issues`
- [ ] Verifica se todas as issues foram resolvidas
- [ ] Output: `bool` - true se clean

### Task 7.5: Atualizar Orchestrator - novo loop de QA
- [ ] Substituir loop atual (RedTeam Global → Focused → Fix)
- [ ] Novo fluxo:
  ```csharp
  while (iteration <= maxIterations) {
    // 1. Scan rápido
    var issues = await context.CallActivityAsync<IssueList>(
      "QA_ScanIssuesActivity", caseId
    );
    
    if (!issues.Any()) break; // Caso clean
    
    // 2. Deep dive em paralelo nas áreas problemáticas
    var deepDiveTasks = issues.Select(issue => 
      context.CallActivityAsync<DeepDiveResult>(
        "QA_DeepDiveActivity", 
        new { caseId, issueArea = issue.Area }
      )
    );
    var analyses = await Task.WhenAll(deepDiveTasks);
    
    // 3. Fix em paralelo
    var fixTasks = analyses.Select(analysis => 
      context.CallActivityAsync(
        "FixEntityActivity", 
        new { caseId, entityId = analysis.EntityId, issueDescription = analysis.Issue }
      )
    );
    await Task.WhenAll(fixTasks);
    
    // 4. Verificar se está clean
    var isClean = await context.CallActivityAsync<bool>(
      "CheckCaseCleanActivityV2", 
      new { caseId, issues }
    );
    
    if (isClean) break;
    iteration++;
  }
  ```

---

## 📋 FASE 8: TESTES E OTIMIZAÇÕES (3-4 dias)

### Task 8.1: Criar testes de integração
- [ ] Arquivo: `CaseGen.Functions.Tests/Integration/ContextPipelineTests.cs`
- [ ] Testar pipeline completo com contexto granular
- [ ] Verificar que contexto carregado é mínimo
- [ ] Medir tokens consumidos vs pipeline antigo

### Task 8.2: Implementar logging de contexto
- [ ] Adicionar logs de quanto contexto foi carregado em cada activity
- [ ] Formato: `"Activity X loaded Y tokens from Z references"`
- [ ] Usar Application Insights para tracking

### Task 8.3: Implementar cache inteligente
- [ ] Cache de contexto entre activities no mesmo orchestration
- [ ] Evitar recarregar mesmas entidades
- [ ] TTL baseado em orchestration instance

### Task 8.4: Criar dashboard de monitoramento
- [ ] Métricas:
  - Tokens médios por activity
  - Cache hit rate
  - Tempo médio por fase
  - Taxa de sucesso QA (iterações necessárias)

### Task 8.5: Documentação
- [ ] Atualizar `docs/CASE_GENERATION_PIPELINE.md`
- [ ] Criar `docs/CONTEXT_ARCHITECTURE.md`
- [ ] Criar diagrama Mermaid do novo fluxo
- [ ] Documentar estrutura de pastas no Blob

---

## 📋 FASE 9: MIGRAÇÃO E ROLLOUT (2-3 dias)

### Task 9.1: Feature flag
- [ ] Adicionar flag `UseGranularContext` em config
- [ ] Permitir A/B testing entre pipeline antigo e novo
- [ ] Rollout gradual (10% → 50% → 100%)

### Task 9.2: Script de migração
- [ ] Criar script para converter casos existentes para novo formato
- [ ] Ler JSON monolítico → dividir em contexto granular
- [ ] Criar referências

### Task 9.3: Validação de produção
- [ ] Gerar 10 casos no novo pipeline
- [ ] Comparar qualidade com pipeline antigo
- [ ] Validar redução de tokens
- [ ] Medir performance

### Task 9.4: Cleanup código antigo
- [ ] Remover activities antigas (PlanActivity, ExpandActivity, etc)
- [ ] Remover models não usados
- [ ] Atualizar testes

---

## 📊 RESUMO DO CRONOGRAMA

| Fase | Duração | Complexidade | Prioridade |
|------|---------|--------------|------------|
| FASE 1: Context Manager | 1-2 dias | Média | 🔴 Crítica |
| FASE 2: Refatorar Plan | 2-3 dias | Baixa | 🔴 Crítica |
| FASE 3: Refatorar Expand | 3-4 dias | Alta | 🔴 Crítica |
| FASE 4: Refatorar Design | 2-3 dias | Média | 🟡 Alta |
| FASE 5: Refatorar GenDocs/GenMedia | 3-4 dias | Alta | 🟡 Alta |
| FASE 6: Refatorar Normalize | 2-3 dias | Média | 🟡 Alta |
| FASE 7: Refatorar QA | 4-5 dias | Alta | 🟢 Média |
| FASE 8: Testes e Otimizações | 3-4 dias | Média | 🟢 Média |
| FASE 9: Migração e Rollout | 2-3 dias | Baixa | 🔵 Baixa |

**TOTAL: 22-31 dias (4-6 semanas)**

---

## 🎯 MILESTONES

- **Milestone 1 (Semana 1)**: Context Manager + Plan refatorado
- **Milestone 2 (Semana 2)**: Expand + Design refatorados
- **Milestone 3 (Semana 3-4)**: GenDocs/GenMedia + Normalize refatorados
- **Milestone 4 (Semana 5)**: QA refatorado + testes
- **Milestone 5 (Semana 6)**: Rollout em produção

---

## ⚠️ RISCOS E MITIGAÇÕES

| Risco | Probabilidade | Impacto | Mitigação |
|-------|---------------|---------|-----------|
| Context Manager falha | Baixa | Alto | Testes rigorosos + fallback para pipeline antigo |
| Referências quebram | Média | Alto | Validação de referências + testes de integridade |
| LLM perde contexto | Média | Médio | Ajustar tamanho de snapshot + incluir sumários |
| Performance piora | Baixa | Médio | Cache agressivo + benchmark contínuo |
| Casos existentes incompatíveis | Alta | Baixo | Script de migração + suporte paralelo temporário |

---

## ✅ CRITÉRIOS DE SUCESSO

1. ✅ Redução de 80%+ nos tokens por activity
2. ✅ Qualidade igual ou superior aos casos atuais
3. ✅ Performance: geração completa em <15 minutos (vs ~20 min atual)
4. ✅ Taxa de QA: 90%+ casos clean em 1 iteração (vs 60% atual)
5. ✅ Zero regressão em casos existentes após migração
