# Developer Guide - CaseZero System

## Overview

Este guia abrangente fornece todas as informações necessárias para desenvolvedores contribuírem com o projeto CaseZero, desde configuração do ambiente até padrões de codificação e processo de desenvolvimento.

---

## Setup do Ambiente de Desenvolvimento

### 1. Pré-requisitos

| Software | Versão | Download |
|----------|--------|----------|
| Node.js | 18.x+ | https://nodejs.org/ |
| .NET SDK | 8.0+ | https://dotnet.microsoft.com/download |
| Git | 2.x+ | https://git-scm.com/ |
| Visual Studio Code | Latest | https://code.visualstudio.com/ |

### 2. Extensões Recomendadas (VS Code)

```json
{
  "recommendations": [
    "ms-dotnettools.csharp",
    "ms-dotnettools.vscode-dotnet-runtime",
    "bradlc.vscode-tailwindcss",
    "esbenp.prettier-vscode",
    "ms-vscode.vscode-typescript-next",
    "formulahendry.auto-rename-tag",
    "christian-kohler.path-intellisense",
    "ms-vscode.vscode-json",
    "redhat.vscode-yaml"
  ]
}
```

### 3. Configuração do Projeto

```bash
# 1. Clone do repositório
git clone https://github.com/lcarli/CaseZero-Alternative.git
cd CaseZero-Alternative

# 2. Configurar backend
cd backend/CaseZeroApi
dotnet restore

# 3. Configurar conexão com Azure SQL Database
# Edite appsettings.Development.json e configure a connection string do Azure
# Veja seção "Azure SQL Database Configuration" abaixo

# 4. Aplicar migrations ao banco de dados do Azure
dotnet ef database update

# 5. Configurar frontend
cd ../../frontend
npm install

# 4. Configurar variáveis de ambiente
cp .env.example .env.development
```

### 4. Azure SQL Database Configuration

**⚠️ IMPORTANTE: Este projeto SEMPRE usa Azure SQL Database (não LocalDB)**

Para configurar a conexão com o banco de dados do Azure:

1. **Obtenha a connection string do Azure SQL Database:**
   - Acesse o Portal do Azure (https://portal.azure.com)
   - Navegue até seu SQL Database
   - Vá em "Connection strings" > "ADO.NET"
   - Copie a connection string

2. **Configure em `appsettings.Development.json`:**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=tcp:your-server.database.windows.net,1433;Database=casezero-db;User ID=your-username;Password=your-password;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
  }
}
```

3. **Variáveis importantes:**
   - `your-server`: Nome do seu Azure SQL Server
   - `your-username`: Usuário do banco de dados
   - `your-password`: Senha do usuário
   - `casezero-db`: Nome do banco de dados

4. **Aplicar migrations:**
```bash
cd backend/CaseZeroApi
dotnet ef database update
```

**Validação:** O sistema irá falhar na inicialização se a connection string não estiver configurada ou contiver valores de placeholder.

### 5. Variáveis de Ambiente

**Backend (`appsettings.Development.json`):**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=tcp:casezero-sql-server.database.windows.net,1433;Database=casezero-db;User ID=admin;Password=YourPassword123!;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
  },
  "JwtSettings": {
    "SecretKey": "DevSecretKeyThatShouldBeAtLeast32Characters!",
    "Issuer": "CaseZeroApi",
    "Audience": "CaseZeroFrontend",
    "ExpiryDays": 7
  },
  "CaseSettings": {
    "CasesPath": "../../cases",
    "MaxFileSize": 10485760
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

**Frontend (`.env.development`):**
```env
VITE_API_BASE_URL=http://localhost:5000/api
VITE_ENV=development
VITE_LOG_LEVEL=debug
```

---

## Arquitetura do Projeto

### 1. Estrutura de Diretórios

```
CaseZero-Alternative/
├── backend/
│   └── CaseZeroApi/
│       ├── Controllers/        # API endpoints
│       ├── Models/            # Entidades de domínio
│       ├── DTOs/              # Data Transfer Objects
│       ├── Services/          # Lógica de negócio
│       ├── Data/              # DbContext e configurações
│       └── Migrations/        # Migrações do banco
├── frontend/
│   └── src/
│       ├── components/        # Componentes React
│       ├── pages/             # Páginas da aplicação
│       ├── contexts/          # Context providers
│       ├── hooks/             # Custom hooks
│       ├── services/          # API services
│       ├── types/             # Definições TypeScript
│       └── engine/            # Game engine
├── cases/                     # Sistema de casos
│   ├── CASE-2024-001/
│   └── CASE-2024-002/
├── docs/                      # Documentação
└── scripts/                   # Scripts utilitários
```

### 2. Fluxo de Dados

```
Frontend (React) ↔ API (ASP.NET Core) ↔ Database (SQLite)
       ↓                     ↓
   Context API        Entity Framework
       ↓                     ↓
  Custom Hooks         Repository Pattern
       ↓                     ↓
   Components            Services Layer
