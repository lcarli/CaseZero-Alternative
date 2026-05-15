# Capítulo 11 - Estratégia de Testes & Garantia de Qualidade

**Documento de Design de Jogo - CaseZero v3.0**  
**Última Atualização:** 14 de novembro de 2025  
**Status:** ✅ Completo

---

## 11.1 Visão Geral

Este capítulo define a **estratégia de testes abrangente, os processos de QA e os pontos de controle de qualidade** para o CaseZero v3.0. Ele cobre testes unitários, de integração, ponta a ponta, de conteúdo, de performance e de acessibilidade.

**Conceitos-chave:**
- Abordagem multinível de testes
- Metas de cobertura automatizada
- Processos de validação de conteúdo
- Indicadores de performance
- Conformidade de acessibilidade

---

## 11.2 Filosofia de Testes

### Princípios centrais

**1. Testar cedo e com frequência**
- Escrever testes em paralelo ao código
- Capturar bugs na origem
- Reduzir tempo de depuração
- Manter cobertura de testes

**2. Automatizar tudo que for possível**
- Testes unitários em cada commit
- Testes de integração em todo PR
- Testes E2E antes de deploy
- Reduzir a carga de testes manuais

**3. Conteúdo é código**
- `case.json` precisa passar na validação
- Checks automatizados de conteúdo
- Playthrough obrigatório de QA
- Sem exceções para conteúdo "criativo"

**4. Testes centrados no usuário**
- Cobrir fluxos reais do usuário
- Acessibilidade é obrigatória
- Performance impacta UX
- Testar em dispositivos reais

---

## 11.3 Pirâmide de testes

```
                    /\
                   /  \
                  / E2E \
                 /--------\
                /          \
               / Integration \
              /--------------\
             /                \
            /   Unit Tests     \
           /____________________\
```

**Distribuição:**
- **70% Testes unitários:** Rápidos, isolados, numerosos
- **20% Testes de integração:** Endpoints de API, operações de banco
- **10% Testes E2E:** Fluxos críticos do usuário

**Justificativa:**
- Testes unitários são rápidos e capturam a maioria dos bugs
- Testes de integração verificam interações entre componentes
- Testes E2E validam a experiência do usuário, mas são mais lentos

---

## 11.4 Testes unitários

### Testes unitários de frontend (Vitest + React Testing Library)

**Metas de cobertura:**
- **Componentes:** 80%
- **Utilidades:** 90%
- **Gerenciamento de estado:** 85%
- **Geral:** mínimo de 80%

**O que testar:**

**Componentes:**
```typescript
// CaseFilesList.test.tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { CaseFilesList } from './CaseFilesList';

describe('CaseFilesList', () => {
  const mockDocuments = [
    { documentId: 'DOC-001', title: 'Police Report', type: 'PoliceReport' },
    { documentId: 'DOC-002', title: 'Witness Statement', type: 'WitnessStatement' }
  ];
  
  it('renders document list', () => {
    render(<CaseFilesList documents={mockDocuments} />);
    expect(screen.getByText('Police Report')).toBeInTheDocument();
    expect(screen.getByText('Witness Statement')).toBeInTheDocument();
  });
  
  it('calls onClick when document selected', () => {
    const handleClick = jest.fn();
    render(<CaseFilesList documents={mockDocuments} onDocumentClick={handleClick} />);
    
    fireEvent.click(screen.getByText('Police Report'));
    expect(handleClick).toHaveBeenCalledWith('DOC-001');
  });
  
  it('filters documents by type', () => {
    render(<CaseFilesList documents={mockDocuments} filterType="PoliceReport" />);
    expect(screen.getByText('Police Report')).toBeInTheDocument();
    expect(screen.queryByText('Witness Statement')).not.toBeInTheDocument();
  });
});
```

**Slices do Redux:**
```typescript
// caseSlice.test.ts
import caseReducer, { loadCase, setActiveCase } from './caseSlice';

describe('caseSlice', () => {
  it('sets active case', () => {
    const initialState = { active: null, loading: false };
    const caseData = { caseId: 'CASE-2024-001', title: 'Test Case' };
    
    const newState = caseReducer(initialState, setActiveCase(caseData));
    expect(newState.active).toEqual(caseData);
  });
  
  it('handles loadCase.pending', () => {
    const initialState = { active: null, loading: false };
    const newState = caseReducer(initialState, loadCase.pending('', 'CASE-2024-001'));
    expect(newState.loading).toBe(true);
  });
  
  it('handles loadCase.fulfilled', () => {
    const initialState = { active: null, loading: true };
    const caseData = { caseId: 'CASE-2024-001', title: 'Test Case' };
    
    const newState = caseReducer(initialState, loadCase.fulfilled(caseData, '', 'CASE-2024-001'));
    expect(newState.active).toEqual(caseData);
    expect(newState.loading).toBe(false);
  });
});
```

