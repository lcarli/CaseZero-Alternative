# Game Time Engine - Sistema de Tempo do Jogo

## 📋 Visão Geral

Sistema de tempo acelerado para o jogo CaseZero, onde o tempo do jogo avança mais rápido que o tempo real. Este sistema controla:

- ⏰ Relógio do jogo (1 hora real = 1 minuto game, 1 minuto real = 1 segundo game)
- 💾 Persistência do tempo entre sessões
- 🔬 Sistema de perícias técnicas baseado em tempo
- 📧 Notificações quando análises forenses estiverem prontas

---

## 🎯 Especificações Técnicas

### Relação de Tempo
- **Multiplicador:** 60x
- **1 segundo real** = 1 minuto de jogo
- **1 minuto real** = 1 hora de jogo
- **Exemplo:** Se você jogar por 10 minutos reais, passaram 10 horas no jogo

### Persistência
- O tempo é salvo quando o usuário sai do caso
- Ao reconectar, o tempo continua de onde parou
- Campo no backend: `CaseSession.GameTimeAtEnd`

### Perícias Técnicas
Durações aproximadas (tempo de jogo):
- **DNA Analysis:** 4-6 horas
- **Fingerprint Analysis:** 2-3 horas
- **Digital Forensics:** 6-12 horas
- **Ballistics Analysis:** 3-5 horas

---

## 🏗️ Arquitetura

### Frontend

#### TimeContext (`frontend/src/contexts/TimeContext.tsx`)
- Gerencia o estado global do tempo do jogo
- Controla o ticker que avança o tempo
- Integra com ForensicsService para perícias
- Props:
  - `initialGameTime?: Date` - Tempo inicial ao carregar sessão salva
  - `caseId?: string` - ID do caso atual

#### CaseEngine (`frontend/src/engine/CaseEngine.ts`)
- Sincroniza `currentGameTime` com TimeContext
- Expõe `getCurrentGameTime()` para componentes
- Notifica listeners quando tempo muda

#### Clock (`frontend/src/components/Clock.tsx`)
- Exibe o tempo atual do jogo
- Mostra status de perícias pendentes
- Interface expansível com detalhes

### Backend

#### CaseSession (`backend/CaseZeroApi/Models/CaseSession.cs`)
```csharp
public class CaseSession
{
    public string? GameTimeAtStart { get; set; }  // ISO 8601 datetime
    public string? GameTimeAtEnd { get; set; }    // ISO 8601 datetime
    // ... outros campos
}
```

#### Endpoints
- `POST /api/casesession/start` - Inicia sessão com gameTimeAtStart
- `POST /api/casesession/end` - Finaliza sessão com gameTimeAtEnd
- `GET /api/casesession/{caseId}/active` - Busca sessão ativa ou última sessão

---

## 📝 Implementação - Roadmap

### ✅ FASE 1: Análise e Ajustes da Engine (COMPLETA)

**Status:** ✅ **100% Completa**
- ✅ TIME_MULTIPLIER ajustado para 60x
- ✅ CaseEngine integrado com TimeContext
- ✅ Backend validado e funcional
- ✅ TimeSync component criado

**Arquivos Modificados:**
- `frontend/src/contexts/TimeContext.tsx` - TIME_MULTIPLIER: 30 → 60
- `frontend/src/engine/CaseEngine.ts` - Métodos getCurrentGameTime(), updateGameTime()
- `frontend/src/contexts/CaseContext.tsx` - Wrapper methods expostos
- `frontend/src/components/TimeSync.tsx` - **NOVO** - Sincronização automática

### ✅ FASE 2: Restauração do Tempo (COMPLETA)

**Status:** ✅ **100% Completa**

**Objetivo:** Fazer o tempo continuar de onde parou

**Implementação:**
- ✅ DesktopPage busca última sessão via `caseSessionApi.getLastSession()`
- ✅ TimeProvider aceita prop `initialGameTime?: Date`
- ✅ Desktop salva `gameTimeAtEnd` ao desconectar
- ✅ Sistema adiciona entry "Investigação retomada" quando resume

