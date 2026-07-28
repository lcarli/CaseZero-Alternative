# Capítulo 02 - Jogabilidade

**Documento de Design de Jogo - CaseZero v3.0**  
**Última atualização:** 13 de novembro de 2025  
**Status:** ✅ Completo

---

## 2.1 Loop Central de Jogabilidade

O ciclo fundamental que os jogadores repetem durante toda a investigação:

```
┌─────────────────────────────────────────────────────────────┐
│                 LOOP CENTRAL DE JOGABILIDADE                │
└─────────────────────────────────────────────────────────────┘

    ┌──────────────┐
    │ LER BRIEFING │  ← Ponto de entrada (5-10 min)
    └──────┬───────┘
           │
           ▼
    ┌──────────────────┐
    │ EXPLORAR ARQUIVO │  ← Fase principal de investigação (60-80% do tempo)
    │ DO CASO          │     - Ler documentos
    └──────┬───────────┘     - Examinar fotos de evidências
           │                 - Revisar perfis de suspeitos
           ▼                 - Estudar a linha do tempo
    ┌──────────────────┐
    │ SOLICITAR        │  ← Momento de decisão estratégica (5-10 min)
    │ PERÍCIAS         │     - Escolher quais evidências analisar
    └──────┬───────────┘     - Selecionar tipo de perícia
           │                 - Enviar requisição
           ▼
    ┌──────────────────┐
    │ AGUARDAR LAUDOS  │  ← Mecânica em tempo real (horas/dias)
    │                  │     - Continuar lendo outros materiais
    └──────┬───────────┘     - Investigar outros casos
           │                 - Voltar quando quiser
           ▼
    ┌──────────────────┐
    │ ANALISAR LAUDOS  │  ← Fase de integração (10-20 min)
    │                  │     - Ler resultados forenses
    └──────┬───────────┘     - Conectar com outras evidências
           │                 - Atualizar teoria
           │
           ▼
    ┌──────────────────┐
    │ FORMAR TEORIA    │  ← Síntese (contínua)
    │                  │     - Quem fez?
    └──────┬───────────┘     - Por quê? (motivo)
           │                 - Como? (método)
           │                 - Qual prova sustenta?
           ▼
    ┌──────────────────┐
    │ ENVIAR SOLUÇÃO   │  ← Decisão final (5-10 min)
    │                  │     - Selecionar culpado
    └──────┬───────────┘     - Escrever explicação
           │                 - Enviar (tentativas limitadas)
           │
           ▼
    ┌──────────────────┐
    │ RECEBER FEEDBACK │  ← Resolução (2-5 min)
    │                  │     - Correto/Incorrreto
    └──────┬───────────┘     - Ver solução oficial
           │                 - Ganhar crédito de promoção e progresso de patente
           │
           ▼
    [Próximo caso ou nova tentativa]
```

### Duração do Loop
- **Passagem única:** 2-8 horas (espalhadas em múltiplas sessões)
- **Sessão típica:** 30-90 minutos
- **Sessão mínima:** 15 minutos (ler um ou dois documentos)
- **Sessão máxima:** 3+ horas (mergulho profundo na investigação)

---

## 2.2 Verbos do Jogador (Ações)

O que o jogador **pode fazer** em CaseZero?

### Verbos Primários (Jogabilidade Central)

**LER**
- Documentos (relatórios policiais, depoimentos, cartas)
- Laudos forenses (quando disponíveis)
- Perfis de suspeitos
- Entradas da linha do tempo
- Briefing por e-mail

**EXAMINAR**
- Fotografias da cena do crime
- Fotos de evidências (múltiplos ângulos)
- Foto da vítima
- Fotos dos suspeitos
- Imagens dos locais

**SOLICITAR**
- Análises forenses (DNA, balística, impressões digitais, toxicologia)
- Múltiplas solicitações em paralelo
- Acompanhar status de requisições pendentes

**ANALISAR**
- Comparar informações de fontes diferentes
- Identificar contradições
- Conectar evidências a suspeitos
- Construir linha do tempo dos eventos

**ANOTAR**
- Registrar observações
- Rastrear teorias
- Listar conexões
- Criar log pessoal da investigação