**Funções utilitárias:**
```typescript
// validation.test.ts
import { validateCaseData, validateSubmission } from './validation';

describe('validateCaseData', () => {
  it('validates correct case data', () => {
    const validCase = { /* valid case.json */ };
    const result = validateCaseData(validCase);
    expect(result.valid).toBe(true);
    expect(result.errors).toHaveLength(0);
  });
  
  it('detects invalid suspect reference', () => {
    const invalidCase = {
      suspects: [{ suspectId: 'SUSP-001' }],
      solution: { culprit: 'SUSP-999' } // Invalid
    };
    const result = validateCaseData(invalidCase);
    expect(result.valid).toBe(false);
    expect(result.errors).toContain('Solution culprit SUSP-999 not found in suspects');
  });
});

describe('validateSubmission', () => {
  it('rejects submission with short motive', () => {
    const submission = {
      culprit: 'SUSP-001',
      motiveExplanation: 'Too short', // < 50 chars
      methodExplanation: 'A much longer explanation of the method used...',
      evidenceSelected: ['EV-001']
    };
    const result = validateSubmission(submission);
    expect(result.valid).toBe(false);
    expect(result.errors).toContain('Motive explanation must be at least 50 characters');
  });
});
```

### Testes unitários de backend (xUnit + Moq)

**Metas de cobertura:**
- **Controllers:** 75%
- **Serviços:** 90%
- **Validadores:** 95%
- **Geral:** mínimo de 80%

**O que testar:**

**Serviços:**
```csharp
// CaseServiceTests.cs
using Xunit;
using Moq;
using CaseZero.Services;
using CaseZero.Data;

public class CaseServiceTests
{
    private readonly Mock<ICaseRepository> _mockRepo;
    private readonly CaseService _service;
    
    public CaseServiceTests()
    {
        _mockRepo = new Mock<ICaseRepository>();
        _service = new CaseService(_mockRepo.Object);
    }
    
    [Fact]
    public async Task GetCaseById_ReturnsCaseData()
    {
        // Arrange
        var expectedCase = new CaseEntity { CaseId = "CASE-2024-001" };
        _mockRepo.Setup(r => r.GetByIdAsync("CASE-2024-001"))
                 .ReturnsAsync(expectedCase);
        
        // Act
        var result = await _service.GetCaseByIdAsync("CASE-2024-001");
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal("CASE-2024-001", result.CaseId);
    }
    
    [Fact]
    public async Task GetCaseById_ThrowsWhenNotFound()
    {
        // Arrange
        _mockRepo.Setup(r => r.GetByIdAsync("INVALID"))
                 .ReturnsAsync((CaseEntity)null);
        
        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
          () => _service.GetCaseByIdAsync("INVALID")
        );
    }
}
```

**Validadores:**
```csharp
// CaseDataValidatorTests.cs
using Xunit;
using FluentValidation.TestHelper;
using CaseZero.Validators;

public class CaseDataValidatorTests
{
    private readonly CaseDataValidator _validator;
    
    public CaseDataValidatorTests()
    {
        _validator = new CaseDataValidator();
    }
    
    [Fact]
    public void Should_HaveError_When_CaseIdInvalid()
    {
        var caseData = new CaseData { Metadata = new CaseMetadata { CaseId = "INVALID" } };
        var result = _validator.TestValidate(caseData);
        result.ShouldHaveValidationErrorFor(c => c.Metadata.CaseId);
    }
    
    [Fact]
    public void Should_HaveError_When_SuspectsOutOfRange()
    {
        var caseData = new CaseData { Suspects = new List<Suspect>() }; // Empty list
        var result = _validator.TestValidate(caseData);
        result.ShouldHaveValidationErrorFor(c => c.Suspects);
    }
    
    [Theory]
    [InlineData(2)] // Valid
    [InlineData(5)] // Valid
    [InlineData(8)] // Valid
    public void Should_NotHaveError_When_SuspectsInRange(int count)
    {
        var caseData = new CaseData 
        { 
            Suspects = Enumerable.Range(1, count).Select(i => new Suspect()).ToList()
        };
        var result = _validator.TestValidate(caseData);
        result.ShouldNotHaveValidationErrorFor(c => c.Suspects);
    }
}
```

