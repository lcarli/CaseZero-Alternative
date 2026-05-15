# Capítulo 12 - Roadmap do Produto & Visão de Futuro

**Documento de Design de Jogo - CaseZero v3.0**  
**Última Atualização:** 14 de novembro de 2025  
**Status:** ✅ Completo

---

## 12.1 Visão Geral

Este capítulo define o **roadmap do produto, o cronograma de desenvolvimento e a visão de futuro** para o CaseZero v3.0 e além. Cobre o planejamento de lançamento, as iterações pós-lançamento, prioridades de features e objetivos estratégicos de longo prazo.

**Conceitos-chave:**
- Estratégia de lançamento em fases
- Conjunto de features do MVP
- Cadência de conteúdo pós-lançamento
- Framework de priorização de features
- Visão estratégica de 3 anos

---

## 12.2 Fases de desenvolvimento

### Fase 1: Fundamentos (Meses 1-3)

**Objetivo:** Construir a arquitetura e os sistemas centrais

**Backend:**
- [ ] Implementação do schema de banco (PostgreSQL)
- [ ] Modelos do Entity Framework Core
- [ ] Sistema de autenticação (JWT)
- [ ] Endpoints principais da API (cases, sessions, users)
- [ ] Configuração da infraestrutura Azure
- [ ] Integração com Blob Storage

**Frontend:**
- [ ] Setup do projeto React (Vite + TypeScript)
- [ ] Arquitetura da store Redux
- [ ] Shell da interface desktop (taskbar, janelas)
- [ ] Biblioteca básica de componentes
- [ ] Configuração de rotas

**DevOps:**
- [ ] Pipeline CI/CD (GitHub Actions)
- [ ] Ambiente de staging
- [ ] Monitoramento (Application Insights)

**Entregas:**
- Autenticação funcionando
- Telas de lista/detalhe de casos
- Visualizador básico de documentos
- Pipeline de deploy

---

### Fase 2: Gameplay central (Meses 4-6)

**Objetivo:** Implementar mecânicas essenciais de jogabilidade

**Features:**
- [ ] App Case Files (visualizador de documentos, galeria de evidências)
- [ ] Integração com PDF.js
- [ ] Visualizador de fotos de evidência com lightbox
- [ ] Sistema de anotações (auto-save)
- [ ] Visualização da linha do tempo
- [ ] Rastreamento de sessão (tempo, progresso)

**Conteúdo:**
- [ ] Primeiro caso de teste (CASE-TEST-001)
- [ ] Templates de documentos finalizados
- [ ] Pipeline de assets estabelecido

**Testes:**
- [ ] Testes unitários (80% de cobertura)
- [ ] Testes de integração da API
- [ ] Testes manuais de fluxos centrais

**Entregas:**
- Experiência completa de leitura de caso
- Sistema de anotações funcional
- Persistência de sessão operante

---

### Fase 3: Perícias & Submissão (Meses 7-9)

**Objetivo:** Adicionar sistema de perícia e envio de solução

**Features:**
- [ ] UI do app Forensics Lab
- [ ] Sistema de requisição de perícia (backend)
- [ ] Azure Function para timers
- [ ] Exibição de timer em tempo real
- [ ] Notificações por e-mail (perícia concluída)
- [ ] App Submit Solution (formulário multi-etapas)
- [ ] Lógica de validação da solução
- [ ] Sistema de feedback

**Conteúdo:**
- [ ] 3 casos Easy
- [ ] 2 casos Medium
- [ ] Templates de laudos periciais

**Testes:**
- [ ] Testes E2E para fluxos críticos
- [ ] Testes de carga (100 usuários simultâneos)
- [ ] QA de conteúdo para todos os casos

**Entregas:**
- Sistema de perícia funcional
- Submissão de solução operacional
- 5 casos jogáveis

---

### Fase 4: Progressão & Polimento (Meses 10-12)

**Objetivo:** Implementar sistema de progressão e polir a UX

**Features:**
- [ ] Sistema de cálculo de XP
- [ ] Progressão de patentes de detetive
- [ ] Página de perfil do usuário
- [ ] Dashboard de estatísticas
- [ ] Conquistas (ocultas)
- [ ] Bloqueio de casos por patente
- [ ] Caso tutorial
- [ ] Fluxo de onboarding
- [ ] Painel de configurações (tema, velocidade de perícia, acessibilidade)

