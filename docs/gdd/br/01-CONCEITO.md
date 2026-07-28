# Capítulo 01 - Conceito

**Documento de Design de Jogo - CaseZero v3.0**  
**Última atualização:** 13 de novembro de 2025  
**Status:** ✅ Completo

---

## 1.1 Conceito Central

> **"Você é um detetive de casos arquivados com nada além de documentos, tempo e a própria mente. Sem atalhos. Sem mágica. Apenas investigação."**

CaseZero é um **jogo realista de investigação policial** no qual jogadores analisam documentos estáticos, examinam fotos de evidências, solicitam perícias e solucionam homicídios arquivados por meio de pura dedução e paciência.

### Pitch de Elevador
*"Imagine Hunt a Killer encontrando Return of the Obra Dinn – uma experiência web de investigação de casos arquivados em que você lê relatórios policiais reais, aguarda resultados forenses em tempo real e envia sua conclusão com tentativas limitadas. Sem sequências de ação, sem árvores de diálogo, sem mão segura – só você, as evidências e a verdade."*

---

## 1.2 Pilares Centrais

O jogo se apoia em quatro princípios fundamentais que orientam cada decisão de design:

### 🎯 **Pilar 1: AUTENTICIDADE**
**"Tem que parecer trabalho policial de verdade"**

- Documentos têm aparência de relatórios policiais, depoimentos e laudos forenses reais
- Sem elementos fantasiosos, tecnologia sci-fi ou poderes sobrenaturais
- Tipos de crime plausíveis (homicídio, furto, fraude, desaparecimento)
- Perícias ocorrem em tempo real (DNA = 24 horas, Balística = 12 horas)
- Tom e linguagem profissionais de ponta a ponta
- Nada de abstrações "gamificadas" – se não existe em investigação real, não existe aqui

**Implicações de design:**
- Todos os documentos devem ser PDFs ou imagens de alta qualidade
- Texto precisa usar terminologia policial/jurídica adequada
- Progressão temporal é obrigatória para perícias
- Nada de barras de vida, mana ou atributos de RPG

### 🧠 **Pilar 2: AUTONOMIA**
**"Você decide o que investigar e quando"**

- Sem marcadores de objetivo ou listas de tarefas
- Sem tutoriais extensos após a introdução inicial
- Sem progressão forçada da história (exceto o tempo das perícias)
- O jogador escolhe quais documentos ler, quais evidências examinar e quais análises solicitar
- Múltiplos caminhos legítimos de investigação levam à mesma solução
- Não existe "ordem correta" de ações

**Implicações de design:**
- A UI deve permitir navegação livre entre todos os materiais do caso
- Sem dicas emergentes ou sugestões durante a investigação
- Tutorial enxuto (no máximo 2–3 telas)
- Jogadores podem enviar a solução quando acharem que estão prontos
- Sem rastreamento de conquistas ou notificações

### 📚 **Pilar 3: ANÁLISE**
**"Ler e pensar são o centro da jogabilidade"**

- 90% da experiência envolve ler documentos e examinar fotos
- O sucesso depende de conectar informações de múltiplas fontes
- A correlação de evidências é mental, não mecânica
- Jogadores devem tomar notas próprias (o jogo oferece um aplicativo de anotações)
- Soluções exigem explicação, não apenas selecionar um nome
- Pistas falsas e informações enganosas são intencionais

**Implicações de design:**
- Documentos precisam ser textos longos (média de 2–5 páginas)
- Fotos de evidências devem ser em alta resolução e com detalhes
- Sem minigames de "encontre a pista" ou caça-pixels
- Nada de destaque automático de informações importantes
- Jogadores precisam de espaço para construir suas próprias conexões

### ⏳ **Pilar 4: PACIÊNCIA**
**"Investigações de verdade levam tempo"**

- Perícias acontecem em tempo real (acelerado, mas ainda exige espera)
- Sem pressão de tempo ou contagens regressivas
- Casos pensados para múltiplas sessões (2–8 horas ao todo)
- Ritmo lento é intencional – trata-se de reflexão, não de velocidade
- Jogadores podem pausar e retornar quando quiserem
- A investigação é uma maratona, não uma corrida