**ENVIAR**
- Solução (quem, por quê, como, prova)
- Explicação do raciocínio
- Resposta final (tentativas limitadas)

### Verbos Secundários (Ações de Suporte)

**NAVEGAR**
- Alternar entre aplicativos
- Abrir/fechar janelas
- Percorrer diretórios de arquivos
- Buscar dentro dos materiais

**ORGANIZAR**
- Arranjar janelas na área de trabalho
- Favoritar documentos importantes
- Sinalizar evidências-chave

**ESPERAR**
- Pelos resultados forenses
- Entre sessões de investigação

---

## 2.3 Fluxo de Sessão

Como uma sessão típica se desenrola:

### Primeira Sessão (Caso Tutorial)
**Duração:** 20-30 minutos

```
0:00 - Tela tutorial 1: "Bem-vindo à Divisão de Casos Arquivados"
0:01 - Tela tutorial 2: "Aqui está sua área de trabalho - E-mail, Arquivos do Caso, Laboratório Forense"
0:02 - Jogador abre o aplicativo de E-mail
0:03 - Lê briefing tutorial (caso simplificado)
0:05 - Prompt tutorial: "Abra Arquivos do Caso para ver as evidências"
0:06 - Jogador explora documentos (2 documentos simples)
0:10 - Prompt tutorial: "Envie sua teoria no app de Submissão"
0:12 - Jogador envia solução
0:13 - Correto! Tutorial concluído, primeiro caso desbloqueado
```

### Sessão Inicial Típica (Primeiro Caso Real)
**Duração:** 45-60 minutos

```
0:00 - Jogador abre E-mail, lê briefing do caso
0:05 - Abre Arquivos do Caso, vê 15 documentos disponíveis
0:07 - Começa pelo Relatório Policial (escolha mais óbvia)
0:15 - Lê o relatório inteiro, faz anotações
0:20 - Abre o primeiro depoimento de testemunha
0:28 - Nota contradição com o relatório policial
0:30 - Abre o segundo depoimento de testemunha
0:35 - Abre aba de Evidências, examina fotos da cena do crime
0:40 - Abre perfis de suspeitos, lê sobre 3 suspeitos
0:50 - Acha que identificou o culpado, mas precisa de prova
0:52 - Abre Laboratório Forense, solicita análise de DNA
0:55 - Vê "Resultados em 24 horas"
0:57 - Faz anotações sobre a teoria atual
1:00 - Fecha o jogo, retornará amanhã
```

### Sessão em Meio à Investigação
**Duração:** 30-45 minutos

```
0:00 - Jogador retorna, confere Laboratório Forense
0:01 - Resultados de DNA prontos! Abre o laudo
0:05 - Lê achados, conecta a um suspeito
0:10 - Relê a declaração de álibi do suspeito
0:15 - Abre a linha do tempo, confere horários
0:20 - Abre o aplicativo de Notas, atualiza teoria
0:25 - Solicita análise balística (quer mais prova)
0:27 - Reexamina fotos de evidências com novo contexto
0:35 - Está confiante na solução, mas aguardará balística
0:40 - Faz anotações finais
0:45 - Encerra a sessão
```

### Sessão Final (Envio da Solução)
**Duração:** 30-45 minutos

```
0:00 - Jogador retorna, resultados de balística disponíveis
0:05 - Lê o laudo, confirma a teoria
0:10 - Revisa todas as evidências principais mais uma vez
0:20 - Abre o app de Submissão
0:22 - Seleciona o culpado no menu
0:25 - Escreve explicação detalhada (200-300 palavras)
0:35 - Revisa o texto
0:38 - Clica em Enviar (tensão!)
0:40 - Tela de feedback: "Correto!" + passo a passo da solução
0:42 - Resultado do caso é registrado, progresso de promoção avança
0:45 - Retorna ao painel, vê próximo caso desbloqueado
```

---

## 2.4 Progressão de Dificuldade

Como o desafio escala ao longo da experiência:

**Nota de implementação:** O jogo em produção usa sete níveis canônicos compartilhados entre dificuldade do caso e patente de detetive: `Rookie`, `Detective`, `Detective2`, `Sergeant`, `Lieutenant`, `Captain`, `Commander`. Os blocos Fácil/Médio/Difícil/Especialista abaixo são apenas um atalho de design, não o enum canônico exibido no jogo.

