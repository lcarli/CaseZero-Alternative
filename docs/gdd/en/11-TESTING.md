# Chapter 11 - Testing Strategy & Quality Assurance

**Game Design Document - CaseZero v3.0**  
**Last Updated:** November 14, 2025  
**Status:** ✅ Complete

---

## 11.1 Overview

This chapter defines the **comprehensive testing strategy, QA processes, and quality gates** for CaseZero v3.0. It covers unit testing, integration testing, end-to-end testing, content testing, performance testing, and accessibility testing.

**Key Concepts:**
- Multi-layer testing approach
- Automated test coverage targets
- Content validation processes
- Performance benchmarks
- Accessibility compliance

---

## 11.2 Testing Philosophy

### Core Principles

**1. Test Early, Test Often**
- Write tests alongside code
- Catch bugs at source
- Reduce debugging time
- Maintain test coverage

**2. Automate Everything Possible**
- Unit tests run on every commit
- Integration tests on PR
- E2E tests before deployment
- Reduce manual testing burden

**3. Content is Code**
- case.json must pass validation
- Automated content checks
- QA playthrough required
- No exceptions for "creative" content

**4. User-Centric Testing**
- Test real user workflows
- Accessibility is mandatory
- Performance impacts UX
- Test on actual devices

---

## 11.3 Testing Pyramid

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

**Distribution:**
- **70% Unit Tests:** Fast, isolated, abundant
- **20% Integration Tests:** API endpoints, database operations
- **10% E2E Tests:** Critical user flows

**Rationale:**
- Unit tests are fast and catch most bugs
- Integration tests verify component interactions
- E2E tests validate user experience but are slower

---

## 11.4 Unit Testing

### Frontend Unit Tests (Vitest + React Testing Library)

**Coverage Targets:**
- **Components:** 80% coverage
- **Utilities:** 90% coverage
- **State Management:** 85% coverage
- **Overall:** 80% minimum

**What to Test:**

**Components:**
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

**Redux Slices:**
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

**Utility Functions:**
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

### Backend Unit Tests (xUnit + Moq)

**Coverage Targets:**
- **Controllers:** 75% coverage
- **Services:** 90% coverage
- **Validators:** 95% coverage
- **Overall:** 80% minimum

**What to Test:**

**Services:**
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

**Validators:**
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

**XP Calculation:**
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

## 11.5 Integration Testing

### API Integration Tests (WebApplicationFactory)

**Coverage:**
- All API endpoints
- Database operations
- Authentication flows
- Error handling

**Example Tests:**

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

### Database Integration Tests

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

## 11.6 End-to-End Testing

### E2E Test Framework (Playwright)

**Coverage:**
- Critical user flows only
- Happy path scenarios
- Essential error cases

**Critical Flows:**

**1. New User Registration & First Case**
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

**2. Complete Case Solve Flow**
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

**3. Forensics Real-Time Flow**
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

### E2E Test Strategy

**When to Run:**
- Before production deployment
- After major feature changes
- Weekly scheduled runs

**Test Environments:**
- **Staging:** Full E2E suite
- **Production:** Smoke tests only (no test data)

**Parallel Execution:**
- Run tests in parallel (4 workers)
- Total E2E runtime: ~15 minutes

---

## 11.7 Content Testing

### Automated Content Validation

**Pre-Publication Checks:**

**1. JSON Schema Validation**
```bash
# Run schema validator
node tools/case-validator.js cases/CASE-2024-001/case.json
```

**2. Referential Integrity**
```python
# Run clue checker
python tools/clue-checker.py cases/CASE-2024-001/case.json
```

**3. Asset Existence**
```bash
# Verify all asset files exist
node tools/asset-checker.js cases/CASE-2024-001/case.json
```

**4. File Size Checks**
```bash
# Check PDFs < 5MB, images < 2MB
node tools/size-checker.js cases/CASE-2024-001/
```

### Manual Content QA

**QA Tester Playthrough:**

**Pre-Test Setup:**
- QA tester has NOT seen case before (blind test)
- Forensics set to instant mode (for speed)
- Screen recording enabled
- Notepad open for issues

**Playthrough Process:**
1. Start case, note first impressions
2. Read all documents systematically
3. Examine all evidence
4. Request relevant forensics
5. Take notes on clues discovered
6. Attempt solution
7. Record time taken