**Fluxo Implementado:**
1. Usuário conecta ao caso
2. Sistema busca `lastSession.gameTimeAtEnd`
3. TimeProvider inicia com esse horário
4. Tempo avança continuamente a partir dali
5. Ao desconectar, salva novo `gameTimeAtEnd`

### ✅ FASE 3: Sistema de Perícias (COMPLETA)

**Status:** ✅ **100% Completa**

**Objetivo:** Implementar análises forenses com duração realista

**Implementação:**
- ✅ Modelo ForensicRequest (Frontend + Backend)
- ✅ Migration EF Core criada
- ✅ Controller REST API completo (CRUD)
- ✅ forensicsService.ts com lógica de durações
- ✅ Verificação automática a cada 30s
- ✅ Completion automática quando gameTime >= estimatedCompletionTime
- ✅ Integração com CaseEngine

**Durações Implementadas:**
- 🧬 DNA: 4-6 horas (240-360 min)
- 👆 Fingerprint: 2-3 horas (120-180 min)
- 💻 DigitalForensics: 6-12 horas (360-720 min)
- 🔫 Ballistics: 3-5 horas (180-300 min)

**API Endpoints:**
- `GET /api/forensicrequest/{caseId}` - Lista todas
- `GET /api/forensicrequest/{caseId}/pending` - Lista pendentes
- `POST /api/forensicrequest` - Cria nova requisição
- `PUT /api/forensicrequest/{caseId}/{id}` - Atualiza status
- `DELETE /api/forensicrequest/{caseId}/{id}` - Cancela

### ✅ FASE 4: Interface do Usuário (COMPLETA)

**Status:** ✅ **100% Completa**

**Objetivo:** Criar interface para visualizar e gerenciar perícias

**Componentes Criados:**

1. **ForensicsQueue** (`frontend/src/components/apps/ForensicsQueue.tsx`)
   - Lista completa de perícias (em andamento + concluídas)
   - Tempo restante em tempo real
   - Badge colorido por tipo de análise (DNA, Fingerprint, Digital, Ballistics)
   - Botão "Ver Resultado" para perícias completas
   - Refresh automático a cada 10 segundos
   - Estados vazios informativos

2. **Clock Melhorado** (`frontend/src/components/Clock.tsx`)
   - Badge animado com contador de perícias pendentes
   - Pulse animation quando há perícias ativas
   - Seção "Perícias em Andamento" no painel expandido
   - Atualização automática a cada 30 segundos
   - Indicador visual "60x tempo real"

3. **Integração CaseContext**
   - `requestForensicAnalysis()` - Solicitar nova análise
   - `getForensicRequests()` - Obter todas as requisições
   - `getPendingForensicRequests()` - Obter pendentes

**Features UI:**
- 🎨 Design consistente com tema dark do jogo
- 🔔 Indicadores visuais para perícias prontas
- ⏱️ Tempo restante formatado (ex: "3h 45m")
- 📊 Separação clara entre pendentes/concluídas
- 🎯 Click no badge abre ForensicsQueue

---

## ✅ STATUS FINAL DA IMPLEMENTAÇÃO

**Progresso Global: 12/14 tarefas (86%)**

### Completo ✅
- [x] TIME_MULTIPLIER 60x
- [x] Integração TimeContext ↔ CaseEngine
- [x] Persistência backend validada
- [x] Recuperação de sessão automática
- [x] Prop initialGameTime
- [x] Save gameTime ao desconectar
- [x] Modelo ForensicRequest (Frontend + Backend)
- [x] Lógica de durações de perícias
- [x] Sistema de verificação automática
- [x] Integração com EngineFileViewer
- [x] Clock com indicadores
- [x] ForensicsQueue component

### Pendente 🔄
- [ ] Testes de persistência (Task 13)
- [ ] Testes do sistema de perícias (Task 14)

---

## 🧪 GUIA DE TESTES

