# Backlog de melhorias — CaseZero-Alternative

> Backlog priorizado de melhorias para os 5 subsistemas: relógio, promoção, forense, pinboard, notebook. Cada epic está decomposto em tarefas independentes com critérios de aceite e referências de código.

## Problema

Cinco subsistemas precisam de melhorias coordenadas:

1. **Relógio / sistema de tempo** — só salva no disconnect, bug de resume com state stale, sem pausa, sem persistência de eventos, ticka quando aba inativa, ETA forense usa relógio real.
2. **Sistema de progressão / promoção** — modelo e thresholds existem (`PromotionRules`, `UserRankHistory`, `User.Rank`) mas nada nunca promove: `SolutionService` jamais incrementa `CasesResolved` ou cria entrada em `UserRankHistory`. Ranks só são exibidos.
3. **Sistema forense** — backend funciona end-to-end (request → queue → background service → reveal asset/email + fallback no-findings + rules engine + SignalR), mas (a) "pedir análise sem custo" desestimula investigação cuidadosa, (b) componente legado `ForensicModule.tsx` (hardcoded, desconectado) confunde, (c) ETA usa wall-clock, não game-clock, e (d) UX não comunica que análises pesam na solução.
4. **Pinboard** — estado local puro, **não persiste**, não vincula a entidades reais do caso, drop zone decorativa, sem labels em conexões.
5. **Notebook** — CRUD backend completo, mas UI bare-bones: sem autosave-on-type, sem search, sem tags, sem markdown, sem atalhos, sem stamps game-time, sem links, hardcoded em inglês (sem i18n).

## Decisões do usuário

- **Promoção**: gatilho **manual review** — Capitão (NPC) envia e-mail in-game oferecendo promoção quando jogador atinge threshold; jogador aceita e o rank sobe + entrada em `UserRankHistory`.
- **Forense ↔ solução**: aplicar **penalidade para "no findings"** — pedir análise inútil tem custo (tempo extra e/ou redução de pontuação) para desencorajar "pedir tudo".
- **Pinboard**: persistido **por caso** com links a entidades reais (evidências, suspeitos, e-mails, anexos).
- **Notebook**: autosave-on-type, i18n nas 4 línguas, search/filter, shortcuts, insert game-timestamp, link-to-entities, markdown render, pin/favorite.
- **Clock**: corrigir bug do resume, salvar periodicamente, pausar ao perder foco (e botão pause/resume UI), persistir TimeEntries, ETA forense em game-time, velocidade configurável, parar quando offline.
- **Limpeza**: remover `ForensicModule.tsx` legado.

## Ordem de prioridade

1. **Clock / Tempo** (epic 1)
2. **Promoção** (epic 2)
3. **Forense** (epic 3)
4. **Pinboard** (epic 4)
5. **Notebook** (epic 5)

---

## Epic 1 — Sistema de tempo (Clock)

> Objetivo: relógio confiável, persistente e que respeite presença do jogador.

### 1.1 — Corrigir bug de resume com `initialGameTime` stale
- **Onde**: `frontend/src/pages/DesktopPage.tsx:44-53`.
- **Problema**: o trecho `const startTime = initialGameTime || (() => { ... })()` lê `initialGameTime` **do estado** logo após chamar `setInitialGameTime(resumeTime)` — React ainda não atualizou; sempre cai no default `08:00`. Resultado: `startSession` recebe `08:00` mesmo retomando sessão.
- **Critério de aceite**: ao retomar caso com `gameTimeAtEnd` anterior, a chamada `startSession.gameTimeAtStart` recebe esse mesmo valor.
- **Como**: usar variável local `resumeTime` em vez de ler do estado, ou refatorar para usar `useRef`/`useReducer`.

### 1.2 — Persistência periódica do `gameTime` (heartbeat)
- **Onde**: novo endpoint `POST /api/casesession/heartbeat/{caseId}` + chamada periódica em `DesktopPage`/`TimeProvider`.
- **Comportamento**: a cada 30s (config) faz PATCH `gameTimeAtEnd` na sessão ativa. Recupera-se de F5/crash sem perder progresso. Last-write-wins.
- **Critério de aceite**: F5 no meio do caso → `getLastSession` retorna `gameTimeAtEnd` próximo ao último tick (≤30s atrás).

