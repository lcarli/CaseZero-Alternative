# 🎯 Plano de Implementação - CaseZero v3.0
## Cold Case Documental Experience

**Data:** 13 de novembro de 2025  
**Versão:** 3.0 - Refatoração Completa  
**Conceito:** Hunt a Killer Digital - Investigação documental pura

---

## 📋 Visão Geral

### Conceito Central
> "Cold Case documental puro. Você tem documentos, tempo real, e sua mente. Nada mais."

### Princípios de Design
- ❌ **SEM** gamificação visual (XP flutuante, achievements)
- ❌ **SEM** HUD com objetivos/dicas
- ❌ **SEM** interrogatórios interativos
- ❌ **SEM** notificações popup
- ✅ **COM** documentos estáticos (PDFs, imagens)
- ✅ **COM** tempo real para análises forenses
- ✅ **COM** descoberta orgânica
- ✅ **COM** autonomia total do jogador

---

## 🗂️ Estrutura do Case JSON (Arquivo Principal)

### Localização
```
cases/
└── CASE-2024-001/
    ├── case.json          ← ARQUIVO PRINCIPAL
    ├── assets/
    │   ├── documents/
    │   ├── photos/
    │   ├── evidence/
    │   └── reports/
    └── README.md
```

### Schema do case.json