**Implicações de design:**
- Sistema de perícias em tempo real com requisições persistentes
- Sem bônus de tempo ou pontuação baseada em velocidade
- Sistema de salvamento precisa preservar todo o progresso
- UI deve mostrar tempo restante das análises pendentes
- Sem "modo rápido" ou opção de pular perícias

---

## 1.3 O que CaseZero NÃO é

Entender o que **não** estamos construindo é tão importante quanto o que estamos:

### ❌ NÃO é um jogo de ação
- Sem perseguições
- Sem combate ou violência interativa
- Sem quick-time events
- Sem mecânicas baseadas em reflexo
- Sem elementos de arcade

### ❌ NÃO é uma visual novel
- Sem árvores de diálogo
- Sem gerenciamento de relacionamento
- Sem enredos ramificados baseados em escolhas
- Sem romance ou social sim
- Sem customização de personagem

### ❌ NÃO é um jogo de objetos escondidos
- Sem caça-pixels
- Sem minigames de "encontre todas as pistas"
- Sem sequências de busca com tempo limitado
- Sem mudança de cursor apontando itens interativos

### ❌ NÃO é um RPG
- Sem atributos (força, inteligência etc.)
- Sem árvores de habilidades ou desbloqueios
- Sem gerenciamento de equipamentos ou inventário
- Sem classes ou builds de personagem
- Sem XP ganho por ações isoladas

### ❌ NÃO é um jogo de quebra-cabeça tradicional
- Sem puzzles mecânicos
- Sem grades lógicas
- Sem decodificação de cifras
- Sem desafios abstratos
- A correlação de evidências é contextual, não mecânica

---

## 1.4 Público-Alvo

### Público primário
**"Entusiastas de true crime" (25–45 anos)**

- Ouvem podcasts policiais (Serial, My Favorite Murder etc.)
- Assistem a séries investigativas (Making a Murderer, The First 48, Forensic Files)
- Jogam títulos de investigação narrativa (Her Story, Return of the Obra Dinn, Disco Elysium)
- Confortáveis com leitura de textos longos
- Perfil paciente e analítico
- Preferem desafios cerebrais a ação

**Perfil psicográfico:**
- Valores: inteligência, lógica, justiça, atenção aos detalhes
- Motivações: resolver problemas complexos, descobrir a verdade, satisfação intelectual
- Estilo de jogo: metódico, toma notas, joga em múltiplas sessões, completionista
- Frustrações em jogos: mão segura em excesso, simplificação exagerada, falta de desafio

### Público secundário
**"Profissionais em busca de profundidade" (30–55 anos)**

- Pouco tempo para jogar (1–2 horas por sessão)
- Preferem qualidade a quantidade
- Apreciam simulações realistas
- Gostam de experiências que respeitem sua inteligência
- Frequentemente atuam em direito, medicina, educação ou áreas técnicas

### Público terciário
**"Fãs de ficção de mistério/detetive"**

- Leem Agatha Christie, Arthur Conan Doyle e thrillers modernos
- Valorizam dedução e lógica acima de ação
- Podem ter menos familiaridade com jogos, mas se interessam pelo conceito
- Apreciam boa escrita e narrativa envolvente

---

## 1.5 Propostas de Valor Únicas (USPs)

O que torna CaseZero diferente de qualquer outro jogo de detetive:

### 🔍 **USP 1: Investigação puramente documental**
**Concorrentes:** Her Story (vídeos), Return of the Obra Dinn (cenas 3D), L.A. Noire (interrogatórios)  
**CaseZero:** Apenas documentos e fotos estáticos – exatamente como a mesa de um detetive de casos arquivados

### ⏰ **USP 2: Perícia em tempo real**
**Concorrentes:** Resultados instantâneos, barras de "pesquisando..."  
**CaseZero:** Solicite análise de DNA, volte em 24 horas reais (ou tempo acelerado) para receber o laudo