### 1.3 — Pausa ao perder foco da janela
- **Onde**: `TimeContext.tsx`.
- **Comportamento**: escutar `visibilitychange` / `blur` / `focus` em `window`; quando aba some, chama `pauseTime()`; ao voltar, `resumeTime()`. Salva `gameTime` no servidor via heartbeat ao pausar.
- **Critério de aceite**: trocar de aba por 5 min e voltar → `gameTime` avançou ≤5s (tempo de eventos de blur/focus). Ícone do `StatusIndicator` reflete o estado.

### 1.4 — Controle manual de pausa/play na UI
- **Onde**: `Clock.tsx` (já tem `pauseTime/resumeTime` no contexto, nunca chamados).
- **Comportamento**: botão pause/play no painel expandido do relógio. Ao pausar, mostra estado claramente. Persiste pausa via heartbeat.
- **Critério de aceite**: pausar pela UI congela o relógio; tickers de forense e eventos temporais também respeitam pausa.

### 1.5 — Persistir `TimeEntries` no backend
- **Onde**: nova tabela `CaseSessionTimeEntries` (FK CaseSession) ou JSON em `CaseSession.TimeEntries`.
- **Comportamento**: cada `addTimeEntry` em `TimeContext` chama backend; ao retomar sessão, eventos são reidratados.
- **Critério de aceite**: logs (`Logs.tsx`) mostram histórico completo de sessões anteriores; eventos não somem em F5.

### 1.6 — ETA de perícia em game-time, não wall-clock
- **Onde**: `ForensicRequestController.cs:99-126`, `ForensicsBackgroundService.cs:54-56`, `ForensicRequest.cs`.
- **Problema**: hoje `EstimatedCompletionTime = DateTime.UtcNow + duration` (wall-clock). Se jogador pausa por 1h, forense "estoura" sem ele lá. Background usa `DateTime.UtcNow <= EstimatedCompletionTime` para concluir.
- **Comportamento**: salvar `GameTimeRequestedAt` (string ISO da gameTime atual) e `DurationGameMinutes`. Background compara com a `GameTimeAtEnd` mais recente da sessão (avançada via heartbeat). Forenses só completam enquanto sessão está ativa **e** game-time avançou suficiente.
- **Critério de aceite**: jogador pede DNA (120min game), volta no dia seguinte, faz login → ETA mostra "Pronta" só após 2 minutos reais com sessão ativa.

### 1.7 — Velocidade configurável (`TIME_MULTIPLIER`)
- **Onde**: `TimeContext.tsx:36`, settings UI nova, persistência em `User` ou localStorage.
- **Comportamento**: dropdown "Velocidade: 30x / 60x / 120x". Forenses respeitam. Default 60x.
- **Critério de aceite**: trocar para 120x → 1s real = 2min game; tudo escalonado consistentemente.

### 1.8 — Parar relógio quando offline (sessão encerrada)
- **Onde**: combinação de 1.2, 1.3, 1.6.
- **Comportamento**: no logout/disconnect, `EndSession` salva `gameTimeAtEnd`. Forense em andamento **não** avança game-time enquanto sessão não estiver `Active`. Background service confere se a sessão dona da forense está ativa.
- **Critério de aceite**: jogador faz disconnect, volta 24h depois → game-clock retoma exatamente onde parou, forenses ainda têm o mesmo tempo restante.

---

## Epic 2 — Sistema de Promoção (Manual Review)

> Objetivo: ranks subem por ação do jogador, mediada por e-mail in-game do Capitão.

### 2.1 — Service `PromotionService` com `EvaluatePendingPromotionAsync`
- **Onde**: novo `Services/PromotionService.cs`, registrado em `Program.cs`.
- **Comportamento**: dado `userId`, calcula `casesResolvedGraded` (igual a `ProfileController:175`), compara com `PromotionRules.ResolvedRequiredFor[NextRank]`, retorna se há promoção pendente.
- **Critério de aceite**: cobertura por unit test em `CaseZeroApi.Tests/Services/PromotionServiceTests.cs`.