```json
{
  "caseId": "CASE-2024-001",
  "version": "3.0",
  "metadata": {
    "title": "Homicídio no Edifício Corporativo",
    "category": "Homicide",
    "difficulty": "Medium",
    "estimatedTimeHours": 4,
    "minimumRank": "Rookie",
    "createdAt": "2024-11-13T00:00:00Z",
    "author": "CaseGen.Functions"
  },
  
  "briefing": {
    "from": "Chefe Marcus Smith",
    "subject": "CASO ARQUIVADO - REABERTURA",
    "date": "2024-11-13T09:00:00Z",
    "body": "Detetive,\n\nEstamos reabrindo o caso do homicídio de Robert Chen...",
    "attachments": []
  },
  
  "victim": {
    "name": "Robert Chen",
    "age": 42,
    "occupation": "CFO - TechCorp",
    "photo": "assets/photos/victim.jpg",
    "background": "Executivo respeitado, casado, 2 filhos..."
  },
  
  "crime": {
    "type": "Homicide",
    "date": "2023-03-15T23:30:00Z",
    "location": "TechCorp Building, 15th Floor",
    "description": "Vítima encontrada morta em seu escritório...",
    "crimeScenePhotos": [
      "assets/photos/scene-01.jpg",
      "assets/photos/scene-02.jpg",
      "assets/photos/scene-03.jpg"
    ]
  },
  
  "documents": [
    {
      "id": "DOC-001",
      "type": "PoliceReport",
      "title": "Relatório Policial Inicial",
      "date": "2023-03-16T08:00:00Z",
      "author": "Officer Sarah Martinez",
      "file": "assets/documents/police-report.pdf",
      "availableAt": "start",
      "tags": ["initial", "official"]
    },
    {
      "id": "DOC-002",
      "type": "WitnessStatement",
      "title": "Declaração - John Silva (Segurança)",
      "date": "2023-03-16T10:30:00Z",
      "author": "John Silva",
      "file": "assets/documents/statement-silva.pdf",
      "availableAt": "start",
      "tags": ["witness", "security"]
    }
  ],
  
  "evidence": [
    {
      "id": "EV-001",
      "name": "Arma do crime",
      "type": "Physical",
      "description": "Pistola calibre .38, encontrada próxima ao corpo",
      "photo": "assets/evidence/weapon.jpg",
      "collectedBy": "CSI Team",
      "collectedAt": "2023-03-16T02:00:00Z",
      "availableAt": "start",
      "forensicAnalysisAvailable": [
        {
          "type": "Ballistics",
          "duration": 12,
          "durationUnit": "hours",
          "reportTemplate": "assets/reports/ballistics-template.pdf"
        },
        {
          "type": "Fingerprints",
          "duration": 8,
          "durationUnit": "hours",
          "reportTemplate": "assets/reports/fingerprint-template.pdf"
        }
      ],
      "tags": ["weapon", "critical"]
    },
    {
      "id": "EV-004",
      "name": "Amostra Biológica",
      "type": "Biological",
      "description": "Sangue encontrado na cena",
      "photo": "assets/evidence/blood-sample.jpg",
      "collectedBy": "CSI Team",
      "collectedAt": "2023-03-16T02:30:00Z",
      "availableAt": "start",
      "forensicAnalysisAvailable": [
        {
          "type": "DNA",
          "duration": 24,
          "durationUnit": "hours",
          "reportTemplate": "assets/reports/dna-template.pdf"
        }
      ],
      "tags": ["biological", "critical"]
    }
  ],
  
  "suspects": [
    {
      "id": "SUSP-001",
      "name": "Michael Torres",
      "age": 38,
      "occupation": "Business Partner",
      "photo": "assets/photos/suspect-torres.jpg",
      "background": "Sócio minoritário da TechCorp...",
      "statement": "assets/documents/statement-torres.pdf",
      "motive": "Disputa financeira",
      "alibi": "Estava em casa sozinho",
      "criminalRecord": "None",
      "availableAt": "start"
    }
  ],
  
  "forensicReports": [
    {
      "id": "REP-001",
      "evidenceId": "EV-001",
      "type": "Ballistics",
      "title": "Análise Balística - Arma do Crime",
      "file": "assets/reports/ballistics-ev001.pdf",
      "findings": "Arma compatível com projétil encontrado...",
      "conclusions": ["Arma é a origem do disparo fatal"],
      "availableAfterRequest": true,
      "processingTime": 12,
      "processingTimeUnit": "hours"
    }
  ],
  
  "timeline": [
    {
      "timestamp": "2023-03-15T22:00:00Z",
      "event": "Vítima entra no escritório",
      "source": "CCTV",
      "verified": true
    },
    {
      "timestamp": "2023-03-15T23:15:00Z",
      "event": "Suspeito A entra no prédio",
      "source": "Security Log",
      "verified": true
    }
  ],
  
  "solution": {
    "culprit": "SUSP-001",
    "motive": "Disputa financeira - dívida de $500k",
    "method": "Disparo à queima-roupa",
    "keyEvidence": ["EV-001", "EV-004", "EV-002"],
    "explanation": "Michael Torres tinha motivo financeiro forte...",
    "hints": []
  },
  
  "gameConfig": {
    "allowPause": true,
    "timeAcceleration": 1,
    "forensicTimeReal": true,
    "maxForensicRequests": 10,
    "submissionAttempts": 3
  }
}
```

---

## 📦 Fases de Implementação

### **FASE 1: Schema & Infraestrutura** (2-3 dias)

#### 1.1 Definir TypeScript Types
**Arquivo:** `frontend/src/types/case.types.ts`

```typescript
export interface Case {
  caseId: string;
  version: string;
  metadata: CaseMetadata;
  briefing: Briefing;
  victim: Victim;
  crime: Crime;
  documents: Document[];
  evidence: Evidence[];
  suspects: Suspect[];
  forensicReports: ForensicReport[];
  timeline: TimelineEvent[];
  solution: Solution;
  gameConfig: GameConfig;
}

export interface CaseMetadata {
  title: string;
  category: string;
  difficulty: 'Easy' | 'Medium' | 'Hard';
  estimatedTimeHours: number;
  minimumRank: DetectiveRank;
  createdAt: string;
  author: string;
}

// ... continuar com todos os tipos
```

#### 1.2 Backend Models
**Arquivo:** `backend/CaseZeroApi/Models/CaseObject.cs`

```csharp
public class CaseObject
{
    public string CaseId { get; set; }
    public string Version { get; set; }
    public CaseMetadata Metadata { get; set; }
    public Briefing Briefing { get; set; }
    // ... todos os campos do JSON
}
```

