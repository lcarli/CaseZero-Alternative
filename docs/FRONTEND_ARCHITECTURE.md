# Frontend Architecture - CaseZero System

## Overview

O frontend do CaseZero é uma Single Page Application (SPA) construída com React 19, TypeScript e Vite. A aplicação simula um ambiente de desktop policial completo, oferecendo uma experiência imersiva de investigação detetivesca.

## Stack Tecnológico

| Tecnologia | Versão | Propósito |
|------------|--------|-----------|
| React | 19.1.0 | Framework principal |
| TypeScript | 5.8.3 | Tipagem estática |
| Vite | 7.0.4 | Build tool e dev server |
| React Router DOM | 7.7.1 | Roteamento |
| styled-components | 6.1.19 | CSS-in-JS styling |
| ESLint | 9.30.1 | Linting |

## Arquitetura Geral

```
frontend/
├── src/
│   ├── components/         # Componentes reutilizáveis
│   ├── contexts/          # Context providers (Auth, Window, etc.)
│   ├── hooks/             # Custom hooks
│   ├── pages/             # Páginas da aplicação
│   ├── services/          # Comunicação com API
│   ├── types/             # Definições TypeScript
│   ├── engine/            # Game engine logic
│   ├── assets/            # Recursos estáticos
│   ├── App.tsx            # Componente raiz
│   └── main.tsx           # Entry point
├── public/                # Assets públicos
├── dist/                  # Build de produção
└── package.json           # Dependências e scripts
```

---

## Estrutura de Componentes

### Componentes Principais

#### 1. App.tsx
**Responsabilidade:** Componente raiz que configura roteamento e providers

```typescript
function App() {
  return (
    <AuthContextProvider>
      <TimeContextProvider>
        <CaseContextProvider>
          <WindowContextProvider>
            <Router>
              <Routes>
                <Route path="/" element={<Home />} />
                <Route path="/login" element={<Login />} />
                <Route path="/dashboard" element={<Dashboard />} />
                <Route path="/desktop" element={<Desktop />} />
              </Routes>
            </Router>
          </WindowContextProvider>
        </CaseContextProvider>
      </TimeContextProvider>
    </AuthContextProvider>
  );
}
```

#### 2. Home.tsx
**Responsabilidade:** Landing page com apresentação do sistema

**Características:**
- Design responsivo
- Animações CSS
- Call-to-action para login/registro

#### 3. Login.tsx
**Responsabilidade:** Autenticação de usuários

**Estados:**
- Login form
- Registration form
- Loading states
- Error handling

#### 4. Dashboard.tsx
**Responsabilidade:** Painel de controle do detective

**Funcionalidades:**
- Estatísticas do usuário
- Lista de casos disponíveis
- Progresso de investigações
- Perfil do usuário

#### 5. Desktop.tsx
**Responsabilidade:** Ambiente de trabalho principal (simulação de desktop policial)

**Características:**
- Sistema de janelas gerenciável
- Dock com aplicações
- Background personalizado
- Clock em tempo real

---

## Sistema de Contextos

### 1. AuthContext
**Arquivo:** `src/contexts/AuthContext.tsx`

**Responsabilidades:**
- Gerenciamento do estado de autenticação
- Armazenamento de tokens JWT
- Informações do usuário logado
- Logout automático

**Interface:**
```typescript
interface AuthContextType {
  user: User | null;
  token: string | null;
  login: (email: string, password: string) => Promise<void>;
  logout: () => void;
  isAuthenticated: boolean;
  loading: boolean;
}
```

### 2. WindowContext
**Arquivo:** `src/contexts/WindowContext.tsx`

**Responsabilidades:**
- Gerenciamento de janelas do desktop
- Estado de maximização/minimização
- Z-index management
- Posicionamento de janelas