### 2.2 — Gatilho pós-submissão correta
- **Onde**: `SolutionService.SubmitAsync` em `backend/CaseZeroApi/Services/SolutionService.cs:100-167`.
- **Comportamento**: após salvar submission graded+correta, chamar `PromotionService.EvaluatePendingPromotionAsync`. Se elegível, criar `PromotionOffer` (nova tabela) e e-mail in-game do Capitão. Idempotente: um único offer ativo por (user, targetRank).
- **Critério de aceite**: jogador completa 3º caso graded correto → próximo login mostra inbox com e-mail do Capitão, sem duplicar a oferta em re-runs.

### 2.3 — Tabela `PromotionOffers` + DTO
- **Onde**: `Models/PromotionOffer.cs`, migração EF.
- **Campos**: `Id`, `UserId`, `TargetRank` (DetectiveRank), `OfferedAt`, `Status` (Pending/Accepted/Expired), `AcceptedAt`, `EmailId` (vinculado ao e-mail in-game gerado).
- **Critério de aceite**: migração corre limpa em `dotnet ef database update`.

### 2.4 — E-mail in-game do Capitão (template)
- **Onde**: reaproveitar `Email.cs` (`EmailType.PromotionNotice` já existe) ou `CaseV2Email` síntético no inbox global do usuário (não por caso) — precisa decidir.
- **Comportamento**: e-mail contém botão "Aceitar promoção" que faz `POST /api/promotion/offers/{id}/accept`. Templates traduzidos nas 4 línguas (pt-BR, en, es, fr).
- **Critério de aceite**: e-mail aparece no inbox global do jogador (fora do contexto de caso) e tem botão funcional.

### 2.5 — Endpoint de aceite + atualização de rank
- **Onde**: novo `Controllers/PromotionController.cs`.
- **Endpoints**:
  - `GET /api/promotion/offers` — lista ofertas pendentes do usuário.
  - `POST /api/promotion/offers/{id}/accept` — promove: atualiza `user.Rank`, `user.LastPromotionDate`, insere `UserRankHistory` (`PreviousRank`, `NewRank`, `Reason="Manual acceptance"`), marca offer como Accepted.
- **Critério de aceite**: após aceite, `GET /api/profile/stats` retorna `currentRank` atualizado e timeline mostra a transição.

### 2.6 — Backfill de `CasesResolved` em User
- **Onde**: `PromotionService` ou job de migration.
- **Comportamento**: ao avaliar promoção, sincronizar `user.CasesResolved` com a contagem real de `CaseSubmissions` graded+corretas. Hoje o campo está sempre 0.
- **Critério de aceite**: usuários existentes que resolveram casos têm `CasesResolved` populado.

### 2.7 — UI: notificação de oferta pendente
- **Onde**: badge no Dock ou no Clock; tela de profile mostra ofertas pendentes.
- **Comportamento**: indicador visual quando há `PromotionOffer.Status == Pending`. Click leva ao inbox (e-mail do Capitão) ou modal de aceite direto.
- **Critério de aceite**: visual claro, não bloqueia gameplay.

### 2.8 — Bloqueio de casos por `RequiredRank`
- **Onde**: `CaseSessionController.StartSession` em `backend/CaseZeroApi/Controllers/CaseSessionController.cs:34-125` e `CasesController` (lista).
- **Problema**: hoje `c.RequiredRank` é retornado mas nunca enforce. Jogador Rook pode entrar em caso de Captain.
- **Comportamento**: ao iniciar sessão, validar `user.Rank >= RequiredRank`. Retorna 403 com mensagem clara. Lista de casos marca os bloqueados.
- **Critério de aceite**: Rook não consegue iniciar caso `requiredRank: "Sergeant"`; UI mostra cadeado.

---

## Epic 3 — Sistema Forense

> Objetivo: forense influencia gameplay (não pode pedir tudo de graça), UX limpa, sem código legado.