**Cálculo de XP:**
```csharp
// XPCalculatorTests.cs
using Xunit;
using CaseZero.Services;

public class XPCalculatorTests
{
    private readonly XPCalculator _calculator;
    
    public XPCalculatorTests()
    {
        _calculator = new XPCalculator();
    }
    
    [Theory]
    [InlineData(Difficulty.Easy, 1, true, false, false, 225)]     // Base 150 + First Attempt 75
    [InlineData(Difficulty.Medium, 1, true, true, true, 510)]     // Base 300 + FA 150 + Speed 30 + Thorough 30
    [InlineData(Difficulty.Hard, 2, false, false, false, 600)]    // Base 600, second attempt, no bonuses
    [InlineData(Difficulty.Expert, 1, true, false, false, 1800)]  // Base 1200 + FA 600
    public void CalculateXP_ReturnsCorrectAmount(
        Difficulty difficulty, 
        int attemptNumber,
        bool isFirstAttempt,
        bool isSpeedBonus,
        bool isThoroughnessBonus,
        int expectedXP)
    {
        // Arrange
        var submission = new CaseSubmission
        {
            Difficulty = difficulty,
            AttemptNumber = attemptNumber,
            IsSpeedBonus = isSpeedBonus,
            IsThoroughnessBonus = isThoroughnessBonus
        };
        
        // Act
        var result = _calculator.CalculateXP(submission);
        
        // Assert
        Assert.Equal(expectedXP, result);
    }
}
```

---

## 11.5 Testes de integração

### Testes de integração da API (WebApplicationFactory)

**Cobertura:**
- Todos os endpoints de API
- Operações de banco de dados
- Fluxos de autenticação
- Tratamento de erros

**Exemplos de testes:**

```csharp
// CaseControllerIntegrationTests.cs
using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;

public class CaseControllerIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    
    public CaseControllerIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }
    
    [Fact]
    public async Task GetCases_ReturnsOk_WithCaseList()
    {
        // Act
    var response = await _client.GetAsync("/api/cases");
        
        // Assert
        response.EnsureSuccessStatusCode();
        var cases = await response.Content.ReadFromJsonAsync<List<CaseListItemDto>>();
        Assert.NotNull(cases);
        Assert.NotEmpty(cases);
    }
    
    [Fact]
    public async Task GetCaseById_ReturnsOk_WithCaseData()
    {
        // Act
    var response = await _client.GetAsync("/api/cases/CASE-2024-001");
        
        // Assert
        response.EnsureSuccessStatusCode();
        var caseData = await response.Content.ReadFromJsonAsync<CaseDetailDto>();
        Assert.NotNull(caseData);
    Assert.Equal("CASE-2024-001", caseData.CaseId);
    }
    
    [Fact]
    public async Task GetCaseById_Returns404_WhenNotFound()
    {
        // Act
    var response = await _client.GetAsync("/api/cases/INVALID");
        
        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
    
    [Fact]
    public async Task SubmitSolution_RequiresAuthentication()
    {
        // Arrange
        var submission = new SubmitSolutionRequest(
            Guid.NewGuid(),
      "SUSP-001",
      "Motive explanation...",
      "Method explanation...",
      new List<string> { "EV-001" }
        );
        
        // Act
    var response = await _client.PostAsJsonAsync("/api/cases/CASE-2024-001/submit", submission);
        
        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
    
    [Fact]
    public async Task SubmitSolution_ReturnsOk_WhenAuthenticated()
    {
        // Arrange
        var token = await GetAuthToken();
    _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        
    var sessionId = await CreateCaseSession("CASE-2024-001");
        var submission = new SubmitSolutionRequest(
            sessionId,
      "SUSP-001",
      "David Reynolds embezzled money and Marcus discovered it during audit...",
      "Reynolds used stolen keycard to enter office after hours and shot victim...",
      new List<string> { "EV-001", "EV-004", "EV-007" }
        );
        
        // Act
    var response = await _client.PostAsJsonAsync("/api/cases/CASE-2024-001/submit", submission);
        
        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SubmissionResultDto>();
        Assert.NotNull(result);
        Assert.NotNull(result.Feedback);
    }
}
```

### Testes de integração de banco de dados

```csharp
// CaseRepositoryIntegrationTests.cs
using Xunit;
using Microsoft.EntityFrameworkCore;

public class CaseRepositoryIntegrationTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly CaseRepository _repository;
    
    public CaseRepositoryIntegrationTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        _context = new AppDbContext(options);
        _repository = new CaseRepository(_context);
    }
    
    [Fact]
    public async Task CreateCase_SavesToDatabase()
    {
        // Arrange
        var caseEntity = new CaseEntity
        {
        CaseId = "CASE-2024-001",
        Title = "Test Case",
            Difficulty = Difficulty.Medium,
        CaseDataJson = "{}"
        };
        
        // Act
        await _repository.CreateAsync(caseEntity);
        await _context.SaveChangesAsync();
        
        // Assert
      var retrieved = await _repository.GetByIdAsync("CASE-2024-001");
        Assert.NotNull(retrieved);
      Assert.Equal("Test Case", retrieved.Title);
    }
    
    [Fact]
    public async Task GetCasesByDifficulty_FiltersCorrectly()
    {
        // Arrange
        await SeedCases();
        
        // Act
        var easyCases = await _repository.GetByDifficultyAsync(Difficulty.Easy);
        
        // Assert
        Assert.All(easyCases, c => Assert.Equal(Difficulty.Easy, c.Difficulty));
    }
    
    public void Dispose()
    {
        _context.Dispose();
    }
}
```