#### 1.3 API Endpoints
**Arquivo:** `backend/CaseZeroApi/Controllers/CaseObjectController.cs`

```csharp
[HttpGet("{caseId}")]
public async Task<ActionResult<CaseObject>> GetCase(string caseId)
{
    // Carrega case.json do disco ou blob storage
}

[HttpGet("{caseId}/asset")]
public async Task<IActionResult> GetAsset(string caseId, string assetPath)
{
    // Retorna arquivo (PDF, imagem, etc.)
}
```

---

### **FASE 2: Backend - Case Management** (3-4 dias)

#### 2.1 CaseLoadingService
**Arquivo:** `backend/CaseZeroApi/Services/CaseLoadingService.cs`

```csharp
public class CaseLoadingService : ICaseLoadingService
{
    public async Task<CaseObject> LoadCaseAsync(string caseId)
    {
        var jsonPath = Path.Combine(_casesPath, caseId, "case.json");
        var json = await File.ReadAllTextAsync(jsonPath);
        return JsonSerializer.Deserialize<CaseObject>(json);
    }
    
    public async Task<byte[]> GetAssetAsync(string caseId, string assetPath)
    {
        var fullPath = Path.Combine(_casesPath, caseId, assetPath);
        return await File.ReadAllBytesAsync(fullPath);
    }
}
```

#### 2.2 ForensicService (Tempo Real)
**Arquivo:** `backend/CaseZeroApi/Services/ForensicService.cs`

```csharp
public class ForensicService : IForensicService
{
    // Tabela: ForensicRequests
    // Campos: Id, UserId, CaseId, EvidenceId, Type, RequestedAt, CompletedAt, Status
    
    public async Task<ForensicRequest> SubmitRequestAsync(
        string userId, 
        string caseId, 
        string evidenceId, 
        string analysisType)
    {
        var evidence = await GetEvidenceAsync(caseId, evidenceId);
        var analysisConfig = evidence.ForensicAnalysisAvailable
            .First(x => x.Type == analysisType);
        
        var request = new ForensicRequest
        {
            UserId = userId,
            CaseId = caseId,
            EvidenceId = evidenceId,
            Type = analysisType,
            RequestedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow.AddHours(analysisConfig.Duration),
            Status = "Pending"
        };
        
        await _context.ForensicRequests.AddAsync(request);
        await _context.SaveChangesAsync();
        
        return request;
    }
    
    public async Task<List<ForensicRequest>> GetUserRequestsAsync(string userId, string caseId)
    {
        return await _context.ForensicRequests
            .Where(x => x.UserId == userId && x.CaseId == caseId)
            .ToListAsync();
    }
    
    public async Task<ForensicReport?> GetReportIfReadyAsync(int requestId)
    {
        var request = await _context.ForensicRequests.FindAsync(requestId);
        
        if (request.CompletedAt <= DateTime.UtcNow)
        {
            request.Status = "Completed";
            await _context.SaveChangesAsync();
            
            // Carrega o template do report e retorna
            return await LoadReportAsync(request.CaseId, request.EvidenceId, request.Type);
        }
        
        return null;
    }
}
```

#### 2.3 CaseSessionService (Tracking)
**Arquivo:** `backend/CaseZeroApi/Services/CaseSessionService.cs`

```csharp
public class CaseSessionService : ICaseSessionService
{
    // Tabela: CaseSessions
    // Campos: Id, UserId, CaseId, StartedAt, LastAccessAt, Status, Progress
    
    public async Task<CaseSession> StartSessionAsync(string userId, string caseId)
    {
        var session = new CaseSession
        {
            UserId = userId,
            CaseId = caseId,
            StartedAt = DateTime.UtcNow,
            LastAccessAt = DateTime.UtcNow,
            Status = "Active"
        };
        
        await _context.CaseSessions.AddAsync(session);
        await _context.SaveChangesAsync();
        
        return session;
    }
    
    public async Task UpdateProgressAsync(int sessionId)
    {
        var session = await _context.CaseSessions.FindAsync(sessionId);
        session.LastAccessAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }
}
```

