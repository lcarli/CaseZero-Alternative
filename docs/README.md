# Documentação Técnica - CaseZero System

Esta pasta contém toda a documentação técnica detalhada do projeto CaseZero-Alternative, um sistema imersivo de investigação detetivesca.

## 📋 Índice da Documentação

### 🚀 Para Iniciantes
- **[README Principal](../README.md)** - Visão geral e como executar o projeto
- **[DEVELOPER_GUIDE.md](DEVELOPER_GUIDE.md)** - Guia completo para desenvolvedores

### 🏗️ Arquitetura e Design
- **[FRONTEND_ARCHITECTURE.md](FRONTEND_ARCHITECTURE.md)** - Arquitetura do frontend React
- **[BACKEND_ARCHITECTURE.md](BACKEND_ARCHITECTURE.md)** - Arquitetura da API de gameplay e contas (`CaseZeroApi`)
- **[DATABASE_SCHEMA.md](DATABASE_SCHEMA.md)** - Schema e estrutura do banco de dados
- **[CASE_GENERATION_PIPELINE.md](CASE_GENERATION_PIPELINE.md)** - Arquitetura e pipeline do gerador de casos (`CaseGen.Functions`)
- **[DIFFICULTY_PROFILE_SYSTEM.md](DIFFICULTY_PROFILE_SYSTEM.md)** - Contratos de volume e topologia das sete dificuldades
- **[GAME_TIME_ENGINE.md](GAME_TIME_ENGINE.md)** - Relógio do jogo, eventos temporais e integração com sessões
- **[CASE_GENERATION_SOAK_TEST_2026-07-24.md](CASE_GENERATION_SOAK_TEST_2026-07-24.md)** - Resultado do batch real por dificuldade, falhas encontradas e critérios de prontidão
- **[PDF_DOCUMENT_TEMPLATES.md](PDF_DOCUMENT_TEMPLATES.md)** - Renderização e layouts profissionais de evidências

### 🔧 APIs e Integrações
- **[API_COMPLETE.md](API_COMPLETE.md)** - Documentação completa da REST API
- **[CASE_JSON_V2_SPEC.md](CASE_JSON_V2_SPEC.md)** - Especificação do `case.json` v2 (contrato único de casos)
- **[EMAIL_SYSTEM_IMPLEMENTATION.md](EMAIL_SYSTEM_IMPLEMENTATION.md)** - Geração, visibilidade e consumo de emails de caso
- **[CASE_GENERATOR_SETUP.md](CASE_GENERATOR_SETUP.md)** - Setup do gerador de casos com AI
- **[RUNNING_FUNCTIONS_LOCALLY.md](RUNNING_FUNCTIONS_LOCALLY.md)** - Rodar a Function App localmente (gerar caso em dev)

### 🧪 Testes e Validação
- **[MANUAL_SMOKE_v2.md](MANUAL_SMOKE_v2.md)** - Roteiro de smoke test manual do contrato e gameplay v2

### 🎮 Game Design Document (GDD)
- **[gdd/en/](gdd/en/)** - GDD em inglês (source of truth)
- **[gdd/br/](gdd/br/)** - GDD em português (tradução espelho)

### 🚀 Planejamento e Evolução
- **[FUTURE_FEATURES.md](FUTURE_FEATURES.md)** - Futuras funcionalidades e melhorias planejadas

### 🚀 Deploy e Operações
- **[DEPLOYMENT.md](DEPLOYMENT.md)** - Guia de deployment para diferentes ambientes
- **[TROUBLESHOOTING.md](TROUBLESHOOTING.md)** - Soluções para problemas comuns
- **[cicd/README.md](cicd/README.md)** - Workflows, infraestrutura e operação de CI/CD

---

## 🎯 Para que Tipo de Usuário?

### 👨‍💻 Desenvolvedores
Se você vai contribuir com código ou entender o sistema:
1. Comece com [DEVELOPER_GUIDE.md](DEVELOPER_GUIDE.md)
2. Leia [FRONTEND_ARCHITECTURE.md](FRONTEND_ARCHITECTURE.md) ou [BACKEND_ARCHITECTURE.md](BACKEND_ARCHITECTURE.md) dependendo da sua área
3. Para trabalhar no gerador, consulte [CASE_GENERATION_PIPELINE.md](CASE_GENERATION_PIPELINE.md)
4. Consulte [API_COMPLETE.md](API_COMPLETE.md) para integração entre sistemas

### 🔧 DevOps/SysAdmins
Se você vai fazer deploy ou manter o sistema:
1. Leia [DEPLOYMENT.md](DEPLOYMENT.md)
2. Consulte [cicd/README.md](cicd/README.md) para workflows e infraestrutura
3. Consulte [DATABASE_SCHEMA.md](DATABASE_SCHEMA.md) para entender o banco
4. Use [API_COMPLETE.md](API_COMPLETE.md) para rotas e health checks disponíveis
5. Tenha [TROUBLESHOOTING.md](TROUBLESHOOTING.md) como referência para problemas

### 🎮 Game Designers / Content Creators
Se você vai criar novos casos investigativos:
1. Leia [CASE_JSON_V2_SPEC.md](CASE_JSON_V2_SPEC.md) para entender o contrato `case.json`
2. Consulte [CASE_GENERATION_PIPELINE.md](CASE_GENERATION_PIPELINE.md) para geração automática com AI
3. Use [CASE_GENERATOR_SETUP.md](CASE_GENERATOR_SETUP.md) para configurar o gerador
4. Para o design do jogo em si, veja o GDD em [gdd/en/](gdd/en/) ou [gdd/br/](gdd/br/)