### Teste 1: Persistência do Tempo

**Objetivo:** Verificar se o tempo continua de onde parou

**Passos:**

1. **Primeira Sessão**
   ```bash
   # Terminal 1 - Backend
   cd backend/CaseZeroApi
   dotnet run
   
   # Terminal 2 - Frontend
   cd frontend
   npm run dev
   ```

2. **Conectar ao Caso**
   - Fazer login
   - Abrir CASE-2024-001
   - Verificar que tempo inicial é 8:00 AM
   - Anotar horário exato (ex: 08:00:00)

3. **Aguardar Tempo Passar**
   - Esperar 2 minutos reais
   - Verificar que passou 2 horas no jogo (10:00:00)
   - Clicar no relógio para ver detalhes expandidos

4. **Desconectar**
   - Clicar no botão de desconexão
   - Anotar o horário final (ex: 10:00:00)

5. **Reconectar**
   - Conectar novamente no mesmo caso
   - **RESULTADO ESPERADO:** Tempo deve começar em 10:00:00 (não 8:00 AM)
   - Aguardar 1 minuto real
   - **RESULTADO ESPERADO:** Tempo avança para 11:00:00

6. **Verificar Backend**
   ```bash
   # Verificar sessão salva
   GET /api/casesession/CASE-2024-001/last
   
   # Resposta esperada:
   {
     "gameTimeAtStart": "2024-01-15T08:00:00Z",
     "gameTimeAtEnd": "2024-01-15T10:00:00Z",
     "sessionDurationMinutes": 2
   }
   ```

**Critérios de Sucesso:**
- ✅ Tempo não reinicia em 8:00 AM ao reconectar
- ✅ Tempo continua exatamente de onde parou
- ✅ Múltiplas reconexões mantêm continuidade
- ✅ Backend salva gameTimeAtEnd corretamente

---

### Teste 2: Sistema de Perícias

**Objetivo:** Verificar solicitação, processamento e conclusão de perícias

**Passos:**

1. **Solicitar Perícia DNA (4-6h)**
   ```typescript
   // Via console do navegador
   const { requestForensicAnalysis } = useCaseContext()
   const request = await requestForensicAnalysis(
     'evidence-001',
     'DNA',
     'Amostra de sangue da cena'
   )
   console.log('Conclusão prevista:', request.estimatedCompletionTime)
   ```

2. **Verificar Badge no Clock**
   - Badge laranja deve aparecer no relógio
   - Número "1" deve estar visível
   - Badge deve pulsar (animation)

3. **Abrir ForensicsQueue**
   - Clicar em "Perícias" no menu (se disponível)
   - Ou acessar diretamente o componente
   - **RESULTADO ESPERADO:**
     - 1 perícia na seção "Em Andamento"
     - Badge "DNA" vermelho
     - Tempo restante visível (ex: "4h 30m")
     - Status atualiza a cada 10s

4. **Aguardar Conclusão**
   - Como a perícia leva 4-6h de jogo
   - E 1 min real = 1h jogo
   - Aguardar ~5 minutos reais
   
5. **Verificar Conclusão**
   - Badge do Clock deve desaparecer ou mostrar "✓"
   - ForensicsQueue deve mostrar:
     - Seção "Concluídas" com 1 item
     - Indicador "✅ Pronta para visualização"
     - Botão "📄 Ver Resultado"

6. **Solicitar Múltiplas Perícias**
   ```typescript
   await requestForensicAnalysis('evidence-002', 'Fingerprint', 'Impressões digitais')
   await requestForensicAnalysis('evidence-003', 'Ballistics', 'Projétil')
   await requestForensicAnalysis('evidence-004', 'DigitalForensics', 'Celular')
   ```
   
   - Badge deve mostrar "3"
   - ForensicsQueue deve listar todas
   - Perícias devem completar em tempos diferentes