---

## 11.6 Testes ponta a ponta

### Framework de E2E (Playwright)

**Cobertura:**
- Apenas fluxos críticos do usuário
- Cenários do caminho feliz
- Erros essenciais

**Fluxos críticos:**

**1. Registro de novo usuário e primeiro caso**
```typescript
// e2e/new-user-flow.spec.ts
import { test, expect } from '@playwright/test';

test('new user can register and start first case', async ({ page }) => {
  // 1. Navigate to app
  await page.goto('https://casezero.com');
  
  // 2. Click Sign Up
  await page.click('text=Sign Up');
  
  // 3. Fill registration form
  await page.fill('input[name="username"]', 'testuser123');
  await page.fill('input[name="email"]', 'testuser123@example.com');
  await page.fill('input[name="password"]', 'SecurePass123!');
  await page.click('button[type="submit"]');
  
  // 4. Verify redirect to dashboard
  await expect(page).toHaveURL(/.*dashboard/);
  await expect(page.locator('text=Welcome, Detective testuser123')).toBeVisible();
  
  // 5. Browse available cases
  await page.click('text=Case Browser');
  await expect(page.locator('.case-card')).toHaveCount(9); // 9 launch cases
  
  // 6. Start first case (Easy)
  await page.click('.case-card:has-text("Easy")').first();
  await page.click('button:has-text("Start Case")');
  
  // 7. Verify case loaded
  await expect(page).toHaveURL(/.*case\/CASE-2024-\d{3}/);
  await expect(page.locator('.desktop-ui')).toBeVisible();
  await expect(page.locator('.taskbar')).toBeVisible();
  
  // 8. Open Case Files app
  await page.click('.taskbar-icon:has-text("Case Files")');
  await expect(page.locator('.case-files-window')).toBeVisible();
  
  // 9. View first document
  await page.click('.document-list .document-item').first();
  await expect(page.locator('.pdf-viewer')).toBeVisible();
});
```

**2. Fluxo completo de resolução do caso**
```typescript
// e2e/solve-case-flow.spec.ts
import { test, expect } from '@playwright/test';

test('user can solve case and submit solution', async ({ page }) => {
  // Setup: Login as existing user with active case
  await loginAsUser(page, 'testuser@example.com', 'password');
  await page.goto('/case/CASE-2024-001');
  
  // 1. Read documents
  await page.click('.taskbar-icon:has-text("Case Files")');
  await page.click('.document-list .document-item:has-text("Police Report")');
  await page.waitForSelector('.pdf-viewer');
  
  // 2. Examine evidence
  await page.click('.evidence-tab');
  await page.click('.evidence-item:has-text("9mm Handgun")');
  await expect(page.locator('.evidence-photo-viewer')).toBeVisible();
  await page.click('.close-button');
  
  // 3. Request forensic analysis
  await page.click('.taskbar-icon:has-text("Forensics Lab")');
  await page.selectOption('select[name="evidence"]', 'EV-001');
  await page.selectOption('select[name="analysisType"]', 'Ballistics');
  await page.click('button:has-text("Submit Request")');
  await expect(page.locator('text=Request submitted')).toBeVisible();
  
  // 4. Wait for forensics (instant mode for testing)
  await page.click('.settings-icon');
  await page.selectOption('select[name="forensicsMode"]', 'instant');
  await page.click('.save-settings');
  
  // 5. View forensic results
  await expect(page.locator('.forensic-request.completed')).toBeVisible({ timeout: 5000 });
  await page.click('.forensic-request.completed:has-text("Ballistics")');
  await expect(page.locator('.pdf-viewer')).toBeVisible();
  
  // 6. Take notes
  await page.click('.notes-panel');
  await page.fill('textarea[name="notes"]', 'SUSPECTS:\n- Reynolds: Financial motive\n- Evidence: Ballistics match');
  
  // 7. Submit solution
  await page.click('.taskbar-icon:has-text("Submit Solution")');
  
  // Page 1: Select culprit
  await page.click('.suspect-option:has-text("David Reynolds")');
  await page.click('button:has-text("Next")');
  
  // Page 2: Motive
  await page.fill('textarea[name="motive"]', 
    'David Reynolds embezzled $500,000 from the company. Marcus Coleman discovered the discrepancy during an audit review and confronted Reynolds. Reynolds feared exposure would lead to prison and financial ruin.'
  );
  await page.click('button:has-text("Next")');
  
  // Page 3: Method
  await page.fill('textarea[name="method"]',
    'Reynolds used a stolen building keycard to enter the office after hours. He confronted Coleman in his office around 1:15 AM and shot him once in the chest with a 9mm Glock. He then ransacked the office to stage a robbery.'
  );
  await page.click('button:has-text("Next")');
  
  // Page 4: Evidence
  await page.check('input[value="EV-001"]'); // 9mm Glock
  await page.check('input[value="EV-004"]'); // Gunshot residue
  await page.check('input[value="EV-007"]'); // Financial records
  await page.check('input[value="DOC-009"]'); // Bank statements
  await page.click('button:has-text("Next")');
  
  // Page 5: Review & Submit
  await page.click('button:has-text("Submit Solution")');
  
  // 8. Verify success
  await expect(page.locator('text=Excellent work, Detective!')).toBeVisible({ timeout: 10000 });
  await expect(page.locator('text=+450 XP')).toBeVisible();
  
  // 9. Return to dashboard
  await page.click('button:has-text("Return to Dashboard")');
  await expect(page).toHaveURL(/.*dashboard/);
});
```