**Interface:**
```typescript
interface WindowContextType {
  windows: Window[];
  openWindow: (windowType: WindowType) => void;
  closeWindow: (windowId: string) => void;
  focusWindow: (windowId: string) => void;
  minimizeWindow: (windowId: string) => void;
  maximizeWindow: (windowId: string) => void;
}
```

### 3. CaseContext
**Arquivo:** `src/contexts/CaseContext.tsx`

**Responsabilidades:**
- Estado atual da investigação
- Dados do caso carregado
- Progresso da investigação
- Evidências desbloqueadas

**Interface:**
```typescript
interface CaseContextType {
  currentCase: Case | null;
  caseSession: CaseSession | null;
  loadCase: (caseId: string) => Promise<void>;
  submitSolution: (solution: CaseSolution) => Promise<void>;
  progress: CaseProgress;
}
```

### 4. TimeContext
**Arquivo:** `src/contexts/TimeContext.tsx`

**Responsabilidades:**
- Gerenciamento do tempo do jogo
- Eventos temporais
- Sincronização com backend

---

## Custom Hooks

### 1. useAuthContext
**Arquivo:** `src/hooks/useAuthContext.ts`

```typescript
export const useAuthContext = () => {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuthContext must be used within AuthContextProvider');
  }
  return context;
};
```

### 2. useWindowContext
**Arquivo:** `src/hooks/useWindowContext.ts`

**Funcionalidades:**
- Acesso ao sistema de janelas
- Helpers para operações comuns
- Event listeners para keyboard shortcuts

### 3. useCaseContext
**Arquivo:** `src/hooks/useCaseContext.ts`

**Funcionalidades:**
- Acesso ao estado do caso atual
- Helpers para evidências
- Validação de progresso

### 4. useTimeContext
**Arquivo:** `src/hooks/useTimeContext.ts`

**Funcionalidades:**
- Tempo atual do jogo
- Formatação de timestamps
- Eventos baseados em tempo

---

## Serviços (API Layer)

### 1. AuthService
**Arquivo:** `src/services/authService.ts`

```typescript
class AuthService {
  private baseURL = 'http://localhost:5000/api/auth';

  async login(email: string, password: string): Promise<AuthResponse> {
    const response = await fetch(`${this.baseURL}/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, password })
    });
    
    if (!response.ok) {
      throw new Error('Login failed');
    }
    
    return response.json();
  }

  async register(userData: RegisterData): Promise<void> {
    // Implementation
  }

  async logout(token: string): Promise<void> {
    // Implementation
  }
}
```

### 2. CaseService
**Arquivo:** `src/services/caseService.ts`

**Métodos:**
- `getCases()`: Lista casos disponíveis
- `getCaseDetails(caseId)`: Carrega caso específico
- `startCaseSession(caseId)`: Inicia sessão de investigação
- `submitSolution(solution)`: Submete solução

### 3. EvidenceService
**Arquivo:** `src/services/evidenceService.ts`

**Métodos:**
- `getEvidence(sessionId, evidenceId)`: Obtém evidência
- `requestForensicAnalysis(evidenceId)`: Solicita análise
- `getForensicResults(analysisId)`: Obtém resultados

---

## Sistema de Tipos TypeScript

### Core Types
**Arquivo:** `src/types/core.ts`

```typescript
export interface User {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  department: string;
  position: string;
  badgeNumber: string;
  rank: string;
  isApproved: boolean;
}

export interface AuthResponse {
  token: string;
  user: User;
  expiresAt: string;
}
```

### Case Types
**Arquivo:** `src/types/case.ts`

```typescript
export interface Case {
  caseId: string;
  metadata: CaseMetadata;
  evidences: Evidence[];
  suspects: Suspect[];
  forensicAnalyses: ForensicAnalysis[];
  timeline: TimelineEvent[];
}

export interface Evidence {
  id: string;
  name: string;
  type: EvidenceType;
  description: string;
  filePath: string;
  unlockRequirements: string[];
  metadata: Record<string, any>;
}