### 📝 **USP 3: Explique sua solução**
**Concorrentes:** Selecionar o nome correto em uma lista, validação automática  
**CaseZero:** Escreva sua explicação, com tentativas limitadas, comprovando o raciocínio

### 🎮 **USP 4: Zero gamificação**
**Concorrentes:** Pop-ups de XP, conquistas, barras de progresso  
**CaseZero:** Sem HUD, sem notificações, sem distrações – apenas o caso

### 🧩 **USP 5: Múltiplos caminhos investigativos**
**Concorrentes:** Progressão linear, gating por desbloqueio  
**CaseZero:** Todo o material do caso liberado desde o início (exceto perícias); investigue em qualquer ordem

### 🏢 **USP 6: Metáfora de desktop**
**Concorrentes:** UI personalizada, interface específica de jogo  
**CaseZero:** Ambiente familiar de desktop – aplicativo de e-mail, visualizador de arquivos, sistema de requisições laboratoriais

---

## 1.6 Inspirações & Referências

### Inspirações diretas

**Hunt a Killer (jogo físico)**
- Documentos e fotos estáticos
- Investigação autoguiada
- Sem "game master"
- Elemento de tempo real (episódios mensais)
- *Adaptação:* Formato digital, casos completos, sistema de perícias

**Return of the Obra Dinn (jogo)**
- Jogabilidade baseada em pura dedução
- Sem mão segura ou dicas
- Jogador faz suas próprias conexões
- Satisfação ao resolver via lógica
- *Adaptação:* Ambiente policial moderno, documentos em vez de cenas 3D

**Her Story (jogo)**
- Investigação não linear
- Busca e filtragem de informação
- Descoberta conduzida pelo jogador
- Sem estados de falha, apenas entendimento
- *Adaptação:* Arquivos estruturados, múltiplas fontes além de vídeo

**Papers, Please (jogo)**
- Exame de documentos como mecânica central
- Atenção a detalhes é recompensada
- Sensação burocrática/autêntica
- Interface minimalista
- *Adaptação:* Contexto investigativo, sem pressão de tempo

**The Case of the Golden Idol (jogo)**
- Jogabilidade de dedução
- Preenchimento de lacunas com informações corretas
- Vários mistérios interconectados
- Sem ação, apenas lógica
- *Adaptação:* Cenário realista, baseado em documentos, casos mais longos

### Referências de clima e tom

**True Detective (série)**
- Trabalho policial cru e realista
- Casos complexos em múltiplas camadas
- Investigação de ritmo lento
- Detetives falhos porém dedicados

**Zodíaco (filme)**
- Investigação obsessiva
- Análise documental e pesquisa
- Tempo passando entre avanços
- Representação realista do trabalho investigativo

**The Wire (série)**
- Autenticidade processual
- Investigação minuciosa
- Múltiplas perspectivas do crime
- Sem glamourização

### Referências visuais

**Se7en (filme)** – estética de fotografia de cena do crime  
**Mindhunter (série)** – atmosfera de sala de documentos/entrevistas  
**The Jinx (documentário)** – apresentação de arquivos  
**Making a Murderer (documentário)** – organização de dossiês

---

## 1.7 Objetivos Centrais da Experiência

O que os jogadores devem **sentir** ao jogar CaseZero?

### 😤 **Frustração → Satisfação**
**A jornada:**
1. Sobrecarga inicial: "Há informação demais"
2. Reconhecimento de padrões: "Isso se conecta com aquilo"
3. Formação de hipótese: "Acho que foi X por causa de Y"
4. Coleta de evidências: "Preciso provar isso"
5. Momento de virada: "Agora tudo faz sentido!"
6. Envio da solução: ansiedade e expectativa
7. Validação: "Eu estava certo!" (ou aprendizado ao errar)

### 🧐 **Curiosidade & Descoberta**
- Vontade de ler "só mais um" documento
- Percepção de detalhes que podem ser relevantes
- Conexão de informações entre fontes distintas
- O momento "aha" quando tudo se encaixa