#### 2.4 SolutionSubmissionService
**Arquivo:** `backend/CaseZeroApi/Services/SolutionSubmissionService.cs`

```csharp
public class SolutionSubmissionService : ISolutionSubmissionService
{
    public async Task<SubmissionResult> SubmitSolutionAsync(
        string userId, 
        string caseId, 
        Solution userSolution)
    {
        var caseObj = await _caseLoadingService.LoadCaseAsync(caseId);
        var correctSolution = caseObj.Solution;
        
        var isCorrect = userSolution.Culprit == correctSolution.Culprit;
        
        var submission = new CaseSubmission
        {
            UserId = userId,
            CaseId = caseId,
            SubmittedAt = DateTime.UtcNow,
            Culprit = userSolution.Culprit,
            Explanation = userSolution.Explanation,
            IsCorrect = isCorrect
        };
        
        await _context.CaseSubmissions.AddAsync(submission);
        
        if (isCorrect)
        {
            // Atualiza XP e estatísticas do usuário
            await _userService.AwardXPAsync(userId, 100);
            await _userService.IncrementCasesResolvedAsync(userId);
        }
        
        await _context.SaveChangesAsync();
        
        return new SubmissionResult
        {
            IsCorrect = isCorrect,
            Feedback = isCorrect 
                ? "Parabéns! Você resolveu o caso." 
                : "Incorreto. Revise as evidências.",
            XPAwarded = isCorrect ? 100 : 0
        };
    }
}
```

---

### **FASE 3: Frontend - Desktop UI** (4-5 dias)

#### 3.1 Remover Componentes Desnecessários

**Remover:**
- HUD de objetivos
- Notificações popup
- Sistema de dicas
- Qualquer gamificação visual

**Manter:**
- Desktop.tsx (core)
- Window.tsx (sistema de janelas)
- Dock.tsx (barra de apps)

#### 3.2 Email Component (Briefing Único)
**Arquivo:** `frontend/src/components/apps/EmailApp.tsx`

```typescript
export const EmailApp: React.FC = () => {
  const { currentCase } = useCaseContext();
  
  if (!currentCase) return <div>Loading...</div>;
  
  const { briefing } = currentCase;
  
  return (
    <EmailContainer>
      <EmailHeader>
        <strong>De:</strong> {briefing.from}<br/>
        <strong>Assunto:</strong> {briefing.subject}<br/>
        <strong>Data:</strong> {new Date(briefing.date).toLocaleString()}
      </EmailHeader>
      
      <EmailBody>
        {briefing.body}
      </EmailBody>
      
      {briefing.attachments.length > 0 && (
        <AttachmentsList>
          <strong>Anexos:</strong>
          {briefing.attachments.map(att => (
            <AttachmentItem key={att.id}>
              📎 {att.name} <button>Download</button>
            </AttachmentItem>
          ))}
        </AttachmentsList>
      )}
    </EmailContainer>
  );
};
```

#### 3.3 Case Files Viewer
**Arquivo:** `frontend/src/components/apps/CaseFilesApp.tsx`