### Casos Fáceis
**Público-alvo:** Jogadores iniciantes, investigadores casuais  
**Características:**
- 2-3 suspeitos (um culpado óbvio)
- 8-12 documentos no total
- Motivo e oportunidade claros
- Poucas pistas falsas
- Linha do tempo direta
- 3-5 evidências
- 2-3 perícias necessárias
- 2-4 horas para resolver

**Exemplo:** "Assassinato no Escritório" - CFO morto, sócio ressentido é suspeito evidente, DNA confirma

### Casos Médios
**Público-alvo:** Jogadores experientes (já solucionaram 2-3 casos)  
**Características:**
- 4-5 suspeitos (múltiplas teorias viáveis)
- 12-18 documentos
- Motivos diversos
- Algumas pistas falsas e informações enganosas
- Linha do tempo complexa com lacunas
- 6-8 evidências
- 3-5 perícias necessárias
- 4-6 horas para resolver

**Exemplo:** "Roubo no Armazém" - Vários suspeitos com álibis, é preciso descobrir quem mente

### Casos Difíceis
**Público-alvo:** Veteranos (5+ casos resolvidos)  
**Características:**
- 6+ suspeitos (todos escondem algo)
- 18-25 documentos
- Motivos ocultos (não óbvios)
- Pistas falsas significativas
- Reconstrução de linha do tempo obrigatória
- 10-15 evidências
- 5-8 perícias necessárias
- 6-8 horas para resolver

**Exemplo:** "Caso Arquivado Reaberto" - Homicídio de 10 anos, depoimentos conflitantes, nova tecnologia de DNA revela a verdade

### Casos de Especialista
**Público-alvo:** Mestres (10+ casos resolvidos, alta taxa de acerto)  
**Características:**
- 8+ suspeitos (possível conspiração)
- 25+ documentos
- Pistas extremamente sutis
- Desinformação deliberada
- Múltiplas cenas de crime
- 15+ evidências
- 8-12 perícias necessárias
- 8-12 horas para resolver

**Exemplo:** "Padrão de Serial Killer" - Conectar 3 assassinatos aparentemente sem relação via perícia

---

## 2.5 Condições de Vitória

Como o jogador "vence" um caso?

### Vitória Primária: Solução Correta
**Requisitos:**
1. ✅ Identificar o culpado correto
2. ✅ Apresentar explicação coerente
3. ✅ Referenciar evidências-chave na explicação
4. ✅ Enviar dentro do limite de tentativas (geralmente 3)

**Recompensas:**
- Conta como um caso corretamente resolvido para promoção quando a solução enviada está correta
- Os limiares de promoção avançam em 3, 8, 16, 28, 44 e 65 casos corretamente resolvidos de forma cumulativa (Rookie começa em 0)
- Caso marcado como "Resolvido" no histórico
- Passo a passo da solução liberado
- Próximo(s) caso(s) desbloqueado(s)

### Sucesso Parcial: Culpado Errado, Bom Raciocínio
**Quando ocorre:**
- Jogador escolhe suspeito errado MAS
- Explicação demonstra processo de investigação sólido
- Interpretação das evidências foi lógica (mesmo incorreta)

**Recompensas:**
- Nenhum crédito de promoção nas regras atuais de promoção por casos corretamente resolvidos
- Feedback detalhado sobre o que faltou
- Pode tentar novamente em nova tentativa

### Resultado de Aprendizado: Múltiplas Falhas
**Quando ocorre:**
- Jogador esgota todas as tentativas (geralmente 3)
- Não consegue resolver o caso

**Resultado:**
- Nenhum crédito de promoção concedido
- Solução completa revelada
- Caso marcado como "Não Resolvido - Revisado"
- Pode ser tentado de novo depois (após solucionar outros casos)
- Ainda contribui para o aprendizado do jogador

---

## 2.6 Estados de Falha e Penalidades

O que NÃO faz o jogador falhar:

### Sem Estados de Falha para:
- ❌ Levar muito tempo (sem limites de tempo)
- ❌ Ler documentos "fora de ordem"
- ❌ Não solicitar perícias
- ❌ Fazer anotações de forma ineficiente
- ❌ Examinar evidências irrelevantes

### Falha Suave: Ficar sem Tentativas
**O que acontece:**
- Após 3 submissões incorretas, o caso é "travado"
- Solução é revelada (não conta como caso corretamente resolvido para promoção)
- É preciso resolver 2 outros casos antes de tentar novamente
- Jogador aprende com os erros

**Por que este sistema existe:**
- Evita chute aleatório
- Incentiva investigação cuidadosa
- Permite aprender com falhas
- Não bloqueia progresso permanentemente

### Sem Penalidades para:
- ✅ Solicitar perícias e não usar os resultados
- ✅ Fazer longas pausas entre sessões
- ✅ Abandonar um caso (pode voltar quando quiser)
- ✅ Ler soluções de casos falhos

---

## 2.7 Ritmo e Cadência

O compasso intencional da jogabilidade:

### Fase de Investigação: **Lenta e Metódica**
- Ler documentos leva 5-15 minutos cada
- Examinar evidências é contemplativo
- Sem pressa, sem cronômetros
- Jogador define o próprio ritmo

**Objetivo de design:** Criar clima meditativo de investigação

### Fase de Perícia: **Antecipação**
- Enviar requisição: Rápido (30 segundos)
- Período de espera: Horas/dias (reais ou acelerados)
- Resultado: Excitação com novas informações

**Objetivo de design:** Reproduzir a espera real de investigações

### Fase de Solução: **Tensão**
- Escrever explicação: Pensada (10-15 minutos)
- Momento de envio: Alta aposta
- Feedback: Alívio ou frustração

**Objetivo de design:** Fazer o envio parecer um momento consequente

### Transições de Sessão: **Pontos de Pausa Naturais**
- Após ler vários documentos
- Após enviar requisições de perícia
- Após receber resultados forenses
- Após uma grande revelação

**Objetivo de design:** Permitir finais de sessão sem culpa

---

## 2.8 Motivação e Ganchos do Jogador

O que mantém os jogadores engajados?

### Ganchos de Curto Prazo (Por Sessão)
**"Só mais um documento..."**
- Ganchos nos documentos (menções a evidências ocultas)
- Mistérios levantados que pedem resposta
- Contradições que exigem resolução
- Resultados forenses prestes a sair

### Ganchos de Médio Prazo (Por Caso)
**"Preciso resolver isso..."**
- Apego ao caso específico
- Teoria que precisa de validação
- Vontade de provar a hipótese certa
- Quadro quase completo, falta uma peça

### Ganchos de Longo Prazo (Entre Casos)
**"Estou virando um detetive melhor..."**
- Progressão de patentes (Rookie → Detective → Detective2 → Sergeant → Lieutenant → Captain → Commander)
- Biblioteca de casos crescente (5, 10, 20 casos solucionados)
- Taxa de sucesso melhorando ao longo do tempo
- Desbloqueio de casos mais difíceis e interessantes

### Ganchos Meta
**"É isso que eu queria de conteúdos true crime..."**
- Realmente *investigar*, não apenas assistir
- Satisfação intelectual
- Compartilhar teorias com amigos/comunidade
- Status de "bom detetive" entre pares

---

## 2.9 Rejogabilidade e Variedade

Como mantemos a experiência fresca?

### Variedade de Casos
**Tipos de crime:**
- Homicídio (mais comum)
- Pessoas desaparecidas
- Roubo/fraude
- Incêndio criminoso
- Agressão

**Cenários:**
- Urbano (escritórios, apartamentos, ruas)
- Suburbano (casas, parques)
- Rural (fazendas, locais isolados)
- Espaços públicos (restaurantes, hotéis)

**Perfis de vítimas:**
- Profissional de negócios
- Estudante
- Aposentado
- Figura pública
- Criminoso

**Épocas (Conteúdo futuro):**
- Moderna (2020s) - tecnologia atual
- 2000s - pré-smartphone
- 1990s - digital limitado
- Caso antigo reaberto com nova tecnologia