```

---

## Padrões de Desenvolvimento

### 1. Nomenclatura

**C# (Backend):**
```csharp
// Classes: PascalCase
public class CaseSessionService { }

// Métodos públicos: PascalCase
public async Task<Case> GetCaseAsync(string caseId) { }

// Métodos privados: PascalCase
private async Task ValidateCase(Case case) { }

// Propriedades: PascalCase
public string CaseId { get; set; }

// Campos privados: camelCase com underscore
private readonly ICaseService _caseService;

// Constantes: PascalCase
public const string DefaultRank = "Rookie";
```

**TypeScript (Frontend):**
```typescript
// Interfaces: PascalCase com 'I' prefix (opcional)
interface CaseData {
  caseId: string;
  title: string;
}

// Components: PascalCase
const CaseViewer: React.FC<CaseViewerProps> = ({ case }) => {
  // Hooks: camelCase
  const [isLoading, setIsLoading] = useState(false);
  
  // Functions: camelCase
  const handleSubmit = async () => { };
  
  return <div>{case.title}</div>;
};

// Constants: UPPER_SNAKE_CASE
const API_BASE_URL = 'http://localhost:5000/api';
```

### 2. Estrutura de Componentes React

```typescript
// CaseViewer.tsx
import React, { useState, useEffect } from 'react';
import styled from 'styled-components';
import { Case } from '../types/case';
import { useCaseContext } from '../hooks/useCaseContext';

// Props interface
interface CaseViewerProps {
  caseId: string;
  onCaseSelect?: (case: Case) => void;
}

// Styled components
const Container = styled.div`
  padding: 1rem;
  background: ${props => props.theme.colors.surface};
`;

// Main component
export const CaseViewer: React.FC<CaseViewerProps> = ({ 
  caseId, 
  onCaseSelect 
}) => {
  // Hooks
  const [isLoading, setIsLoading] = useState(false);
  const { currentCase, loadCase } = useCaseContext();

  // Effects
  useEffect(() => {
    if (caseId && caseId !== currentCase?.caseId) {
      handleLoadCase();
    }
  }, [caseId]);

  // Event handlers
  const handleLoadCase = async () => {
    setIsLoading(true);
    try {
      const case = await loadCase(caseId);
      onCaseSelect?.(case);
    } catch (error) {
      console.error('Failed to load case:', error);
    } finally {
      setIsLoading(false);
    }
  };

  // Render helpers
  const renderCaseInfo = () => (
    <div>
      <h2>{currentCase?.metadata.title}</h2>
      <p>{currentCase?.metadata.description}</p>
    </div>
  );

  // Main render
  if (isLoading) {
    return <div>Loading...</div>;
  }

  return (
    <Container>
      {currentCase ? renderCaseInfo() : <div>No case selected</div>}
    </Container>
  );
};

