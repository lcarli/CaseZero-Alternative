# Apêndice C: Registro de Decisões de Design

## Visão Geral

Este documento registra todas as decisões de design relevantes tomadas durante o desenvolvimento do CaseZero v3.0, incluindo o raciocínio por trás de cada escolha, alternativas consideradas e o contexto que embasou a decisão. O registro funciona como memória institucional para a equipe e ajuda a explicar "por que fizemos assim" para futuros desenvolvedores, designers e stakeholders.

---

## Formato do Registro de Decisões

Cada entrada segue esta estrutura:

- **ID da Decisão**: Identificador único (DEC-YYYY-NNN)
- **Data**: Quando a decisão foi tomada
- **Categoria**: Área do jogo impactada (Jogabilidade, Técnica, Conteúdo, UI/UX, Negócios)
- **Status**: Aprovada, Implementada, Reavaliação, Depreciada
- **Decisão**: O que foi decidido
- **Contexto**: Situação e restrições que levaram a essa decisão
- **Justificativa**: Motivos para a escolha feita
- **Alternativas Consideradas**: Outras opções avaliadas
- **Trade-offs**: Desvantagens ou limitações conhecidas
- **Impacto**: Partes do jogo impactadas
- **Decisões Relacionadas**: Ligações com decisões correlatas

---

## 1. Decisões de Jogabilidade

### DEC-2024-001: Sem Interrogatório ou Árvores de Diálogo

**Data**: novembro de 2024  
**Categoria**: Jogabilidade  
**Status**: Aprovada  
**Decisão**: O CaseZero NÃO terá mecânicas de interrogatório, árvores de diálogo ou interações em tempo real com NPCs.

**Contexto**: Protótipos iniciais (v1.0, v2.0) incluíam interrogatórios com suspeitos. Feedback apontou que a mecânica parecia "gamificada" e tirava o foco da experiência autêntica de análise documental.

**Justificativa**:

- Interrogatórios exigem escrita de diálogos complexa e lógica ramificada, aumentando o tempo de produção em 40-60%
- Jogadores relataram maior prazer ao ler depoimentos e documentos do que navegar em menus de diálogo
- Trabalho investigativo real se apoia fortemente em análise documental, não em questionamentos em tempo real
- Remover interrogatórios libera foco para o ponto forte: investigação baseada em documentos

**Alternativas Consideradas**:

1. **Árvores de diálogo simples**: 5-10 perguntas por suspeito. Rejeitada por ampliar o escopo e ressaltar a sensação "game-like".
2. **Interrogatórios em vídeo pré-gravados**: Estilo Her Story. Rejeitado pelo alto custo de produção e necessidade de elenco.
3. **Transcrições de entrevistas em texto**: Considerada viável, mas incorporada ao sistema de documentos (depoimentos e entrevistas passam a ser documentos).

**Trade-offs**:

- Menos "ação" ou sensação de agência direta
- Parte do público pode esperar mecânicas tradicionais de adventure
- Limita técnicas narrativas específicas (revelações ao vivo, reações dinâmicas)

**Impacto**:

- Capítulo 03 (Mecânicas) – Sem sistema de interrogatório
- Capítulo 04 (Estrutura de Caso) – Transcrições de entrevistas são documentos estáticos
- Capítulo 10 (Pipeline de Conteúdo) – Sem necessidade de casting ou gravação de diálogos
- Capítulo 12 (Roadmap) – Interrogatório movido para Ano 2+ em "Mecânicas Avançadas" (opcional)

**Decisões Relacionadas**: DEC-2024-002, DEC-2024-003

---

### DEC-2024-002: Perícias em Tempo Real (Não Instantâneas)

**Data**: novembro de 2024  
**Categoria**: Jogabilidade  
**Status**: Aprovada  
**Decisão**: Solicitações de perícia terão tempo de processamento em tempo real (2-24 horas) em vez de resultado instantâneo.

**Contexto**: Equilibrar realismo com engajamento. É preciso diferenciar exame de evidência (instantâneo) de análises forenses especializadas (laboratório).

**Justificativa**:

- Aumenta o realismo: laboratórios forenses reais levam dias/semanas
- Cria decisões estratégicas: jogadores priorizam quais perícias pedir primeiro
- Incentiva investigação paralela enquanto os resultados não chegam
- Reduz pedidos impulsivos: cada requisição exige planejamento
- Sustenta modelo live service: jogadores retornam para checar laudos, aumentando retenção

**Alternativas Consideradas**:

1. **Resultados instantâneos**: Rejeitado por ser pouco realista e reduzir profundidade estratégica.
2. **Aceleração de tempo in-game**: Rejeitado por complicar rastreamento de sessão e soar artificial.
3. **Pular tempo mediante custo**: Rejeitado para evitar percepção pay-to-win.
4. **Sem perícias**: Rejeitado, pois perícias são fundamentais na investigação moderna.

**Trade-offs**:

- Jogadores não conseguem concluir um caso em sessão única
- Risco de abandono durante a espera
- Requer sistema de notificação para avisar conclusão
- Complexidade no backend (Azure Functions com triggers de timer)

**Impacto**:

- Capítulo 03 (Mecânicas) – Sistema do Laboratório Forense com contagem regressiva
- Capítulo 04 (Estrutura de Caso) – Tipos de perícia e durações base
- Capítulo 07 (UI) – Widget de requisições pendentes, sistema de notificações
- Capítulo 08 (Técnico) – Implementação de Azure Functions para processamento em background
- Capítulo 11 (Testes) – Testes E2E com aceleração de tempo para validação

**Decisões Relacionadas**: DEC-2024-003, DEC-2024-011

---

### DEC-2024-003: Tentativas de Submissão Limitadas (Máx. 3)

**Data**: novembro de 2024  
**Categoria**: Jogabilidade  
**Status**: Aprovada  
**Decisão**: Jogadores têm no máximo 3 tentativas por caso, com recompensas de XP decrescentes a cada tentativa.

**Contexto**: Criar stakes significativos sem frustração ou necessidade de reiniciar o caso do zero.

**Justificativa**:

- Gera tensão e incentiva investigação completa antes do envio
- Evita tentativas aleatórias ou brute force
- Premia análise cuidadosa e registro de notas
- Permite aprendizado com feedback sem punição permanente
- Alinha-se a investigações reais: não é possível acusar infinitamente a pessoa errada

**Alternativas Consideradas**:

1. **Apenas uma tentativa**: Rejeitado por ser punitivo demais.
2. **Tentativas ilimitadas**: Rejeitado por remover stakes e incentivar adivinhação.
3. **Cooldown temporal** (24h entre tentativas): Rejeitado por ser irritante e pouco útil.
4. **Sistema de dicas com penalidade**: Considerado, mas movido para Ano 2; contraria o pilar "sem mão segurando".

**Trade-offs**:

- Jogadores com dificuldade podem sentir bloqueio na progressão
- Necessidade de feedback claro para explicar erros
- Exige balanceamento para garantir que 3 tentativas sejam suficientes em todos os casos

**Impacto**:

- Capítulo 03 (Mecânicas) – Sistema de submissão com controle de tentativas
- Capítulo 06 (Progressão) – Cálculo de XP por tentativa (1ª: 100%, 2ª: 70%, 3ª: 50%)
- Capítulo 07 (UI) – Exibição do contador de tentativas, mensagens de feedback
- Capítulo 09 (Esquema de Dados) – Entidade CaseSubmission armazena número da tentativa
- Capítulo 11 (Testes) – Casos de teste para limite de tentativas e cálculo de XP

**Decisões Relacionadas**: DEC-2024-004, DEC-2024-013

---

### DEC-2024-004: Explique Sua Resposta (Submissão Escrita)

**Data**: novembro de 2024  
**Categoria**: Jogabilidade  
**Status**: Aprovada  
**Decisão**: Jogadores devem escrever explicações para motivo (100-500 palavras) e método (50-300 palavras), em vez de apenas escolher opções múltiplas.

**Contexto**: Validar entendimento do jogador e evitar sorte. Inspirado pela filosofia de atenção ao detalhe de Papers, Please.

**Justificativa**:

- Obriga o jogador a demonstrar compreensão, não apenas pattern matching
- Revela raciocínio do jogador, permitindo feedback melhor
- Aumenta investimento na solução (esforço próprio gera maior cuidado)
- Dificulta uso de guias externos (mais trabalhoso copiar textos do que selecionar respostas)
- Confirma que o jogador entendeu o caso, não só memorizou pistas

**Alternativas Consideradas**:

1. **Somente múltipla escolha**: Rejeitado por ser fácil de adivinhar e não validar entendimento.
2. **Sem explicação escrita**: Rejeitado pelos mesmos motivos.
3. **Avaliação por IA**: Considerado para futuro (Ano 2+), mas exige NLP complexo com risco de falsos positivos/negativos.
4. **Revisão por pares**: Interessante para recursos comunitários, mas não para o núcleo do jogo.

**Trade-offs**:

- Requer avaliação manual ou lógica robusta (inicialmente matching por palavras-chave)
- Pode afastar quem não gosta de escrever
- Desafio de acessibilidade para não nativos em inglês (mitigado por localização)
- Complexidade de validação (aceitar variações de resposta correta)

**Impacto**:

- Capítulo 03 (Mecânicas) – Formulário de submissão em múltiplas etapas
- Capítulo 04 (Estrutura de Caso) – Solução inclui texto referência para validação de motivo/método
- Capítulo 07 (UI) – Campos de texto com contagem de palavras e feedback
- Capítulo 09 (Esquema de Dados) – DTO de submissão inclui strings de motivo/método
- Capítulo 11 (Testes) – Lógica de validação por palavras-chave e limites de tamanho

**Decisões Relacionadas**: DEC-2024-003, DEC-2024-005

---

### DEC-2024-005: Sem Pressão de Tempo ou Cronômetros (Exceto Perícias)

**Data**: novembro de 2024  
**Categoria**: Jogabilidade  
**Status**: Aprovada  
**Decisão**: Jogadores investigam no próprio ritmo, sem cronômetros, multiplicadores de score por velocidade ou penalidades temporais (exceto o tempo real das perícias).

**Contexto**: Definir o que o pilar "Paciência" significa na prática. Contrabalançar tendências modernas que recompensam rapidez.

**Justificativa**:

- Alinha-se ao pilar "Paciência": investigação deve ser metódica, não apressada
- Reduz ansiedade e torna o jogo acessível a quem precisa de pausas
- Estimula leitura cuidadosa e anotação, não skimming
- Diferencia de jogos detectives com foco em ação/arcade (quick-time events)
- Detetives reais não resolvem casos cronometrados

**Alternativas Consideradas**:

1. **Modo opcional de time trial**: Considerado para Ano 2+ como desafio separado.
2. **Bônus de velocidade**: Rejeitado por ir contra o pilar "Paciência" e incentivar leitura apressada.
3. **Caso "esfria" após X dias**: Rejeitado por criar pressão artificial e frustrante.

**Trade-offs**:

- Menor senso de urgência pode reduzir engajamento para alguns
- Sem leaderboards ou elementos competitivos no lançamento
- Pode parecer "lento" frente a jogos modernos

**Impacto**:

- Capítulo 01 (Conceito) – Pilar "Paciência" descrito explicitamente
- Capítulo 03 (Mecânicas) – Sem contagens regressivas na fase investigativa
- Capítulo 06 (Progressão) – XP não considera tempo (exceto bônus opcional de eficiência futuro)
- Capítulo 07 (UI) – Nenhum elemento de tempo além da contagem de perícias
- Capítulo 12 (Roadmap) – Modo time trial movido para Ano 2+ como recurso opcional

**Decisões Relacionadas**: DEC-2024-002, DEC-2024-006

---

## 2. Decisões de Arquitetura Técnica

### DEC-2024-006: Plataforma Web-First (Não Desktop/Mobile Nativo)

**Data**: novembro de 2024  
**Categoria**: Técnica  
**Status**: Aprovada  
**Decisão**: Desenvolver CaseZero como aplicação web (React + ASP.NET Core) focada em navegadores desktop/tablet, e não como app nativo desktop ou mobile.

**Contexto**: Escolher plataforma principal do MVP. Considerar recursos, complexidade de deploy e alcance do público-alvo.

**Justificativa**:

- Cross-platform nativo (Windows, macOS, Linux) sem builds separados
- Iteração e deploy rápidos (sem filas de aprovação em lojas)
- Menor custo de desenvolvimento (um único codebase)
- Atualizações e correções rápidas (deploy server-side)
- Stack web (React, TypeScript) alinhada à expertise da equipe
- PWA permite suporte offline e experiência "parecida com app"

**Alternativas Consideradas**:

1. **App desktop via Electron**: Rejeitado pelo tamanho do bundle (>100 MB) e uso excessivo de recursos.
2. **Apps mobile nativos** (Swift/Kotlin): Rejeitado por limitações de tela e dificuldade de leitura de documentos.
3. **Unity/Unreal Engine**: Rejeitado por ser overkill para jogo baseado em documentos, aumenta complexidade.
4. **React Native**: Considerado para port futuro, não para o MVP.