**What to Check:**
- [ ] All clues present and discoverable
- [ ] No contradictions in documents
- [ ] Timeline is consistent
- [ ] Forensics results make sense
- [ ] Solution is provable with evidence
- [ ] No unintended solutions exist
- [ ] Red herrings are disprovable
- [ ] Difficulty feels appropriate
- [ ] Estimated time is accurate
- [ ] Typos/grammar errors

**QA Report Template:**
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

## 11.8 Performance Testing

### Performance Targets

**Page Load:**
- Dashboard: < 2 seconds
- Case load: < 3 seconds
- Document load: < 1 second
- Evidence photo: < 500ms

**API Response Times:**
- GET requests: < 200ms (p95)
- POST requests: < 500ms (p95)
- Database queries: < 100ms (p95)

**Frontend Metrics:**
- First Contentful Paint (FCP): < 1.5s
- Largest Contentful Paint (LCP): < 2.5s
- Time to Interactive (TTI): < 3.5s
- Cumulative Layout Shift (CLS): < 0.1

### Load Testing

**Tools:** Apache JMeter or Artillery

**Scenarios:**

**1. Normal Load**
- 100 concurrent users
- 60 minute duration
- Mixed operations (browse, read, submit)
- **Target:** < 500ms avg response time, 0% errors

**2. Peak Load**
- 500 concurrent users
- 30 minute duration
- Simulates launch day traffic
- **Target:** < 1000ms avg response time, < 1% errors

**3. Stress Test**
- Ramp up to 1000+ users
- Find breaking point
- Measure degradation curve
- **Target:** Graceful degradation, no crashes

**Example Artillery Config:**
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

### Performance Monitoring

**Production Monitoring:**
- Azure Application Insights
- Real user monitoring (RUM)
- Synthetic monitors (uptime checks)
- Alert on p95 > 1000ms

---

## 11.9 Accessibility Testing

### WCAG 2.1 Compliance

**Target:** AA Level (minimum)

**Requirements:**

**1. Perceivable**
- [ ] All images have alt text
- [ ] Text contrast ratio ≥ 4.5:1
- [ ] Audio descriptions for videos (if applicable)
- [ ] Text can be resized to 200% without loss of content

**2. Operable**
- [ ] All functionality available via keyboard
- [ ] No keyboard traps
- [ ] Skip navigation links present
- [ ] Focus indicators visible (2px outline)

**3. Understandable**
- [ ] Page language specified (lang="en")
- [ ] Navigation is consistent
- [ ] Error messages are clear
- [ ] Labels are present for all inputs

**4. Robust**
- [ ] Valid HTML
- [ ] ARIA landmarks used correctly
- [ ] Compatible with screen readers

### Automated Accessibility Testing

**Tools:**
- **axe DevTools:** Browser extension
- **Lighthouse:** Chrome audit
- **WAVE:** Web accessibility evaluator

**CI Integration:**
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

### Manual Accessibility Testing

**Screen Reader Testing:**
- **NVDA (Windows):** Test all critical flows
- **JAWS (Windows):** Test submission flow
- **VoiceOver (macOS):** Test document viewing

**Keyboard Navigation Testing:**
- Tab through entire app
- Use arrow keys for lists
- Enter to activate buttons
- Escape to close modals

**Color Blind Testing:**
- Use color blind simulators
- Verify no critical info is color-only
- Test with grayscale

---

## 11.10 Security Testing

### Automated Security Scans

**SAST (Static Analysis):**
- **Snyk:** Dependency vulnerability scanning
- **SonarQube:** Code quality and security issues
- Run on every PR

**DAST (Dynamic Analysis):**
- **OWASP ZAP:** Penetration testing
- Run against staging weekly

**Dependency Audits:**
```bash
# Frontend
npm audit

# Backend
dotnet list package --vulnerable
```

### Manual Security Testing

**Authentication Testing:**
- [ ] Passwords are hashed (bcrypt)
- [ ] JWT tokens expire (1 hour)
- [ ] Refresh tokens rotate
- [ ] HTTPS enforced
- [ ] CSRF protection enabled

**Authorization Testing:**
- [ ] Users can only access own sessions
- [ ] API endpoints check ownership
- [ ] SQL injection attempts fail
- [ ] XSS attempts sanitized

**Input Validation:**
- [ ] All inputs validated server-side
- [ ] File uploads restricted (PDFs only)
- [ ] File size limits enforced
- [ ] Malicious filenames rejected

---

## 11.11 Regression Testing

### Test Suite Maintenance