export default CaseViewer;
```

### 3. Estrutura de Controllers (Backend)

```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CasesController : ControllerBase
{
    private readonly ICaseService _caseService;
    private readonly ILogger<CasesController> _logger;

    public CasesController(
        ICaseService caseService,
        ILogger<CasesController> logger)
    {
        _caseService = caseService;
        _logger = logger;
    }

    /// <summary>
    /// Gets all available cases
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<CaseOverviewDto>), 200)]
    [ProducesResponseType(401)]
    public async Task<ActionResult<List<CaseOverviewDto>>> GetCases()
    {
        try
        {
            var cases = await _caseService.GetAvailableCasesAsync();
            return Ok(cases);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving cases");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Gets a specific case by ID
    /// </summary>
    [HttpGet("{caseId}")]
    [ProducesResponseType(typeof(CaseDetailsDto), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(401)]
    public async Task<ActionResult<CaseDetailsDto>> GetCase(string caseId)
    {
        if (string.IsNullOrEmpty(caseId))
            return BadRequest("Case ID is required");

        try
        {
            var case = await _caseService.GetCaseDetailsAsync(caseId);
            if (case == null)
                return NotFound($"Case {caseId} not found");

            return Ok(case);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving case {CaseId}", caseId);
            return StatusCode(500, "Internal server error");
        }
    }
}
```

---

## Processo de Desenvolvimento

### 1. Workflow Git

```bash
# 1. Criar branch para feature
git checkout -b feature/case-timeline-view

# 2. Fazer mudanças e commits frequentes
git add .
git commit -m "feat: add timeline component"

# 3. Push da branch
git push origin feature/case-timeline-view

# 4. Criar Pull Request no GitHub

# 5. Após aprovação, merge via GitHub
# 6. Limpar branch local
git checkout main
git pull origin main
git branch -d feature/case-timeline-view
```

### 2. Padrão de Commits (Conventional Commits)

```bash
# Tipos de commit
feat: nova funcionalidade
fix: correção de bug
docs: documentação
style: formatação (sem mudança funcional)
refactor: refatoração de código
test: adição ou correção de testes
chore: tarefas de manutenção

# Exemplos
git commit -m "feat: add forensic analysis request feature"
git commit -m "fix: resolve case loading timeout issue"
git commit -m "docs: update API documentation"
git commit -m "refactor: extract case validation logic to service"
```

### 3. Code Review Guidelines

**Checklist para Reviewer:**
- [ ] Código segue padrões de nomenclatura
- [ ] Lógica de negócio está clara e bem estruturada
- [ ] Tratamento de erros adequado
- [ ] Testes unitários incluídos (quando aplicável)
- [ ] Documentação atualizada
- [ ] Performance considerada
- [ ] Segurança verificada
- [ ] Acessibilidade considerada (frontend)

**Checklist para Author:**
- [ ] Auto-review realizado
- [ ] Testes passando
- [ ] Linting sem erros
- [ ] Commits bem organizados
- [ ] PR description clara
- [ ] Screenshots incluídas (mudanças UI)

---

## Testing

### 1. Backend Testing (C#)

**Unit Tests com xUnit:**
```csharp
public class CaseServiceTests
{
    private readonly Mock<ICaseRepository> _mockRepository;
    private readonly Mock<ILogger<CaseService>> _mockLogger;
    private readonly CaseService _service;

    public CaseServiceTests()
    {
        _mockRepository = new Mock<ICaseRepository>();
        _mockLogger = new Mock<ILogger<CaseService>>();
        _service = new CaseService(_mockRepository.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task GetCaseAsync_ValidId_ReturnsCase()
    {
        // Arrange
        var caseId = "CASE-2024-001";
        var expectedCase = new Case { CaseId = caseId, Title = "Test Case" };
        _mockRepository.Setup(r => r.GetByIdAsync(caseId))
                      .ReturnsAsync(expectedCase);

        // Act
        var result = await _service.GetCaseAsync(caseId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(caseId, result.CaseId);
        _mockRepository.Verify(r => r.GetByIdAsync(caseId), Times.Once);
    }

    [Fact]
    public async Task GetCaseAsync_InvalidId_ThrowsException()
    {
        // Arrange
        var caseId = "INVALID";
        _mockRepository.Setup(r => r.GetByIdAsync(caseId))
                      .ReturnsAsync((Case)null);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.GetCaseAsync(caseId));
    }
}
```

**Integration Tests:**
```csharp
public class CasesControllerIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public CasesControllerIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task GetCases_ReturnsSuccessAndCorrectContentType()
    {
        // Arrange
        var token = await GetValidJwtToken();
        _client.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/cases");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal("application/json; charset=utf-8", 
            response.Content.Headers.ContentType.ToString());
    }
}
```

### 2. Frontend Testing (React)

**Component Tests com React Testing Library:**
```typescript
// CaseViewer.test.tsx
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { CaseViewer } from './CaseViewer';
import { CaseContextProvider } from '../contexts/CaseContext';

const mockCase = {
  caseId: 'CASE-2024-001',
  metadata: {
    title: 'Test Case',
    description: 'Test Description'
  }
};

const renderWithContext = (component: React.ReactElement) => {
  return render(
    <CaseContextProvider>
      {component}
    </CaseContextProvider>
  );
};

describe('CaseViewer', () => {
  test('renders case title correctly', async () => {
    renderWithContext(<CaseViewer caseId="CASE-2024-001" />);
    
    await waitFor(() => {
      expect(screen.getByText('Test Case')).toBeInTheDocument();
    });
  });

  test('calls onCaseSelect when case is loaded', async () => {
    const mockOnCaseSelect = jest.fn();
    
    renderWithContext(
      <CaseViewer 
        caseId="CASE-2024-001" 
        onCaseSelect={mockOnCaseSelect} 
      />
    );

    await waitFor(() => {
      expect(mockOnCaseSelect).toHaveBeenCalledWith(mockCase);
    });
  });
});
```

**Hook Tests:**
```typescript
// useCaseContext.test.tsx
import { renderHook, act } from '@testing-library/react';
import { useCaseContext } from './useCaseContext';
import { CaseContextProvider } from '../contexts/CaseContext';

const wrapper = ({ children }: { children: React.ReactNode }) => (
  <CaseContextProvider>{children}</CaseContextProvider>
);

describe('useCaseContext', () => {
  test('should load case successfully', async () => {
    const { result } = renderHook(() => useCaseContext(), { wrapper });

    await act(async () => {
      await result.current.loadCase('CASE-2024-001');
    });

    expect(result.current.currentCase).toBeDefined();
    expect(result.current.currentCase?.caseId).toBe('CASE-2024-001');
  });
});
```

### 3. Comandos de Teste

```bash
# Backend
cd backend/CaseZeroApi
dotnet test                    # Executar todos os testes
dotnet test --filter "Unit"    # Executar apenas testes unitários
dotnet test --collect:"XPlat Code Coverage"  # Com coverage

# Frontend
cd frontend
npm test                       # Executar testes em modo watch
npm run test:ci               # Executar testes uma vez
npm run test:coverage         # Com coverage
```

---

## Debugging

### 1. Backend Debugging

**VS Code launch.json:**
```json
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": ".NET Core Launch (web)",
      "type": "coreclr",
      "request": "launch",
      "preLaunchTask": "build",
      "program": "${workspaceFolder}/backend/CaseZeroApi/bin/Debug/net8.0/CaseZeroApi.dll",
      "args": [],
      "cwd": "${workspaceFolder}/backend/CaseZeroApi",
      "stopAtEntry": false,
      "serverReadyAction": {
        "action": "openExternally",
        "pattern": "\\bNow listening on:\\s+(https?://\\S+)"
      },
      "env": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  ]
}
```

**Logging configurado:**
```csharp
public class CaseService
{
    private readonly ILogger<CaseService> _logger;