**3. Fluxo de perícia em tempo real**
```typescript
// e2e/forensics-timer-flow.spec.ts
import { test, expect } from '@playwright/test';

test('forensic request completes after timer (accelerated)', async ({ page }) => {
  await loginAsUser(page, 'testuser@example.com', 'password');
  await page.goto('/case/CASE-2024-001');
  
  // 1. Set accelerated time (10x)
  await page.click('.settings-icon');
  await page.selectOption('select[name="forensicsMode"]', '10x');
  await page.click('.save-settings');
  
  // 2. Request DNA analysis (24h base = 2.4h at 10x = 144 minutes)
  await page.click('.taskbar-icon:has-text("Forensics Lab")');
  await page.selectOption('select[name="evidence"]', 'EV-004');
  await page.selectOption('select[name="analysisType"]', 'DNA');
  await page.click('button:has-text("Submit Request")');
  
  // 3. Verify pending status
  await expect(page.locator('.forensic-request.pending')).toBeVisible();
  await expect(page.locator('text=/.*h.*m remaining/')).toBeVisible();
  
  // 4. Wait for completion (simulate time passing)
  // In real test, would wait actual time or mock timer
  await page.waitForSelector('.forensic-request.completed', { timeout: 180000 }); // 3 min max
  
  // 5. Verify notification
  await expect(page.locator('.toast-notification:has-text("DNA Analysis Complete")')).toBeVisible();
  
  // 6. View report
  await page.click('.forensic-request.completed:has-text("DNA")');
  await expect(page.locator('.pdf-viewer')).toBeVisible();
});
```

### Estratégia de testes E2E

**Quando executar:**
- Antes de deploy em produção
- Após alterações grandes de funcionalidade
- Execuções semanais agendadas

**Ambientes de teste:**
- **Staging:** suíte E2E completa
- **Produção:** apenas smoke tests (sem dados de teste)

**Execução paralela:**
- Rodar testes em paralelo (4 workers)
- Tempo total de E2E: ~15 minutos

---

## 11.7 Testes de conteúdo

### Validação automatizada de conteúdo

**Checks pré-publicação:**

**1. Validação de schema JSON**
```bash
# Run schema validator
node tools/case-validator.js cases/CASE-2024-001/case.json
```

**2. Integridade referencial**
```python
# Run clue checker
python tools/clue-checker.py cases/CASE-2024-001/case.json
```

**3. Existência de assets**
```bash
# Verify all asset files exist
node tools/asset-checker.js cases/CASE-2024-001/case.json
```

**4. Tamanho de arquivos**
```bash
# Check PDFs < 5MB, images < 2MB
node tools/size-checker.js cases/CASE-2024-001/
```

### QA manual de conteúdo

**Playthrough do QA Tester:**

**Preparação antes do teste:**
- QA tester nunca viu o caso (teste às cegas)
- Perícias configuradas para modo instantâneo (agilidade)
- Gravação de tela ligada
- Bloco de notas aberto para issues

**Processo de playthrough:**
1. Iniciar o caso, anotar primeiras impressões
2. Ler todos os documentos de forma sistemática
3. Examinar todas as evidências
4. Solicitar perícias relevantes
5. Registrar notas das pistas encontradas
6. Tentar a solução
7. Registrar tempo gasto

**O que verificar:**
- [ ] Todas as pistas presentes e descobertas
- [ ] Sem contradições nos documentos
- [ ] Linha do tempo consistente
- [ ] Resultados forenses coerentes
- [ ] Solução comprovável com evidências
- [ ] Sem soluções não intencionais
- [ ] Falsas pistas são refutáveis
- [ ] Dificuldade adequada
- [ ] Tempo estimado é preciso
- [ ] Erros de digitação/gramática

