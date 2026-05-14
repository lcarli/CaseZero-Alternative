# CaseZero - Detective Investigation System

[![Deploy to DEV](https://github.com/lcarli/CaseZero-Alternative/actions/workflows/cd-dev.yml/badge.svg)](https://github.com/lcarli/CaseZero-Alternative/actions/workflows/cd-dev.yml)
[![Deploy to PROD](https://github.com/lcarli/CaseZero-Alternative/actions/workflows/cd-prod.yml/badge.svg)](https://github.com/lcarli/CaseZero-Alternative/actions/workflows/cd-prod.yml)
[![Deploy 3-Tier Infrastructure](https://github.com/lcarli/CaseZero-Alternative/actions/workflows/infrastructure-3tier.yml/badge.svg)](https://github.com/lcarli/CaseZero-Alternative/actions/workflows/infrastructure-3tier.yml)

Um sistema imersivo de investigação detetivesca onde você assume o papel de um detetive experiente resolvendo casos complexos.

## 🎮 Características do Sistema

- **Interface Completa**: Home page, login, registro, dashboard e ambiente desktop
- **Autenticação Segura**: Sistema de JWT com controle de acesso
- **Gestão de Casos**: Dashboard com estatísticas e progresso do usuário
- **Interface Policial Autêntica**: Ambiente desktop simulando sistemas policiais reais
- **API Robusta**: Backend .NET com Entity Framework e SQLite
- **Sistema Objeto Caso**: Estrutura modular para criação de casos investigativos

## 🔍 Sistema Objeto Caso

**NOVO!** Sistema completo para criação e gerenciamento de casos investigativos modulares:

### Características Principais:
- **Casos Modulares**: Estrutura padronizada para fácil criação de novos casos
- **Progressão Controlada**: Sistema de desbloqueio baseado em evidências e tempo
- **Análises Forenses**: Simulação realista de laboratório com tempos de resposta
- **Eventos Temporais**: Memos, testemunhas e atualizações que aparecem dinamicamente
- **Timeline Interativa**: Reconstrução cronológica dos eventos do crime
- **API REST Completa**: Endpoints para carregamento e validação de casos

### Exemplo Incluído: Case001
"Homicídio no Edifício Corporativo" - Um caso completo de assassinato com:
- 6 evidências interconectadas (documentos, fotos, vídeos, dados)
- 3 suspeitos com perfis detalhados e motivos
- 4 análises forenses (DNA, digitais, perícia digital)
- 3 eventos temporais (memos do chefe, nova testemunha, atualizações)
- Timeline completa do crime
- Solução definitiva com evidências conclusivas

### Documentação:
- 📖 [Documentação Completa do Sistema Objeto Caso](docs/OBJETO_CASO.md)
- 🧪 [Testes HTTP](tests/http-requests/README.md) - Endpoints de teste

## 🤖 Case Generator AI

**NOVO!** Sistema de geração automática de casos usando IA:

### Características:
- **Pipeline AI de 6 Fases**: Geração completa automatizada de casos
  - Phase 1: **Seeding** - Criação de arquivos base e bundle ID
  - Phase 2: **Planning** - Planejamento hierárquico (Core → Suspects → Timeline → Evidence)
  - Phase 3: **Expansion** - Expansão detalhada de conteúdo
  - Phase 4: **Design** - Visual consistency registry e master references
  - Phase 5: **Generation** - Documentos PDF e imagens via DALL-E
  - Phase 6: **Validation** - Normalização + RedTeam analysis + Surgical fixes
- **Arquitetura Modular**: 6 serviços especializados para cada fase
- **Azure Functions**: Orquestração com .NET 9.0 Isolated Worker
- **Monitoramento Completo**: Application Insights e logging estruturado
- **Storage Dedicado**: Armazenamento para casos e bundles gerados

### Arquitetura de Serviços:
```
functions/CaseGen.Functions/Services/CaseGeneration/
├── PlanGenerationService.cs      (282 lines) - Planejamento hierárquico
├── ExpandService.cs              (513 lines) - Expansão de conteúdo
├── DesignService.cs              (361 lines) - Design visual
├── DocumentGenerationService.cs  (219 lines) - Geração de PDFs
├── MediaGenerationService.cs     (149 lines) - Geração de imagens
└── ValidationService.cs          (218 lines) - Validação e RedTeam
```

### Documentação:
- 🤖 [Setup Completo do Case Generator](docs/CASE_GENERATOR_SETUP.md)
- 📋 [Pipeline de Geração](docs/CASE_GENERATION_PIPELINE.md)
- 🏗️ [Arquitetura do Backend](docs/BACKEND_ARCHITECTURE.md)
- 🧪 [Testes HTTP](tests/http-requests/README.md)

### Deploy Rápido:
```bash
# Via GitHub Actions - Workflow: "🤖 Deploy Case Generator Infrastructure"
# Ou manualmente:
az deployment group create \
  --resource-group casezero-casegen-dev-rg \
  --template-file infrastructure/Functions/main.bicep \
  --parameters @infrastructure/Functions/parameters.dev.json
```

## 🚀 Como Executar

> **🔧 CI/CD Disponível**: Este projeto inclui pipelines completos de CI/CD com GitHub Actions. Veja a [documentação de CI/CD](docs/cicd/README.md) para implantação automatizada em Azure.

### Pré-requisitos

- Node.js 18+ e npm
- .NET 8 SDK
- Git

### 1. Clone o Repositório

```bash
git clone https://github.com/lcarli/CaseZero-Alternative.git
cd CaseZero-Alternative
```

### 2. Configure o Backend

```bash
cd backend/CaseZeroApi

# Restaurar dependências
dotnet restore

# Executar o servidor (irá criar banco SQLite automaticamente)
dotnet run
```

O backend estará disponível em: `http://localhost:5000`

### 3. Configure o Frontend

Em outro terminal:

```bash
cd frontend

# Instalar dependências
npm install

# Nota: É normal aparecer algumas vulnerabilidades moderadas de dependências
# Execute 'npm audit' para ver detalhes, mas evite 'npm audit fix --force'
# pois pode quebrar compatibilidade

# Executar servidor de desenvolvimento
npm run dev
```

O frontend estará disponível em: `http://localhost:5173`

> **⚠️ Nota sobre Testes**: Durante desenvolvimento ativo, alguns testes podem falhar temporariamente. Para verificar se a aplicação está funcionando, teste manualmente a interface e as funcionalidades principais. Veja [TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md) para soluções de problemas comuns.

## 🔐 Sistema de Usuários

O sistema implementa um fluxo moderno de autenticação com verificação por email:

### Registro Simplificado
- **Apenas 3 campos necessários**: Nome, Sobrenome e Email Pessoal
- **Email Institucional Automático**: Gerado no formato `{nome}.{sobrenome}@fic-police.gov`
- **Dados Automáticos**: Badge, departamento (ColdCase) e posição (rook) gerados automaticamente

### Verificação por Email
- Email de verificação enviado para o email pessoal
- Design HTML responsivo seguindo identidade visual do jogo
- Token de verificação válido por 24 horas
- Email de boas-vindas após verificação

### Níveis de Acesso
- **Rook**: Nível inicial para novos usuários
- **Detective**: Nível intermediário
- **Sergeant, Lieutenant, Captain, Commander**: Níveis avançados

## 🧪 Testando o Sistema

### Via Arquivos HTTP (REST Client):

Utilize os arquivos `.http` organizados em `tests/http-requests/`:

```
tests/http-requests/
├── test-casegen.http              # Testes gerais de geração
├── casegen-functions/
│   ├── test-real-pdf.http         # Testes de PDF rendering
│   └── test-cover-page.http       # Testes de cover page
└── casezero-api/
    └── CaseZeroApi.http           # Testes de API endpoints
```

Veja [documentação completa](tests/http-requests/README.md) para instruções de uso.

### Via API Manual:
```bash
# 1. Obter token de autenticação (substitua pelas suas credenciais)
TOKEN=$(curl -X POST "http://localhost:5000/api/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"email": "john.doe@fic-police.gov", "password": "Password123!"}' | \
  jq -r '.token')

# 2. Listar casos disponíveis
curl -H "Authorization: Bearer $TOKEN" \
  http://localhost:5000/api/caseobject

# 3. Carregar Case001
curl -H "Authorization: Bearer $TOKEN" \
  http://localhost:5000/api/caseobject/Case001

# 4. Validar estrutura do caso
curl -H "Authorization: Bearer $TOKEN" \
  http://localhost:5000/api/caseobject/Case001/validate
```

## 🏗️ Arquitetura

### Frontend (React + TypeScript)
- **Framework:** React 19 com TypeScript
- **Roteamento:** React Router DOM
- **Estilização:** styled-components
- **Build:** Vite
- **Autenticação:** Context API + JWT tokens

### Backend (.NET Core)

**CaseZeroApi** (Web API)
- **Framework:** ASP.NET Core 8
- **Banco de Dados:** SQLite com Entity Framework Core
- **Autenticação:** JWT + ASP.NET Identity
- **API:** RESTful endpoints
- **CORS:** Configurado para localhost:5173
- **Sistema de Casos:** CaseObjectService + API endpoints

**CaseGen.Functions** (Azure Functions)
- **Runtime:** .NET 9.0 Isolated Worker
- **Arquitetura:** 6 serviços especializados (1,742 linhas organizadas)
- **Storage:** Azure Blob Storage + Table Storage
- **LLM:** Azure OpenAI (GPT-4o)
- **Images:** DALL-E 3 via Azure OpenAI
- **Caching:** RedTeam analysis caching
- **Logging:** Structured logging com Application Insights

## 📋 Fluxo do Usuário

1. **Home Page** - Apresentação do jogo detetivesco
2. **Registro** - Registro simplificado com verificação por email
3. **Verificação de Email** - Ativação da conta via email pessoal
4. **Login** - Autenticação com email institucional/senha
5. **Dashboard** - Visão geral de estatísticas e casos
6. **Desktop** - Ambiente de trabalho para investigação de casos
7. **Casos** - Sistema modular de casos investigativos

## 🗂️ Estrutura do Projeto

```
├── frontend/                      # React frontend
│   ├── src/
│   │   ├── components/           # Componentes reutilizáveis
│   │   ├── contexts/             # React Contexts (Auth, Window)
│   │   ├── hooks/                # Custom hooks
│   │   ├── pages/                # Páginas da aplicação
│   │   └── services/             # API services
├── backend/                      # .NET backend API only
│   ├── CaseZeroApi/             # Web API (.NET 8)
│   │   ├── Controllers/         # API controllers
│   │   ├── Models/              # Modelos de dados
│   │   ├── DTOs/                # Data Transfer Objects
│   │   ├── Data/                # DbContext
│   │   └── Services/            # Business logic
│   ├── CaseZeroApi.Tests/       # Unit tests (API)
│   └── CaseZeroApi.IntegrationTests/  # Integration tests
├── functions/                   # Azure Functions case generator
│   └── CaseGen.Functions/       # Durable pipeline (.NET 9)
│       ├── Functions/           # Function endpoints
│       ├── Services/
│       │   └── CaseGeneration/ # 6 serviços especializados
│       ├── Models/              # Domain models
│       └── Schemas/             # JSON schemas
├── cases/                        # Casos investigativos
│   └── case_001/                # The Missing Heir (v2 reference case)
│       ├── case.json            # Configuração do caso (v2)
│       └── assets/              # Assets do caso (PDFs, fotos, áudio, etc.)
├── tests/
│   └── http-requests/           # Testes HTTP REST Client
│       ├── test-casegen.http    # Testes gerais
│       ├── casegen-functions/   # Testes Functions
│       └── casezero-api/        # Testes API
├── infrastructure/              # IaC (Bicep templates)
│   ├── Functions/               # Case Generator infra
│   └── Web/                     # Web App infra
└── docs/                        # Documentação técnica
    ├── BACKEND_ARCHITECTURE.md
    ├── CASE_GENERATION_PIPELINE.md
    ├── CASE_JSON_V2_SPEC.md     # ← Canonical case contract
    └── PDF_DOCUMENT_TEMPLATES.md
```

## 🔧 Desenvolvimento

### Comandos Úteis

**Frontend:**
```bash
npm run dev      # Servidor de desenvolvimento
npm run build    # Build para produção
npm run lint     # Verificar código
npm run test     # Executar testes (vitest)
npm run test:run # Executar testes uma vez
npm audit        # Verificar vulnerabilidades (normal ter algumas moderadas)
```

**Backend:**
```bash
dotnet run              # Executar servidor
dotnet build           # Compilar projeto
dotnet test            # Executar testes (alguns podem falhar durante desenvolvimento)
dotnet ef migrations   # Gerenciar migrações
```

**Testes:**
```bash
# Usar REST Client extension no VS Code
# Abrir arquivos em tests/http-requests/*.http
```

## 📈 Criando Novos Casos

### Método 1: Geração Automática com IA

Use o **Case Generator AI** para gerar casos completos automaticamente:

```bash
# Via HTTP REST Client
# Ver tests/http-requests/casegen-functions/test-real-pdf.http
POST http://localhost:7071/api/GenerateCase
Content-Type: application/json

{
  "difficulty": "Detective",
  "timezone": "-03:00",
  "generateImages": true
}
```

### Método 2: Criação Manual

1. **Copie a estrutura de um caso existente**:
```bash
cp -r cases/case_001 cases/case_002
```

2. **Edite o arquivo `case.json`** com novos dados

3. **Substitua os arquivos** nas subpastas com novo conteúdo

4. **Teste via API** com os endpoints do CaseObjectController

Ver [documentação completa](docs/OBJETO_CASO.md) para detalhes sobre estrutura de casos.

## 🛡️ Segurança

- Autenticação JWT com tokens de 7 dias
- Senhas hasheadas com BCrypt
- Validação de dados no frontend e backend
- Proteção de rotas sensíveis
- CORS configurado adequadamente
- Validação de arquivos de caso com sandboxing

## 📱 Responsividade

O sistema foi desenvolvido com design responsivo, funcionando em:
- Desktop (resolução primária)
- Tablets 
- Dispositivos móveis

## 🤝 Contribuindo

1. Faça um fork do projeto
2. Crie uma branch para sua feature (`git checkout -b feature/AmazingFeature`)
3. Commit suas mudanças (`git commit -m 'Add AmazingFeature'`)
4. Push para a branch (`git push origin feature/AmazingFeature`)
5. Abra um Pull Request

### Criando Novos Casos:
1. Use o **Case Generator AI** para geração automática via IA
2. Ou copie um caso existente como template manual
3. Siga a estrutura documentada em `docs/OBJETO_CASO.md`
4. Teste com arquivos HTTP em `tests/http-requests/`

## 📄 Licença

Este projeto está licenciado sob a Licença MIT - veja o arquivo [LICENSE](LICENSE) para detalhes.

## � Atualizações Recentes

### v2.0 - Refatoração e Organização (Outubro 2025)

**✅ Concluído:**
- **Task 1**: 7 PDF templates documentados + limpeza de documentação
- **Task 2**: CaseGenerationService dividido em 6 serviços especializados (3,938 → 1,742 linhas organizadas)
- **Task 4**: Removidos arquivos `package-lock.json` órfãos
- **Task 6**: Arquivos `.http` organizados em `tests/http-requests/`

**📊 Métricas:**
- **Redução de complexidade**: 56% do código organizado
- **Serviços criados**: 6 especializados + 1 coordinator
- **Build status**: ✅ 0 erros, 3 warnings pré-existentes
- **Cobertura de testes**: Em desenvolvimento (Task 3)

### Próximas Melhorias
- [ ] **Task 3**: Testes unitários para os 6 novos serviços
- [ ] Atualização da documentação de arquitetura
- [ ] Melhorias na pipeline de CI/CD

## �🚀 CI/CD e DevOps

Este projeto utiliza práticas modernas de DevOps com:

- **CI/CD Automatizado**: GitHub Actions com pipelines para desenvolvimento e produção
- **Infraestrutura como Código**: Templates BICEP para Azure
- **Testes Automatizados**: Testes unitários e de integração
- **Segurança**: Verificações de segurança e análise de vulnerabilidades
- **Monitoramento**: Application Insights e alertas de saúde

### 🔗 Links Úteis

- [📖 Documentação de CI/CD](docs/cicd/README.md)
- [🏗️ Guia de Configuração Azure](docs/cicd/azure-setup.md)
- [🔐 Variáveis e Secrets](docs/cicd/variables-and-secrets.md)
- [📊 Sistema Objeto Caso](docs/OBJETO_CASO.md)
- [🧪 Testes HTTP](tests/http-requests/README.md)
- [📋 Pipeline de Geração](docs/CASE_GENERATION_PIPELINE.md)
- [📄 Templates PDF](docs/PDF_DOCUMENT_TEMPLATES.md)