**Trade-offs**:

- Limitações do navegador (acesso a sistema de arquivos, offline)
- Requer conexão à internet (mitigado por Service Worker)
- Menos "sensação premium" se comparado a app nativo
- Restrições de performance frente a soluções nativas

**Impacto**:

- Capítulo 08 (Técnico) – Frontend em React + TypeScript, backend ASP.NET Core
- Capítulo 07 (UI) – Metáfora de SO desktop otimizada para viewport web
- Capítulo 11 (Testes) – Testes de compatibilidade (Chrome, Firefox, Safari, Edge)
- Capítulo 12 (Roadmap) – Otimização mobile movida para Ano 2 T3

**Decisões Relacionadas**: DEC-2024-007, DEC-2024-008

---

### DEC-2024-007: PostgreSQL com JSONB (Não NoSQL)

**Data**: novembro de 2024  
**Categoria**: Técnica  
**Status**: Aprovada  
**Decisão**: Usar PostgreSQL com coluna JSONB para armazenar case.json, ao invés de banco puramente NoSQL (MongoDB, CosmosDB).

**Contexto**: Necessidade de guardar dados relacionais (usuários, sessões) E documentos case.json flexíveis.

**Justificativa**:

- JSONB entrega o melhor dos dois mundos: integridade relacional + flexibilidade de JSON
- Garantias ACID fortes para dados de usuários, sessões e submissões
- Indexação e consultas eficientes com JSONB (índices GIN)
- Custo menor que CosmosDB em escala pequena/média
- Equipe familiarizada com SQL
- Evita complexidade de múltiplos bancos (RDBMS + NoSQL)

**Alternativas Consideradas**:

1. **MongoDB**: Rejeitado pela falta de constraints relacionais e consistência mais fraca.
2. **Azure CosmosDB**: Rejeitado pelo custo elevado e por ser exagero para o MVP.
3. **SQL Server com JSON**: Considerado, mas PostgreSQL oferece performance superior com JSONB e é open source.
4. **SQL + Blob Storage separado**: Rejeitado por dificultar queries filtrando dados do case.json.

**Trade-offs**:

- JSONB menos flexível que NoSQL puro para evolução de schema
- Requer Azure Database for PostgreSQL (sem tier gratuito como CosmosDB free tier)
- Equipe precisa dominar recursos específicos do PostgreSQL

**Impacto**:

- Capítulo 08 (Técnico) – PostgreSQL 15+ com colunas JSONB
- Capítulo 09 (Esquema de Dados) – Modelo híbrido: tabelas relacionais + coluna JSONB
- Capítulo 11 (Testes) – Testes de integração SQL, validações de consultas JSONB
- Infraestrutura – Azure Database for PostgreSQL (tier Básico/General Purpose)

**Decisões Relacionadas**: DEC-2024-008, DEC-2024-009

---

### DEC-2024-008: Azure Functions para Perícias (Não WebJobs/Serviço em Background)

**Data**: novembro de 2024  
**Categoria**: Técnica  
**Status**: Aprovada  
**Decisão**: Utilizar Azure Functions com Timer Trigger para processar a conclusão de perícias, em vez de WebJobs ou serviços em background hospedados na API.

**Contexto**: Necessidade de mecanismo confiável e escalável para verificar requisições a cada 5 minutos e gerar laudos.

**Justificativa**:

- Serverless: dispensa manutenção de processo rodando 24/7
- Escala automática: Azure gerencia recursos computacionais
- Custo eficiente: paga apenas pelo tempo de execução (~8.640 execuções/mês = custo mínimo)
- Des acoplado: worker de perícias roda independente da API, aumentando confiabilidade
- Nativo Azure: integração fluida com App Service, Blob Storage, Application Insights

**Alternativas Consideradas**:

1. **HostedService em ASP.NET Core**: Rejeitado por rodar no processo web, dificultando escala e restart.
2. **Azure WebJobs**: Alternativa viável, mas Functions têm tooling mais moderno e desenvolvimento local facilitado.
3. **Azure Logic Apps**: Rejeitado por ser overkill para lógica simples de timer, menos flexível para código customizado.
4. **Polling no cliente**: Rejeitado por ser ineficiente (muitas chamadas) e depender da presença do jogador online.

**Trade-offs**:

- Cold start em Functions (mitigado pela execução a cada 5 minutos)
- Artefato de deploy separado (Functions vs API)
- Complexidade adicional de debug (sistema distribuído)

**Impacto**:

- Capítulo 08 (Técnico) – Arquitetura com Azure Functions e Timer Trigger
- Capítulo 08 (Técnico) – Fluxo de conclusão de perícias
- Capítulo 11 (Testes) – Testes de integração das Functions, simulação de timer
- Infraestrutura – Plano Consumption de Azure Functions

**Decisões Relacionadas**: DEC-2024-002, DEC-2024-009

---

### DEC-2024-009: Azure Blob Storage para Assets (Não Apenas CDN ou Banco)

**Data**: novembro de 2024  
**Categoria**: Técnica  
**Status**: Aprovada  
**Decisão**: Guardar arquivos de caso (PDFs, imagens, laudos) no Azure Blob Storage com Azure CDN na frente, e não no banco de dados ou em solução somente-CDN.

**Contexto**: Precisa de armazenamento escalável e econômico para binários, com distribuição global e suporte a cache offline.

**Justificativa**:

- Blob Storage é otimizado para arquivos binários e muito econômico (US$ 0,018/GB/mês em tier Hot)
- Desacopla armazenamento de compute (API não serve arquivos diretamente)
- Azure CDN garante cache em bordas globais, oferecendo baixa latência
- Suporta tiers (Hot para casos ativos, Cool para arquivados) economizando custos
- SAS tokens permitem acesso seguro e temporário sem expor chaves

**Alternativas Consideradas**:

1. **Armazenar no banco como BLOB/BYTEA**: Rejeitado por inflar o banco, tornar queries lentas e backups caros.
2. **Servir direto via API**: Rejeitado por custo de banda e competição por recursos com a API.
3. **CDN terceirizada** (Cloudflare, Fastly): Considerada, mas Azure CDN integra-se melhor ao Blob Storage.
4. **GitHub Releases** para assets estáticos: Rejeitado por não ser pensado para conteúdo dinâmico nem oferecer controle de acesso.

**Trade-offs**:

- Mais um serviço para gerenciar
- Geração de SAS tokens adiciona complexidade à API
- Recuperação de armazenamento frio tem latência (mitigado mantendo casos ativos no tier Hot)