    public async Task<Case> GetCaseAsync(string caseId)
    {
        _logger.LogInformation("Loading case {CaseId}", caseId);
        
        try
        {
            var case = await _repository.GetByIdAsync(caseId);
            _logger.LogInformation("Successfully loaded case {CaseId}", caseId);
            return case;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load case {CaseId}", caseId);
            throw;
        }
    }
}
```

### 2. Frontend Debugging

**VS Code debugging:**
```json
{
  "name": "Launch Chrome",
  "request": "launch",
  "type": "chrome",
  "url": "http://localhost:5173",
  "webRoot": "${workspaceFolder}/frontend/src"
}
```

**Browser DevTools:**
```typescript
// Debug hooks em componentes
const CaseViewer = ({ caseId }: CaseViewerProps) => {
  const { currentCase, loadCase } = useCaseContext();
  
  // Debug logging
  useEffect(() => {
    console.log('CaseViewer rendered with caseId:', caseId);
    console.log('Current case state:', currentCase);
  }, [caseId, currentCase]);

  return (
    <div>
      {/* Component JSX */}
    </div>
  );
};
```

---

## Performance

### 1. Backend Performance

**Entity Framework Optimization:**
```csharp
// Incluir relações necessárias
var cases = await _context.Cases
    .Include(c => c.Evidences)
    .Include(c => c.Suspects)
    .Where(c => c.IsActive)
    .ToListAsync();

