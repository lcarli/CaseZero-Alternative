# Backend - CaseZero

Este diretório contém a Web API principal e seus testes automatizados. O pipeline **CaseGen.Functions** agora vive no diretório raiz `functions/CaseGen.Functions`.

## 📁 Estrutura

```
backend/
├── CaseZeroApi/                      # Web API REST (.NET 8)
│   ├── Controllers/                  # API endpoints
│   ├── Models/                       # Domain models
│   ├── DTOs/                         # Data Transfer Objects
│   ├── Data/                         # Entity Framework DbContext
│   ├── Services/                     # Business logic services
│   └── Program.cs                    # App configuration
├── CaseZeroApi.Tests/                # Unit tests (API)
├── CaseZeroApi.IntegrationTests/     # Integration tests
└── README.md                         # Este arquivo
```

---

## 🌐 CaseZeroApi - Web API REST

### 📋 Propósito

API REST principal do sistema CaseZero, responsável por:
- **Autenticação e autorização** de usuários (JWT)
- **CRUD de casos** investigativos
- **Gerenciamento de progresso** do jogador
- **Sistema de ranking** e estatísticas
- **Carregamento de casos** do filesystem

### 🛠️ Stack Tecnológica

- **Framework**: ASP.NET Core 8
- **ORM**: Entity Framework Core
- **Database**: Azure SQL Database (sempre, dev e prod)
- **Auth**: JWT Bearer tokens + ASP.NET Identity
- **Email**: SMTP via configuration

### ⚙️ Configuração do Banco de Dados

**⚠️ IMPORTANTE: Este projeto SEMPRE usa Azure SQL Database**

1. **Configure a connection string em `appsettings.Development.json`:**

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=tcp:your-server.database.windows.net,1433;Database=casezero-db;User ID=your-username;Password=your-password;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
  }
}
```

2. **Obtenha as credenciais no Azure Portal:**
   - SQL Server: `your-server.database.windows.net`
   - Database: `casezero-db`
   - User ID e Password: Configure no SQL Server

3. **O sistema validará a connection string no startup** e falhará se:
   - A connection string não estiver configurada
   - Contiver valores de placeholder (`your-server`, `your-username`)

### 📡 Principais Endpoints

#### Autenticação (`/api/auth`)
```http
POST /api/auth/register          # Registrar novo usuário
POST /api/auth/login             # Login com email/senha
POST /api/auth/verify-email      # Verificar email via token
GET  /api/auth/resend-verification # Reenviar email de verificação
```

#### Casos (`/api/caseobject`)
```http
GET  /api/caseobject             # Listar casos disponíveis
GET  /api/caseobject/{id}        # Obter caso específico
GET  /api/caseobject/{id}/validate # Validar estrutura do caso
```

#### Usuários (`/api/users`)
```http
GET  /api/users/me               # Dados do usuário atual
PUT  /api/users/me               # Atualizar perfil
GET  /api/users/stats            # Estatísticas do jogador
```

### 🔧 Como Executar

```bash
cd CaseZeroApi

# Restaurar dependências
dotnet restore

# Configure a connection string do Azure SQL em appsettings.Development.json
# Veja seção "Configuração do Banco de Dados" acima

# Aplicar migrações ao banco do Azure
dotnet ef database update

# Executar
dotnet run

# Disponível em: http://localhost:5000
```

### 🧪 Testes

```bash
# Unit tests
cd CaseZeroApi.Tests
dotnet test

# Integration tests
cd CaseZeroApi.IntegrationTests
dotnet test
```

---

## 🤖 CaseGen.Functions - Case Generator AI

> 📁 Código fonte: `functions/CaseGen.Functions`

### 📋 Propósito

Sistema de geração automática de casos detetivescos usando IA, baseado em Azure Functions com arquitetura modular.

### 🛠️ Stack Tecnológica

- **Framework**: Azure Functions (.NET 9 Isolated Worker)
- **LLM**: Azure OpenAI (GPT-4o)
- **Images**: DALL-E 3 via Azure OpenAI
- **Storage**: Azure Blob Storage + Table Storage
- **Caching**: RedTeam analysis caching
- **Monitoring**: Application Insights

### 🏗️ Arquitetura de Serviços (v2.0)

O sistema foi refatorado em **6 serviços especializados**, cada um responsável por uma fase específica da geração:

#### 1. **PlanGenerationService** (282 linhas)
**Fase 2: Planning**
- `PlanCoreAsync()` - Estrutura base do caso
- `PlanSuspectsAsync()` - Lista inicial de suspeitos
- `PlanTimelineAsync()` - Cronologia de eventos
- `PlanEvidenceAsync()` - Plano de evidências + Golden Truth

#### 2. **ExpandService** (513 linhas)
**Fase 3: Expansion**
- `ExpandSuspectAsync()` - Perfis detalhados de suspeitos
- `ExpandEvidenceAsync()` - Detalhamento de evidências
- `ExpandTimelineAsync()` - Expansão da timeline
- `SynthesizeRelationsAsync()` - Síntese de relações

#### 3. **DesignService** (361 linhas)
**Fase 4: Design**
- `DesignVisualConsistencyRegistryAsync()` - Registro de consistência visual
- `GenerateMasterReferencesAsync()` - Imagens de referência (suspeitos, evidências, locais)

#### 4. **DocumentGenerationService** (219 linhas)
**Fase 5: Document Generation**
- `GenerateDocumentFromSpecAsync()` - Conteúdo de documentos
- `RenderDocumentFromJsonAsync()` - Renderização PDF
- Suporta 6 tipos: police_report, interview, memo_admin, forensics_report, evidence_log, witness_statement

#### 5. **MediaGenerationService** (149 linhas)
**Fase 5: Media Generation**
- `GenerateMediaFromSpecAsync()` - Especificações de imagens
- `RenderMediaFromJsonAsync()` - Geração DALL-E 3
- Suporta: CCTV frames, document scans, scene photos, forensic photos

#### 6. **ValidationService** (218 linhas)
**Fase 6: Validation**
- `NormalizeCaseDeterministicAsync()` - Normalização de dados
- `ValidateRulesAsync()` - Validação de regras de qualidade
- `RedTeamGlobalAnalysisAsync()` - Análise macro de problemas
- `FixCaseAsync()` - Correções cirúrgicas via PrecisionEditor

### 📡 Principais Functions

#### Geração Completa
```http
POST /api/GenerateCase
Body: {
  "difficulty": "Detective",
  "timezone": "-03:00",
  "generateImages": true
}
```

#### Endpoints Individuais
```http
POST /api/PlanCore              # Fase 2: Planning
POST /api/ExpandSuspect         # Fase 3: Expansion
POST /api/DesignVisualRegistry  # Fase 4: Design
POST /api/RenderDocument        # Fase 5: PDF Rendering
POST /api/RenderMedia           # Fase 5: Image Generation
POST /api/ValidateCase          # Fase 6: Validation
```

### 🔧 Como Executar Localmente

```bash
cd ../functions/CaseGen.Functions