```typescript
export const CaseFilesApp: React.FC = () => {
  const { currentCase } = useCaseContext();
  const [selectedTab, setSelectedTab] = useState<'documents' | 'evidence' | 'suspects' | 'reports'>('documents');
  const [selectedItem, setSelectedItem] = useState<string | null>(null);
  
  return (
    <CaseFilesContainer>
      <Sidebar>
        <TabButton 
          active={selectedTab === 'documents'} 
          onClick={() => setSelectedTab('documents')}
        >
          📄 Documents
        </TabButton>
        <TabButton 
          active={selectedTab === 'evidence'} 
          onClick={() => setSelectedTab('evidence')}
        >
          🔬 Evidence
        </TabButton>
        <TabButton 
          active={selectedTab === 'suspects'} 
          onClick={() => setSelectedTab('suspects')}
        >
          👤 Suspects
        </TabButton>
        <TabButton 
          active={selectedTab === 'reports'} 
          onClick={() => setSelectedTab('reports')}
        >
          📋 Reports
        </TabButton>
      </Sidebar>
      
      <ContentArea>
        {selectedTab === 'documents' && <DocumentsList />}
        {selectedTab === 'evidence' && <EvidenceList />}
        {selectedTab === 'suspects' && <SuspectsList />}
        {selectedTab === 'reports' && <ReportsList />}
      </ContentArea>
      
      {selectedItem && (
        <ViewerPanel>
          <DocumentViewer itemId={selectedItem} />
        </ViewerPanel>
      )}
    </CaseFilesContainer>
  );
};
```

#### 3.4 Forensics Lab App
**Arquivo:** `frontend/src/components/apps/ForensicsLabApp.tsx`

```typescript
export const ForensicsLabApp: React.FC = () => {
  const { currentCase } = useCaseContext();
  const { sessionId } = useCaseSession();
  const [requests, setRequests] = useState<ForensicRequest[]>([]);
  
  const submitRequest = async (evidenceId: string, analysisType: string) => {
    const result = await forensicsApi.submitRequest(sessionId, evidenceId, analysisType);
    setRequests([...requests, result]);
  };
  
  const refreshRequests = async () => {
    const updated = await forensicsApi.getRequests(sessionId);
    setRequests(updated);
  };
  
  useEffect(() => {
    const interval = setInterval(refreshRequests, 30000); // Check every 30s
    return () => clearInterval(interval);
  }, []);
  
  return (
    <ForensicsContainer>
      <Section>
        <h3>Submit New Analysis Request</h3>
        <RequestForm onSubmit={submitRequest} />
      </Section>
      
      <Section>
        <h3>Pending Requests</h3>
        {requests.filter(r => r.status === 'Pending').map(req => (
          <RequestCard key={req.id}>
            <strong>{req.type}</strong> - {req.evidenceId}
            <br/>
            Started: {new Date(req.requestedAt).toLocaleString()}
            <br/>
            Ready at: {new Date(req.completedAt).toLocaleString()}
            <br/>
            {req.completedAt <= new Date() ? (
              <button onClick={() => viewReport(req.id)}>View Report</button>
            ) : (
              <TimeRemaining>
                ⏱️ {calculateTimeRemaining(req.completedAt)}
              </TimeRemaining>
            )}
          </RequestCard>
        ))}
      </Section>
      
      <Section>
        <h3>Completed Reports</h3>
        {requests.filter(r => r.status === 'Completed').map(req => (
          <ReportCard key={req.id}>
            <strong>{req.type}</strong> - {req.evidenceId}
            <button onClick={() => viewReport(req.id)}>View Report</button>
          </ReportCard>
        ))}
      </Section>
    </ForensicsContainer>
  );
};
```

#### 3.5 Solution Submission App
**Arquivo:** `frontend/src/components/apps/SubmissionApp.tsx`

```typescript
export const SubmissionApp: React.FC = () => {
  const { currentCase } = useCaseContext();
  const { sessionId } = useCaseSession();
  const [culprit, setCulprit] = useState('');
  const [explanation, setExplanation] = useState('');
  
  const submitSolution = async () => {
    const result = await solutionApi.submit(sessionId, {
      culprit,
      explanation,
      keyEvidence: [] // opcional
    });
    
    if (result.isCorrect) {
      alert('✅ Parabéns! Você resolveu o caso.');
      // Redirecionar para dashboard
    } else {
      alert('❌ Incorreto. Revise as evidências.');
    }
  };
  
  return (
    <SubmissionContainer>
      <h2>Submit Your Conclusion</h2>
      
      <FormGroup>
        <label>Culprit</label>
        <select value={culprit} onChange={e => setCulprit(e.target.value)}>
          <option value="">Select suspect...</option>
          {currentCase.suspects.map(s => (
            <option key={s.id} value={s.id}>{s.name}</option>
          ))}
        </select>
      </FormGroup>
      
      <FormGroup>
        <label>Your Explanation</label>
        <textarea 
          value={explanation} 
          onChange={e => setExplanation(e.target.value)}
          placeholder="Explain your reasoning..."
          rows={10}
        />
      </FormGroup>
      
      <WarningBox>
        ⚠️ You have 3 submission attempts. Choose carefully.
      </WarningBox>
      
      <SubmitButton onClick={submitSolution}>
        Submit Solution
      </SubmitButton>
    </SubmissionContainer>
  );
};
```