**Conteúdo:**
- [ ] 1 caso Hard
- [ ] 1 caso Expert
- [ ] Caso tutorial (15-20 minutos)

**Polimento:**
- [ ] Animações de UI (transições de 200 ms)
- [ ] Efeitos sonoros (opcional)
- [ ] Polimento de estados de loading
- [ ] Melhoria de mensagens de erro
- [ ] Auditoria de acessibilidade

**Testes:**
- [ ] Testes de acessibilidade (WCAG AA)
- [ ] Otimização de performance
- [ ] Beta test (50 usuários)
- [ ] Semana de bug bash

**Entregas:**
- Sistema de progressão completo
- 9 casos no total (3 Easy, 3 Medium, 2 Hard, 1 Expert)
- Tutorial finalizado
- MVP pronto para lançamento

---

## 12.3 Plano de lançamento (Mês 13)

### Pré-lançamento (Semanas 1-2)

**Marketing:**
- [ ] Landing page publicada
- [ ] Contas em redes sociais criadas
- [ ] Press kit preparado
- [ ] Cópias para review prontas
- [ ] Trailer produzido

**Técnico:**
- [ ] Ambiente de produção testado
- [ ] CDN configurada
- [ ] Procedimentos de backup verificados
- [ ] Alertas de monitoramento configurados
- [ ] Plano de rollback documentado

**Conteúdo:**
- [ ] Todos os 9 casos aprovados no QA final
- [ ] Assets enviados para produção
- [ ] Banco de dados populado

### Semana de lançamento (Semana 3)

**Dia 1 (Soft Launch):**
- Deploy para produção (acesso limitado)
- Monitorar bugs críticos
- Divulgação para influenciadores menores

**Dia 3 (Lançamento público):**
- Abrir registro para o público geral
- Distribuir press release
- Campanha em redes sociais
- Posts em Reddit/HN
- E-mail para beta testers

**Dia 5:**
- Monitorar feedback de usuários
- Publicar hotfixes críticos
- Revisar analytics

**Dia 7:**
- Retrospectiva da primeira semana
- Planejar correções imediatas

### Pós-lançamento (Semana 4)

**Focos:**
- Correções de bugs (prioridade P0/P1)
- Questões de performance
- Incorporação de feedback
- Preparar primeira atualização de conteúdo

**Métricas de atenção:**
- Daily Active Users (DAU)
- Taxa de conclusão de casos
- Tempo médio de sessão
- Taxa de crash
- Volume de chamados de suporte

---

## 12.4 Roadmap pós-lançamento

### Meses 1-3: Estabilização

**Objetivos:**
- Corrigir bugs do lançamento
- Estabilizar a experiência do usuário
- Estabelecer cadência de conteúdo

**Features:**
- [ ] Melhorias menores de UX baseadas em feedback
- [ ] Correções de bugs (meta: <10 bugs P1 abertos)
- [ ] Otimizações de performance

**Conteúdo:**
- 1 novo caso (Mês 3)
- Usar backlog se necessário

**Métricas de sucesso:**
- Taxa de crash < 0,1%
- Taxa de conclusão de casos > 60%
- Tempo médio de sessão > 45 minutos
- Tempo de resposta do suporte < 24h

---

### Meses 4-6: Qualidade de vida

**Objetivos:**
- Adicionar features solicitadas
- Melhorar retenção
- Expandir biblioteca de casos

**Features:**
- [ ] Filtros no navegador de casos (por tipo, tags)
- [ ] Ordenação (dificuldade, tempo, taxa de conclusão)
- [ ] Seção "Jogados Recentemente"
- [ ] Recomendações de casos
- [ ] Toggle de tema claro/escuro (UI apenas)
- [ ] Guia de atalhos de teclado
- [ ] Opção de imprimir documentos

**Conteúdo:**
- 1 novo caso por mês (3 no total)
- Mix de dificuldades

**Métricas de sucesso:**
- Retenção no mês 3 > 40%
- Média de casos resolvidos por usuário > 2
- NPS > 40

---

### Meses 7-12: Expansão

**Objetivos:**
- Adicionar novos tipos de evidência
- Introduzir conteúdo sazonal
- Iniciar localização