// Projection para DTOs
var caseOverviews = await _context.Cases
    .Where(c => c.IsActive)
    .Select(c => new CaseOverviewDto
    {
        CaseId = c.CaseId,
        Title = c.Title,
        Category = c.Category
    })
    .ToListAsync();

// Pagination
var pagedCases = await _context.Cases
    .Skip((page - 1) * pageSize)
    .Take(pageSize)
    .ToListAsync();
```

**Caching:**
```csharp
public class CachedCaseService : ICaseService
{
    private readonly ICaseService _baseService;
    private readonly IMemoryCache _cache;

    public async Task<Case> GetCaseAsync(string caseId)
    {
        var cacheKey = $"case_{caseId}";
        
        if (_cache.TryGetValue(cacheKey, out Case cachedCase))
        {
            return cachedCase;
        }

        var case = await _baseService.GetCaseAsync(caseId);
        _cache.Set(cacheKey, case, TimeSpan.FromMinutes(30));
        
        return case;
    }
}
```

### 2. Frontend Performance

**React Optimization:**
```typescript
// Memo para componentes pesados
const ExpensiveComponent = React.memo(({ data }: Props) => {
  // Expensive rendering logic
  return <div>{data.map(item => <Item key={item.id} {...item} />)}</div>;
});

// useMemo para cálculos pesados
const CaseStats = ({ cases }: Props) => {
  const stats = useMemo(() => {
    return calculateComplexStats(cases);
  }, [cases]);

  return <div>{stats.summary}</div>;
};

// useCallback para funções
const CaseList = ({ onCaseSelect }: Props) => {
  const handleSelect = useCallback((caseId: string) => {
    onCaseSelect(caseId);
  }, [onCaseSelect]);

  return (
    <div>
      {cases.map(case => (
        <CaseItem 
          key={case.id} 
          case={case} 
          onSelect={handleSelect} 
        />
      ))}
    </div>
  );
};
```

**Code Splitting:**
```typescript
// Lazy loading de componentes
const CaseViewer = React.lazy(() => import('./CaseViewer'));
const ForensicLab = React.lazy(() => import('./ForensicLab'));

const Desktop = () => {
  return (
    <div>
      <Suspense fallback={<div>Loading...</div>}>
        <CaseViewer />
        <ForensicLab />
      </Suspense>
    </div>
  );
};
```

---

## Contribuindo com Novos Casos

### 1. Estrutura de um Caso

```
cases/CASE-YYYY-XXX/
├── case.json                 # Configuração principal
├── evidence/                 # Arquivos de evidência
│   ├── documento.pdf
│   ├── foto.jpg
│   └── video.mp4
├── suspects/                 # Descrições de suspeitos
│   ├── suspeito1.txt
│   └── suspeito2.txt
├── forensics/                # Resultados de análises
│   ├── dna_resultado.pdf
│   └── digitais_resultado.pdf
├── memos/                    # Memorandos temporais
│   └── memo_chefe.txt
└── witnesses/                # Depoimentos
    └── testemunha.pdf
```

### 2. Arquivo case.json

```json
{
  "caseId": "CASE-2024-003",
  "metadata": {
    "title": "Roubo na Joalheria Central",
    "description": "Investigação de roubo com reféns",
    "difficulty": "Medium",
    "estimatedTime": "90 minutes",
    "category": "Robbery",
    "tags": ["robbery", "hostage", "jewelry"],
    "createdDate": "2024-01-15T00:00:00Z"
  },
  "evidences": [
    {
      "id": "evidence_001",
      "name": "Câmera de Segurança - Entrada",
      "type": "video",
      "description": "Gravação do momento do roubo",
      "filePath": "evidence/camera_entrada.mp4",
      "unlockRequirements": [],
      "metadata": {
        "timestamp": "2024-01-10T15:30:00Z",
        "duration": "00:05:30",
        "quality": "720p"
      }
    }
  ],
  "suspects": [
    {
      "id": "suspect_001",
      "name": "Carlos Silva",
      "age": 28,
      "occupation": "Desempregado",
      "description": "Ex-funcionário da joalheria",
      "filePath": "suspects/carlos_silva.txt",
      "alibi": "Estava em casa assistindo TV",
      "motive": "Demitido há 2 meses por justa causa",
      "unlockRequirements": ["evidence_001"]
    }
  ],
  "solution": {
    "primarySuspect": "suspect_002",
    "motive": "Necessidade financeira urgente",
    "keyEvidence": ["evidence_003", "forensic_001"],
    "timeline": [
      {
        "time": "15:00",
        "event": "Suspeito chega ao local",
        "evidence": ["evidence_001"]
      }
    ]
  }
}
```

### 3. Validação de Casos

```bash
# Validar estrutura do caso
./validate_case.sh CASE-2024-003

