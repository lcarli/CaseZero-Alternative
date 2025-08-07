# Documentação Técnica - CaseZero System

Esta pasta contém toda a documentação técnica detalhada do projeto CaseZero-Alternative, um sistema imersivo de investigação detetivesca.

## 📋 Índice da Documentação

### 🚀 Para Iniciantes
- **[README Principal](../README.md)** - Visão geral e como executar o projeto
- **[DEVELOPER_GUIDE.md](DEVELOPER_GUIDE.md)** - Guia completo para desenvolvedores

### 🏗️ Arquitetura e Design
- **[FRONTEND_ARCHITECTURE.md](FRONTEND_ARCHITECTURE.md)** - Arquitetura do frontend React
- **[BACKEND_ARCHITECTURE.md](BACKEND_ARCHITECTURE.md)** - Arquitetura do backend .NET
- **[DATABASE_SCHEMA.md](DATABASE_SCHEMA.md)** - Schema e estrutura do banco de dados

### 🔧 APIs e Integrações
- **[API.md](API.md)** - Documentação completa da REST API
- **[OBJETO_CASO.md](OBJETO_CASO.md)** - Sistema de casos modulares

### 🚀 Planejamento e Evolução
- **[FUTURE_FEATURES.md](FUTURE_FEATURES.md)** - Futuras funcionalidades e melhorias planejadas

### 🚀 Deploy e Operações
- **[DEPLOYMENT.md](DEPLOYMENT.md)** - Guia de deployment para diferentes ambientes
- **[TROUBLESHOOTING.md](TROUBLESHOOTING.md)** - Soluções para problemas comuns

---

## 🎯 Para que Tipo de Usuário?

### 👨‍💻 Desenvolvedores
Se você vai contribuir com código ou entender o sistema:
1. Comece com [DEVELOPER_GUIDE.md](DEVELOPER_GUIDE.md)
2. Leia [FRONTEND_ARCHITECTURE.md](FRONTEND_ARCHITECTURE.md) ou [BACKEND_ARCHITECTURE.md](BACKEND_ARCHITECTURE.md) dependendo da sua área
3. Consulte [API.md](API.md) para integração entre sistemas

### 🔧 DevOps/SysAdmins
Se você vai fazer deploy ou manter o sistema:
1. Leia [DEPLOYMENT.md](DEPLOYMENT.md)
2. Consulte [DATABASE_SCHEMA.md](DATABASE_SCHEMA.md) para entender o banco
3. Use [API.md](API.md) para health checks e monitoramento
4. Tenha [TROUBLESHOOTING.md](TROUBLESHOOTING.md) como referência para problemas

### 🎮 Game Designers
Se você vai criar novos casos investigativos:
1. Leia [OBJETO_CASO.md](OBJETO_CASO.md)
2. Consulte [DEVELOPER_GUIDE.md](DEVELOPER_GUIDE.md) seção "Contribuindo com Novos Casos"

### 🏢 Product Managers/Stakeholders
Se você quer entender o sistema tecnicamente:
1. Comece com [README Principal](../README.md)
2. Leia [FRONTEND_ARCHITECTURE.md](FRONTEND_ARCHITECTURE.md) e [BACKEND_ARCHITECTURE.md](BACKEND_ARCHITECTURE.md) para visão geral
3. Consulte [API.md](API.md) para funcionalidades disponíveis

---

## 📊 Stack Tecnológico Resumido

| Camada | Tecnologia | Versão | Documentação |
|--------|------------|--------|--------------|
| **Frontend** | React + TypeScript | 19.x | [FRONTEND_ARCHITECTURE.md](FRONTEND_ARCHITECTURE.md) |
| **Backend** | .NET Core API | 8.0 | [BACKEND_ARCHITECTURE.md](BACKEND_ARCHITECTURE.md) |
| **Banco** | SQLite + EF Core | 8.0 | [DATABASE_SCHEMA.md](DATABASE_SCHEMA.md) |
| **API** | REST + JWT | - | [API.md](API.md) |
| **Deploy** | Docker + Nginx | - | [DEPLOYMENT.md](DEPLOYMENT.md) |

---

## 🎮 Funcionalidades Principais

### Sistema de Investigação
- **Casos Modulares**: Sistema completo de casos investigativos ([OBJETO_CASO.md](OBJETO_CASO.md))
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
- **Problema no sistema?** → [TROUBLESHOOTING.md](TROUBLESHOOTING.md)
- **Endpoints da API?** → [API.md](API.md)
- **Como criar um novo caso?** → [OBJETO_CASO.md](OBJETO_CASO.md)
- **Futuras funcionalidades?** → [FUTURE_FEATURES.md](FUTURE_FEATURES.md)
- **Estrutura do banco?** → [DATABASE_SCHEMA.md](DATABASE_SCHEMA.md)
- **Como contribuir?** → [DEVELOPER_GUIDE.md](DEVELOPER_GUIDE.md)
- **Arquitetura React?** → [FRONTEND_ARCHITECTURE.md](FRONTEND_ARCHITECTURE.md)
- **Arquitetura .NET?** → [BACKEND_ARCHITECTURE.md](BACKEND_ARCHITECTURE.md)

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

A documentação segue o versionamento do projeto principal. Mudanças significativas na documentação:

- **v1.0.0** - Documentação inicial básica
- **v2.0.0** - Documentação técnica completa (atual)

Para versões anteriores da documentação, consulte as tags do repositório.