**Impacto**:

- Capítulo 08 (Técnico) – Arquitetura com Blob + CDN + geração de SAS
- Capítulo 09 (Esquema de Dados) – Entidades de Documento/Evidência armazenam URLs, não binário
- Capítulo 10 (Pipeline de Conteúdo) – Workflow de upload para Blob durante publicação
- Infraestrutura – Azure Blob Storage (Hot/Cool) e Azure CDN Standard

**Decisões Relacionadas**: DEC-2024-007, DEC-2024-010

---

### DEC-2024-010: Autenticação via JWT (Não Cookies de Sessão)

**Data**: novembro de 2024  
**Categoria**: Técnica  
**Status**: Aprovada  
**Decisão**: Utilizar JWT (JSON Web Tokens) com refresh tokens para autenticação, e não cookies de sessão tradicionais.

**Contexto**: Necessidade de autenticação stateless para API escalável que funcione cross-domain (PWA, apps futuros).

**Justificativa**:

- Stateless: dispensa storage de sessão no servidor, facilitando escala horizontal
- Suporte cross-domain: funciona em subdomínios e apps móveis futuros
- Self-contained: inclui claims (ID, username, papel), reduzindo consultas adicionais
- Padrão do mercado: modelo de segurança amplamente compreendido e com bibliotecas maduras
- Compatível com PWA: tokens podem ser armazenados em LocalStorage/SessionStorage para uso offline

**Alternativas Consideradas**:

1. **Cookies de sessão com Redis**: Rejeitado pela complexidade (instância Redis), custo e por não ser stateless.
2. **OAuth2 com Azure AD B2C**: Considerado para enterprise, mas overkill para MVP e aumenta atrito para o usuário.
3. **API keys**: Rejeitado por serem menos seguras (sem expiração), difíceis de rotacionar.

**Trade-offs**:

- Não há revogação imediata antes da expiração (mitigado com tokens curtos + refresh)
- Token maior que ID de sessão (mitigado via gzip)
- Requer prevenção rigorosa de XSS (refresh token HTTPOnly, sanitização de inputs)

**Impacto**:

- Capítulo 08 (Técnico) – Fluxo de autenticação JWT, estrutura do token, rotação de refresh
- Capítulo 08 (Técnico) – Considerações de segurança (armazenamento, prevenção de XSS)
- Capítulo 09 (Esquema de Dados) – Usuário mantém refresh tokens, expiração
- Capítulo 11 (Testes) – Testes de login, refresh e expiração

**Decisões Relacionadas**: DEC-2024-011

---

### DEC-2024-011: React + Redux Toolkit (Não Context API ou Zustand)

**Data**: novembro de 2024  
**Categoria**: Técnica  
**Status**: Aprovada  
**Decisão**: Utilizar Redux Toolkit para gerenciamento de estado no frontend, em vez de React Context API ou bibliotecas alternativas (Zustand, Jotai, Recoil).

**Contexto**: Necessidade de gerenciamento de estado robusto para dados complexos (casos, documentos, evidências, perícias, notas, UI).

**Justificativa**:

- Redux Toolkit simplifica o boilerplate (createSlice, RTK Query)
- Debug time-travel: Redux DevTools são valiosos para estados complexos
- Persistência: redux-persist para cache offline de dados do caso
- Middleware: fácil adicionar logging, analytics, rastreamento de erros
- Familiaridade da equipe: padrão amplamente adotado, documentação extensa
- RTK Query: caching de API embutido, estados de loading, updates otimistas

**Alternativas Consideradas**:

1. **React Context API**: Rejeitado por problemas de re-render com estado profundo, ausência de DevTools e middleware.
2. **Zustand**: Leve e atraente, porém com menos tooling, comunidade menor e equipe menos familiarizada.
3. **Jotai/Recoil**: Abordagem baseada em átomos interessante, mas experimental e com ecossistema menos maduro.
4. **MobX**: Rejeitado pelo learning curve e por atualizações menos explícitas (debug difícil).

**Trade-offs**:

- Redux tem mais boilerplate que soluções simples (mitigado pelo RTK)
- Bundle maior (~40 KB) frente a Context (0 KB) ou Zustand (~3 KB)
- Curva de aprendizado para devs juniores

**Impacto**:

- Capítulo 08 (Técnico) – Arquitetura Redux Toolkit, estrutura de slices, integração RTK Query
- Capítulo 08 (Técnico) – Estratégia de persistência (redux-persist)
- Capítulo 11 (Testes) – Testes unitários de slices e mocks do RTK Query

**Decisões Relacionadas**: DEC-2024-006, DEC-2024-012

---

## 3. Decisões de Conteúdo e Design Narrativo

### DEC-2024-012: 2-8 Suspeitos por Caso (Não Fixos)

**Data**: novembro de 2024  
**Categoria**: Conteúdo  
**Status**: Aprovada  
**Decisão**: Casos devem conter entre 2 e 8 suspeitos, variando conforme a dificuldade, e não um número fixo.

**Contexto**: Balancear complexidade/realismo com carga cognitiva do jogador. Casos reais variam no número de suspeitos.

**Justificativa**:

- Casos Easy: 2-3 suspeitos (diferenciação clara, estrutura simples)
- Casos Medium: 4-5 suspeitos (complexidade moderada, múltiplas teorias viáveis)
- Casos Hard: 6-7 suspeitos (alta complexidade, diversos red herrings)
- Casos Expert: 7-8 suspeitos (complexo máximo, relações intrincadas)
- Contagem variável soa mais realista (nem todo caso tem 5 suspeitos)
- Garante flexibilidade criativa aos roteiristas

**Alternativas Consideradas**:

1. **Fixar 5 suspeitos**: Rejeitado por ser formulaico e limitar criatividade.
2. **Sem mínimo/máximo**: Rejeitado porque 1 suspeito trivializa e 10+ sobrecarrega.
3. **Suspeitos desbloqueáveis**: Rejeitado por contrariar o princípio "informação completa desde o início".

**Trade-offs**:

- Layouts de UI variáveis (galeria precisa acomodar 2-8 suspeitos)
- Balanceamento de dificuldade mais complexo (ajustar a cada contagem)

**Impacto**:

- Capítulo 04 (Estrutura de Caso) – Diretrizes de contagem por dificuldade
- Capítulo 09 (Esquema de Dados) – Regra de validação: 2 ≤ suspects.length ≤ 8
- Capítulo 10 (Pipeline de Conteúdo) – Guias para escolher número de suspeitos
- Capítulo 11 (Testes) – Validação de limites mínimo/máximo