**Template de relatório de QA:**
```markdown
# QA Report: CASE-2024-XXX

**Tester:** [Name]
**Date:** [Date]
**Time Taken:** [X hours]
**Attempts:** [1/2/3]
**Result:** [Solved / Failed]

## Issues Found

### Critical (Must Fix)
- [ ] Issue description
- [ ] Issue description

### Major (Should Fix)
- [ ] Issue description

### Minor (Nice to Fix)
- [ ] Issue description

## Positive Notes
- What worked well

## Difficulty Assessment
- Intended: [Easy/Medium/Hard/Expert]
- Actual Feel: [Easy/Medium/Hard/Expert]
- Recommendation: [Keep / Adjust]

## Overall Rating
[1-10, 10 = excellent]
```

---

## 11.8 Testes de performance

### Metas de performance

**Carregamento de página:**
- Dashboard: < 2 segundos
- Carregamento do caso: < 3 segundos
- Carregamento de documento: < 1 segundo
- Foto de evidência: < 500 ms

**Tempos de resposta da API:**
- Requisições GET: < 200 ms (p95)
- Requisições POST: < 500 ms (p95)
- Consultas ao banco: < 100 ms (p95)

**Métricas de frontend:**
- First Contentful Paint (FCP): < 1,5 s
- Largest Contentful Paint (LCP): < 2,5 s
- Time to Interactive (TTI): < 3,5 s
- Cumulative Layout Shift (CLS): < 0,1

### Testes de carga

**Ferramentas:** Apache JMeter ou Artillery

**Cenários:**

**1. Carga normal**
- 100 usuários simultâneos
- Duração de 60 minutos
- Operações mistas (navegar, ler, enviar)
- **Meta:** < 500 ms de resposta média, 0% de erros

**2. Pico de carga**
- 500 usuários simultâneos
- Duração de 30 minutos
- Simula tráfego no dia do lançamento
- **Meta:** < 1000 ms de resposta média, < 1% de erros

**3. Teste de estresse**
- Ramp up até 1000+ usuários
- Identificar ponto de quebra
- Medir curva de degradação
- **Meta:** Degradação graciosa, sem crashes

**Exemplo de config do Artillery:**
```yaml
# artillery-config.yml
config:
  target: 'https://api.casezero.com'
  phases:
    - duration: 300
      arrivalRate: 10
      name: Warm up
    - duration: 600
      arrivalRate: 50
      name: Normal load
    - duration: 300
      arrivalRate: 100
      name: Peak load
  defaults:
    headers:
      Authorization: 'Bearer {{token}}'

scenarios:
  - name: Browse and start case
    flow:
      - get:
          url: '/api/cases'
      - get:
          url: '/api/cases/CASE-2024-001'
      - post:
          url: '/api/cases/CASE-2024-001/start'
          json:
            userId: '{{$randomUUID}}'
```

### Monitoramento de performance

**Em produção:**
- Azure Application Insights
- Monitoramento de usuários reais (RUM)
- Monitores sintéticos (uptime)
- Alerta quando p95 > 1000 ms

---

## 11.9 Testes de acessibilidade

### Conformidade com WCAG 2.1

**Meta:** nível AA (mínimo)

**Requisitos:**

**1. Perceptível**
- [ ] Todas as imagens com texto alternativo
- [ ] Contraste de texto ≥ 4,5:1
- [ ] Descrições em áudio para vídeos (se houver)
- [ ] Texto pode ser ampliado em 200% sem perda

**2. Operável**
- [ ] Toda funcionalidade disponível via teclado
- [ ] Sem armadilhas de teclado
- [ ] Links de pular navegação presentes
- [ ] Indicadores de foco visíveis (contorno de 2px)

**3. Compreensível**
- [ ] Idioma da página especificado (`lang="en"`)
- [ ] Navegação consistente
- [ ] Mensagens de erro claras
- [ ] Labels presentes em todos os inputs

**4. Robusto**
- [ ] HTML válido
- [ ] Marcos ARIA usados corretamente
- [ ] Compatível com leitores de tela

### Testes automatizados de acessibilidade

**Ferramentas:**
- **axe DevTools:** extensão de navegador
- **Lighthouse:** auditoria do Chrome
- **WAVE:** avaliador de acessibilidade web

**Integração em CI:**
```javascript
// accessibility.test.js
import { injectAxe, checkA11y } from 'axe-playwright';

test('dashboard is accessible', async ({ page }) => {
  await page.goto('/dashboard');
  await injectAxe(page);
  await checkA11y(page, null, {
    detailedReport: true,
    detailedReportOptions: { html: true }
  });
});
```