### 3.1 — Remover `ForensicModule.tsx` legado
- **Onde**: `frontend/src/components/apps/ForensicModule.tsx`, imports em `Dock.tsx` e onde estiver registrado.
- **Justificativa**: hardcoded para CASE-2024-00x (que nem existe no storage), local state, não chama backend, paralelo ao `ForensicsQueue.tsx` real.
- **Critério de aceite**: build limpo, nenhuma referência viva ao componente.

### 3.2 — Penalidade de tempo para análise "no findings"
- **Onde**: `ForensicsBackgroundService.cs:80-103`.
- **Comportamento**: quando `outcome` é null OR `outcome.Findings == false`, multiplicar `EstimatedCompletionTime` por fator configurável (ex: 1.5x) **e** registrar a "perda" na sessão. Em game-time (depende de 1.6).
- **Critério de aceite**: pedir DNA em asset irrelevante demora ~50% a mais do que pedir num asset com findings; e-mail de "no findings" chega no horário aumentado.

### 3.3 — Penalidade de pontuação (categoria Analysis)
- **Onde**: `SolutionService.SubmitAsync` em `backend/CaseZeroApi/Services/SolutionService.cs:66-81`.
- **Comportamento**: além de premiar `requiredAnalysisIds` hit, **descontar** % por análise "no-findings" solicitada (analisar e descobrir o útil é parte da pontuação). Fórmula: `analysisScore = base - (noFindingsRequests * penaltyWeight)`. Cap mínimo em 0. Ignora Rookie cases (lab indisponível).
- **Critério de aceite**: dois jogadores resolvem o caso; quem pediu 10 análises inúteis tem score menor que quem pediu só as necessárias. Tests cobrem.

### 3.4 — UX: chip "Conta para a solução" em análises relevantes
- **Onde**: `frontend/src/components/apps/ForensicsQueue.tsx`, `SubmitCase.tsx:556-566`.
- **Comportamento**: na lista de análises completas, indicar visualmente quais resultados são `requiredAnalysisIds`. **Atenção spoiler**: NÃO marcar antes da análise; só após completed E player visualizou o e-mail/asset de resultado.
- **Critério de aceite**: jogador percebe que algumas análises "valeram" a pena sem que isso seja entregue de graça antes.

### 3.5 — Sinalização clara de no-findings vs com-findings
- **Onde**: `ForensicsQueue.tsx`.
- **Comportamento**: cards de "concluída" mostram badge "Sem achados" vs "Achados encontrados". Botão "Ver Resultado" só aparece em com-findings.
- **Critério de aceite**: jogador entende ao olhar a fila o que vale ler.

### 3.6 — Integração do "Ver Resultado" com DocumentViewer
- **Onde**: `ForensicsQueue.tsx:222-228` (TODO marcado no código).
- **Comportamento**: clicar abre `DocumentViewerWindow` com o asset desbloqueado.
- **Critério de aceite**: relatório forense abre sem ir à pasta de evidências manualmente.

### 3.7 — Auditoria/dedup: impedir reanálise idêntica
- **Onde**: `ForensicRequestController.CreateForensicRequest`.
- **Comportamento**: bloquear `POST` quando já existe request `(caseId, userId, inputAssetId, analysisType)` em pending/in-progress/completed. Retornar 409 com link para a request existente.
- **Critério de aceite**: jogador não consegue pedir DNA do mesmo asset duas vezes.

### 3.8 — Limpar dead-code: `requestAnalysis` em `CaseEngine`
- **Onde**: `frontend/src/engine/CaseEngine.ts:176-198` (memo já registrada).
- **Critério de aceite**: build limpo, sem código v0 legado de "requiresAnalysis".

---

## Epic 4 — Pinboard

> Objetivo: pinboard persistente por caso, ligado a entidades reais.

### 4.1 — Modelo + backend
- **Onde**: `Models/PinboardItem.cs` + `Models/PinboardConnection.cs` + migração.
- **Campos PinboardItem**: `Id`, `UserId`, `CaseId`, `Kind` (asset/email/suspect/note/freeform), `LinkedEntityId` (id do asset/suspect/email; null para freeform), `Title`, `Body`, `X`, `Y`, `Color`, `CreatedAt`, `UpdatedAt`.
- **Campos PinboardConnection**: `Id`, `UserId`, `CaseId`, `FromItemId`, `ToItemId`, `Label`, `Kind` (suspicion/contradiction/timeline/freeform), `CreatedAt`.

