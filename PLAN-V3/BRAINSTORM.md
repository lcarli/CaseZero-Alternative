# 🧠 Brainstorm - CaseZero v3.0
## Análise e Design do Jogo

**Data:** 13 de novembro de 2025  
**Objetivo:** Detalhar cada componente do jogo através de análise estruturada

---

## 📁 PARTE 1: O CASO (Case Structure)

### 1.1 O que um caso DEVE conter?

#### **Informações Básicas**
- **ID único** (CASE-2024-001)
- **Título** descritivo (ex: "Homicídio no Edifício Corporativo")
- **Categoria** do crime (Homicide, Theft, Fraud, Missing Person, etc.)
- **Dificuldade** (Easy, Medium, Hard, Expert)
- **Tempo estimado** de conclusão (2h, 4h, 8h)
- **Rank mínimo** requerido (Rookie, Detective, Veteran, etc.)
- **Status** (Archived/Cold Case - sempre arquivado, reabertura)

#### **Contexto Narrativo**
- **Vítima**
  - Nome completo
  - Idade
  - Ocupação
  - Foto
  - Background (vida pessoal, profissional, relacionamentos)
  - Causa da morte / situação

- **Crime**
  - Tipo específico (murder, robbery, kidnapping, etc.)
  - Data e hora do ocorrido
  - Local detalhado (endereço, descrição)
  - Descrição da cena
  - Circunstâncias (como foi descoberto)
  - Fotos da cena do crime (múltiplas)

- **Timeline** de Eventos
  - Lista cronológica de eventos relevantes
  - Cada evento com: timestamp, descrição, fonte (CCTV, testemunha, etc.)
  - Eventos verificados vs. não verificados

#### **Evidências e Investigação**
- **Documentos**
  - Relatório policial inicial (sempre presente)
  - Declarações de testemunhas (múltiplas)
  - Relatórios de primeira resposta
  - Documentos relacionados ao caso (contratos, emails, mensagens)
  - Cada documento deve ter:
    - ID único
    - Tipo
    - Título
    - Data de criação
    - Autor
    - Arquivo (PDF/imagem)
    - Tags/categorias
    - Quando fica disponível (start, after-X, etc.)

- **Evidências Físicas**
  - Itens coletados na cena
  - Cada evidência deve ter:
    - ID único
    - Nome
    - Tipo (Physical, Biological, Digital, Trace)
    - Descrição
    - Foto de alta qualidade
    - Quem coletou
    - Quando foi coletada
    - Onde foi encontrada
    - Estado/condição
    - Chain of custody
    - **Análises forenses disponíveis** (Ballistics, DNA, Fingerprints, Toxicology, etc.)
    - Tempo de processamento de cada análise
    - Template do laudo para cada análise

- **Suspeitos**
  - Lista de pessoas de interesse
  - Cada suspeito deve ter:
    - ID único
    - Nome completo
    - Idade
    - Ocupação
    - Foto/mugshot
    - Background completo
    - Relação com a vítima
    - Motivo potencial
    - Álibi (descrição completa)
    - Ficha criminal (se houver)
    - Declaração/depoimento (documento PDF)
    - Quando fica disponível

- **Laudos Forenses**
  - Relatórios de análises completadas
  - Cada laudo deve ter:
    - ID único
    - Evidência relacionada
    - Tipo de análise
    - Título
    - Arquivo (PDF)
    - Descobertas principais
    - Conclusões
    - Se disponível imediatamente ou após solicitação
    - Tempo de processamento

#### **Solução**
- **Culpado** (ID do suspeito correto)
- **Motivo** completo e detalhado
- **Método** (como o crime foi cometido)
- **Evidências-chave** (lista de IDs que provam)
- **Explicação completa** da solução
- **Pistas** (opcional - para diferentes níveis de dificuldade)

#### **Configuração do Jogo**
- Permitir pause? (sim/não)
- Aceleração de tempo (1x, 2x, 5x, etc.)
- Tempo forense é real? (true/false)
- Máximo de solicitações forenses permitidas (limite)
- Tentativas de submissão (geralmente 3)

---

### 1.2 Questões em Aberto

**Q1:** Quantos documentos iniciais deve ter um caso?
- Mínimo: Relatório policial + 1 declaração?
- Ideal: Relatório + 3-5 declarações?
- Máximo: Quanto é demais?

**Q2:** Evidências devem ter "surpresas"?
- Ex: Evidência que parece irrelevante mas laudo revela algo crítico?
- Ou todas evidências são "honestas" desde o início?

**Q3:** Como estruturar suspeitos?
- Sempre mostrar todos desde o início?
- Ou revelar suspeitos conforme progresso?
- Quantos suspeitos ideal? (3-5? mais?)