**Critérios de Sucesso:**
- ✅ Perícia é criada no backend
- ✅ Tempo de conclusão calculado corretamente
- ✅ Badge atualiza automaticamente
- ✅ ForensicsQueue mostra status em tempo real
- ✅ Perícia completa quando gameTime >= estimatedCompletionTime
- ✅ Status muda para "completed"
- ✅ Múltiplas perícias funcionam simultaneamente

---

### Teste 3: Verificação Automática

**Objetivo:** Confirmar que o sistema verifica perícias automaticamente

**Passos:**

1. **Solicitar Perícia de Curta Duração**
   ```typescript
   await requestForensicAnalysis('evidence-test', 'Fingerprint', 'Teste rápido')
   // Fingerprint: 2-3h jogo = ~2-3 min reais
   ```

2. **Não Fazer Nada**
   - Deixar o jogo aberto
   - Não clicar em nada
   - Aguardar 3 minutos

3. **Verificar Completion Automática**
   - Sistema deve verificar a cada 30s
   - Após 2-3 min, perícia deve completar automaticamente
   - Badge deve atualizar sem refresh manual
   - Console deve mostrar: "Forensic analysis completed: Fingerprint for Teste rápido"

**Critérios de Sucesso:**
- ✅ Verificação ocorre automaticamente
- ✅ Não requer ação do usuário
- ✅ UI atualiza sem refresh
- ✅ Console mostra logs corretos

---

### Teste 4: Edge Cases

#### 4.1 Primeira Vez no Caso

**Passos:**
1. Limpar banco de dados de sessões
2. Conectar em caso novo
3. **RESULTADO ESPERADO:** Tempo inicia em 8:00 AM

#### 4.2 Fechamento Abrupto do Navegador

**Passos:**
1. Conectar no caso
2. Aguardar 1 min (tempo avança para 9:00)
3. Force-quit do navegador (não desconectar normalmente)
4. Reabrir e reconectar
5. **RESULTADO ESPERADO:** Último gameTimeAtEnd pode não estar salvo se não houve desconexão normal

#### 4.3 Backend Offline Durante Perícia

**Passos:**
1. Solicitar perícia
2. Parar backend (Ctrl+C)
3. Aguardar tempo de conclusão
4. Reiniciar backend
5. **RESULTADO ESPERADO:** Sistema tenta completar ao reconectar

---

## 🔧 Comandos Úteis para Testes

### Verificar Estado do Sistema

```bash
# Ver todas as sessões
curl http://localhost:5000/api/casesession

# Ver sessão específica
curl http://localhost:5000/api/casesession/CASE-2024-001/last

# Ver perícias pendentes
curl http://localhost:5000/api/forensicrequest/CASE-2024-001/pending

# Ver todas as perícias
curl http://localhost:5000/api/forensicrequest/CASE-2024-001
```

### Console do Navegador

```javascript
// Obter estado atual
const { gameTime, startTime, isRunning } = useTimeContext()
console.log('Game Time:', gameTime.toLocaleTimeString())
console.log('Elapsed:', (gameTime - startTime) / 1000 / 60, 'hours')

// Ver perícias
const { getPendingForensicRequests } = useCase()
const pending = await getPendingForensicRequests()
console.table(pending)

// Forçar verificação
const { engine } = useCase()
engine.checkForensicRequests()
```

---

## 📊 Checklist Final

### Persistência
- [ ] Tempo salva ao desconectar
- [ ] Tempo restaura ao reconectar
- [ ] Múltiplas sessões funcionam
- [ ] Fechamento abrupto não causa erro fatal

### Perícias
- [ ] DNA (4-6h) funciona
- [ ] Fingerprint (2-3h) funciona
- [ ] DigitalForensics (6-12h) funciona
- [ ] Ballistics (3-5h) funciona
- [ ] Completion automática funciona
- [ ] Múltiplas perícias simultâneas funcionam

### UI
- [ ] Clock mostra badge correto
- [ ] Badge pulsa quando há perícias
- [ ] ForensicsQueue lista corretamente
- [ ] Tempo restante atualiza
- [ ] "Ver Resultado" aparece quando pronto
- [ ] Painel expandido mostra contador