**Features:**
- [ ] Novos tipos de evidência (digital, gravações de áudio)
- [ ] Novas perícias (forense digital, análise de áudio)
- [ ] Filtros de dificuldade de caso
- [ ] Refinamento do retry para casos falhos
- [ ] Melhorias na página de estatísticas
- [ ] Notificações de conquistas (após solução)

**Conteúdo:**
- 1-2 casos por mês (6-12 no total)
- Casos sazonais/temáticos (ex.: Holiday Murder Mystery)
- Início da localização dos 3 casos mais populares (Francês, Espanhol)

**Localização:**
- [ ] Infraestrutura de i18n
- [ ] Tradução de UI (4 idiomas)
- [ ] Tradução de casos (top 3 casos, 2 idiomas)

**Métricas de sucesso:**
- Biblioteca total: 15-21 casos
- Retenção no mês 6 > 35%
- Usuários internacionais > 20%

---

## 12.5 Roadmap do Ano 2

### Q1 (Meses 13-15): Features de comunidade

**Objetivos:**
- Aumentar engajamento da comunidade
- Adicionar features sociais
- Elevar a viralidade

**Features:**
- [ ] Avaliação de casos (1-5 estrelas pós-solução)
- [ ] Reviews/comentários de usuários (opcional)
- [ ] "Compartilhe sua solução" (com spoiler)
- [ ] Leaderboards (opcional, baseado em tempo)
- [ ] Perfis de detetive (toggle público/privado)
- [ ] Sistema de amigos (opcional)
- [ ] "Desafie um amigo" (enviar recomendação de caso)

**Conteúdo:**
- 3 casos por trimestre
- Temas escolhidos pela comunidade

**Privacidade:**
- Todas as features sociais opt-in
- Perfis podem ser privados
- Sem compartilhamento obrigatório

---

### Q2 (Meses 16-18): Mecânicas avançadas

**Objetivos:**
- Aprofundar a jogabilidade
- Adicionar complexidade para veteranos
- Experimentar novos formatos

**Features:**
- [ ] Sistema de interrogatório (perguntas a suspeitos, uso limitado)
- [ ] Reconstrução de cena (builder de linha do tempo drag-and-drop)
- [ ] Ferramenta de comparação de evidências (visualização lado a lado)
- [ ] Diário do caso (notas estruturadas vs livres)
- [ ] Casos multipartes (2-3 casos em sequência)

**Conteúdo:**
- 3 casos com novas mecânicas
- 1 série de casos multipartes (3 casos)

**Experimentação:**
- Testes A/B das novas features
- Pesquisas de feedback
- Programa beta opcional

---

### Q3 (Meses 19-21): Mobile & Acessibilidade

**Objetivos:**
- Alcançar usuários mobile
- Melhorar acessibilidade
- Expandir audiência

**Features:**
- [ ] UI responsiva para tablet/celular
- [ ] Gestos touch (pinch-to-zoom, swipe)
- [ ] Visualizador PDF otimizado para mobile
- [ ] Melhorias de PWA (Progressive Web App)
- [ ] Aprimoramentos de modo offline
- [ ] Controle por voz (experimental)
- [ ] Integração com ampliador de tela

**Conteúdo:**
- 3 casos otimizados para mobile
- Casos mais curtos (1-2 horas) para mobile

**Acessibilidade:**
- Conformidade WCAG AAA (quando viável)
- Melhorias para acessibilidade cognitiva
- Fonte amigável para dislexia

---

### Q4 (Meses 22-24): Ferramentas de conteúdo

**Objetivos:**
- Escalar produção de conteúdo
- Viabilizar conteúdo criado por usuários (UGC)
- Construir comunidade de criadores

**Features:**
- [ ] Editor de casos (inicialmente interno)
- [ ] Biblioteca de templates (documentos, laudos)
- [ ] Uploader de assets
- [ ] Ferramenta de validação (integrada)
- [ ] Modo preview
- [ ] Workflow de publicação
- [ ] Sistema de submissão de casos UGC (beta público)
- [ ] Votação da comunidade em casos UGC

**Conteúdo:**
- 3 casos oficiais
- 10+ casos da comunidade (se UGC estiver pronto)

**Monetização (opcional):**
- Pacotes de casos premium
- Revenue share para criadores (UGC)

---

## 12.6 Visão para o Ano 3

### Direções de expansão