**Q4:** Laudos forenses - tempo real vs. acelerado
- Forçar espera real (ex: 8h = esperar 8h)?
- Ou permitir acelerar (ex: 1min = 1h)?
- Sistema de "créditos" para acelerar?

**Q5:** Múltiplas soluções ou única?
- Solução sempre é única e determinística?
- Ou permitir interpretações diferentes (desde que fundamentadas)?

**Q6:** Como balancear dificuldade?
- Easy: Poucas evidências, motivo óbvio, 1-2 suspeitos?
- Medium: Evidências ambíguas, múltiplos motivos, 3-4 suspeitos?
- Hard: Red herrings, evidências contraditórias, 5+ suspeitos?

**Q7:** Assets - quantidade mínima vs. ideal
- Fotos da cena: mínimo 3? ideal 6-8?
- Fotos de evidências: 1 por evidência ou múltiplos ângulos?
- PDFs: gerar como? Ferramenta manual ou automatizar?

---

### 1.3 Estrutura de Pastas Proposta

```
cases/
└── CASE-2024-001/
    ├── case.json                    ← Arquivo principal único
    ├── README.md                    ← Descrição do caso (opcional)
    └── assets/
        ├── documents/               ← PDFs de relatórios, declarações
        │   ├── police-report.pdf
        │   ├── statement-silva.pdf
        │   └── ...
        ├── photos/                  ← Fotos diversas
        │   ├── victim.jpg
        │   ├── scene-01.jpg
        │   ├── scene-02.jpg
        │   ├── suspect-torres.jpg
        │   └── ...
        ├── evidence/                ← Fotos de evidências
        │   ├── weapon.jpg
        │   ├── blood-sample.jpg
        │   └── ...
        └── reports/                 ← Laudos forenses (templates)
            ├── ballistics-ev001.pdf
            ├── dna-ev004.pdf
            └── ...
```

**Alternativa:** Organizar por tipo de conteúdo ou deixar flat?

---

### 1.4 Exemplo de Caso Mínimo Viável (MVP)

**CASE-2024-001: "Homicídio no Escritório"**

**Vítima:** Robert Chen, 42, CFO  
**Crime:** Homicídio por arma de fogo  
**Local:** Escritório 15º andar, TechCorp Building  
**Data:** 15/03/2023, ~23:30

**Documentos Iniciais:**
1. Relatório policial (2 páginas)
2. Declaração do segurança (1 página)
3. Declaração da esposa (1 página)

**Evidências:**
1. Arma do crime (pistola .38) - Balística disponível (12h)
2. Projétil extraído - Balística disponível (12h)
3. Sangue na cena - DNA disponível (24h)
4. Impressões digitais na maçaneta - Análise disponível (8h)

**Suspeitos:**
1. Michael Torres (sócio) - Motivo: disputa financeira
2. Linda Chen (esposa) - Motivo: seguro de vida
3. David Park (funcionário) - Motivo: demissão recente

**Solução:** Michael Torres
**Evidências-chave:** Balística da arma, impressões digitais, DNA

---

### 1.5 Próximos Tópicos para Brainstorm

- [ ] **PARTE 2:** O Jogador (Profile, Progression, Stats)
- [ ] **PARTE 3:** A Interface (Desktop, Apps, Interações)
- [ ] **PARTE 4:** Análises Forenses (Tipos, Tempo, Resultados)
- [ ] **PARTE 5:** Submissão de Solução (Formato, Validação, Feedback)
- [ ] **PARTE 6:** Progressão e XP (Como funciona? Quando recebe?)
- [ ] **PARTE 7:** Tutorial (O que ensinar? Como ensinar?)
- [ ] **PARTE 8:** Geração de Casos (Como CaseGen.Functions irá gerar case.json?)

---

## 💭 Notas e Ideias Soltas

### Ideias Interessantes
- **Notebook do Detetive**: App onde jogador pode fazer anotações manuais?
- **Board de Conexões**: Visualizar conexões entre suspeitos/evidências?
- **Arquivo de Casos**: Ver histórico de casos resolvidos?
- **Comparação de Análises**: Comparar laudos forenses lado a lado?

### Inspirações
- Hunt a Killer (físico → digital)
- Her Story / Telling Lies (descoberta não-linear)
- Return of the Obra Dinn (dedução pura)
- Papers Please (interface minimalista, decisões impactantes)

### Preocupações
- ⚠️ Análises forenses com tempo real: jogadores vão esperar ou abandonar?
- ⚠️ Sem dicas: casos precisam ser muito bem balanceados
- ⚠️ PDFs: acessibilidade (móvel, tablets)?
- ⚠️ Quantidade de leitura: quanto é demais?

---

**Status:** 🚧 Parte 1 em análise - aguardando feedback para continuar