### 4.2 — Controller + endpoints
- **Endpoints**: `GET /api/pinboard/{caseId}`, `POST /api/pinboard/{caseId}/items`, `PUT /api/pinboard/items/{id}`, `DELETE /api/pinboard/items/{id}`, `POST /api/pinboard/{caseId}/connections`, `DELETE /api/pinboard/connections/{id}`, `PUT /api/pinboard/items/{id}/position` (otimizado p/ drag).
- **Critério de aceite**: documentado em Swagger.

### 4.3 — Frontend: carregar/salvar via backend
- **Onde**: `Pinboard.tsx`, novo `services/pinboardService.ts`.
- **Comportamento**: substitui `useState` local por estado sincronizado. Debounce de 500ms em moves para evitar storm.
- **Critério de aceite**: itens persistem entre sessões e dispositivos do mesmo usuário.

### 4.4 — Drop real: arrastar do FileViewer/Email para o pinboard
- **Onde**: `Pinboard.tsx`, `FileViewer.tsx`, `EmailApp.tsx`, eventualmente `SubmitCase`.
- **Comportamento**: arrastar arquivo/asset/suspect cria item linkado. `DropZone` hoje é decorativa; conectar `onDrop` real.
- **Critério de aceite**: arrastar evidência do explorador de arquivos cria card vinculado com título e ícone corretos.

### 4.5 — Edição inline de texto e cor
- **Onde**: `Pinboard.tsx`.
- **Comportamento**: double-click em card → edita título/body. Picker de cor para diferenciar agrupamentos. Salva via PUT.
- **Critério de aceite**: card hardcoded "Evidence item" não existe mais.

### 4.6 — Conexões com label e tipo
- **Onde**: `Pinboard.tsx`.
- **Comportamento**: ao criar conexão, prompt para label opcional e tipo (cor da linha varia). Click na linha permite editar/deletar.
- **Critério de aceite**: jogador pode anotar "viu na cena do crime" entre suspeito e evidência.

### 4.7 — Snap grid + zoom/pan (opcional)
- **Onde**: `Pinboard.tsx`.
- **Comportamento**: snap a grid de 20px ao soltar. Pinch/scroll zoom. Pan com botão direito.
- **Critério de aceite**: organização fica menos bagunçada; cabe mais conteúdo.

### 4.8 — i18n nas 4 línguas
- **Onde**: `frontend/src/locales/*.ts`.
- **Critério de aceite**: todos os textos do pinboard usam `t()` com chaves pt-BR/en/es/fr.

---

## Epic 5 — Notebook (Bloco de notas)

> Objetivo: ferramenta de pensamento usável durante a investigação.

### 5.1 — i18n nas 4 línguas
- **Onde**: `Notebook.tsx` (hardcoded em inglês: "Investigation Notebook", "Loading notes...", "No notes yet", "Failed to ...", modal de delete etc.) + `frontend/src/locales/*.ts`.
- **Critério de aceite**: trocar idioma muda 100% dos textos visíveis no notebook.

### 5.2 — Autosave on type (debounced)
- **Onde**: `Notebook.tsx`.
- **Comportamento**: debounce 800ms; indicador "Saving... / Saved" sempre visível durante edição. Não esperar botão Save.
- **Critério de aceite**: digitar e fechar a janela sem clicar Save → conteúdo persiste.

### 5.3 — Atalhos de teclado
- **Onde**: `Notebook.tsx`.
- **Atalhos**: `Ctrl+N` (nova nota), `Ctrl+S` (forçar save), `Ctrl+F` (buscar nas notas), `Ctrl+B/I` (bold/italic markdown), `Esc` (sair de edição de título), `Ctrl+Shift+T` (inserir timestamp do game-time).
- **Critério de aceite**: cada atalho funciona dentro da janela ativa do notebook.