### 🕵️ **Sensação de ser detetive**
- Sentir-se um investigador de verdade
- Fazer deduções próprias
- Não ser guiado sobre o que pensar
- Confiar na própria análise

### ⏰ **Antecipação & Paciência**
- Aguardar resultados forenses
- Construir o caso ao longo de várias sessões
- Degustar o processo investigativo
- Não correr para o final

### 🎯 **Conquista Intelectual**
- Resolver por pura lógica
- Demonstrar domínio dos detalhes
- Conquistar a resposta, não recebê-la
- Orgulho pela dedução correta

---

## 1.8 Filosofia de Design

### **"Respeite a inteligência do jogador"**

Cada decisão de design deve perguntar: *"Isso trata o jogador como um adulto inteligente?"*

- ✅ Forneça informação, deixe que ele interprete
- ✅ Permita erros e aprendizado
- ✅ Confie na capacidade de descobrir
- ❌ Não explique o óbvio
- ❌ Não conduza pela mão
- ❌ Não simplifique demais

### **"Menos é mais"**

Resista ao impulso de adicionar recursos:
- Menos mecânicas, implementadas com profundidade
- Interface limpa ao invés de rica em botões
- Qualidade de casos acima de quantidade
- Apenas informação essencial

### **"Autenticidade acima de acessibilidade"**

Quando houver conflito, escolha autenticidade:
- Temporização real de perícias > gratificação instantânea
- Documentos complexos > resumos simplificados
- Linguagem profissional > tom casual
- Desafio realista > curva de dificuldade "perfeita"

### **"O caso é o jogo"**

O dossiê é o conteúdo, a mecânica e a experiência:
- Toda UI serve à investigação do caso
- Sem sistemas meta (loja, upgrades etc.)
- Toda progressão vem de resolver casos
- Cada caso é uma experiência autônoma

---

## 1.9 Métricas de Sucesso

Como saberemos se CaseZero atinge seus objetivos?

### Métricas de experiência do jogador
- **Tempo até a primeira solução:** 2–6 horas (indica dificuldade adequada)
- **Taxa de conclusão:** 40%+ finalizam o primeiro caso (indica engajamento)
- **Precisão da solução:** 30–50% acertam na primeira tentativa (indica desafio)
- **Duração da sessão:** média de 30–90 minutos (indica profundidade)
- **Retorno:** 60%+ iniciam um segundo caso (indica satisfação)

### Indicadores qualitativos
- Avaliações mencionam "realista", "desafiador", "satisfatório"
- Comunidade discute teorias e soluções de casos
- Jogadores compartilham notas e processos investigativos
- Poucas reclamações de "leitura demais"
- Pedidos por mais casos, não por mais recursos

### Sinais de alerta (falha de design)
- ⚠️ Jogadores dizendo "Não sei o que fazer"
- ⚠️ Alta evasão nos primeiros 15 minutos
- ⚠️ Reclamações de "chato" ou "lento demais"
- ⚠️ Pedidos por dicas ou opções de pular
- ⚠️ Baixo uso do sistema de perícias

---

## 1.10 Análise de Concorrentes

### Concorrentes diretos

| Jogo | Forças | Fraquezas | Vantagem de CaseZero |
|------|--------|-----------|----------------------|
| **Her Story** | Mecânica de busca engenhosa, investigação não linear | Limitado a vídeos, caso único | Múltiplos casos, vários tipos de evidência |
| **Return of the Obra Dinn** | Dedução pura, sem mão segura | Ambientação fantástica, navegação 3D complexa | Cenário moderno/realista, interface simples |
| **The Case of the Golden Idol** | Quebra-cabeças excelentes, bom ritmo | Estilo estilizado, casos curtos | Ambiente realista, casos mais profundos |
| **Contradiction** | Investigação em FMV | Progressão linear, caso único | Investigação aberta, múltiplos casos |
| **Hunt a Killer (físico)** | Documentos autênticos, imersivo | Caro, entrega lenta, requer espaço físico | Digital, instantâneo, acessível |