### Performance
- [ ] Sem memory leaks
- [ ] Verificações não travam UI
- [ ] Refresh automático funciona
- [ ] Cleanup ao desmontar componentes

---

## 🏁 Conclusão

O **Game Time Engine** está **86% completo** (12/14 tarefas).

**Próximos Passos:**
1. Executar suite de testes acima
2. Corrigir bugs encontrados
3. Adicionar testes automatizados
4. Deploy e testes em produção

**Arquivos Principais:**
- `frontend/src/contexts/TimeContext.tsx`
- `frontend/src/engine/CaseEngine.ts`
- `frontend/src/services/forensicsService.ts`
- `frontend/src/components/Clock.tsx`
- `frontend/src/components/apps/ForensicsQueue.tsx`
- `backend/CaseZeroApi/Models/ForensicRequest.cs`
- `backend/CaseZeroApi/Controllers/ForensicRequestController.cs`
5. Desconecte do caso
6. Reconecte
7. Verifique que o horário é o mesmo de quando saiu

---

## 📚 Referências

### Arquivos Principais

**Frontend:**
- `frontend/src/contexts/TimeContext.tsx` - Contexto de tempo
- `frontend/src/hooks/useTimeContext.ts` - Hook para usar tempo
- `frontend/src/components/Clock.tsx` - Relógio visual
- `frontend/src/services/forensicsService.ts` - Serviço de perícias
- `frontend/src/engine/CaseEngine.ts` - Motor do jogo

**Backend:**
- `backend/CaseZeroApi/Models/CaseSession.cs` - Modelo de sessão
- `backend/CaseZeroApi/Controllers/CaseSessionController.cs` - API de sessões
- `backend/CaseZeroApi/DTOs/CaseDtos.cs` - DTOs de sessão

### APIs Relevantes

```
POST /api/casesession/start
Body: { caseId, gameTimeAtStart }

POST /api/casesession/end  
Body: { caseId, gameTimeAtEnd }

GET /api/casesession/{caseId}/active
Response: { gameTimeAtStart, gameTimeAtEnd, ... }
```

---

## 🚀 Próximos Passos

**Prioridade ALTA:**
1. Ajustar multiplicador de tempo (60x)
2. Implementar recuperação de sessão ao conectar
3. Salvar tempo ao desconectar

**Prioridade MÉDIA:**
4. Sistema de perícias com timing
5. Notificações de conclusão

**Prioridade BAIXA:**
6. UI/UX melhorado
7. Analytics e métricas

---

## 📝 Notas de Implementação

### Decisões de Design

**Por que 60x?**
- Permite que um caso de 24h de jogo seja completado em ~24 minutos reais
- Mantém senso de urgência sem ser excessivamente rápido
- Perícias levam tempo suficiente para criar estratégia

**Por que salvar em ISO 8601?**
- Formato universal independente de timezone
- Fácil conversão entre frontend e backend
- Compatível com Date() do JavaScript

**Por que usar GameTimeAtEnd em vez de duração?**
- Preserva o momento exato no tempo do jogo
- Facilita debug (ver exatamente quando parou)
- Permite calcular duração se necessário

---

## 🐛 Problemas Conhecidos

- [ ] TIME_MULTIPLIER ainda está em 30x (precisa ser 60x)
- [ ] TimeContext não está integrado com CaseEngine
- [ ] Recuperação de sessão não implementada
- [ ] Sistema de perícias ainda em planejamento

---

## 📊 Status do Projeto

**Última Atualização:** 14 de novembro de 2025

**Status Geral:** 🟡 Em Desenvolvimento

- **Fase 1:** 🟡 40% completo (base existe, precisa ajustes)
- **Fase 2:** 🔴 0% completo (não iniciado)
- **Fase 3:** 🔴 0% completo (não iniciado)
- **Fase 4:** 🔴 0% completo (não iniciado)

---

*Este documento será atualizado conforme o desenvolvimento progride.*