### Variedade de Evidências
**Tipos de documento:**
- Relatórios policiais (formato padrão)
- Depoimentos de testemunhas (transcrições de entrevista)
- Cartas/e-mails pessoais
- Registros financeiros
- Logs telefônicos
- Entradas de diário
- Prontuários médicos
- Fichas de emprego

**Tipos de perícia:**
- Análise de DNA
- Balística
- Impressões digitais
- Toxicologia
- Vestígios (fibras, cabelo)
- Forense digital (futuro)
- Grafoscopia
- Laudos de autópsia

### Variedade de Suspeitos
**Arquétipos (usados com cuidado para evitar estereótipos):**
- Rival de negócios
- Amante ressentido
- Membro da família
- Funcionário/colega
- Desconhecido com conexão
- Pessoa no lugar errado (inocente)

---

## 2.10 Recursos Sociais e Comunitários

Como os jogadores interagem além da experiência solo:

### Experiência Solo em Primeiro Lugar
**Jogo central é single-player:**
- Sem necessidade de multiplayer
- Sem mecânicas de coop (inicialmente)
- Caso autocontido

### Recursos Comunitários Opcionais

**Fóruns de discussão:**
- Tópicos por caso com tags de spoiler
- Compartilhamento de teorias (antes da solução)
- Discussão pós-solução
- Ranking/avaliação de casos

**Compartilhamento indireto:**
- Status "Resolvi X casos"
- Insígnia de patente de detetive
- Percentual de sucesso
- Lista de casos favoritos

**Sem recursos competitivos diretos:**
- ❌ Sem placar (evita foco em velocidade)
- ❌ Sem ranking baseado em tempo
- ❌ Sem modo versus
- ❌ Sem notificações de conquistas

**Por quê:** Competição enfraquece a experiência contemplativa de investigação

---

## 2.11 Acessibilidade e Suporte ao Jogador

Como oferecemos ajuda sem conduzir pela mão:

### Recursos de Acessibilidade

**Suporte à leitura:**
- Ajuste de tamanho de fonte na UI (não nos PDFs)
- Modo de alto contraste
- Fonte amigável para dislexia
- Leitura em voz alta (nativa do navegador)

**Suporte à navegação:**
- Atalhos de teclado para todos os apps
- Navegação por tabulação nos documentos
- Atalhos para gerenciamento de janelas
- Sistema de favoritos para documentos

**Suporte de tempo:**
- Pausar temporizador de perícia (nas configurações)
- Acelerar tempo (opções 2x, 5x, 10x)
- Salvamento automático
- Retornar exatamente de onde parou

### Ferramentas Opcionais (Não Intrusivas)

**Caderno do Detetive:**
- Espaço em branco para anotações
- NÃO preenche pistas automaticamente
- NÃO destaca informações importantes
- Ferramenta puramente do jogador

**Favoritos de documentos:**
- Marcar documentos como "importantes"
- Navegação rápida para itens marcados
- Puramente organizacional

**Visão de linha do tempo:**
- Representação visual dos eventos
- Derivada dos documentos (sem info escondida)
- Apenas ajuda na visualização

### O que NÃO Oferecemos

**Sem sistema de dicas:**
- ❌ Sem "clique aqui para ajuda"
- ❌ Sem sistema de dicas progressivas
- ❌ Sem assistente de IA sugerindo próximos passos
- ❌ Sem marcadores de objetivo

**Sem simplificações:**
- ❌ Sem "modo fácil" que altera o caso
- ❌ Sem geração de resumo
- ❌ Sem anotações automáticas

**Filosofia:** Ferramentas sim, atalhos não

---

## 2.12 Tutorial e Onboarding

Como novos jogadores aprendem a jogar:

### Caso Tutorial: "Primeiro Dia"
**Duração:** 15-20 minutos  
**Complexidade:** Extremamente simples  
**Estrutura:**