**1. Modo cooperativo (multiplayer)**
- Casos para 2 jogadores
- Visualização de documentos compartilhada
- Comunicação por chat/voz
- Perícias simultâneas
- Anotações colaborativas
- Submissão conjunta da solução

**Desafios técnicos:**
- Sincronização em tempo real
- Resolução de conflitos
- Infraestrutura de servidores

**2. Geração procedural de casos**
- Criação assistida por IA
- Geração baseada em templates
- Colocação aleatória de evidências
- Rejogabilidade infinita (meta ambiciosa)

**Desafios técnicos:**
- Garantir solucionabilidade
- Controle de qualidade
- Coerência narrativa

**3. Eventos ao vivo**
- Casos com tempo limitado
- Leaderboards (speed-solving)
- Desafios da comunidade
- Eventos sazonais

**4. Campanhas narrativas**
- Arcos com 10 casos
- Personagens recorrentes
- Meta-trama entre casos
- Desenvolvimento de personagens

**5. Realidade virtual (VR)**
- Exploração de cena do crime em 3D
- Exame de evidências com interação manual
- Leitura imersiva de documentos
- Interrogatórios em VR

**Desafios técnicos:**
- Custos de criação de assets 3D
- Requisitos de hardware VR
- Mitigação de motion sickness

---

## 12.7 Framework de priorização de features

### Método MoSCoW

**Must Have (Lançamento):**
- Autenticação
- Visualização de casos (documentos, evidências)
- Sistema de perícia
- Submissão de solução
- Progressão (patentes, XP)
- 9 casos

**Should Have (Ano 1):**
- Filtros no navegador de casos
- Dashboard de estatísticas
- Conquistas
- Melhorias de tutorial
- 12+ casos adicionais

**Could Have (Ano 2):**
- Features sociais
- Mecânicas avançadas (interrogatório)
- Otimização mobile
- Ferramentas UGC

**Won't Have (Ano 3+):**
- Modo cooperativo
- Geração procedural
- Suporte a VR

### Pontuação RICE

**Fórmula:** (Reach × Impact × Confidence) / Effort

**Exemplo:**
- **Feature:** Filtros no navegador de casos
  - Reach: 80% dos usuários
  - Impact: Médio (2/3)
  - Confidence: Alta (90%)
  - Effort: 2 semanas
  - **Score:** (0,8 × 2 × 0,9) / 2 = 0,72

**Priorize as features com maiores pontuações RICE**

---

## 12.8 Gestão de dívida técnica

### Categorias de dívida

**1. Qualidade de código**
- Refatorar componentes legados (pré-v3.0)
- Remover código morto
- Melhorar cobertura de testes (meta 85%+)

**2. Performance**
- Otimizar renderização de PDFs (lazy loading)
- Reduzir tamanho de bundle (code splitting)
- Otimizar queries de banco

**3. Infraestrutura**
- Atualizar dependências regularmente
- Migrar para frameworks novos quando estáveis
- Acelerar pipeline CI/CD

### Estratégia de redução

**Regra dos 20%:**
- Reservar 20% da capacidade de cada sprint para dívida técnica
- Rotacionar áreas de foco a cada sprint
- Rastrear dívida no backlog

**Reviews trimestrais:**
- Avaliar impacto da dívida
- Priorizar dívidas de alto impacto
- Planejar sprints de refatoração

---

## 12.9 Estratégia de conteúdo

### Cadência de conteúdo

**Ano 1:**
- Lançamento: 9 casos
- Meses 1-3: +1 caso (total 10)
- Meses 4-6: +3 casos (total 13)
- Meses 7-12: +6 casos (total 19)

**Ano 2:**
- 12-18 casos (1-1,5 por mês)
- Biblioteca total: 31-37 casos

**Ano 3:**
- 18-24 casos (se UGC, menos oficiais)
- Biblioteca total: 49-61 casos

### Temas de conteúdo

**Temas principais:**
- Mistérios de assassinato clássicos
- Crimes financeiros (fraude, desvio)
- Crimes corporativos
- Casos domésticos (família, relacionamentos)

**Temas experimentais:**
- Casos históricos (anos 1920, 1950 etc.)
- Ambientações internacionais (com sensibilidade cultural)
- Casos seriais (múltiplas vítimas)
- Casos arquivados (décadas atrás)