---

### **FASE 4: Tutorial & Onboarding** (1-2 dias)

#### 4.1 First Time Tutorial
**Arquivo:** `frontend/src/components/tutorial/FirstTimeTutorial.tsx`

```typescript
export const FirstTimeTutorial: React.FC = () => {
  const [step, setStep] = useState(0);
  const { markTutorialComplete } = useAuth();
  
  const steps = [
    {
      message: "Bem-vindo à Cold Case Division",
      description: "Este é seu desktop. Você tem acesso a:\n\n📧 EMAIL - Briefings do departamento\n📁 CASE FILES - Documentos dos casos\n🧪 FORENSICS LAB - Solicite análises",
      highlight: null
    },
    {
      message: "Leia seu primeiro briefing",
      description: "Clique no ícone de Email para começar sua investigação.",
      highlight: "email-icon"
    }
  ];
  
  if (step >= steps.length) {
    markTutorialComplete();
    return null;
  }
  
  return (
    <TutorialOverlay>
      <TutorialBox>
        <h3>{steps[step].message}</h3>
        <p>{steps[step].description}</p>
        <button onClick={() => setStep(step + 1)}>
          {step === steps.length - 1 ? 'Entendi' : 'Próximo'}
        </button>
      </TutorialBox>
      {steps[step].highlight && <HighlightArrow target={steps[step].highlight} />}
    </TutorialOverlay>
  );
};
```

---

### **FASE 5: Database Schema** (1 dia)

#### 5.1 Novas Tabelas

```sql
-- ForensicRequests
CREATE TABLE ForensicRequests (
    Id INT PRIMARY KEY IDENTITY,
    UserId NVARCHAR(450) NOT NULL,
    CaseId NVARCHAR(50) NOT NULL,
    EvidenceId NVARCHAR(50) NOT NULL,
    Type NVARCHAR(50) NOT NULL,
    RequestedAt DATETIME2 NOT NULL,
    CompletedAt DATETIME2 NOT NULL,
    Status NVARCHAR(20) NOT NULL,
    FOREIGN KEY (UserId) REFERENCES AspNetUsers(Id)
);

-- CaseSessions (atualizar schema existente)
ALTER TABLE CaseSessions ADD Progress INT DEFAULT 0;

-- CaseSubmissions (atualizar schema existente)
ALTER TABLE CaseSubmissions ADD XPAwarded INT DEFAULT 0;
```

---

### **FASE 6: Caso de Exemplo** (2-3 dias)

#### 6.1 Criar case.json completo
**Arquivo:** `cases/CASE-2024-001/case.json`

Criar JSON completo seguindo o schema definido.

#### 6.2 Gerar Assets
- Relatório policial (PDF)
- Declarações de testemunhas (3 PDFs)
- Fotos da cena do crime (6 imagens)
- Fotos de evidências (8 imagens)
- Fotos de suspeitos (3 imagens)
- Laudos forenses (3 PDFs templates)

#### 6.3 Testar Fluxo Completo
1. Login como Rookie
2. Tutorial aparece
3. Abre email
4. Explora Case Files
5. Solicita análise forense
6. Aguarda tempo real
7. Lê laudo
8. Submete solução

---

## 📊 Checklist de Implementação