export type EvidenceType = 
  | 'document' 
  | 'photo' 
  | 'video' 
  | 'audio' 
  | 'digital' 
  | 'physical';
```

### Window Types
**Arquivo:** `src/types/window.ts`

```typescript
export interface WindowState {
  id: string;
  type: WindowType;
  title: string;
  isOpen: boolean;
  isMinimized: boolean;
  isMaximized: boolean;
  position: { x: number; y: number };
  size: { width: number; height: number };
  zIndex: number;
}

export type WindowType =
  | 'case-viewer'
  | 'evidence-viewer'
  | 'forensic-lab'
  | 'suspect-profiles'
  | 'timeline'
  | 'email'
  | 'file-manager';
```

---

## Engine do Jogo

### CaseEngine
**Arquivo:** `src/engine/CaseEngine.ts`

**Responsabilidades:**
- Lógica de desbloqueio de conteúdo
- Validação de requisitos
- Gerenciamento de progresso
- Cálculo de pontuação

```typescript
class CaseEngine {
  private case: Case;
  private session: CaseSession;

  constructor(case: Case, session: CaseSession) {
    this.case = case;
    this.session = session;
  }

  checkUnlockRequirements(itemId: string): boolean {
    const item = this.findItem(itemId);
    if (!item || !item.unlockRequirements) return true;

    return item.unlockRequirements.every(req => 
      this.session.unlockedContent.includes(req)
    );
  }

  calculateProgress(): CaseProgress {
    // Implementation
  }

  validateSolution(solution: CaseSolution): ValidationResult {
    // Implementation
  }
}
```

---

## Componentes de UI

### 1. Desktop Components

#### WindowManager
**Responsabilidade:** Gerencia todas as janelas abertas
- Drag & drop
- Redimensionamento
- Z-index management

#### Dock
**Responsabilidade:** Barra de aplicações
- Ícones animados
- Indicadores de estado
- Quick access

#### Clock
**Responsabilidade:** Relógio do sistema
- Tempo real
- Formatação personalizada
- Timezone support

### 2. Case Components

#### CaseViewer
**Responsabilidade:** Visualização principal do caso
- Informações gerais
- Lista de evidências
- Progresso

#### EvidenceViewer
**Responsabilidade:** Visualização de evidências
- Suporte a múltiplos formatos
- Zoom e navegação
- Anotações

#### ForensicLab
**Responsabilidade:** Interface de análises forenses
- Solicitação de análises
- Acompanhamento de progresso
- Visualização de resultados

#### Timeline
**Responsabilidade:** Linha do tempo dos eventos
- Ordenação cronológica
- Filtros
- Evidências associadas

---

## Styled Components

### Theme System
**Arquivo:** `src/styles/theme.ts`

```typescript
export const theme = {
  colors: {
    primary: '#1e3a8a',
    secondary: '#64748b',
    background: '#0f172a',
    surface: '#1e293b',
    text: '#f8fafc',
    accent: '#3b82f6'
  },
  fonts: {
    mono: 'Consolas, monospace',
    sans: 'Inter, sans-serif'
  },
  spacing: {
    xs: '0.25rem',
    sm: '0.5rem',
    md: '1rem',
    lg: '1.5rem',
    xl: '2rem'
  }
};
```

### Common Components
```typescript
export const Button = styled.button<{ variant?: 'primary' | 'secondary' }>`
  padding: ${props => props.theme.spacing.md};
  background: ${props => 
    props.variant === 'primary' 
      ? props.theme.colors.primary 
      : props.theme.colors.secondary
  };
  color: ${props => props.theme.colors.text};
  border: none;
  border-radius: 0.375rem;
  cursor: pointer;
  transition: all 0.2s ease;

  &:hover {
    opacity: 0.8;
    transform: translateY(-1px);
  }
`;
```

---

## Roteamento

### Estrutura de Rotas
```typescript
const router = createBrowserRouter([
  {
    path: '/',
    element: <Home />
  },
  {
    path: '/login',
    element: <Login />
  },
  {
    path: '/register',
    element: <Register />
  },
  {
    path: '/dashboard',
    element: <ProtectedRoute><Dashboard /></ProtectedRoute>
  },
  {
    path: '/desktop',
    element: <ProtectedRoute><Desktop /></ProtectedRoute>
  },
  {
    path: '/case/:caseId',
    element: <ProtectedRoute><CaseDetail /></ProtectedRoute>
  }
]);
```

### Protected Routes
```typescript
const ProtectedRoute = ({ children }: { children: React.ReactNode }) => {
  const { isAuthenticated, loading } = useAuthContext();

  if (loading) return <LoadingSpinner />;
  if (!isAuthenticated) return <Navigate to="/login" />;

  return <>{children}</>;
};
```

---

## Estado Global

### Estrutura do Estado
```typescript
interface AppState {
  auth: {
    user: User | null;
    token: string | null;
    isAuthenticated: boolean;
  };
  case: {
    currentCase: Case | null;
    session: CaseSession | null;
    progress: CaseProgress;
  };
  ui: {
    windows: WindowState[];
    activeWindow: string | null;
    notifications: Notification[];
  };
  game: {
    time: Date;
    events: GameEvent[];
    settings: GameSettings;
  };
}
```

---

## Performance

### Otimizações Implementadas

1. **Code Splitting**
   - Lazy loading de rotas
   - Dynamic imports para componentes grandes

2. **Memoization**
   - React.memo para componentes pesados
   - useMemo para cálculos complexos
   - useCallback para funções

3. **Asset Optimization**
   - Vite bundling otimizado
   - Image lazy loading
   - Font preloading

4. **State Management**
   - Context splitting por domínio
   - Minimal re-renders
   - Local state quando possível

---

## Testing

### Estratégia de Testes
```typescript
// Component Testing
describe('CaseViewer', () => {
  it('should render case information correctly', () => {
    render(<CaseViewer case={mockCase} />);
    expect(screen.getByText(mockCase.metadata.title)).toBeInTheDocument();
  });
});