**Decisões Relacionadas**: DEC-2024-013, DEC-2024-014

---

### DEC-2024-013: Quatro Níveis de Dificuldade (Easy, Medium, Hard, Expert)

**Data**: novembro de 2024  
**Categoria**: Conteúdo  
**Status**: Aprovada  
**Decisão**: Implementar quatro níveis de dificuldade, e não três ou cinco.

**Contexto**: Garantir progressão clara que contemple novatos e veteranos sem opções de menos ou demais.

**Justificativa**:

- **Easy**: Tier de onboarding, 2-3 suspeitos, pistas claras (meta 50-60% acerto)
- **Medium**: Tier padrão, 4-5 suspeitos, complexidade moderada (30-40% acerto)
- **Hard**: Desafio, 6-7 suspeitos, red herrings (15-25% acerto)
- **Expert**: Prestígio, 7-8 suspeitos, pistas sutis (5-15% acerto)
- Quatro tiers formam curva natural (iniciante, intermediário, avançado, especialista)
- Alinha-se a progressões comuns em jogos (rankings competitivos)

**Alternativas Consideradas**:

1. **Três tiers** (Easy/Medium/Hard): Rejeitado por faltar granularidade e tier extremo.
2. **Cinco tiers** (Tutorial + quatro níveis): Rejeitado porque tutorial é onboarding separado, não dificuldade.
3. **Dificuldade dinâmica**: Rejeitado por contrariar filosofia de "caso autêntico" (caso não muda conforme habilidade).

**Trade-offs**:

- Produção precisa entregar conteúdo para os quatro tiers (carga maior)
- Easy pode parecer simples demais aos experientes (necessário para onboarding)
- Expert será difícil para a maioria (aceitável, atende nicho)

**Impacto**:

- Capítulo 04 (Estrutura de Caso) – Definições e calibragem de dificuldade
- Capítulo 06 (Progressão) – XP por dificuldade (Easy 150, Medium 300, Hard 600, Expert 1200)
- Capítulo 10 (Pipeline de Conteúdo) – Checklist de calibragem, testes cegos
- Capítulo 12 (Roadmap) – MVP inclui ao menos 2 casos por tier, exceto Expert (1 caso)

**Decisões Relacionadas**: DEC-2024-012, DEC-2024-015

---

### DEC-2024-014: case.json como Fonte Única de Verdade

**Data**: novembro de 2024  
**Categoria**: Conteúdo  
**Status**: Aprovada  
**Decisão**: Todo o conteúdo do caso (metadata, vítima, crime, suspeitos, evidências, documentos, perícias, linha do tempo, solução) é definido em um único case.json, sem divisão em múltiplos arquivos ou tabelas.

**Contexto**: Estrutura clara para roteiristas, designers e devs, suportando versionamento, validação e referências de assets.

**Justificativa**:

- Arquivo único facilita versionamento (diffs, branching, merge)
- Roteiristas visualizam toda a estrutura em um só lugar
- Validação via JSON Schema garante consistência
- Assets referenciados por paths relativos (mantém JSON enxuto)
- Backend carrega o caso com uma query (coluna JSONB)
- Viabiliza localização (case-fr.json, case-es.json separados)

**Alternativas Consideradas**:

1. **JSONs separados por seção**: Rejeitado por fragmentar e dificultar validação cruzada.
2. **Entrada direta no banco**: Rejeitado por acoplar conteúdo ao banco, sem versionamento e revisão complexa.
3. **YAML no lugar de JSON**: Rejeitado porque JSON tem tooling e validação melhores, além de parsing nativo.
4. **XML**: Rejeitado por verbosidade e legibilidade reduzida.

**Trade-offs**:

- case.json grandes (500-2000 linhas) podem ser trabalhosos (mitigado por folding e hints de schema)
- Schemas precisam ser bem definidos no início (mudanças futuras são mais difíceis)
- Possíveis conflitos de merge (mitigado por seções claras)

**Impacto**:

- Capítulo 04 (Estrutura de Caso) – Documentação completa do schema
- Capítulo 09 (Esquema de Dados) – Estrutura, regras de validação e JSON Schema
- Capítulo 10 (Pipeline de Conteúdo) – Fase de montagem do case.json, ferramenta case-validator.js
- Capítulo 11 (Testes) – Validação do case.json e conformidade com o schema

**Decisões Relacionadas**: DEC-2024-009, DEC-2024-015

---

### DEC-2024-015: Lançamento do MVP com 9 Casos (Não 5 ou 15)

**Data**: novembro de 2024  
**Categoria**: Negócios  
**Status**: Aprovada  
**Decisão**: Lançar o MVP com 9 casos distribuídos por dificuldade: 3 Easy, 3 Medium, 2 Hard, 1 Expert.

**Contexto**: Equilibrar volume de conteúdo convincente com cronograma e orçamento.

**Justificativa**:

- **9 casos = 20-45 horas** para jogador médio (2-5h por caso)
- Competitivo com jogos similares (Her Story ~7 vídeos, Obra Dinn ~12 "casos")
- Variedade suficiente para mostrar diferentes tipos de crime/motivos/métodos
- Sustenta o sistema de progressão (patentes via 9 casos)
- Produção viável em 12 meses (~1 caso/mês nas fases 3-4)
- Distribuição garante onboarding (3 Easy) e conteúdo aspiracional (1 Expert)

**Alternativas Consideradas**:

1. **5 casos**: Rejeitado por entregar pouco conteúdo e valor percebido baixo (US$ 19,99 por 10-15h).
2. **15 casos**: Rejeitado por inviabilizar timeline (adiaria MVP em 6+ meses) e extrapolar orçamento.
3. **Formato episódico** (3 casos por episódio): Rejeitado por fragmentar a base de jogadores e complicar precificação.

**Trade-offs**:

- 9 casos pode soar "curto" para hardcore (mitigado com pipeline pós-lançamento)
- Distribuição 3-3-2-1 é assimétrica, mas necessária para cumprir cronograma

**Impacto**:

- Capítulo 10 (Pipeline de Conteúdo) – Orçamento de produção US$ 50k-110k
- Capítulo 12 (Roadmap) – MVP entrega 9 casos em 12 meses
- Capítulo 12 (Roadmap) – Pós-lançamento: 1-2 casos/mês (meta Ano 1: 19 casos)
- Negócios – Precificação premium (US$ 19,99-29,99) justificada por 20-45 horas + updates

**Decisões Relacionadas**: DEC-2024-013, DEC-2024-016

---

## 4. Decisões de UI/UX