### 🏢 Product Managers/Stakeholders
Se você quer entender o sistema tecnicamente:
1. Comece com [README Principal](../README.md)
2. Leia [FRONTEND_ARCHITECTURE.md](FRONTEND_ARCHITECTURE.md) e [BACKEND_ARCHITECTURE.md](BACKEND_ARCHITECTURE.md) para visão geral
3. Consulte [CASE_GENERATION_PIPELINE.md](CASE_GENERATION_PIPELINE.md) para entender o gerador de casos
4. Consulte [API_COMPLETE.md](API_COMPLETE.md) para funcionalidades disponíveis

---

## 📊 Stack Tecnológico Resumido

| Camada | Tecnologia | Versão | Documentação |
|--------|------------|--------|--------------|
| **Frontend** | React + TypeScript | 19.x | [FRONTEND_ARCHITECTURE.md](FRONTEND_ARCHITECTURE.md) |
| **Backend API** | ASP.NET Core | 8.0 | [BACKEND_ARCHITECTURE.md](BACKEND_ARCHITECTURE.md) |
| **Functions** | Azure Functions (.NET) | 9.0 | [CASE_GENERATION_PIPELINE.md](CASE_GENERATION_PIPELINE.md) |
| **PDF Generation** | QuestPDF | 2025.7.1 | [PDF_DOCUMENT_TEMPLATES.md](PDF_DOCUMENT_TEMPLATES.md) |
| **Banco** | Azure SQL/SQL Server ou SQLite + EF Core | 9.x | [DATABASE_SCHEMA.md](DATABASE_SCHEMA.md) |
| **API** | REST + JWT | - | [API_COMPLETE.md](API_COMPLETE.md) |
| **Storage** | Azure Blob + Azurite | - | [CASE_GENERATION_PIPELINE.md](CASE_GENERATION_PIPELINE.md) |

---

## 🎮 Funcionalidades Principais

### Sistema de Investigação
- **Casos Modulares**: Sistema completo de casos investigativos ([CASE_JSON_V2_SPEC.md](CASE_JSON_V2_SPEC.md))
- **Evidências Interativas**: Documentos, fotos, vídeos, análises forenses
- **Progressão Controlada**: Desbloqueio baseado em evidências e tempo
- **Timeline Dinâmica**: Reconstrução cronológica dos eventos

### Interface de Desktop Policial
- **Ambiente Autêntico**: Simulação de sistema policial real
- **Múltiplas Janelas**: Sistema de janelas gerenciável tipo desktop
- **Aplicações Integradas**: Visualizador de evidências, laboratório forense, perfis de suspeitos

### Sistema de Usuários
- **Autenticação JWT**: Sistema seguro de login/registro
- **Ranks e Progressão**: Sistema de classificação de detetives
- **Aprovação Admin**: Controle de acesso ao sistema

---

## 🔍 Busca Rápida

Procurando informações sobre:

- **Como rodar o projeto?** → [README Principal](../README.md) seção "Como Executar"
- **Como fazer deploy?** → [DEPLOYMENT.md](DEPLOYMENT.md)
- **Como funciona o CI/CD?** → [cicd/README.md](cicd/README.md)
- **Problema no sistema?** → [TROUBLESHOOTING.md](TROUBLESHOOTING.md)
- **Endpoints da API?** → [API_COMPLETE.md](API_COMPLETE.md)
- **Como criar um novo caso?** → [CASE_GENERATION_PIPELINE.md](CASE_GENERATION_PIPELINE.md)
- **Futuras funcionalidades?** → [FUTURE_FEATURES.md](FUTURE_FEATURES.md)
- **Estrutura do banco?** → [DATABASE_SCHEMA.md](DATABASE_SCHEMA.md)
- **Como contribuir?** → [DEVELOPER_GUIDE.md](DEVELOPER_GUIDE.md)
- **Arquitetura React?** → [FRONTEND_ARCHITECTURE.md](FRONTEND_ARCHITECTURE.md)
- **Arquitetura da API .NET?** → [BACKEND_ARCHITECTURE.md](BACKEND_ARCHITECTURE.md)
- **Arquitetura do gerador de casos?** → [CASE_GENERATION_PIPELINE.md](CASE_GENERATION_PIPELINE.md)
- **Como validar manualmente um caso?** → [MANUAL_SMOKE_v2.md](MANUAL_SMOKE_v2.md)

---

## 📝 Como Contribuir com a Documentação

1. **Identifique a lacuna**: O que está faltando ou desatualizado?
2. **Determine o arquivo**: Qual documento deve ser atualizado?
3. **Siga o padrão**: Mantenha consistência com o formato existente
4. **Teste as instruções**: Certifique-se que funcionam na prática
5. **Faça um PR**: Submeta suas mudanças para revisão

### Padrões da Documentação
- Use Markdown com sintaxe GitHub
- Inclua exemplos de código quando relevante
- Adicione diagramas para conceitos complexos
- Mantenha índices atualizados
- Use linguagem clara e objetiva

---

## 🔄 Versionamento da Documentação

A documentação acompanha as mudanças significativas do projeto. A versão atual
reflete o estado do site, da infraestrutura e do gerador em julho de 2026,
incluindo o contrato `case.json` **v2**, retries completos, progresso por fases,
solver bloqueante e publicação validada no Blob.

Para versões anteriores da documentação, consulte as tags do repositório.