**Conteúdo sazonal:**
- Casos temáticos de feriados (dezembro)
- Casos de férias de verão (julho)
- Especial de Halloween (outubro)

---

## 12.10 Estratégia de monetização

### Modelo de lançamento: Compra premium

**Faixa de preço:** US$ 19,99-29,99

**Inclui:**
- Todos os 9 casos do lançamento
- Todos os futuros casos oficiais (sem DLC)
- Sem anúncios, sem IAP
- Compra única

**Racional:**
- Alinhado à experiência "sem pressão de tempo"
- Sem pay-to-win
- Experiência premium
- Sustentável para equipe pequena

### Modelos alternativos (considerar futuramente)

**1. Assinatura (US$ 4,99/mês ou US$ 39,99/ano)**
- Casos mensais
- Casos exclusivos
- Acesso antecipado a novas features

**2. Pacotes de casos (DLC)**
- Jogo base: US$ 14,99 (5 casos)
- Case Pack 1: US$ 9,99 (5 casos)
- Season Pass: US$ 24,99 (todos os futuros pacotes)

**3. Freemium**
- 3 casos grátis (Easy)
- Pagamento para desbloquear (Premium)
- Ou anúncios (não recomendado)

**Decisão:** Manter compra premium no lançamento, reavaliar no Ano 2

---

## 12.11 Marketing & comunidade

### Marketing de lançamento

**Canais:**
- Reddit (r/Games, r/DetectiveGames, r/WebGames)
- Twitter/X (comunidade indie)
- YouTube (reviewers indie)
- Twitch (streamers)
- Steam (se houver app desktop)

**Mensagem:**
- "Trabalho de detetive realista, sem pressão"
- "Sherlock Holmes encontra Papers, Please"
- "Resolva crimes no seu ritmo"

**Público-alvo:**
- Fãs de mistério/puzzle (30-45 anos)
- Jogadores casuais buscando profundidade
- Ouvintes de true crime
- Entusiastas de jogos indie

### Construção de comunidade

**Servidor Discord:**
- Discussão de casos (canais com spoiler)
- Reports de bugs
- Pedidos de features
- Vitrine de criadores (UGC)

**Engajamento recorrente:**
- Atualizações mensais de desenvolvimento
- Teasers de casos
- Conteúdo de bastidores
- Sessões de Q&A

**Feedback do usuário:**
- Pesquisas pós-caso
- Botão de feedback in-app
- Votação comunitária de features

---

## 12.12 Crescimento de equipe

### Equipe de lançamento (5-6 pessoas)

- 1 Lead Developer (full-stack)
- 1 Frontend Developer
- 1 Backend Developer
- 2 Case Writers
- 1 Designer (UI/UX + documentos)
- (Opcional) 1 QA Tester

### Equipe do Ano 1 (7-8 pessoas)

- +1 Case Writer (total 3)
- +1 QA Tester
- +1 Community Manager (meio período)

### Equipe do Ano 2 (10-12 pessoas)

- +1 Developer (foco mobile)
- +1 Case Writer (total 4)
- +1 Designer
- +1 DevOps Engineer (meio período)
- +1 Localization Coordinator

### Equipe do Ano 3 (15-20 pessoas)

- +2 Developers (modo co-op)
- +2 Case Writers (total 6)
- +1 Moderador de conteúdo UGC
- +1 Marketing Manager
- +1 Artista 3D (se VR/3D)

**Budget:** Consulte [10-PRODUCAO-DE-CONTEUDO.md](10-PRODUCAO-DE-CONTEUDO.md) para detalhes de custo

---

## 12.13 Gestão de riscos

### Riscos identificados

**1. Gargalo na produção de conteúdo**
- **Risco:** Incapacidade de produzir casos rapidamente
- **Mitigação:** Construir backlog pré-lançamento, contratar roteiristas, sistema UGC

**2. Escalabilidade técnica**
- **Risco:** Servidor cair com alta carga
- **Mitigação:** Testes de carga, auto-scaling, CDN

**3. Inconsistência de qualidade nos casos**
- **Risco:** Casos muito fáceis/difíceis, escrita fraca
- **Mitigação:** QA rigoroso, calibração de dificuldade, treinamento de roteiristas