```
Cena: Exercício de treinamento para novo detetive

1. Tela de Boas-vindas
   - "Bem-vindo à Divisão de Casos Arquivados"
   - Visão geral breve (2 frases)
   - Clique em "Iniciar Treinamento"

2. Introdução à Área de Trabalho
   - Mostra desktop com 3 apps
   - Ícone de E-mail pulsa
   - Texto: "Clique em E-mail para receber seu primeiro caso"

3. Briefing (Simplificado)
   - E-mail curto (100 palavras)
   - Caso de treinamento: quadro roubado
   - Apenas 2 documentos para revisar

4. Introdução aos Arquivos do Caso
   - Ícone pulsa
   - Abre 2 documentos
   - Texto: "Leia ambos os documentos"

5. Introdução às Evidências
   - Mostra 1 foto de evidência
   - Texto: "Este quadro foi encontrado na casa do suspeito"

6. Solução
   - Abre o app de Submissão automaticamente
   - Apenas 1 opção de suspeito
   - Caixa de texto simples
   - Botão Enviar

7. Conclusão
   - "Treinamento concluído!"
   - Caso real agora desbloqueado
   - Sem novos tutoriais
```

### Aprendizado Pós-Tutorial

**Baseado em descoberta:**
- Jogadores exploram o Laboratório para entender perícias
- Sem tutorial forçado para cada recurso
- Tooltips ao passar o mouse (pode ser desativado)
- Botão de ajuda leva a manual curto (opcional)

**Design do primeiro caso real:**
- Um pouco mais difícil que o tutorial (mas ainda Fácil)
- Introduz perícias de forma natural (evidência pede análise)
- 3 suspeitos (tutorial tinha 1)
- Mais documentos (8-10 vs. 2 do tutorial)

---

## 2.13 Impacto da Monetização na Jogabilidade

Como o modelo de negócio afeta a experiência:

### Compra Premium = Sem Compromissos

**O que PODEMOS fazer:**
- ✅ Perícias levam tempo real (sem pressão para monetizar aceleração)
- ✅ Casos têm duração necessária (sem enchimento artificial)
- ✅ Dificuldade autêntica (sem ajustes artificiais para retenção)
- ✅ Sem missões diárias ou bônus de login
- ✅ Sem sistemas de energia/fôlego
- ✅ Sem anúncios interrompendo a investigação

**O que os jogadores recebem:**
- Paga uma vez, joga para sempre
- Todos os casos do pacote inclusos
- Sem custos ocultos
- Sem manipulação psicológica
- Respeito ao tempo do jogador

### Modelo de DLC: Mais Casos

**Pacotes adicionais de casos:**
- Mesma qualidade do jogo base
- Rotulados com clareza (dificuldade, tema)
- Compra opcional (jogo base é completo)
- Sem táticas de FOMO
- Sem exclusivos temporários

---

## 2.14 Casos Limite e Situações Especiais

Como lidamos com cenários incomuns:

### Jogador Desiste Cedo
**Cenário:** Jogador abandona o caso após 10 minutos

**Resposta do sistema:**
- Caso salvo automaticamente
- Permanece disponível no painel
- Sem penalidade
- Pode retornar a qualquer momento

**Nota de design:** Tudo bem – nem todo caso agradará todos os jogadores

### Jogador Resolve sem Perícias
**Cenário:** Jogador deduz corretamente sem solicitar análises

**Resposta do sistema:**
- ✅ Solução ainda é aceita
- ✅ Ainda conta como caso corretamente resolvido para promoção se a submissão final estiver correta
- 🎖️ Bônus: reconhecimento "Intuição de Detetive"
- Observação: raro, mas deve ser recompensado

### Jogador Solicita Todas as Perícias de Uma Vez
**Cenário:** Jogador clica em todas as opções de perícia no início

**Resposta do sistema:**
- ✅ Todas as requisições aceitas
- ⏱️ Todos os cronômetros iniciam simultaneamente
- 💰 Sem limite de custo (requisições ilimitadas)

**Nota de design:** Não é ideal, mas não punimos – o jogador aprenderá o ritmo

### Jogador Leva Meses para Finalizar
**Cenário:** Jogador inicia o caso e só retorna após 3 meses

**Resposta do sistema:**
- ✅ Todo progresso preservado
- ✅ Perícias concluídas há tempos
- 📝 Opcional: botão "Resumo do Caso" para refrescar a memória
- Pode continuar exatamente de onde parou

---

## 2.15 Métricas de Sucesso da Jogabilidade

Como medimos se a jogabilidade está funcionando:

### Métricas de Engajamento
- **Tempo médio de sessão:** 30-60 minutos (indica profundidade)
- **Sessões por caso:** 3-5 (indica ritmo adequado)
- **Taxa de conclusão:** 60%+ do início ao fim (indica engajamento)
- **Tempo até solução:** 3-6 horas para casos fáceis (indica dificuldade equilibrada)

### Métricas de Qualidade
- **Taxa de acerto na primeira tentativa:** 30-40% (desafiador, mas possível)
- **Taxa de nova tentativa após falha:** 70%+ tentam novamente (indica motivação)
- **Uso de perícias:** 60%+ solicitam pelo menos uma análise (indica compreensão da mecânica)
- **Uso de anotações:** 40%+ abrem o caderno (indica profundidade de engajamento)

### Sinais de Alerta
- ⚠️ Sessões < 15 minutos (experiência rasa)
- ⚠️ 80%+ abandonam antes de concluir o primeiro caso (difícil/chato demais)
- ⚠️ 90%+ acerto na primeira tentativa (fácil demais)
- ⚠️ <10% solicitam perícias (mecânica não entendida)

---

## 2.16 Roteiro de Evolução da Jogabilidade

Como a jogabilidade pode evoluir pós-lançamento:

### Fase 1 (Lançamento): Loop Central
- Investigação single-player
- Leitura de documentos
- Solicitações de perícia
- Envio de solução

### Fase 2 (Pós-lançamento): Refinamentos
- **Construtor de Linha do Tempo:** Ferramenta visual para organizar eventos
- **Quadro de Evidências:** Painel para fixar conexões
- **Comparar Documentos:** Visualização lado a lado
- **Busca:** Pesquisa por palavra-chave em todos os documentos

### Fase 3 (Futuro): Novas Mecânicas
- **Transcrições de entrevistas:** Ler documentos em formato perguntas e respostas
- **Filmagens de segurança:** Clipes de vídeo para analisar
- **Forense digital:** Registros de e-mail/telefone com metadados
- **Reinterrogatório:** Novas perguntas liberadas no meio do caso

### Fase 4 (Longo prazo): Recursos Avançados
- **Modo coop:** Dois detetives compartilham o caso (colaboração assíncrona)
- **Casos personalizados:** Casos criados pela comunidade (curados)
- **Gerador de Casos para Jogadores:** Criação procedural/UGC (muito longo prazo)

**Importante:** O loop central permanece intocado. Adições são melhorias, não substituições.

---

## 2.17 Resumo

**O loop de jogabilidade de CaseZero é: Ler → Examinar → Solicitar → Esperar → Analisar → Teorizar → Enviar**

**Principais princípios de jogabilidade:**
- 📖 Leitura é o verbo principal
- 🕰️ Ritmo é deliberadamente lento
- 🧠 Desafio vem da dedução, não de mecânicas
- ⏰ Perícias em tempo real geram antecipação
- 🎯 Envio da solução tem peso (tentativas limitadas)

**Experiência do jogador:**
- Sessões duram 30-90 minutos
- Casos levam 3-5 sessões para concluir
- A dificuldade escala pelos sete níveis implementados, de Rookie a Commander
- Autonomia e investigação guiada pelo jogador o tempo todo

**Métricas de sucesso:**
- 60%+ taxa de conclusão
- 30-40% acerto na primeira tentativa
- Sessões médias de 30-60 minutos
- 70%+ retornam após falhar

---

**Próximo capítulo:** [03-MECANICAS.md](03-MECANICAS.md) - Sistemas detalhados de mecânicas

**Documentos relacionados:**
- [01-CONCEITO.md](01-CONCEITO.md) - Justificativas das escolhas de jogabilidade
- [04-ESTRUTURA-DE-CASO.md](04-ESTRUTURA-DE-CASO.md) - Como os casos sustentam o loop
- [07-INTERFACE-DO-USUARIO.md](07-INTERFACE-DO-USUARIO.md) - Apresentação da jogabilidade

---

**Histórico de revisões:**

| Data | Versão | Mudanças | Autor |
|------|--------|----------|-------|
| 13/11/2025 | 1.0 | Tradução completa para PT-BR | Assistente de IA |