### Posicionamento de mercado

**CaseZero ocupa a interseção de:**
- Jogos de investigação (Her Story, Obra Dinn) → foco na dedução
- Conteúdo true crime (podcasts, documentários) → ambientação realista
- Jogos de puzzle (Golden Idol) → desafios lógicos
- Jogos de simulação (Papers, Please) → sistemas autênticos

**Posição única:** O único jogo digital que simula investigação de casos arquivados com documentos realistas e perícias em tempo real.

---

## 1.11 Plataforma & Escopo Técnico

### Plataforma
**Baseado em web (desktop/tablet)**

**Por que web:**
- ✅ Acesso instantâneo, sem downloads
- ✅ Multiplataforma por padrão
- ✅ Atualizações e patches simples
- ✅ Navegadores já lidam com PDFs nativamente
- ✅ Sessões persistentes via salvamento em nuvem

**Requisitos:**
- Desktop/notebook (leitura confortável de textos longos)
- Tablet é aceitável (tela grande para PDFs)
- Mobile não suportado (tela pequena para análise documental)

### Escopo técnico

**Tecnologias centrais:**
- Frontend: React + TypeScript
- Backend Web API: C# .NET 8
- Pipeline de geração de casos: Azure Durable Functions em C# .NET 9
- Banco de dados: Azure SQL
- Armazenamento: Azure Blob Storage (PDFs/imagens e bundles publicados de casos)
- Autenticação: Sessões via JWT

**Nível de complexidade:** Médio
- Sem multiplayer em tempo real
- Sem física complexa ou renderização 3D
- Desafio principal é conteúdo, não código
- O backend combina CRUD relativamente direto na API com um pipeline mais complexo de geração de casos em Durable Functions

---

## 1.12 Estratégia de Monetização

### Modelo de negócio: **Compra premium**

**Por que não free-to-play:**
- Combina com percepção de qualidade
- Sem pressão por mecânicas de monetização
- Respeita o tempo e a atenção do jogador
- Atrai público comprometido

### Estrutura de preços

**Lançamento inicial:**
- Jogo base: US$ 19,99 (inclui 3 casos + tutorial)
- Pacotes de casos adicionais: US$ 9,99 cada (3 casos por pacote)

**Pós-lançamento:**
- Passe de temporada: US$ 29,99 (12 casos ao longo de 6 meses)
- Coleção completa: US$ 49,99 (todo o conteúdo atual + futuro da S1)

**Projeções conservadoras (Ano 1):**
- 5.000 vendas @ US$ 19,99 = US$ 99.950
- Pacote de casos 1 (30% adesão): 1.500 @ US$ 9,99 = US$ 14.985
- **Total estimado Ano 1:** ~US$ 115.000

### Sem microtransações
- ❌ Nada de cosméticos
- ❌ Nada de pular tempo (quebraria o design)
- ❌ Nada de dicas ou soluções vendidas
- ❌ Nada de anúncios
- ✅ Apenas expansão de conteúdo (novos casos)

---

## 1.13 Filosofia de Desenvolvimento

### Valores do time
1. **Qualidade acima de velocidade** – Melhor um caso excelente do que três medianos
2. **Foco acima de features** – Dominar o loop central antes de expandir
3. **Iteração acima de perfeição** – Entregar, aprender, melhorar
4. **Respeito ao jogador** – Sem artifícios obscuros ou manipulação

### Prioridades de desenvolvimento
1. **Jogabilidade central** (visualização de documentos, perícias, envio de solução)
2. **Primeiro caso completo** (valida todo o conceito)
3. **Polimento & sensação** (metáfora de desktop, estética profissional)
4. **Casos adicionais** (pipeline de conteúdo, variedade)
5. **Recursos pós-lançamento** (baseados em feedback)

### Produto Mínimo Viável (MVP)
- ✅ 1 caso completo (dificuldade média)
- ✅ UI de desktop completa (E-mail, Dossiê, Laboratório)
- ✅ Sistema de perícias funcional (requisições em tempo real)
- ✅ Envio de solução com validação
- ✅ Perfil básico (patente, estatísticas)
- ✅ Tutorial enxuto (2–3 telas)

