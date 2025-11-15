# Plano de Implementação — FileViewer Roadmap v1

Documento mestre contendo as sete fases do roadmap de evolução do FileViewer. Cada fase entrega blocos completos e independentes, com validações claras antes de avançar.

## 📌 Objetivos Gerais

- Garantir que todo caso gerado pela pipeline seja exibível via FileViewer sem dependências manuais.
- Alinhar backend, engine e frontend para suportar arquivos ricos (texto, fotos, mídia, laudos) com contexto investigativo.
- Criar base para recursos colaborativos, produtividade de investigador e extensões com IA no futuro.

---

## Fase 1 — Inventário & Fundamentos (Semana 1)

**Meta:** mapear completamente o acervo atual, padronizar nomenclatura e habilitar migração segura para o novo modelo.

**Entregáveis:**

- Script `tools/fileviewer-audit.ts` exportando lista de arquivos em `cases/**/assets` com metadados (tipo, tamanho, hash, uso) em CSV.
- Documento `docs/fileviewer/file-taxonomy.md` definindo taxonomia de tipos (`briefing`, `witness_statement`, `photo_crime_scene`, etc.).
- Planilha de decisões (keep/merge/remove) para cada pasta legada (assets duplicados, PDFs obsoletos, etc.).

**Critérios de saída:**

- 100% das pastas auditadas e classificadas.
- Taxonomia aprovada pelo time de narrativa e engenharia.

---

## Fase 2 — Pipeline & API de Arquivos (Semana 2)

**Meta:** expor um endpoint único que consolide arquivos de um caso com os metadados definidos na fase 1.

**Entregáveis:**

- Novo serviço `FileInventoryService` no backend (`backend/CaseZeroApi/Services`) com cache e suporte a Blob Storage.
- Endpoint `GET /cases/{caseId}/files` retornando `FileViewerResponse` (modelo único já usado no frontend/engine).
- Testes de integração cobrindo casos sem assets, casos com assets grandes e cenários de cache inválido.

**Critérios de saída:**

- Endpoint retornando em <400 ms para casos médios (até 100 arquivos).
- Cobertura >80% para o serviço.

---

## Fase 3 — DataModel + Engine (Semana 3)

**Meta:** alinhar o CaseEngine para consumir a nova API e preparar enriquecimento sem depender do frontend.

**Entregáveis:**

- Atualização em `frontend/src/engine/CaseEngine.ts` com métodos `loadFileInventory` e `getFileById`.
- Hook `useFileInventory(caseId)` com cache em React Query e estados de loading/erro padronizados.
- Testes unitários no engine garantindo ordenação, filtros e suporte a lazy loading.

**Critérios de saída:**

- Engine entrega a mesma estrutura para todos os apps (Desktop, Mobile futuro, ferramentas internas).
- Tempo de carregamento inicial do Desktop <2s em ambiente local.

---

## Fase 4 — FileViewer UX Core (Semanas 4-5)

**Meta:** reescrever a experiência do FileViewer (EngineFileViewer + componentes auxiliares) focando em: navegação, dual panes e preview confiável.

**Entregáveis:**

- Layout dual-pane responsivo em `frontend/src/components/apps/EngineFileViewer.tsx` (lista + painel principal).
- Componente `FileDetailsPanel` com preview nativo para PDF, imagens, texto e fallback download.
- Barra de busca global (por título, tipo, tags) com highlight em tempo real.
- Telemetria básica no `frontend/src/services/telemetry.ts` para abrir arquivo, trocar idioma e acionar download.

**Critérios de saída:**

- QA valida fluxo em `CASE-2024-001` e `CASE-2024-002` sem regressões.
- LCP < 2.5s no Desktop (Chrome, 70 Mbps).

---

## Fase 5 — Produtividade & Contexto (Semana 6)

**Meta:** adicionar recursos que ajudem o investigador a trabalhar com múltiplos arquivos simultâneos.

**Entregáveis:**

- Pins/Estrelas persistidos no `localStorage` (sincronizados por caseId + userId futuro).
- Notas rápidas por arquivo (`notes/fileviewer` no backend aguardando toggle) com CRUD local-only.
- Timeline de atividades dentro do FileViewer (últimos arquivos abertos, downloads feitos).
- Suporte a comparação lado a lado (split view), com sincronização de zoom para imagens.

**Critérios de saída:**

- Usuário consegue alternar entre arquivos críticos sem perder contexto.
- Métrica de engajamento (tempo médio dentro do FileViewer) coletada no GA/Clarity.

---

## Fase 6 — Localização & Acessibilidade (Semana 7)

**Meta:** aplicar política “4 idiomas” em todo texto do FileViewer e garantir acessibilidade mínima.

**Entregáveis:**

- Strings externas em `frontend/src/locales/{en,pt,es,de}/fileviewer.json` com paridade total.
- Suporte a atalhos de teclado (setas, Enter para abrir, `Cmd+K` para busca).
- Ajustes de contraste, foco visível e leitores de tela (aria labels) em todos os botões/links.

**Critérios de saída:**

- Testes de i18n passando (Vitest + screenshot diff para idiomas).
- Lighthouse Accessibility ≥ 90.

---

## Fase 7 — QA, Observabilidade e Lançamento (Semana 8)

**Meta:** preparar o rollout controlado e garantir observabilidade ponta a ponta.

**Entregáveis:**

- Plano de testes cruzado (frontend + API) no arquivo `PLAN-V3/qa-checklist-fileviewer.md`.
- Dashboard no Application Insights (Requests, Latência, Erros por tipo de arquivo).
- Feature flag `fileviewer_v2` no backend + toggles no frontend (`useFeatureFlag`).
- Guia de rollout e reversão em `docs/release-notes/fileviewer-v2.md`.

**Critérios de saída:**

- Testes e2e no Playwright cobrindo cenários críticos (abrir PDF pesado, erro de rede, troca de idioma).
- Flag liberada para 10% da base interna antes do GA.

---

## 📅 Sequenciamento & Dependências

1. Fase 1 precisa terminar antes de qualquer alteração de API.
2. Fase 2 e 3 podem rodar em paralelo parcialmente (engine pode mockar API).
3. Fases 4 e 5 dependem de 2/3 finalizadas.
4. Fase 6 depende do design congelado da Fase 4.
5. Fase 7 depende de todas as anteriores e libera o go-live.

---

## ✅ Checklist Resumido

- [ ] Auditoria concluída e taxonomia congelada (F1).
- [ ] Endpoint /cases/{caseId}/files ativo (F2).
- [ ] Engine + hooks entregando inventário completo (F3).
- [ ] Novo FileViewer dual-pane em produção (F4).
- [ ] Recursos de produtividade e contexto habilitados (F5).
- [ ] Localização + acessibilidade validadas (F6).
- [ ] Observabilidade e feature flag configuradas (F7).
- [ ] Tutorial aparece na primeira vez
- [ ] Desktop carrega com apps corretos
- [ ] CASO-2024-001 funciona 100%
- [ ] Documentação completa
- [ ] Testes passam

---

**Próximos Passos:** Começar pelo Sprint 1 - Definição do schema e estrutura básica do case.json.