### 5.4 — Inserir game-timestamp
- **Onde**: `Notebook.tsx` + `useTimeContext`.
- **Comportamento**: botão "🕐 Inserir tempo" e atalho `Ctrl+Shift+T` inserem `[gameTime atual ISO]` no cursor.
- **Critério de aceite**: nota fica datada com o tempo do jogo, não tempo real.

### 5.5 — Search/filter
- **Onde**: `Notebook.tsx`, sidebar.
- **Comportamento**: input de busca filtra por título + content (client-side). Highlight match.
- **Critério de aceite**: digitar "DNA" filtra notas que mencionam DNA.

### 5.6 — Markdown render (preview toggle)
- **Onde**: `Notebook.tsx`, dependência `react-markdown` (já no projeto? verificar).
- **Comportamento**: toggle entre raw e rendered (preview). Headings, bold, italic, lists, links, blockquotes.
- **Critério de aceite**: nota com `# Título\n- item` renderiza formatado.

### 5.7 — Link a entidades (asset/suspect/email)
- **Onde**: `Notebook.tsx`, novo `[[asset:id]]`-style syntax ou autocomplete `@` que sugere entidades do caso.
- **Comportamento**: tokens viram links clicáveis que abrem o asset/suspect/email correspondente.
- **Critério de aceite**: clicar em `[[asset:abc123]]` abre o `DocumentViewer` daquele asset.

### 5.8 — Pin/favorite + ordenação
- **Onde**: `Notebook.tsx`, modelo `Note` (`IsPinned bool`), migração EF.
- **Comportamento**: estrelinha por nota. Sidebar ordena pinned primeiro, depois por `UpdatedAt`.
- **Critério de aceite**: notas favoritas aparecem no topo, independente de data.

### 5.9 — Validação de tamanho + erros amigáveis
- **Onde**: `Notebook.tsx`, `Note.cs` (`Title MaxLength=500`, content sem limite).
- **Comportamento**: aviso visual antes de bater o limite; erros backend traduzidos para mensagens i18n.

---

## Trabalho transversal

- **i18n**: todas as strings novas (epics 2, 4, 5) devem ter chaves nas 4 línguas (pt-BR, en, es, fr) conforme convenção do projeto.
- **Tests**: criar/atualizar testes em `backend/CaseZeroApi.Tests` e `backend/CaseZeroApi.IntegrationTests` para promotion, forensic penalties, pinboard CRUD, session heartbeat. Frontend tests para hooks novos.
- **Migrações EF**: cada nova tabela/coluna em sua própria migração (`PromotionOffers`, `PinboardItems`, `PinboardConnections`, `IsPinned` em Notes, `GameTimeRequestedAt`/`DurationGameMinutes` em ForensicRequests, possivelmente `TimeEntries`).
- **CaseGen.Functions e CaseGen.Functions.Test** permanecem em .NET SDK 9 (regra do projeto).

## Riscos / pontos de atenção

- **Heartbeat de tempo (1.2)** sobrecarrega o backend se muitos usuários simultâneos. Mitigar: 30s + write-only patch, sem retorno pesado.
- **ETA forense em game-time (1.6)** requer que o background service consulte sessão ativa por request; precisa estar bem indexado (`CaseId+UserId+Status`).
- **Penalidade forense de score (3.3)** pode irritar jogadores. Sugiro tornar configurável por caso (`forensicsDefaults.noFindingsPenalty`) e default suave.
- **Manual review de promoção (2.4)** dificulta progresso se jogador ignora inbox; talvez badge persistente no Dock seja crucial.
- **Pinboard drop real (4.4)** depende de HTML5 DragAndDrop entre Windows do desktop simulado; pode exigir ajuste no `Window.tsx`.
- **Bloqueio por rank (2.8)** pode trancar jogadores que já jogaram tudo até hoje sem nunca ter rank > Rook. Combinar com 2.6 (backfill) para evitar regressão.

## Notas

- Plano não estima datas/prazos.
- Cada item é independente o suficiente para ser feito por um agente em paralelo dentro do mesmo epic (autopilot_fleet).
- Atualizar este `plan.md` quando entrar em implementação.