**Todo o restante é pós-MVP.**

---

## 1.14 Visão de Longo Prazo

### Ano 1: Fundação
- Lançar com 3 casos cobrindo faixas baixa/média/alta das dificuldades implementadas
- Escalar o pipeline de conteúdo já existente para lançamentos regulares
- Construir base inicial de jogadores
- Coletar feedback e iterar

### Ano 2: Expansão
- 12+ casos adicionais (pacotes trimestrais)
- Novos tipos de evidência (forense digital, imagens de segurança)
- Diversificação de crimes
- Recursos comunitários (fóruns, compartilhamento de casos)

### Ano 3: Evolução
- Casos criados por jogadores (ferramentas UGC)
- Investigação colaborativa (co-op para 2 jogadores)
- Perícias avançadas (comparar laudos, sintetizar resultados)
- Otimização para tablets/mobile

### Objetivo final
**"Ser a experiência digital definitiva de investigação de casos arquivados"**

O lugar onde fãs hardcore de jogos de detetive buscam um desafio real e entusiastas de true crime realizam o sonho investigativo.

---

## 1.15 Avaliação de Riscos

### Preocupações de alto risco

**"Jogadores acham lento/chato"**
- Mitigação: Escrita excelente, casos envolventes, opção de acelerar tempo
- Aceitação: Não é para todos – tudo bem

**"Leitura de documentos é exigente demais"**
- Mitigação: PDFs bem diagramados, estrutura clara, notas opcionais
- Aceitação: Público-alvo gosta de ler

**"Perícias em tempo real frustram"**
- Mitigação: Várias requisições em paralelo, aceleração opcional, ETA transparente
- Aceitação: Pilar central – não será comprometido

### Preocupações de médio risco

**"Casos difíceis/fáceis demais"**
- Mitigação: Playtests, múltiplas dificuldades, comunicação clara
- Solução: Iterar com base nas taxas de conclusão

**"Conteúdo insuficiente no lançamento"**
- Mitigação: 3 casos de alta qualidade, roadmap transparente
- Solução: Pipeline rápido pós-lançamento

**"Problemas técnicos com PDFs"**
- Mitigação: Implementação robusta de PDF.js, fallback, testes
- Solução: Priorizar testes de compatibilidade

---

## 1.16 Resumo

**CaseZero é um jogo realista de investigação de casos arquivados onde jogadores analisam documentos, examinam evidências e solucionam homicídios por meio de pura dedução.**

**Pilares centrais:**
- 🎯 Autenticidade – sensação de trabalho policial real
- 🧠 Autonomia – investigação conduzida pelo jogador
- 📚 Análise – leitura e raciocínio são o jogo
- ⏳ Paciência – leva tempo e recompensa minúcia

**Público-alvo:** Entusiastas de true crime e pensadores analíticos em busca de desafio cerebral

**Posicionamento único:** O único jogo digital que simula investigação de casos arquivados com documentos realistas e perícias em tempo real

**Objetivo:** Respeitar a inteligência do jogador, entregar uma experiência autêntica de detetive e fornecer satisfação intelectual através de dedução pura

---

**Próximo capítulo:** [02-JOGABILIDADE.md](02-JOGABILIDADE.md) – Loop central de jogabilidade

**Documentos relacionados:**
- [04-ESTRUTURA-DE-CASO.md](04-ESTRUTURA-DE-CASO.md) – O que define um bom caso
- [05-NARRATIVA.md](05-NARRATIVA.md) – Escrita para investigação
- [07-INTERFACE-DO-USUARIO.md](07-INTERFACE-DO-USUARIO.md) – Design da metáfora de desktop

---

**Histórico de revisões:**

| Data | Versão | Mudanças | Autor |
|------|--------|----------|-------|
| 13/11/2025 | 1.0 | Tradução completa para PT-BR | Assistente de IA |
