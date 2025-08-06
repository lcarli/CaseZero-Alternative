# CaseZero - Detective Investigation System

Um sistema imersivo de investigação detetivesca onde você assume o papel de um detetive experiente resolvendo casos complexos.

## 🎮 Características do Sistema

- **Interface Completa**: Home page, login, registro, dashboard e ambiente desktop
- **Autenticação Segura**: Sistema de JWT com controle de acesso
- **Gestão de Casos**: Dashboard com estatísticas e progresso do usuário
- **Interface Policial Autêntica**: Ambiente desktop simulando sistemas policiais reais
- **API Robusta**: Backend .NET com Entity Framework e SQLite

## 🚀 Como Executar

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

O backend estará disponível em: `http://localhost:5006`

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

## 🔐 Usuário de Teste

O sistema vem com um usuário pré-configurado para testes:

- **Email:** `detective@police.gov`
- **Senha:** `Password123!`

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

## 📋 Fluxo do Usuário

1. **Home Page** - Apresentação do jogo detetivesco
2. **Registro** - Solicitar acesso ao sistema (requer aprovação)
3. **Login** - Autenticação com email/senha
4. **Dashboard** - Visão geral de estatísticas e casos
5. **Desktop** - Ambiente de trabalho para investigação de casos

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
├── cases/                  # Casos de teste e assets
└── docs/                   # Documentação
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

## 🛡️ Segurança

- Autenticação JWT com tokens de 7 dias
- Senhas hasheadas com BCrypt
- Validação de dados no frontend e backend
- Proteção de rotas sensíveis
- CORS configurado adequadamente

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

## 📄 Licença

Este projeto está licenciado sob a Licença MIT - veja o arquivo [LICENSE](LICENSE) para detalhes.