// Hook Testing
describe('useAuthContext', () => {
  it('should provide authentication state', () => {
    const { result } = renderHook(() => useAuthContext(), {
      wrapper: AuthContextProvider
    });
    expect(result.current.isAuthenticated).toBe(false);
  });
});
```

---

## Build e Deploy

### Comandos de Build
```bash
# Development
npm run dev

# Production build
npm run build

# Preview production build
npm run preview

# Linting
npm run lint
```

### Configuração do Vite
```typescript
export default defineConfig({
  plugins: [react()],
  build: {
    outDir: 'dist',
    sourcemap: true,
    rollupOptions: {
      output: {
        manualChunks: {
          vendor: ['react', 'react-dom'],
          router: ['react-router-dom']
        }
      }
    }
  },
  server: {
    port: 5173,
    proxy: {
      '/api': 'http://localhost:5000'
    }
  }
});
```

---

## Convenções de Código

### Naming Conventions
- **Components:** PascalCase (`CaseViewer.tsx`)
- **Hooks:** camelCase with 'use' prefix (`useAuthContext.ts`)
- **Types:** PascalCase (`User`, `CaseState`)
- **Constants:** UPPER_SNAKE_CASE (`API_BASE_URL`)

### File Organization
```
components/
├── ui/                    # UI primitives
│   ├── Button.tsx
│   ├── Input.tsx
│   └── Modal.tsx
├── features/              # Feature-specific components
│   ├── case/
│   ├── auth/
│   └── desktop/
└── layout/                # Layout components
    ├── Header.tsx
    └── Sidebar.tsx
```

### Import Organization
```typescript
// External libraries
import React from 'react';
import { useState } from 'react';

// Internal modules
import { AuthService } from '../services/authService';
import { User } from '../types/user';

// Relative imports
import './Component.styles';
```