### Backend
- [ ] Schema case.json definido
- [ ] TypeScript types criados
- [ ] Backend models (C#) criados
- [ ] CaseLoadingService implementado
- [ ] ForensicService com tempo real
- [ ] CaseSessionService
- [ ] SolutionSubmissionService
- [ ] API endpoints (/api/case/{id}, /api/forensics, etc.)
- [ ] Database migrations

### Frontend
- [ ] Remover HUD/dicas/gamificação
- [ ] EmailApp (briefing único)
- [ ] CaseFilesApp (viewer de documentos)
- [ ] ForensicsLabApp (tempo real)
- [ ] SubmissionApp
- [ ] Tutorial minimalista
- [ ] Document viewer (PDF/imagens)
- [ ] API integration

### Conteúdo
- [ ] case.json completo (CASE-2024-001)
- [ ] Relatório policial (PDF)
- [ ] 3 declarações de testemunhas (PDFs)
- [ ] 6 fotos da cena
- [ ] 8 fotos de evidências
- [ ] 3 fotos de suspeitos
- [ ] 3 laudos forenses (templates)

### Testes
- [ ] Carregar caso do JSON
- [ ] Visualizar documentos
- [ ] Solicitar análise forense
- [ ] Tempo real funciona
- [ ] Submeter solução correta
- [ ] Submeter solução incorreta
- [ ] Tutorial aparece na primeira vez

---

## 🚀 Ordem de Desenvolvimento

### Sprint 1 (Semana 1)
1. Definir schema case.json
2. Criar TypeScript types
3. Criar backend models
4. Implementar CaseLoadingService
5. Criar CASE-2024-001/case.json (estrutura básica)

### Sprint 2 (Semana 2)
6. Implementar ForensicService (tempo real)
7. Implementar SolutionSubmissionService
8. Database migrations
9. API endpoints

### Sprint 3 (Semana 3)
10. Remover componentes desnecessários no frontend
11. Implementar EmailApp
12. Implementar CaseFilesApp (básico)
13. Document viewer (PDF/imagens)

### Sprint 4 (Semana 4)
14. Implementar ForensicsLabApp
15. Implementar SubmissionApp
16. Tutorial minimalista
17. Integração frontend-backend

### Sprint 5 (Semana 5)
18. Gerar assets completos (CASE-2024-001)
19. Testes end-to-end
20. Ajustes finais
21. Documentação

---

## 📝 Notas Importantes

### Sobre o case.json
- É o **arquivo principal** de cada caso
- **CaseGen.Functions** irá gerar este arquivo no futuro
- Deve conter **tudo** necessário para o jogo funcionar
- Assets referenciados devem estar na pasta `assets/`

### Sobre o Tempo Real
- Análises forenses levam horas reais (configurável)
- Sem notificações - jogador deve verificar manualmente
- Status persiste no banco (mesmo se fechar o navegador)

### Sobre a Experiência
- **100% autônomo** após tutorial
- **Sem dicas** durante investigação
- **Sem gamificação** visual
- Documentos são **estáticos** (PDFs, imagens)

---

## ✅ Definição de Pronto

### Um caso está completo quando:
- [ ] case.json válido e completo
- [ ] Todos os assets existem e estão acessíveis
- [ ] Briefing carrega e exibe corretamente
- [ ] Documentos podem ser visualizados
- [ ] Evidências podem ser exploradas
- [ ] Análises forenses podem ser solicitadas
- [ ] Tempo real funciona (pode aguardar ou acelerar)
- [ ] Laudos aparecem após tempo decorrido
- [ ] Solução pode ser submetida
- [ ] Feedback correto/incorreto funciona
- [ ] XP é atualizado se correto

### O sistema está pronto quando:
- [ ] Tutorial aparece na primeira vez
- [ ] Desktop carrega com apps corretos
- [ ] CASO-2024-001 funciona 100%
- [ ] Documentação completa
- [ ] Testes passam

---

**Próximos Passos:** Começar pelo Sprint 1 - Definição do schema e estrutura básica do case.json.