**4. Retenção de usuários**
- **Risco:** Jogadores consomem todos os casos e saem
- **Mitigação:** Atualizações regulares, conquistas, features sociais

**5. Competição**
- **Risco:** Lançamento de jogos similares
- **Mitigação:** Foco em diferenciais (autenticidade, sem pressão), construir comunidade

**6. Desafios de localização**
- **Risco:** Más traduções, falta de sensibilidade cultural
- **Mitigação:** Tradutores profissionais, revisão por nativos, consultores culturais

---

## 12.14 Métricas de sucesso

### Metas de lançamento (Mês 1)

- **Usuários:** 5.000 registrados
- **DAU/MAU:** 0,3 (30% ativos diários)
- **Taxa de conclusão:** 50% completam ≥1 caso
- **Receita:** US$ 75.000 (5.000 × US$ 15 médio)
- **Taxa de crash:** <0,5%

### Metas do Ano 1

- **Usuários:** 50.000 registrados
- **DAU/MAU:** 0,25
- **Casos resolvidos médios:** 3 por usuário
- **Retenção no mês 3:** 40%
- **Receita:** US$ 750.000
- **NPS:** >40

### Metas do Ano 2

- **Usuários:** 200.000 registrados
- **Biblioteca:** 30+ casos
- **Usuários internacionais:** 25%
- **Casos UGC:** 50+ (se lançado)
- **Receita:** US$ 2M+

### Metas do Ano 3

- **Usuários:** 500.000+ registrados
- **Biblioteca:** 50+ casos oficiais
- **Expansão de plataforma:** Mobile, VR (se aplicável)
- **Comunidade:** Discord ativo (10k+ membros)
- **Receita:** US$ 5M+

---

## 12.15 Pontos de pivô

### Quando pivotar

**Cenário 1: Baixo engajamento (<30% taxa de conclusão)**
- **Análise:** Casos difíceis ou desinteressantes
- **Pivot:** Simplificar dificuldade, adicionar sistema de dicas, mais feedback

**Cenário 2: Alto churn (retenção mês 1 <20%)**
- **Análise:** Conteúdo insuficiente ou poucos hooks de retenção
- **Pivot:** Acelerar produção de conteúdo, adicionar desafios diários

**Cenário 3: Monetização fraca (<US$ 50k no mês 1)**
- **Análise:** Preço errado ou baixa conversão
- **Pivot:** Testar preço menor (US$ 14,99), adicionar demo (1 caso grátis)

**Cenário 4: Demanda alta (>20k usuários no mês 1)**
- **Análise:** Sucesso inesperado, issues de escala
- **Pivot:** Contratar rápido, ampliar infraestrutura, adiar features

**Cenário 5: UGC domina**
- **Análise:** Casos da comunidade superam oficiais
- **Pivot:** Virar plataforma, curadoria de UGC, revenue share

---

## 12.16 Visão de longo prazo (5+ anos)

### Objetivos estratégicos

**1. O Netflix dos jogos de detetive**
- Biblioteca com 100+ casos
- Novos casos semanais
- Modelo de assinatura
- Reconhecimento de IP original

**2. Plataforma para conteúdo de mistério**
- Ecossistema UGC próspero
- Marketplace de criadores
- Ferramentas amplamente utilizadas
- Conteúdo guiado pela comunidade

**3. Expansão cross-media**
- Podcasts (casos em áudio)
- Adaptação para jogo de tabuleiro
- Série de livros (novelizações)
- Inspiração para série de TV

**4. Uso educacional**
- Adoção por cursos de justiça criminal
- Currículo de pensamento crítico
- Treinamento de lógica e raciocínio
- Uso em sala de aula

**5. Reconhecimento em premiações**
- Indicação/vitória IGF
- BAFTA Games Award
- Seleção IndieCade
- Apple Design Award

---

## 12.17 Backlog de features (sem data)

### Features bacanas de ter

**Gameplay:**
- [ ] Sistema de dicas (progressivas, penalidade de XP)
- [ ] Modo foto (registrar evidências)
- [ ] Resumos de caso (auto-gerados pós-solução)
- [ ] Botão "Investigar" (destaca pistas automaticamente)
- [ ] Diário de detetive (templates de notas)
- [ ] Quadro de evidências (painel com alfinetes/fios)