# Restaurar dependências
dotnet restore

# Compilar
dotnet build

# Executar (requer Azurite rodando)
cd bin/Debug/net9.0
func start

# Disponível em: http://localhost:7071
```

### ⚙️ Configuração Necessária

Criar `local.settings.json`:

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "CaseGeneratorStorage__ConnectionString": "UseDevelopmentStorage=true",
    "AzureOpenAI__Endpoint": "https://your-openai.openai.azure.com/",
    "AzureOpenAI__ApiKey": "your-api-key",
    "AzureOpenAI__DeploymentName": "gpt-4o",
    "AzureOpenAI__ImagesDeploymentName": "dall-e-3"
  }
}
```

### 🧪 Testando

Use os arquivos HTTP em `tests/http-requests/casegen-functions/`:

```bash
# Via VS Code REST Client extension
tests/http-requests/casegen-functions/test-real-pdf.http
tests/http-requests/casegen-functions/test-cover-page.http
```

---

## 📊 Métricas do Projeto

### CaseGen.Functions v2.0 Refactoring

| Métrica | Antes | Depois | Melhoria |
|---------|-------|--------|----------|
| **Linhas em CaseGenerationService** | 3,938 | ~300 (coordinator) | 92% redução |
| **Serviços especializados** | 0 | 6 | +6 |
| **Linhas organizadas** | 0 | 1,742 | - |
| **Complexidade** | Alta | Baixa | 56% redução |
| **Separation of Concerns** | ❌ | ✅ | - |
| **Build Errors** | - | 0 | ✅ |

### Distribuição de Código

```
CaseGeneration Services (1,742 lines total):
├── ExpandService (513) ████████████████████████████░░░░░░░░░░░░ 29%
├── DesignService (361) ████████████████████░░░░░░░░░░░░░░░░░░░░ 21%
├── PlanGenerationService (282) ██████████████████░░░░░░░░░░░░░░░░░░░ 16%
├── DocumentGenerationService (219) ████████████░░░░░░░░░░░░░░░░░░░░░░ 13%
├── ValidationService (218) ████████████░░░░░░░░░░░░░░░░░░░░░░░░░ 13%
└── MediaGenerationService (149) ████████░░░░░░░░░░░░░░░░░░░░░░░░░░░░░  8%
```

---

## 🔗 Links Úteis

- [📖 Documentação Completa](../docs/)
- [🏗️ Arquitetura Backend](../docs/BACKEND_ARCHITECTURE.md)
- [📋 Pipeline de Geração](../docs/CASE_GENERATION_PIPELINE.md)
- [📄 Templates PDF](../docs/PDF_DOCUMENT_TEMPLATES.md)
- [🧪 Testes HTTP](../tests/http-requests/README.md)
- [🚀 Deploy e Infraestrutura](../infrastructure/)

---

## 🤝 Contribuindo

### Adicionando Novos Serviços

1. Criar serviço em `functions/CaseGen.Functions/Services/CaseGeneration/`
2. Implementar interface com métodos async
3. Injetar dependências via constructor
4. Registrar no `Program.cs`:
   ```csharp
   builder.Services.AddScoped<NovoService>();
   ```

### Padrões de Código

- **Logging estruturado**: Use `ILogger<T>`
- **Async/await**: Todos os métodos I/O devem ser async
- **Dependency Injection**: Use constructor injection
- **Error handling**: Try-catch com logging apropriado
- **Cancellation tokens**: Suporte para operações longas

---

## 📝 Notas de Versão

### v2.0 (Outubro 2025)
- ✅ Refatoração completa do CaseGenerationService
- ✅ 6 serviços especializados criados
- ✅ Separação clara de responsabilidades
- ✅ Build bem-sucedido com 0 erros
- ⏳ Testes unitários em desenvolvimento (Task 3)

### v1.0 (Agosto 2025)
- ✅ CaseZeroApi inicial
- ✅ CaseGen.Functions inicial
- ✅ Sistema de autenticação JWT
- ✅ Pipeline de geração de casos com IA