### Testes manuais de acessibilidade

**Leitores de tela:**
- **NVDA (Windows):** cobrir todos os fluxos críticos
- **JAWS (Windows):** testar fluxo de submissão
- **VoiceOver (macOS):** testar visualização de documentos

**Navegação por teclado:**
- Tab por todo o app
- Setas para listas
- Enter para acionar botões
- Escape para fechar modais

**Testes de daltonismo:**
- Usar simuladores de daltonismo
- Verificar se nenhuma informação crítica depende só de cor
- Testar com escala de cinza

---

## 11.10 Testes de segurança

### Scans de segurança automatizados

**SAST (análise estática):**
- **Snyk:** varredura de vulnerabilidades em dependências
- **SonarQube:** qualidade de código e issues de segurança
- Executar em todo PR

**DAST (análise dinâmica):**
- **OWASP ZAP:** pentests
- Executar semanalmente em staging

**Auditorias de dependências:**
```bash
# Frontend
npm audit

# Backend
dotnet list package --vulnerable
```

### Testes manuais de segurança

**Autenticação:**
- [ ] Senhas com hash (bcrypt)
- [ ] Tokens JWT expiram (1 hora)
- [ ] Refresh tokens giram
- [ ] HTTPS obrigatório
- [ ] CSRF habilitado

**Autorização:**
- [ ] Usuários acessam apenas próprias sessões
- [ ] Endpoints verificam ownership
- [ ] Tentativas de SQL injection falham
- [ ] XSS sanitizado

**Validação de entrada:**
- [ ] Todos os inputs validados no servidor
- [ ] Uploads restritos (apenas PDFs)
- [ ] Limites de tamanho aplicados
- [ ] Nomes de arquivo maliciosos rejeitados

---

## 11.11 Testes de regressão

### Manutenção da suíte de testes

**Após cada release:**
- Revisar testes falhos
- Atualizar testes para novas features
- Remover testes obsoletos
- Refatorar testes frágeis

**Suíte de regressão:**
- Todos os testes unitários (em cada commit)
- Todos os testes de integração (em PR)
- Testes E2E críticos (antes do deploy)

### Pipeline de integração contínua

```yaml
# .github/workflows/ci.yml
name: CI Pipeline

on:
  push:
    branches: [main, develop]
  pull_request:
    branches: [main]

jobs:
  frontend-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - uses: actions/setup-node@v3
        with:
          node-version: 18
      - run: npm ci
      - run: npm run lint
      - run: npm run test:unit
      - run: npm run test:coverage
      - uses: codecov/codecov-action@v3

  backend-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - uses: actions/setup-dotnet@v3
        with:
          dotnet-version: 9.0
      - run: dotnet restore
      - run: dotnet build
      - run: dotnet test --collect:"XPlat Code Coverage"
      - uses: codecov/codecov-action@v3

  integration-tests:
    runs-on: ubuntu-latest
    services:
      postgres:
        image: postgres:15
        env:
          POSTGRES_PASSWORD: test
    steps:
      - uses: actions/checkout@v3
      - run: dotnet test --filter Category=Integration

  e2e-tests:
    runs-on: ubuntu-latest
    if: github.ref == 'refs/heads/main'
    steps:
      - uses: actions/checkout@v3
      - uses: actions/setup-node@v3
      - run: npx playwright install
      - run: npm run test:e2e
```

---

## 11.12 Gestão de dados de teste

### Dados de casos de teste

**Seed data:**
- 2 casos de teste (1 Easy, 1 Medium)
- 5 usuários de teste com diversos estados de progresso
- Solicitações de perícia exemplo (pendente/completa)
- Submissões exemplo (correta/incorreta)

**Banco de teste:**
```csharp
// SeedData.cs
public static class SeedData
{
    public static void Initialize(AppDbContext context)
    {
        // Test users
        context.Users.AddRange(
            new UserEntity 
            { 
            UserId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Username = "testuser1",
            Email = "testuser1@example.com",
            PasswordHash = BCrypt.HashPassword("TestPass123!")
            }
        );
        
        // Test case
        context.Cases.Add(new CaseEntity
        {
          CaseId = "CASE-TEST-001",
          Title = "The Test Case Murder",
            Difficulty = Difficulty.Easy,
            CaseDataJson = LoadTestCaseJson()
        });
        
        context.SaveChanges();
    }
}
```

### Estratégia de reset de dados

**Após cada teste:**
- Reverter transações do banco (unit/integration)
- Usar banco in-memory (rápido)
- Limpar arquivos de teste

**Testes E2E:**
- Utilizar banco de teste separado
- Reset entre execuções
- Contas de teste isoladas

---

## 11.13 Rastreamento e resolução de bugs