### DEC-2024-016: Metáfora de SO Desktop (Não UI Tradicional)

**Data**: novembro de 2024  
**Categoria**: UI/UX  
**Status**: Aprovada  
**Decisão**: UI com metáfora de sistema operacional desktop, com janelas (Case Files, Forensics Lab, Email), e não interface tradicional de jogo/menu HUD.

**Contexto**: Definir identidade visual reforçando o fantasy "você é um detetive em sua mesa".

**Justificativa**:

- Metáfora imediatamente familiar (todos usam computadores)
- Suporta multitarefa (visualizar documento e notas simultaneamente)
- Janelas redimensionáveis/moveis/minimizáveis = agência do jogador
- Reforça pilar de autenticidade (detetives usam computadores)
- Diferencia do UI tradicional de games
- Permite expansão futura (novos "apps")

**Alternativas Consideradas**:

1. **UI tradicional** (painéis fixos/HUD): Rejeitado por parecer "game-like" demais e limitar flexibilidade.
2. **Metáfora de mesa física** (Papers, Please): Rejeitado por ser literal demais e limitar recursos digitais (ctrl+F, zoom).
3. **Dashboard/Kanban**: Rejeitado por ser menos intuitivo que um SO familiar.

**Trade-offs**:

- Metáfora desktop em mobile é complexa (adiada para Ano 2)
- Implementação de gerenciador de janelas (sobreposição, z-index, foco)
- Necessidade de construir chrome customizado (title bar, botões)

**Impacto**:

- Capítulo 07 (UI) – Sistema de design do desktop (gerenciador, dock, apps)
- Capítulo 08 (Técnico) – Estado de janelas no frontend (slice ui do Redux)
- Capítulo 11 (Testes) – Testes de interação (arrastar, redimensionar, foco)
- Futuro – Facilidade para adicionar novos "apps" (Configurações, Perfil, Comunidade)

**Decisões Relacionadas**: DEC-2024-017

---

### DEC-2024-017: PDF.js para Visualização de Documentos (Não Renderizador Custom)

**Data**: novembro de 2024  
**Categoria**: UI/UX, Técnica  
**Status**: Aprovada  
**Decisão**: Usar PDF.js (Mozilla) para renderizar documentos PDF, e não renderizador custom ou abordagem baseada em imagens.

**Contexto**: Necessidade de exibir PDFs com zoom, busca e acessibilidade.

**Justificativa**:

- PDF.js é padrão de mercado, mantido pela Mozilla, amplamente testado
- Suporta recursos de PDF (fontes, imagens, gráficos vetoriais)
- Text layer permite Ctrl+F e leitores de tela
- Controle de zoom/navegação prontos
- Renderização client-side (sem processamento no servidor)
- Open source (licença MIT) e mantido ativamente

**Alternativas Consideradas**:

1. **Converter PDFs em imagens**: Rejeitado por remover camada de texto (sem busca/acessibilidade) e aumentar tamanho.
2. **Iframe com viewer do navegador**: Rejeitado por inconsistência entre browsers, falta de customização e riscos de segurança.
3. **Renderização custom via canvas**: Rejeitado por reinventar a roda e ter alto custo de desenvolvimento.
4. **Wrapper React-PDF**: Considerado, mas adiciona camada extra; foi escolhido usar PDF.js diretamente.

**Trade-offs**:

- Bundle do PDF.js (~500 KB) aumenta o load inicial
- PDFs complexos podem exigir CPU
- Viewer precisa ser estilizado para o tema do jogo

**Impacto**:

- Capítulo 07 (UI) – Design do Document Viewer integrado ao PDF.js
- Capítulo 08 (Técnico) – Integração e configuração do PDF.js
- Capítulo 10 (Pipeline de Conteúdo) – Templates de documentos voltados para exportação em PDF
- Capítulo 11 (Testes) – Testes de renderização e busca

**Decisões Relacionadas**: DEC-2024-016, DEC-2024-018

---

### DEC-2024-018: Auto-save de Notas a Cada 30 Segundos (Não Save Manual)

**Data**: novembro de 2024  
**Categoria**: UI/UX  
**Status**: Aprovada  
**Decisão**: Salvar notas automaticamente no servidor a cada 30 segundos; sem botão de save manual.

**Contexto**: Evitar perda de dados com mínimo de requisições. Equilibrar conveniência e carga no backend.

**Justificativa**:

- Evita frustração com notas perdidas (crash, fechamento acidental)
- Intervalo de 30s equilibra frescor e volume de requisições (máx. 120/h por usuário)
- Usuários modernos esperam autosave (Google Docs, Notion)
- Remove carga cognitiva (sem "lembrei de salvar?")
- Indicador visual ("Último salvamento há Xs") oferece feedback

**Alternativas Consideradas**:

1. **Botão de save manual**: Rejeitado por ser antiquado; usuários esquecem e perdem dados.
2. **Salvar a cada tecla**: Rejeitado pelo excesso de chamadas e impacto em conexões lentas.
3. **Salvar apenas on blur/unmount**: Rejeitado por ser esparso; risco de perda em crash.
4. **Somente LocalStorage**: Rejeitado por não sincronizar entre dispositivos e ser perdido ao limpar cache.

**Trade-offs**:

- Até 30 segundos de notas podem ser perdidos (risco aceitável)
- Backend precisa lidar com atualizações concorrentes (controle de concorrência otimista)

**Impacto**:

- Capítulo 03 (Mecânicas) – Comportamento do sistema de notas
- Capítulo 07 (UI) – Aplicativo de notas mostra "Último salvamento"
- Capítulo 08 (Técnico) – Endpoint `/api/cases/{id}/notes` com debounce
- Capítulo 09 (Esquema de Dados) – CaseSession armazena notas (JSON) e timestamp
- Capítulo 11 (Testes) – Testes de autosave (debounce e concorrência)

**Decisões Relacionadas**: DEC-2024-011

---

## 5. Decisões de Negócios e Monetização

### DEC-2024-019: Modelo Premium (Não Free-to-Play)

**Data**: novembro de 2024  
**Categoria**: Negócios  
**Status**: Aprovada  
**Decisão**: CaseZero adotará compra premium (US$ 19,99-29,99) sem anúncios, DLC ou microtransações, e não modelo free-to-play.

**Contexto**: Definir modelo de receita alinhado à filosofia do jogo e ao público-alvo.

**Justificativa**:

- Alinha-se ao público (adultos 18+ dispostos a pagar por conteúdo de qualidade)
- Mantém paridade com jogos comparáveis (Obra Dinn US$ 19,99, Papers Please US$ 9,99)
- Ausência de anúncios preserva imersão
- Sem pressão de DLC: experiência completa na compra
- Atualizações pós-lançamento gratuitas geram boa vontade e comunidade
- Implementação mais simples (sem IAP, ad networks, complexidade de pagamentos)
- Filosofia indie: "pague uma vez, tenha para sempre"

**Alternativas Consideradas**:

1. **Free-to-play com ads**: Rejeitado pois anúncios quebram imersão e afastam o público-alvo.
2. **Freemium** (3 casos grátis, pague pelo resto): Rejeitado por dividir base e reduzir valor percebido.
3. **Assinatura** (US$ 4,99/mês): Rejeitado por exigir fluxo constante de conteúdo; público prefere posse.
4. **Case packs em DLC**: Considerado para Ano 2+, mas arrisca fragmentar a comunidade.

**Trade-offs**:

- Barreiras de entrada (pagamento inicial)
- Sem camada gratuita viral
- Receita concentrada no lançamento (sem recorrência)
- Necessidade de entregar alto valor no dia 1 (9 casos no mínimo)

**Impacto**:

- Capítulo 12 (Roadmap) – Modelo de lançamento e precificação
- Capítulo 12 (Roadmap) – Atualizações gratuitas no Ano 1
- Negócios – Marketing destaca "sem ads, sem DLC, compra definitiva"
- Ano 2+ – Reavaliar DLC/expansões após 20+ casos

**Decisões Relacionadas**: DEC-2024-015, DEC-2024-020

---

### DEC-2024-020: MVP Apenas em Inglês, Localização no Ano 1

**Data**: novembro de 2024  
**Categoria**: Negócios, Conteúdo  
**Status**: Aprovada  
**Decisão**: MVP lança apenas em inglês; localizações em francês, espanhol, português e alemão entram no Ano 1 (meses 7-12).

**Contexto**: Equilibrar alcance internacional com timeline e orçamento do MVP. Localização é cara e demorada.

**Justificativa**:

- Inglês-first valida product-market fit antes de investir em localização
- Público-alvo inicial (EUA, UK, Canadá, Austrália) majoritariamente anglófono
- Custo por idioma: US$ 10k-15k (tradução, QA, adaptação cultural)
- Timeline de localização: 2-3 meses por idioma
- MVP não comporta 4+ idiomas sem atrasar 3-6 meses
- Indicadores pós-lançamento orientam priorização

**Alternativas Consideradas**:

1. **MVP multilíngue**: Rejeitado por dobrar/triplicar timeline/orçamento.
2. **Tradução automática**: Rejeitado por baixa qualidade e impacto negativo na experiência.
3. **Tradução comunitária**: Considerado para Ano 2+, depende de ferramentas e controle de qualidade.

**Trade-offs**:

- Exclui não anglófonos no MVP (aceitável para validação)
- Concorrentes localizados podem ter vantagem internacional
- Conteúdo textual denso implica custo alto de localização

**Impacto**:

- Capítulo 10 (Pipeline de Conteúdo) – Estratégia, timeline e orçamento de localização
- Capítulo 12 (Roadmap) – Francês (meses 7-9), Espanhol/Português/Alemão (meses 10-12)
- Negócios – Foco inicial em mercados anglófonos, expansão no Ano 1
- Infraestrutura – i18n preparado (react-i18next), mas apenas en-US no lançamento

**Decisões Relacionadas**: DEC-2024-015, DEC-2024-019

---

## 6. Decisões de Gerenciamento de Escopo

### DEC-2024-021: Sem Multiplayer ou Co-op no Lançamento

**Data**: novembro de 2024  
**Categoria**: Escopo  
**Status**: Aprovada  
**Decisão**: MVP será apenas single-player. Recursos multiplayer/co-op (resolução compartilhada, notas colaborativas) ficam para Ano 3+.

**Contexto**: Multiplayer adiciona muita complexidade técnica (sync em tempo real, resolução de conflitos, matchmaking, anti-cheat). Prioridade é validar o single-player.

**Justificativa**:

- Experiência principal é single-player: investigação é pessoal, cuidadosa, no próprio ritmo
- Multiplayer exige infraestrutura em tempo real (WebSockets, SignalR), sincronização de estado
- MVP não precisa de multiplayer para validar proposta
- Jogos comparáveis são single-player (Papers Please, Obra Dinn, Her Story)
- Co-op é "nice to have", não essencial aos pilares

**Alternativas Consideradas**:

1. **Co-op assíncrono** (compartilhar notas): Adiado para Ano 2 como parte das features comunitárias.
2. **Leaderboards competitivos**: Considerado para Ano 1, mas conflita com pilar "sem pressão de tempo".
3. **Modo espectador**: Interessante, porém nicho; adiado indefinidamente.

**Trade-offs**:

- Perde tendência social (Among Us, co-op)
- Menos potencial de viralização (sem convite para amigos)
- Reduz atratividade para quem prefere multiplayer

**Impacto**:

- Capítulo 01 (Conceito) – Ênfase no single-player
- Capítulo 08 (Técnico) – Sem infraestrutura em tempo real (WebSockets)
- Capítulo 12 (Roadmap) – Co-op movido para visão Ano 3+ (experimental)

**Decisões Relacionadas**: DEC-2024-022

---

### DEC-2024-022: Sem Conteúdo Gerado por Usuário (UGC) no Lançamento

**Data**: novembro de 2024  
**Categoria**: Escopo  
**Status**: Aprovada  
**Decisão**: MVP não inclui editor de casos nem ferramentas UGC. UGC é adiado para Ano 2 T4.

**Contexto**: Editor de casos é complexo (builder GUI, validação, fluxo de publicação). Prioridade é validar o jogo base antes de investir em plataforma UGC.

**Justificativa**:

- Criação de casos exige conhecimento aprofundado (escrita de mistério, design de evidências)
- Editor tem complexidade similar a criar outro app
- Controle de qualidade é desafiador (moderação/curadoria)
- MVP não depende de UGC para validar proposta
- Casos curados pelos devs garantem qualidade no lançamento

**Alternativas Consideradas**:

1. **Editor simples de JSON**: Ainda exige domínio do schema, pouco acessível.
2. **Criador baseado em templates**: Limita criatividade e gera casos formulaicos.
3. **Suporte a mods da comunidade**: Interessante, porém arriscado (cheating, conteúdo inadequado).

**Trade-offs**:

- Gargalo de produção continua interno (1-2 casos/mês)
- Perde oportunidade de engajamento comunitário e conteúdo infinito
- Limita longevidade (casos finitos criados pelos devs)