**After Each Release:**
- Review failed tests
- Update tests for new features
- Remove obsolete tests
- Refactor brittle tests

**Regression Test Suite:**
- All unit tests (runs on every commit)
- All integration tests (runs on PR)
- Critical E2E tests (runs before deploy)

### Continuous Integration Pipeline

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

## 11.12 Test Data Management

### Test Case Data

**Seed Data:**
- 2 test cases (1 Easy, 1 Medium)
- 5 test users with various progress states
- Sample forensic requests (pending/completed)
- Sample submissions (correct/incorrect)

**Test Database:**
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

### Data Reset Strategy

**After Each Test:**
- Rollback database transactions (unit/integration)
- Use in-memory database (fast)
- Clean up test files

**E2E Tests:**
- Use separate test database
- Reset between test runs
- Isolated test accounts

---

## 11.13 Bug Tracking & Resolution

### Bug Severity Levels

**Critical (P0):**
- App crashes or unusable
- Data loss
- Security vulnerability
- **SLA:** Fix within 24 hours

**High (P1):**
- Major feature broken
- Significant UX degradation
- Content validation fails
- **SLA:** Fix within 3 days

**Medium (P2):**
- Minor feature issue
- Cosmetic bug
- Workaround available
- **SLA:** Fix within 1 week

**Low (P3):**
- Enhancement request
- Minor improvement
- Nice-to-have
- **SLA:** Backlog

### Bug Report Template

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

## 11.14 Quality Gates

### Pre-Merge Checklist

**Before Merging PR:**
- [ ] All unit tests pass
- [ ] Code coverage ≥ 80%
- [ ] No linting errors
- [ ] Integration tests pass
- [ ] No new security vulnerabilities
- [ ] Code reviewed by 1+ developers
- [ ] Documentation updated

### Pre-Deployment Checklist

**Before Production Deploy:**
- [ ] All tests pass (unit, integration, E2E)
- [ ] Performance benchmarks met
- [ ] Security scan clean
- [ ] Accessibility audit pass
- [ ] Staging environment tested
- [ ] Rollback plan documented
- [ ] Monitoring alerts configured

### Case Publication Checklist

**Before Publishing Case:**
- [ ] JSON schema validation passed
- [ ] Referential integrity verified
- [ ] All assets present and valid
- [ ] QA tester completed blind playthrough
- [ ] No critical issues found
- [ ] Difficulty calibrated
- [ ] Content Manager approved
- [ ] Assets uploaded to CDN
- [ ] Database entry created

---

## 11.15 Summary

**Testing Strategy:**
- **70% Unit Tests:** Fast, isolated component testing
- **20% Integration Tests:** API and database verification
- **10% E2E Tests:** Critical user flows only

**Coverage Targets:**
- Frontend: 80% overall (components 80%, utils 90%)
- Backend: 80% overall (services 90%, validators 95%)

**Automated Testing:**
- Unit tests run on every commit
- Integration tests on PR
- E2E tests before production deploy
- CI/CD pipeline enforces quality gates

**Content Testing:**
- Automated validation (JSON schema, referential integrity)
- Manual QA playthrough (blind test, 2-3 days)
- Difficulty calibration via testing

**Performance:**
- Load testing (100-500 concurrent users)
- API response < 200ms (p95)
- Page load < 3 seconds
- Monitoring via Azure Application Insights

**Accessibility:**
- WCAG 2.1 AA compliance
- Automated testing (axe, Lighthouse)
- Manual screen reader testing
- Keyboard navigation validation

**Security:**
- SAST/DAST scans weekly
- Dependency audits automated
- Manual penetration testing
- HTTPS enforced, JWT auth

**Quality Gates:**
- Code coverage ≥ 80% to merge
- All tests pass before deploy
- Case QA playthrough required
- Content Manager approval mandatory

---

**Next Chapter:** [12-ROADMAP.md](12-ROADMAP.md) - Development roadmap and future features

**Related Documents:**
- [08-TECHNICAL.md](08-TECHNICAL.md) - System architecture
- [10-CONTENT-PIPELINE.md](10-CONTENT-PIPELINE.md) - Content QA process
- [09-DATA-SCHEMA.md](09-DATA-SCHEMA.md) - Validation rules

---

**Revision History:**

| Date | Version | Changes | Author |
|------|---------|---------|--------|
| 2025-11-14 | 1.0 | Initial complete draft | AI Assistant |