# Testar via API
curl -H "Authorization: Bearer $TOKEN" \
  http://localhost:5000/api/caseobject/CASE-2024-003/validate
```

---

## Scripts Utilitários

### 1. Script de Setup

```bash
#!/bin/bash
# scripts/setup-dev.sh

echo "🚀 Setting up CaseZero development environment..."

# Verificar pré-requisitos
command -v node >/dev/null 2>&1 || { echo "Node.js is required"; exit 1; }
command -v dotnet >/dev/null 2>&1 || { echo ".NET SDK is required"; exit 1; }

# Setup backend
echo "📦 Setting up backend..."
cd backend/CaseZeroApi
dotnet restore
dotnet ef database update

# Setup frontend
echo "🎨 Setting up frontend..."
cd ../../frontend
npm install

# Criar arquivos de configuração
echo "⚙️ Creating configuration files..."
if [ ! -f .env.development ]; then
    cp .env.example .env.development
fi

echo "✅ Setup completed!"
echo "Backend: cd backend/CaseZeroApi && dotnet run"
echo "Frontend: cd frontend && npm run dev"
```

### 2. Script de Linting

```bash
#!/bin/bash
# scripts/lint.sh

echo "🔍 Running linting..."

# Backend
echo "📋 Linting backend..."
cd backend/CaseZeroApi
dotnet format --verify-no-changes

# Frontend
echo "🎨 Linting frontend..."
cd ../../frontend
npm run lint

echo "✅ Linting completed!"
```

### 3. Script de Build

```bash
#!/bin/bash
# scripts/build.sh

echo "🔨 Building CaseZero..."

# Backend
echo "📦 Building backend..."
cd backend/CaseZeroApi
dotnet build -c Release

# Frontend
echo "🎨 Building frontend..."
cd ../../frontend
npm run build

echo "✅ Build completed!"
```

---

## Troubleshooting

### 1. Problemas Comuns

**Backend não inicia:**
```bash
# Verificar se as dependências estão instaladas
dotnet restore

# Verificar se o banco existe
dotnet ef database update

# Verificar logs
dotnet run --verbosity diagnostic
```

**Frontend não compila:**
```bash
# Limpar cache
rm -rf node_modules package-lock.json
npm install

# Verificar versão do Node
node --version  # Deve ser 18+

# Verificar TypeScript
npx tsc --noEmit
```

**Testes falhando:**
```bash
# Backend
dotnet clean
dotnet build
dotnet test

# Frontend
npm run test:ci
```

### 2. Debug de Performance

**Backend slow queries:**
```csharp
// Habilitar logging de queries
protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
{
    optionsBuilder.LogTo(Console.WriteLine, LogLevel.Information);
}
```

**Frontend performance:**
```typescript
// React DevTools Profiler
import { Profiler } from 'react';

const onRenderCallback = (id, phase, actualDuration) => {
  console.log('Component:', id, 'Phase:', phase, 'Duration:', actualDuration);
};

<Profiler id="CaseViewer" onRender={onRenderCallback}>
  <CaseViewer />
</Profiler>
```

### 3. Recursos Úteis

- [.NET Documentation](https://docs.microsoft.com/en-us/dotnet/)
- [React Documentation](https://reactjs.org/docs/)
- [TypeScript Handbook](https://www.typescriptlang.org/docs/)
- [Entity Framework Documentation](https://docs.microsoft.com/en-us/ef/)
- [SQLite Documentation](https://www.sqlite.org/docs.html)