**Impacto**:

- Capítulo 12 (Roadmap) – Editor de casos no Ano 2 T4 (6+ meses de esforço)
- Capítulo 12 (Roadmap) – Visão Ano 3+ inclui plataforma UGC (votação, revenue share)
- Negócios – Produção de conteúdo permanece interna nos Anos 1-2

**Decisões Relacionadas**: DEC-2024-015, DEC-2024-023

---

### DEC-2024-023: Sem App Mobile no Lançamento (Somente PWA)

**Data**: novembro de 2024  
**Categoria**: Escopo, Técnica  
**Status**: Aprovada  
**Decisão**: MVP mira navegadores desktop/tablet como PWA; apps nativos iOS/Android ficam para Ano 2 T3.

**Contexto**: Desenvolvimento mobile requer codebases separadas (Swift, Kotlin) ou React Native. Prioridade é validar experiência desktop.

**Justificativa**:

- Leitura de documentos é melhor em telas grandes (desktop/tablet)
- Telas pequenas dificultam PDF + anotações
- Lojas mobile adicionam atrito (aprovação, 30% de taxa, atualizações mais lentas)
- PWA oferece "instalar na tela inicial" sem passar pela loja
- MVP está focado em usuários desktop inicialmente

**Alternativas Consideradas**:

1. **React Native**: Adiado para Ano 2 como app nativo real.
2. **Design responsivo somente**: Incluso no MVP (suporte a tablet), mas UX em celular fica comprometida.
3. **Capacitor/Ionic**: Wrapper do app web, considerado para solução intermediária no Ano 2.

**Trade-offs**:

- Perde mercado mobile (maior segmento de games)
- Jogadores esperam apps nativos (PWA menos conhecido)
- Visibilidade em App Store seria um canal de marketing inexistente no MVP

**Impacto**:

- Capítulo 08 (Técnico) – Implementação PWA (Service Worker, manifest.json, offline)
- Capítulo 12 (Roadmap) – Otimização mobile (UI responsiva, gestos) no Ano 2 T3
- Capítulo 12 (Roadmap) – Avaliação de apps nativos no Ano 3+
- Negócios – Foco em mercado desktop/tablet no lançamento

**Decisões Relacionadas**: DEC-2024-006, DEC-2024-016

---

## 7. Decisões a Futuro e Reavaliações

### DEC-2024-024: Reavaliar Bônus de XP (Métricas do Ano 1)

**Data**: novembro de 2024  
**Categoria**: Jogabilidade  
**Status**: Aprovada – Revisitar Pós-Lançamento  
**Decisão**: Lançar com bônus de XP por primeira tentativa (+50%), eficiência de tempo (+10-20%) e sem dicas (+20%). Reavaliar conforme métricas do Ano 1.

**Contexto**: Balancear recompensas extrínsecas (XP) com motivação intrínseca (satisfação). Estrutura atual é estimativa informada.

**Justificativa**:

- Bônus incentivam "jogo ideal" sem serem punitivos
- Primeiro envio premia investigação cuidadosa
- Bônus de eficiência é leve o suficiente para não ferir o pilar "sem pressão"
- Bônus sem dicas reforça autossuficiência

**Critérios de Revisita**:

- Se >60% dos jogadores usam as 3 tentativas: considerar aumentar bônus da primeira tentativa ou melhorar feedback
- Se <10% conquista bônus de tempo: avaliar remover (baixo impacto)
- Se houver reclamações de pressão: remover ou tornar opcional (líderes/apenas ranking)

**Impacto**:

- Capítulo 06 (Progressão) – Estrutura de bônus de XP documentada
- Capítulo 12 (Roadmap) – Analytics Ano 1: distribuição de tentativas, tempo, uso de dicas
- Pós-lançamento – Possível patch de rebalanceamento de XP

**Decisões Relacionadas**: DEC-2024-003, DEC-2024-005

---

### DEC-2024-025: Plano de Encerramento (Se Necessário)

**Data**: novembro de 2024  
**Categoria**: Negócios  
**Status**: Aprovada  
**Decisão**: Caso CaseZero não alcance sustentabilidade (<5k usuários no mês 6, <US$ 50k de receita no Ano 1), executar desligamento responsável com aviso de 6 meses, reembolsos, versão DRM-free e open source do editor.

**Contexto**: Planejamento responsável para cenário de insucesso. Abordagem "player-first" para encerramento.

**Justificativa**:

- Transparência preserva confiança mesmo em caso de falha
- Política de reembolso demonstra boa-fé (até 1 ano após compra)
- Lançar versão DRM-free garante acesso perpétuo offline
- Editor open source permite continuidade pela comunidade
- Aviso de 6 meses dá tempo para jogadores concluírem conteúdo

**Etapas de Encerramento**:

1. Anúncio público (blog, email, redes)
2. Janela de reembolso (via processadora de pagamentos)
3. Update final liberando todos os casos e habilitando modo offline
4. Editor open source no GitHub (licença MIT)
5. Arquivamento de documentação, tutoriais, guias de escrita
6. Desativar necessidade de autenticação (todos os casos jogáveis offline)
7. Handoff da comunidade (transferência do Discord para moderadores)

**Impacto**:

- Capítulo 12 (Roadmap) – Plano de encerramento documentado
- Negócios – Estratégia de saída ética protege os jogadores
- Comunidade – Comunicação honesta mantém boa vontade mesmo no insucesso

---

## Apêndice: Categorias de Decisão

- **Jogabilidade**: Mecânicas centrais, dificuldade, progressão, ações do jogador
- **Técnica**: Arquitetura, infraestrutura, frameworks, bibliotecas
- **Conteúdo**: Casos, narrativa, assets, localização
- **UI/UX**: Design de interface, interações, estilo visual
- **Negócios**: Monetização, precificação, mercados, estratégia de lançamento
- **Escopo**: Features incluídas/excluídas, timeline, recursos

---

## Apêndice: Status das Decisões

- **Aprovada**: Decisão tomada, equipe alinhada, pronta para implementação
- **Implementada**: Decisão executada e presente no código/produção
- **Reavaliação**: Decisão em revisão por novas informações
- **Depreciada**: Decisão revogada e substituída por nova orientação

---

## Histórico de Revisões

| Versão | Data | Mudanças | Autor |
|--------|------|----------|-------|
| 1.0 | 14/11/2024 | Registro inicial com 25 decisões-chave | Assistente de IA |

---

**Status do Documento:** Completo  
**Última Atualização:** 14 de novembro de 2024  
**Próxima Revisão:** Mensal durante o desenvolvimento; trimestral após o lançamento
