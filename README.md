# CaseZero - Detective Investigation System

[![CI](https://github.com/lcarli/CaseZero-Alternative/actions/workflows/ci.yml/badge.svg)](https://github.com/lcarli/CaseZero-Alternative/actions/workflows/ci.yml)
[![Deploy to DEV](https://github.com/lcarli/CaseZero-Alternative/actions/workflows/cd-dev.yml/badge.svg)](https://github.com/lcarli/CaseZero-Alternative/actions/workflows/cd-dev.yml)
[![Deploy to PROD](https://github.com/lcarli/CaseZero-Alternative/actions/workflows/cd-prod.yml/badge.svg)](https://github.com/lcarli/CaseZero-Alternative/actions/workflows/cd-prod.yml)
[![Infrastructure](https://github.com/lcarli/CaseZero-Alternative/actions/workflows/infrastructure.yml/badge.svg)](https://github.com/lcarli/CaseZero-Alternative/actions/workflows/infrastructure.yml)

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
- 🛠️ Script de validação: `./validate_case.sh Case001`

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

# Executar servidor de desenvolvimento
npm run dev
```

O frontend estará disponível em: `http://localhost:5173`

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

## 🧪 Testando o Sistema Objeto Caso

### Via Script de Validação:
```bash
./validate_case.sh Case001
```

### Via API:
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
- **Framework:** ASP.NET Core 8
- **Banco de Dados:** SQLite com Entity Framework Core
- **Autenticação:** JWT + ASP.NET Identity
- **API:** RESTful endpoints
- **CORS:** Configurado para localhost:5173
- **Sistema de Casos:** CaseObjectService + API endpoints

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
├── frontend/                 # React frontend
│   ├── src/
│   │   ├── components/      # Componentes reutilizáveis
│   │   ├── contexts/        # React Contexts (Auth, Window)
│   │   ├── hooks/           # Custom hooks
│   │   ├── pages/           # Páginas da aplicação
│   │   └── services/        # API services
├── backend/                 # .NET backend
│   └── CaseZeroApi/
│       ├── Controllers/     # API controllers
│       ├── Models/          # Modelos de dados
│       ├── DTOs/           # Data Transfer Objects
│       ├── Data/           # DbContext
│       └── Services/       # Business logic
├── cases/                  # Casos investigativos
│   └── Case001/           # Exemplo: Homicídio Corporativo
│       ├── case.json      # Configuração do caso
│       ├── evidence/      # Evidências
│       ├── suspects/      # Suspeitos
│       ├── forensics/     # Análises forenses
│       ├── memos/         # Memorandos temporais
│       └── witnesses/     # Testemunhas
├── docs/                   # Documentação
│   └── OBJETO_CASO.md     # Doc. do Sistema de Casos
└── validate_case.sh       # Script de validação
```

## 🔧 Desenvolvimento

### Comandos Úteis

**Frontend:**
```bash
npm run dev      # Servidor de desenvolvimento
npm run build    # Build para produção
npm run lint     # Verificar código
```

**Backend:**
```bash
dotnet run              # Executar servidor
dotnet build           # Compilar projeto
dotnet ef migrations   # Gerenciar migrações
```

**Casos:**
```bash
./validate_case.sh CaseXXX  # Validar estrutura do caso
```

## 📈 Criando Novos Casos

1. **Copie a estrutura do Case001**:
```bash
cp -r cases/Case001 cases/Case002
```

2. **Edite o arquivo `case.json`** com novos dados

3. **Substitua os arquivos** nas subpastas com novo conteúdo

4. **Valide a estrutura**:
```bash
./validate_case.sh Case002
```

5. **Teste via API** com os endpoints do CaseObjectController

Ver [documentação completa](docs/OBJETO_CASO.md) para detalhes.

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
1. Use o Case001 como template
2. Siga a estrutura documentada em `docs/OBJETO_CASO.md`
3. Valide com `./validate_case.sh`
4. Teste todos os endpoints da API

## 📄 Licença

Este projeto está licenciado sob a Licença MIT - veja o arquivo [LICENSE](LICENSE) para detalhes.

## 🚀 CI/CD e DevOps

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