### Níveis de severidade

**Critical (P0):**
- App quebra ou fica inutilizável
- Perda de dados
- Vulnerabilidade de segurança
- **SLA:** corrigir em até 24 horas

**High (P1):**
- Feature principal quebrada
- Degradação severa de UX
- Validação de conteúdo falha
- **SLA:** corrigir em até 3 dias

**Medium (P2):**
- Problema menor de feature
- Bug cosmético
- Existe workaround
- **SLA:** corrigir em até 1 semana

**Low (P3):**
- Solicitação de melhoria
- Ajuste menor
- Nice-to-have
- **SLA:** backlog

### Template de bug report

```markdown
# Bug Report

**Title:** [Short description]

**Severity:** [P0/P1/P2/P3]

**Environment:**
- Browser: [Chrome 120 / Firefox 121 / Safari 17]
- OS: [Windows 11 / macOS 14 / iOS 17]
- App Version: [v3.0.2]

**Steps to Reproduce:**
1. Step 1
2. Step 2
3. Step 3

**Expected Behavior:**
[What should happen]

**Actual Behavior:**
[What actually happens]

**Screenshots/Videos:**
[Attach if applicable]

**Console Errors:**
```
[Paste any error messages]
```

**Additional Context:**
[Any other relevant info]
```

---

## 11.14 Gates de qualidade

### Checklist pré-merge

**Antes de fazer merge do PR:**
- [ ] Todos os testes unitários passam
- [ ] Cobertura de código ≥ 80%
- [ ] Sem erros de lint
- [ ] Testes de integração aprovados
- [ ] Sem vulnerabilidades novas
- [ ] Código revisado por 1+ devs
- [ ] Documentação atualizada

### Checklist pré-deploy

**Antes de deploy em produção:**
- [ ] Todos os testes passam (unit, integration, E2E)
- [ ] Metas de performance atingidas
- [ ] Scan de segurança limpo
- [ ] Auditoria de acessibilidade aprovada
- [ ] Ambiente de staging testado
- [ ] Plano de rollback documentado
- [ ] Monitoramento com alertas configurado

### Checklist de publicação de caso

**Antes de publicar um caso:**
- [ ] Validação de schema JSON aprovada
- [ ] Integridade referencial verificada
- [ ] Todos os assets presentes e válidos
- [ ] QA tester completou playthrough cego
- [ ] Sem issues críticas abertas
- [ ] Dificuldade calibrada
- [ ] Aprovado pelo Content Manager
- [ ] Assets enviados para a CDN
- [ ] Entrada criada no banco

---

## 11.15 Resumo

**Estratégia de testes:**
- **70% Testes unitários:** componentes rápidos e isolados
- **20% Testes de integração:** verificação de API e banco
- **10% Testes E2E:** apenas fluxos críticos

**Metas de cobertura:**
- Frontend: 80% geral (componentes 80%, utils 90%)
- Backend: 80% geral (serviços 90%, validadores 95%)

**Testes automatizados:**
- Unit em cada commit
- Integração em PR
- E2E antes de deploy em produção
- Pipeline CI/CD reforça os gates

**Testes de conteúdo:**
- Validação automatizada (schema JSON, integridade)
- Playthrough manual (teste cego, 2-3 dias)
- Calibração de dificuldade via testes

**Performance:**
- Testes de carga (100-500 usuários)
- API < 200 ms (p95)
- Página < 3 segundos
- Monitoramento via Azure Application Insights

**Acessibilidade:**
- Conformidade WCAG 2.1 AA
- Testes automatizados (axe, Lighthouse)
- Testes manuais com leitores de tela
- Validação de navegação por teclado

**Segurança:**
- Scans SAST/DAST semanais
- Auditorias de dependências automatizadas
- Pentest manual
- HTTPS obrigatório, JWT auth

**Gates de qualidade:**
- Cobertura ≥ 80% para merge
- Todos os testes aprovados antes do deploy
- Playthrough de QA obrigatório para casos
- Aprovação do Content Manager é mandatória

---

**Próximo capítulo:** [12-ROADMAP.md](12-ROADMAP.md) – Cronograma de desenvolvimento e futuras features

**Documentos relacionados:**
- [08-TECNICO.md](08-TECNICO.md) – Arquitetura do sistema
- [10-PRODUCAO-DE-CONTEUDO.md](10-PRODUCAO-DE-CONTEUDO.md) – Processo de QA de conteúdo
- [09-ESQUEMA-DE-DADOS.md](09-ESQUEMA-DE-DADOS.md) – Regras de validação

---

**Histórico de revisões:**

| Data | Versão | Mudanças | Autor |
|------|--------|----------|-------|
| 14/11/2025 | 1.0 | Tradução completa para PT-BR | Assistente de IA |