**Social:**
- [ ] Modo espectador (ver amigos jogando)
- [ ] Pacotes co-op (para 2 jogadores)
- [ ] Modo competitivo (speed-solving)
- [ ] Guildas/Clãs (agências de detetive)

**Conteúdo:**
- [ ] Posicionamento aleatório de evidências (replay value)
- [ ] Múltiplas soluções válidas (casos ambíguos)
- [ ] Dilemas morais (suspeitos em área cinzenta)
- [ ] Casos não resolvidos (jogador apresenta teoria)

**Técnico:**
- [ ] Voz atuada nas entrevistas (áudio)
- [ ] Reconstrução 3D da cena do crime
- [ ] Exame de evidências em AR (câmera do celular)
- [ ] Notas assistidas por IA (resumo)

---

## 12.18 Plano de encerramento (se necessário)

### Estratégia de desligamento controlado

**Se o projeto precisar terminar:**

**Meses 1-2: Anúncio**
- Comunicado público (aviso de 6 meses)
- Política de reembolso (se compra <1 ano)
- Explicar motivos com transparência

**Meses 3-4: Preservação**
- Liberar todos os casos sem DRM
- Open source do editor de casos
- Arquivar documentação
- Transferir para comunidade

**Meses 5-6: Transição**
- Desligar servidores com cuidado
- Habilitar modo offline permanente
- Transferir ownership do Discord
- Mensagem de agradecimento

**Longo prazo:**
- Jogo permanece jogável offline
- Comunidade pode hospedar casos
- Código/assets disponíveis (quando possível)

**Racional:** Respeitar o investimento dos jogadores, preservar o trabalho

---

## 12.19 Resumo

**Cronograma de desenvolvimento:**
- **Meses 1-12:** Desenvolvimento do MVP (9 casos, features centrais)
- **Mês 13:** Lançamento
- **Ano 1:** Estabilização + 10 casos adicionais (total 19)
- **Ano 2:** Expansão + features sociais + UGC (12-18 casos)
- **Ano 3:** Maturidade da plataforma + features avançadas

**Plano de lançamento:**
- Soft launch no Dia 1, lançamento público no Dia 3
- 9 casos no lançamento (3 Easy, 3 Medium, 2 Hard, 1 Expert)
- Modelo de compra premium (US$ 19,99-29,99)
- Meta: 5.000 usuários no Mês 1

**Estratégia pós-lançamento:**
- Meses 1-3: Estabilização, correções de bugs
- Meses 4-6: Features de QoL, 3 novos casos
- Meses 7-12: Expansão, 6-12 casos, localização

**Prioridades do Ano 2:**
- Features de comunidade (ratings, social)
- Mecânicas avançadas (interrogatório, reconstrução)
- Otimização mobile
- Ferramentas UGC (beta)

**Visão do Ano 3:**
- Modo cooperativo (multiplayer)
- Geração procedural (experimental)
- Suporte a VR (meta ambiciosa)
- Biblioteca com 50+ casos

**Métricas de sucesso:**
- Lançamento: 5.000 usuários, taxa de conclusão 50%
- Ano 1: 50.000 usuários, retenção mês 3 de 40%
- Ano 2: 200.000 usuários, 30+ casos
- Ano 3: 500.000+ usuários, liderança de plataforma

**Pontos de pivô:**
- Monitorar engajamento, retenção, monetização
- Ajustar dificuldade, preço, ritmo de conteúdo conforme necessário
- Priorizar feedback da comunidade

**Visão de longo prazo:**
- Tornar-se o "Netflix dos jogos de detetive"
- Construir plataforma UGC próspera
- Expandir para outras mídias (podcast, board game, TV)
- Adoção educacional

---

**Próximo capítulo:** [13-GLOSSARIO.md](13-GLOSSARIO.md) – Termos e definições

**Documentos relacionados:**
- [01-CONCEITO.md](01-CONCEITO.md) – Visão central
- [06-PROGRESSAO.md](06-PROGRESSAO.md) – Detalhes do sistema de progressão
- [10-PRODUCAO-DE-CONTEUDO.md](10-PRODUCAO-DE-CONTEUDO.md) – Produção de conteúdo

---

**Histórico de revisões:**

| Data | Versão | Mudanças | Autor |
|------|--------|----------|-------|
| 14/11/2025 | 1.0 | Tradução completa para PT-BR | Assistente de IA |
