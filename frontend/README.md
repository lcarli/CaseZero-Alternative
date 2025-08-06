# CaseZero Alternative - Frontend

## 🚀 Tecnologias Utilizadas

Este frontend foi construído com as seguintes tecnologias modernas:

- **React 19** - Biblioteca JavaScript para construção de interfaces de usuário
- **TypeScript** - Superset do JavaScript que adiciona tipagem estática
- **Vite** - Ferramenta de build rápida para desenvolvimento frontend
- **Styled Components** - Biblioteca CSS-in-JS para estilização de componentes
- **React Router** - Biblioteca para navegação declarativa em aplicações React
- **ESLint** - Ferramenta de análise de código para manter qualidade

## 📁 Estrutura do Projeto

```
frontend/
├── src/
│   ├── components/          # Componentes React reutilizáveis
│   │   ├── Navigation.tsx   # Componente de navegação
│   │   ├── Home.tsx         # Página inicial
│   │   └── About.tsx        # Página sobre o projeto
│   ├── assets/              # Recursos estáticos (imagens, ícones)
│   ├── App.tsx              # Componente principal da aplicação
│   ├── main.tsx             # Ponto de entrada da aplicação
│   └── index.css            # Estilos globais
├── public/                  # Arquivos públicos estáticos
├── package.json             # Dependências e scripts do projeto
├── vite.config.ts           # Configuração do Vite
├── tsconfig.json            # Configuração do TypeScript
└── eslint.config.js         # Configuração do ESLint
```

## 🛠️ Como Executar

### Pré-requisitos

- Node.js (versão 18 ou superior)
- npm ou yarn

### Instalação e Execução

1. **Instalar dependências:**
   ```bash
   npm install
   ```

2. **Executar em modo desenvolvimento:**
   ```bash
   npm run dev
   ```
   O aplicativo estará disponível em `http://localhost:5173`

3. **Construir para produção:**
   ```bash
   npm run build
   ```

4. **Preview da build de produção:**
   ```bash
   npm run preview
   ```

5. **Executar linter:**
   ```bash
   npm run lint
   ```

## ✨ Funcionalidades Implementadas

- ✅ Configuração completa do React com TypeScript
- ✅ Roteamento com React Router (páginas Home e About)
- ✅ Estilização com Styled Components
- ✅ Design responsivo com gradiente moderno
- ✅ Navegação funcional entre páginas
- ✅ Componentes reutilizáveis
- ✅ Hot Module Replacement (HMR) para desenvolvimento
- ✅ Build otimizada para produção
- ✅ Configuração de ESLint para qualidade de código

## 🎨 Design e Estilização

O projeto utiliza uma abordagem moderna de design com:

- Gradiente de fundo em tons de azul e roxo
- Efeitos de glassmorphism nos cards
- Animações e transições suaves
- Design responsivo para diferentes tamanhos de tela
- Tipografia otimizada e hierarquia visual clara

## 🔧 Scripts Disponíveis

- `npm run dev` - Inicia o servidor de desenvolvimento
- `npm run build` - Constrói a aplicação para produção
- `npm run preview` - Preview da build de produção
- `npm run lint` - Executa o linter para verificar qualidade do código

## 📝 Próximos Passos

Esta é a base sólida para o desenvolvimento do frontend. Próximas implementações podem incluir:

- Integração com APIs do backend
- Autenticação e autorização
- Gerenciamento de estado global (Redux/Zustand)
- Testes unitários e de integração
- PWA (Progressive Web App)
- Internacionalização (i18n)

---

## Vite + React + TypeScript

This template provides a minimal setup to get React working in Vite with HMR and some ESLint rules.
