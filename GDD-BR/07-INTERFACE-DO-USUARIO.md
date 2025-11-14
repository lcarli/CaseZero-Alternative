# Capítulo 07 - Interface & Experiência do Usuário

**Documento de Design de Jogo - CaseZero v3.0**  
**Última atualização:** 13 de novembro de 2025  
**Status:** ✅ Completo

---

## 7.1 Visão Geral

Este capítulo define a **interface visual, os padrões de interação e a experiência do usuário** em CaseZero. A UI adota a metáfora de um **sistema operacional de desktop** — os jogadores navegam por uma estação de trabalho simulada de detetive com janelas, ícones e aplicativos familiares.

**Conceitos-chave:**

- Metáfora de desktop (inspirada em Windows/macOS)
- Organização baseada em aplicativos (Email, Arquivos do Caso, Laboratório Forense)
- Sistema de gerenciamento de janelas
- Estética minimalista e profissional
- Design com acessibilidade como prioridade
- Ausência de UI gamificada desnecessária

---

## 7.2 Filosofia de Design

### Princípios Centrais

**1. Familiaridade por Metáfora**
- Usar convenções de OS desktop (janelas, barra de tarefas, ícones)
- Jogadores já entendem sistemas de arquivos e aplicativos
- Reduz curva de aprendizado
- Parece uma estação real de detetive

**2. Conteúdo em Primeiro Lugar**
- A interface serve ao conteúdo (documentos, evidências)
- Sem animações chamativas ou efeitos supérfluos
- Apresentação limpa e profissional
- UI deve ser invisível quando funciona bem

**3. Estética Profissional**
- Tema escuro (reduz fadiga ocular em leituras longas)
- Alto contraste para legibilidade
- Tipografia limpa
- Nada cartunesco

**4. Acessibilidade Nativa**
- Navegação completa por teclado
- Suporte a leitores de tela
- Tamanhos de texto ajustáveis
- Modo de alto contraste
- Ausência total de pressão de tempo

---

## 7.3 Estrutura da Metáfora de Desktop

### Layout da Área de Trabalho

```
┌──────────────────────────────────────────────────────────────┐
│ Estação do Detetive CaseZero                    [_][□][X]    │
├──────────────────────────────────────────────────────────────┤
│                                                              │
│                                                              │
│        [📧]                [📁]              [🧪]            │
│        Email               Arquivos do Caso  Laboratório     │
│                                              Forense         │
│                                                              │
│                                                              │
│                         [📋]                                 │
│                    Enviar Solução                            │
│                                                              │
│                                                              │
│                                                              │
├──────────────────────────────────────────────────────────────┤
│ [🏠] [📧] [📁] [🧪] [📋]                    🕐 11h47         │
└──────────────────────────────────────────────────────────────┘
```

**Componentes da Área de Trabalho:**

1. **Plano de fundo:** Cor sólida escura (#1a1a1a), sem distrações
2. **Ícones de aplicativos:** 4 apps principais centralizados
3. **Barra de tarefas:** Barra inferior com acessos rápidos + info do sistema
4. **Sistema de janelas:** Apps abrem como janelas arrastáveis

---

## 7.4 Design dos Aplicativos

### App 1: Email (📧)

**Propósito:** Receber briefings de casos e atualizações

**Layout da janela:**
```
┌────────────────────────────────────────────────┐
│ 📧 Email                            [_][□][X]  │
├────────────────────────────────────────────────┤
│ Caixa de Entrada                                  │
├────────────────────────────────────────────────┤
│ ● Nova Designação de Caso - CASE-2024-001       │
│   Divisão de Detetives • 16 de março de 2025    │
│                                                 │
│   Laudo Forense Disponível - EV-001 Balística   │
│   Laboratório Forense • 17 de março de 2025     │
│                                                 │
│   Laudo Forense Disponível - EV-004 DNA         │
│   Laboratório Forense • 18 de março de 2025     │
│                                                 │
│                                                 │
│                                                 │
│                                                 │
│                                                 │
│                                                 │
└────────────────────────────────────────────────┘
```

**Visualização do email:**
```
┌────────────────────────────────────────────────┐
│ De: Divisão de Detetives                       │
│ Para: Você                                     │
│ Data: 16 de março de 2025, 09h00               │
│ Assunto: Nova Designação - CASE-2024-001       │
├────────────────────────────────────────────────┤
│                                                 │
│ Detetive,                                        │
│                                                 │
│ Você foi designado ao CASE-2024-001:            │
│ "O Assassinato no Escritório Central"          │
│                                                 │
│ Um executivo foi encontrado morto em seu        │
│ escritório. A investigação inicial indica       │
│ homicídio. Revise os arquivos e apresente       │
│ suas conclusões.                               │
│                                                 │
│ Os arquivos do caso estão no aplicativo         │
│ Arquivos do Caso.                              │
│                                                 │
│ Boa sorte.                                      │
│                                                 │
│ - Divisão de Casos Arquivados                   │
│                                                 │
│ [Abrir Arquivos do Caso]                        │
└────────────────────────────────────────────────┘
```

**Recursos:**
- Lista simples de emails
- Leitura de briefing do caso
- Notificação de resultados forenses
- Link direto para Arquivos do Caso
- Sem funcionalidade de resposta (comunicação unilateral)

---

### App 2: Arquivos do Caso (📁)

**Propósito:** Acessar todos os documentos, evidências e informações

**Janela principal:**
```
┌────────────────────────────────────────────────┐
│ 📁 Arquivos do Caso - CASE-2024-001 [_][□][X] │
├────────────────────────────────────────────────┤
│ ◀ Voltar aos Casos                              │
├────────────────────────────────────────────────┤
│ 📂 Documentos                              (12) │
│ 📂 Evidências                              (8)  │
│ 📂 Suspeitos                               (3)  │
│ 📂 Informações da Vítima                   (2)  │
│ 📂 Laudos Forenses                         (2)  │
│ 📂 Linha do Tempo                          (1)  │
│ 📓 Minhas Notas                                │
│                                                 │
│                                                 │
│                                                 │
│                                                 │
│                                                 │
└────────────────────────────────────────────────┘
```

**Pasta Documentos:**
```
┌────────────────────────────────────────────────┐
│ 📁 Arquivos do Caso > Documentos [_][□][X]     │
├────────────────────────────────────────────────┤
│ ◀ Voltar                                       │
├────────────────────────────────────────────────┤
│ 📄 Relatório Policial - Incidente #2023-0315   │
│    3 páginas • 16 de março de 2025             │
│    [Ver Documento]                             │
│                                                 │
│ 📄 Declaração de Testemunha - John Silva       │
│    2 páginas • 16 de março de 2025             │
│    [Ver Documento]                             │
│                                                 │
│ 📄 Entrevista com Suspeito - Michael Torres    │
│    4 páginas • 17 de março de 2025             │
│    [Ver Documento]                             │
│                                                 │
│ 📄 Registros Financeiros - Torres & Chen       │
│    2 páginas • 17 de março de 2025             │
│    [Ver Documento]                             │
│                                                 │
└────────────────────────────────────────────────┘
```

**Visualizador de Documentos (PDF):**
```
┌────────────────────────────────────────────────┐
│ Relatório Policial - Incidente #2023-0315 [_][□][X] │
├────────────────────────────────────────────────┤
│ [<] Página 1 de 3 [>]    [⊕][⊖][⚲]     [🔍]    │
├────────────────────────────────────────────────┤
│                                                 │
│  DEPARTAMENTO DE POLÍCIA METROPOLITANO          │
│  RELATÓRIO DE INCIDENTE                         │
│                                                 │
│  Nº do Caso: 2023-0315                          │
│  Classificação: Homicídio                       │
│  Data/Hora: 15 de março de 2023, 23h30 (estim.) │
│  Local: 450 Market Street, 15º andar            │
│  Oficial Responsável: Martinez, Sarah           │
│  Data do Registro: 16 de março de 2023, 08h00   │
│                                                 │
│  RESUMO:                                        │
│  Por volta das 00h30 de 16/03/2023, esta        │
│  oficial atendeu a ocorrência de indivíduo      │
│  sem sinais vitais no local indicado...         │
│                                                 │
│                                                 │
├────────────────────────────────────────────────┤
│ [📌 Favoritar] [🔍 Buscar] [📋 Copiar Texto]    │
└────────────────────────────────────────────────┘
```

**Pasta Evidências:**
```
┌────────────────────────────────────────────────┐
│ 📁 Arquivos do Caso > Evidências [_][□][X]     │
├────────────────────────────────────────────────┤
│ ◀ Voltar                                       │
├────────────────────────────────────────────────┤
│ 🔫 EV-001: Arma de fogo - Revólver .38         │
│    Tipo: Física - Arma                         │
│    Coletada: 16 de março de 2023, 02h00        │
│    [Ver Fotos] [Solicitar Perícia]             │
│                                                 │
│ 🩸 EV-004: Amostra de sangue                   │
│    Tipo: Biológica - Sangue                    │
│    Coletada: 16 de março de 2023, 03h30        │
│    [Ver Fotos] [Solicitar Perícia]             │
│                                                 │
│ 📋 EV-007: Registro de Acesso                  │
│    Tipo: Documento - Logs                     │
│    Coletado: 16 de março de 2023, 10h00        │
│    [Ver Documento]                             │
│                                                 │
└────────────────────────────────────────────────┘
```

**Visualizador de Fotos de Evidência:**
```
┌────────────────────────────────────────────────┐
│ EV-001: Arma de fogo - Revólver .38  [_][□][X] │
├────────────────────────────────────────────────┤
│                                                 │
│                                                 │
│          [FOTO EM ALTA RESOLUÇÃO]              │
│          Arma sobre a mesa de evidências       │
│          com régua para escala                 │
│                                                 │
│                                                 │
├────────────────────────────────────────────────┤
│ [◀ Anterior] 1 de 3 [Próxima ▶]     [⊕][⊖]     │
│                                                 │
│ Tipo: Física - Arma                             │
│ Coletada: 16 de março de 2023, 02h00           │
│ Local: Cena do crime, próximo à vítima         │
│ Coletada por: Equipe CSI Alpha                 │
│                                                 │
│ [🔬 Solicitar Perícia]                          │
└────────────────────────────────────────────────┘
```

**Minhas Notas:**
```
┌────────────────────────────────────────────────┐
│ 📓 Minhas Notas                    [_][□][X]  │
├────────────────────────────────────────────────┤
│ [Nova Nota] [Notas do Caso] [Suspeitos] [Linha │
│  do Tempo]                                     │
├────────────────────────────────────────────────┤
│ Caso #2024-001 - Notas de Investigação         │
│ ──────────────────────────────────────────     │
│                                                 │
│ SUSPEITOS:                                      │
│ - Michael Torres: Sócio, devendo US$ 500 mil,   │
│   álibi fraco, registro de acesso indica        │
│   presença no prédio durante a TOD              │
│                                                 │
│ - Linda Chen: Esposa, seguro de vida de US$ 2M, │
│   porém CCTV confirma que estava em casa        │
│                                                 │
│ - David Park: Ex-funcionário, motivo de         │
│   vingança, mas álibi no bar confirmado         │
│   por múltiplas testemunhas                     │
│                                                 │
│ EVIDÊNCIAS-CHAVE:                               │
│ - Arma (EV-001): Registrada em nome de Torres,  │
│   impressões digitais dele no cabo              │
│ - Sangue (EV-004): DNA corresponde a Torres     │
│ - Log de acesso: Torres entrou às 23h15         │
│                                                 │
│ TEORIA:                                         │
│ Torres precisava de dinheiro, confrontou a      │
│ vítima...                                       │
│                                                 │
│ [Salvo automaticamente há 2 minutos]            │
└────────────────────────────────────────────────┘
```

---

### App 3: Laboratório Forense (🧪)

**Propósito:** Solicitar análises forenses e ver resultados

**Janela principal:**
```
┌────────────────────────────────────────────────┐
│ 🧪 Laboratório Forense               [_][□][X] │
├────────────────────────────────────────────────┤
│ [Evidências Disponíveis] [Pendentes] [Concluídos] │
├────────────────────────────────────────────────┤
│                                                 │
│ EVIDÊNCIAS DISPONÍVEIS:                         │
│                                                 │
│ EV-001: Arma de fogo - Revólver .38             │
│ ☐ Análise Balística (12 horas)                  │
│ ☐ Análise de Impressões Digitais (8 horas)      │
│ [Solicitar Selecionados]                        │
│                                                 │
│ EV-004: Amostra de Sangue                        │
│ ☐ Análise de DNA (24 horas)                     │
│ [Solicitar Selecionados]                        │
│                                                 │
└────────────────────────────────────────────────┘
```

**Aba Pendentes:**
```
┌────────────────────────────────────────────────┐
│ 🧪 Laboratório Forense > Pendentes [_][□][X]  │
├────────────────────────────────────────────────┤
│ [Evidências Disponíveis] [Pendentes] [Concluídos] │
├────────────────────────────────────────────────┤
│                                                 │
│ ANÁLISES EM ANDAMENTO:                          │
│                                                 │
│ EV-001 - Análise Balística                      │
│ Solicitado: 17 de março, 02h00                  │
│ Status: Em progresso ⏱️                         │
│ Conclusão: 17 de março, 14h00                   │
│ Tempo Restante: 10 horas 23 minutos             │
│ ▓▓▓▓▓▓▓░░░░░░░ 52%                              │
│                                                 │
│ EV-004 - Análise de DNA                         │
│ Solicitado: 17 de março, 02h05                  │
│ Status: Em progresso ⏱️                         │
│ Conclusão: 18 de março, 02h05                   │
│ Tempo Restante: 22 horas 18 minutos             │
│ ▓▓░░░░░░░░░░░░ 14%                              │
│                                                 │
└────────────────────────────────────────────────┘
```

**Aba Concluídos:**
```
┌────────────────────────────────────────────────┐
│ 🧪 Laboratório Forense > Concluídos [_][□][X] │
├────────────────────────────────────────────────┤
│ [Evidências Disponíveis] [Pendentes] [Concluídos] │
├────────────────────────────────────────────────┤
│                                                 │
│ ANÁLISES CONCLUÍDAS:                            │
│                                                 │
│ ✓ EV-001 - Análise Balística                    │
│   Concluída: 17 de março, 14h00                 │
│   [Ver Laudo]                                   │
│                                                 │
│ ✓ EV-004 - Análise de DNA                       │
│   Concluída: 18 de março, 02h05                 │
│   [Ver Laudo]                                   │
│                                                 │
└────────────────────────────────────────────────┘
```

---

### App 4: Enviar Solução (📋)

**Propósito:** Enviar a solução final do caso

**Formulário de envio:**
```
┌────────────────────────────────────────────────┐
│ 📋 Enviar Solução - CASE-2024-001 [_][□][X]   │
├────────────────────────────────────────────────┤
│                                                 │
│ QUEM COMETEU O CRIME?                           │
│                                                 │
│ [Selecionar Suspeito ▼]                         │
│ ├─ Michael Torres                               │
│ ├─ Linda Chen                                   │
│ ├─ David Park                                   │
│ └─ Outro/Desconhecido                           │
│                                                 │
│ ────────────────────────────────────────────    │
│                                                 │
│ EXPLIQUE O MOTIVO:                              │
│ (Mínimo 50 palavras)                            │
│                                                 │
│ ┌────────────────────────────────────────┐     │
│ │ Torres devia US$ 500.000 à vítima e    │     │
│ │ enfrentava a perda de suas ações na    │     │
│ │ empresa. O desespero financeiro levou-o │     │
│ │ a...                                    │     │
│ └────────────────────────────────────────┘     │
│                                                 │
│ COMO O CRIME FOI COMETIDO?                      │
│ (Mínimo 50 palavras)                            │
│                                                 │
│ ┌────────────────────────────────────────┐     │
│ │ Torres usou seu cartão de acesso para   │     │
│ │ entrar às 23h15. Confrontou a vítima no │     │
│ │ escritório. Durante a discussão...      │     │
│ └────────────────────────────────────────┘     │
│                                                 │
│ [▼ Continuar]                                   │
└────────────────────────────────────────────────┘
```

**Seleção de evidências (Página 2):**
```
┌────────────────────────────────────────────────┐
│ 📋 Enviar Solução - CASE-2024-001 [_][□][X]   │
├────────────────────────────────────────────────┤
│                                                 │
│ SELECIONE AS EVIDÊNCIAS-CHAVE:                  │
│ (Marque todas que sustentam sua conclusão)      │
│                                                 │
│ ☑ EV-001 - Arma de fogo (.38)                   │
│   Balística compatível, impressões de Torres    │
│                                                 │
│ ☑ EV-004 - Amostra de Sangue                    │
│   DNA corresponde a Michael Torres              │
│                                                 │
│ ☑ EV-007 - Log de Acesso                        │
│   Situa Torres no prédio durante o crime        │
│                                                 │
│ ☐ EV-008 - Registros Telefônicos da Vítima      │
│                                                 │
│ ☑ DOC-009 - Registros Financeiros               │
│   Demonstra dívida de US$ 500 mil, motivo       │
│                                                 │
│ ────────────────────────────────────────────    │
│                                                 │
│ Tentativas Restantes: 3/3                       │
│                                                 │
│ ⚠️ Atenção: Envio incorreto consome uma         │
│ tentativa. Revise sua teoria com cuidado.       │
│                                                 │
│ [◀ Voltar] [Cancelar] [Enviar Solução]          │
└────────────────────────────────────────────────┘
```

---

## 7.5 Gerenciamento de Janelas

### Controles das janelas

**Janela padrão:**
```
┌────────────────────────────────────────────────┐
│ 📧 Email                            [_][□][X]  │
│                    ▲▲▲                           │
│  Barra de título  Arraste para mover            │
└────────────────────────────────────────────────┘

Controles:
[_] = Minimizar (vai para a barra de tarefas)
[□] = Maximizar (tela cheia)
[X] = Fechar (retorna à área de trabalho)
```

**Estados de janela:**

1. **Normal:** Janela flutuante, arrastável, redimensionável
2. **Maximizada:** Tela cheia, cobre a área de trabalho
3. **Minimizada:** Oculta, ícone na barra de tarefas

**Múltiplas janelas:**
- Possível abrir vários apps simultaneamente
- Janelas empilham (a mais recente fica no topo)
- Clique para trazer a janela à frente
- Atalho Alt+Tab alterna entre janelas

---

## 7.6 Esquema de Cores & Tipografia

### Paleta de cores

**Cores primárias:**
```
Plano de fundo:   #1a1a1a (cinza muito escuro)
Janela:           #2a2a2a (cinza escuro)
Painel:           #333333 (cinza médio escuro)
Borda:            #444444 (cinza médio)
Texto:            #e0e0e0 (cinza claro)
Destaque:         #4a9eff (azul)
Sucesso:          #4caf50 (verde)
Aviso:            #ff9800 (laranja)
Erro:             #f44336 (vermelho)
```

**Exemplo visual:**
```
┌──────────────────────────────────────────────────┐ ← Borda #444444
│ Fundo de janela #2a2a2a                          │
│                                                  │
│ Texto #e0e0e0 sobre fundo escuro                │
│                                                  │
│ [Botão #4a9eff]  ← Cor de destaque              │
│                                                  │
│ ✓ Mensagem de sucesso (#4caf50)                 │
│ ⚠️ Mensagem de aviso (#ff9800)                  │
│ ✗ Mensagem de erro (#f44336)                    │
│                                                  │
└──────────────────────────────────────────────────┘
```

### Tipografia

**Família tipográfica:**
- Texto de UI: Inter, -apple-system, system-ui (sans-serif limpa)
- Texto de documentos: Georgia, serif (confortável para leitura longa)
- Monoespaçada: "Courier New", Courier, monospace (logs/dados)

**Tamanhos de fonte:**
- Título grande: 24px (negrito)
- Título médio: 18px (negrito)
- Corpo: 16px (regular)
- Texto pequeno: 14px (regular)
- Texto minúsculo: 12px (metadados, horários)

**Legibilidade:**
- Altura de linha: 1,5 (150%)
- Espaçamento entre letras: 0,02em
- Largura máxima de linha: 80 caracteres (documentos)
- Alto contraste (conforme WCAG AAA)

---

## 7.7 Layout Responsivo

### Desktop (alvo principal)

**Resolução mínima:** 1280x720  
**Resolução ideal:** 1920x1080  
**Tamanho máximo da janela interna:** 1600x900

**Layout:**
- 4 ícones de app centralizados na área de trabalho
- Janelas abrem em 800x600 (padrão)
- Redimensionáveis até 1600x900
- Barra de tarefas com 60px de altura

### Tablet/Mobile (consideração futura)

**Fora do MVP, mas considerar:**
- Exibição de um app por vez (sem desktop)
- Apps em tela cheia
- Gestos de deslizar para alternar
- Controles otimizados para toque

**Observação:** Desktop é prioridade. Mobile pode vir pós-lançamento.

---

## 7.8 Recursos de Acessibilidade

### Navegação por teclado

**Atalhos globais:**
- `Alt+1` - Abrir Email
- `Alt+2` - Abrir Arquivos do Caso
- `Alt+3` - Abrir Laboratório Forense
- `Alt+4` - Abrir Enviar Solução
- `Alt+Tab` - Alternar entre janelas abertas
- `Escape` - Fechar janela ativa
- `F11` - Alternar tela cheia

**Dentro das janelas:**
- `Tab` - Navegar entre elementos
- `Enter` - Ativar botão/link
- `Space` - Alternar checkbox
- `Setas` - Navegar em listas
- `Page Up/Down` - Rolar documentos

### Suporte a leitores de tela

**Rótulos ARIA:**
- Todos os elementos interativos rotulados
- Estrutura de documento (títulos, listas)
- Campos de formulário com descrições
- Atualizações de status anunciadas

**Exemplo:**
```html
<button aria-label="Ver Relatório Policial - Incidente 2023-0315, 3 páginas">
  Ver Documento
</button>
```

### Acessibilidade visual

**Modo alto contraste:**
- Razões de contraste elevadas
- Mínimo 7:1 (WCAG AAA)
- Opção em Configurações

**Escala de texto:**
- Texto de UI: 100%, 125%, 150%, 200%
- Zoom de documentos: controle independente
- Layout preservado em tamanhos maiores

**Daltonismo:**
- Não depender apenas de cor
- Usar ícones + cor
- Padrões para diferenciar estados

---

## 7.9 Fluxos de Usuário

### Fluxo para primeira vez

**1. Tutorial (Email)**
```
Abrir Email → Ler briefing do tutorial → Clicar em "Iniciar Treinamento"
```

**2. Caso de treinamento**
```
Abrir Arquivos do Caso → Ler 2 documentos → Ver evidências →
Enviar solução → Receber feedback de sucesso
```

**3. Primeiro caso real**
```
Notificação por email → Abrir Arquivos do Caso → Explorar documentos →
Solicitar perícias → Aguardar → Revisar resultados → Enviar solução
```

### Fluxo típico de investigação

**Início de novo caso:**
```
Notificação por email → Ler briefing → Abrir Arquivos do Caso →
Ler relatório policial → Revisar suspeitos
```

**Meio da investigação:**
```
Abrir Arquivos do Caso → Ler declarações → Ver evidências →
Abrir Laboratório Forense → Solicitar DNA → Fazer anotações →
Fechar e aguardar
```

**Conclusão do caso:**
```
Email: perícia pronta → Abrir Laboratório Forense → Ler laudo →
Atualizar notas → Abrir Enviar Solução → Preencher formulário → Enviar →
Ver resultado
```

---

## 7.10 Notificações & Feedback

### Notificações do sistema

**Badge no Email:**
```
[📧 ●] ← Ponto vermelho indica email não lido
```

**Toast (canto inferior direito):**
```
┌────────────────────────────────────┐
│ ✓ Laudo Forense Disponível        │
│ EV-001 Análise Balística          │
│ [Ver agora] [Dispensar]           │
└────────────────────────────────────┘
```

**Tipos:**
- Novo caso designado
- Laudo forense pronto
- Solução enviada
- Promoção de patente

### Feedback in-app

**Estados de carregamento:**
```
Enviando solução...
▓▓▓▓▓▓▓▓▓░░░ 75%
```

**Mensagens de sucesso:**
```
✓ Caso Resolvido!
Você identificou corretamente o culpado.
```

**Mensagens de erro:**
```
✗ Submissão Incompleta
Explique o motivo (mínimo 50 palavras)
```

---

## 7.11 Configurações & Preferências

### Menu de configurações

**Acesso:** Ícone de engrenagem na barra de tarefas

**Painel de configurações:**
```
┌────────────────────────────────────────────────┐
│ ⚙️ Configurações                   [_][□][X]  │
├────────────────────────────────────────────────┤
│ [Exibição] [Áudio] [Perícias] [Acessibilidade] │
├────────────────────────────────────────────────┤
│                                                 │
│ EXIBIÇÃO                                        │
│                                                 │
│ Tema:                                           │
│ ○ Escuro (Padrão)                               │
│ ○ Claro                                         │
│ ○ Alto Contraste                                │
│                                                 │
│ Escala da UI:                                   │
│ ◉ 100%  ○ 125%  ○ 150%  ○ 200%                 │
│                                                 │
│ Animações de janela:                            │
│ [■] Ativar (abrir/fechar suave)                 │
│                                                 │
│ ────────────────────────────────────────────    │
│                                                 │
│ [Aplicar] [Cancelar]                            │
└────────────────────────────────────────────────┘
```

**Configurações de perícia:**
```
TEMPORIZAÇÃO DAS PERÍCIAS

Modo de tempo:
◉ Tempo real (Padrão)
  DNA: 24 horas, Balística: 12 horas etc.
  Progresso continua com o jogo fechado.

○ Acelerado (1 hora = 1 minuto)
  DNA: 24 minutos, Balística: 12 minutos
  Para ritmo mais rápido.

○ Instantâneo (Modo História)
  Todas as análises concluem imediatamente.
  Desativa progressão de patentes.
```

**Configurações de acessibilidade:**
```
ACESSIBILIDADE

Visual:
[■] Modo de Alto Contraste
[■] Reduzir Movimento
[■] Suporte a Leitor de Tela

Entrada:
[■] Dicas de Navegação por Teclado
[ ] Suporte a Teclas de Aderência

Leitura:
Tamanho da fonte: [▼ 16px (Padrão)]
Fonte: [▼ Padrão]
Espaçamento entre linhas: [▼ 1,5x]
```

---

## 7.12 Carregamento & Transições

### Telas de carregamento

**Inicialização do jogo:**
```
┌────────────────────────────────────────────────┐
│                                                 │
│                                                 │
│                  CaseZero                       │
│              Divisão de Casos Arquivados        │
│                                                 │
│             Carregando estação...               │
│             ▓▓▓▓▓▓▓░░░░░░░ 52%                 │
│                                                 │
│                                                 │
└────────────────────────────────────────────────┘
```

**Carregamento do caso:**
```
Carregando Arquivos do Caso...
▓▓▓▓▓▓▓▓▓▓▓▓ 100%

Preparando documentos... ✓
Carregando evidências... ✓
Verificando perícias... ✓
```

### Transições

**Abrir/fechar janelas:**
- Fade + escala suave (200 ms)
- Opcional (desativável nas configurações)

**Troca de aplicativo:**
- Instantânea (sem animação)
- Janela atual vai para o fundo

**Navegação de página:**
- Rolagem suave (100 ms)
- Mantém posição ao retornar

---

## 7.13 Estados de Erro & Casos-limite

### Problemas de conexão

**Modo offline:**
```
⚠️ Conexão perdida

Você está offline. Alguns recursos ficam indisponíveis:
- Solicitações de perícia
- Envio de solução
- Sincronização de perfil

Você ainda pode:
- Ler arquivos do caso
- Fazer anotações
- Ver perícias concluídas

[Tentar reconectar] [Continuar offline]
```

### Erros de dados

**Arquivos do caso ausentes:**
```
✗ Erro ao carregar o caso

Não foi possível carregar os arquivos. Possíveis motivos:
- Dados salvos corrompidos
- Problema no servidor
- DLC ausente

[Reportar problema] [Voltar ao painel] [Tentar novamente]
```

### Erros do usuário

**Submissão incompleta:**
```
⚠️ Submissão Incompleta

Faltam informações obrigatórias:
- Explicação do motivo com menos de 50 palavras (atual: 32)
- Selecione ao menos uma evidência

[Voltar] [Cancelar]
```

---

## 7.14 Painel & Seleção de Casos

### Painel principal

**Após login:**
```
┌──────────────────────────────────────────────────┐
│ CaseZero - Divisão de Casos Arquivados          │
├──────────────────────────────────────────────────┤
│                                                  │
│ Bem-vindo de volta, Detetive!                    │
│                                                  │
│ Patente: Detetive Líder ⭐                        │
│ XP: 9.450 / 12.000  ▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓░░░░ 79%    │
│                                                  │
│ ──────────────────────────────────────────────── │
│                                                  │
│ CASOS ATIVOS (2)                                 │
│                                                  │
│ CASE-2024-015: A Conspiração do Porto            │
│ Dificuldade: Especialista • 6,2 horas • 45%      │
│ [Continuar]                                      │
│                                                  │
│ CASE-2024-014: O Roubo do Museu                  │
│ Dificuldade: Difícil • 2,1 horas • 20%           │
│ [Continuar]                                      │
│                                                  │
│ ──────────────────────────────────────────────── │
│                                                  │
│ [Explorar Novos Casos] [Ver Perfil] [Configurações] │
│                                                  │
└──────────────────────────────────────────────────┘
```

### Navegador de casos

**Explorar casos disponíveis:**
```
┌──────────────────────────────────────────────────┐
│ Explorar Casos                                   │
├──────────────────────────────────────────────────┤
│ Filtro: [Todos ▼] [Fácil] [Médio] [Difícil] [Especialista] │
│ Ordenar: [Mais Recentes ▼]                       │
├──────────────────────────────────────────────────┤
│                                                  │
│ CASE-2024-016: O Cálice Envenenado               │
│ Dificuldade: Especialista • Est. 10-12 horas     │
│ Suspeitos: 9 • Documentos: 28 • Evidências: 14   │
│ "Um colecionador de vinhos morre misteriosamente │
│ em um jantar. Foi assassinato ou acidente?"      │
│ [Iniciar Caso]                                   │
│                                                  │
│ CASE-2024-015: A Conspiração do Porto            │
│ Dificuldade: Especialista • Est. 8-10 horas      │
│ Suspeitos: 8 • Documentos: 24 • Evidências: 12   │
│ [Continuar] (Em andamento)                       │
│                                                  │
│ CASE-2024-014: O Roubo do Museu                  │
│ Dificuldade: Difícil • Est. 6-8 horas            │
│ Suspeitos: 6 • Documentos: 18 • Evidências: 10   │
│ [Continuar] (Em andamento)                       │
│                                                  │
└──────────────────────────────────────────────────┘
```

---

## 7.15 Animações & Acabamento

### Animações sutis

**Ativas por padrão (podem ser desativadas):**
- Fade ao abrir/fechar janelas (200 ms)
- Destaque ao passar o mouse em botões
- Rolagem suave
- Animação de preenchimento de barra de progresso
- Notificações deslizando para dentro

**Sem animações:**
- Transições de página (instantâneas)
- Spinners de carregamento (usar barra estática)
- Efeitos decorativos
- Paralaxe ou fundos em movimento

### Detalhes de acabamento

**Microinterações:**
- Feedback de clique (leve escala)
- Estado hover (realce sutil)
- Estado ativo (borda diferenciada)
- Indicador de foco (contorno azul)

**Efeitos sonoros (opcionais, desligados por padrão):**
- Abrir/fechar janelas (som suave)
- Clique em botão (mínimo)
- Notificação (sino discreto)
- Caso resolvido (tom satisfatório)

**Controle de volume:**
- Master: 0-100%
- Sons de UI: 0-100%
- Opção de desativar completamente

---

## 7.16 Considerações de Performance

### Metas de otimização

**Tempos de carregamento:**
- Inicializar app: <3 segundos
- Carregar caso: <2 segundos
- Abrir documento: <500 ms
- Abrir foto de evidência: <1 segundo

**Responsividade:**
- Interações de UI: <100 ms
- Arrastar janelas: 60 FPS
- Rolagem: suave a 60 FPS
- Sem travamentos ou engasgos

### Carregamento de assets

**Lazy Loading:**
- Documentos carregam ao abrir (não todos de uma vez)
- Fotos de evidência carregam sob demanda
- Páginas de PDF renderizadas conforme necessário
- Carregamento antecipado para documentos prováveis

**Cache:**
- Documentos vistos recentemente em cache
- Fotos de evidência em cache
- Metadados do caso em cache
- Limpa cache ao alternar de caso

---

## 7.17 Considerações por Plataforma

### Windows

**Integração:**
- Controles nativos de janela
- Integração com barra de tarefas
- Atalhos padrão do Windows
- Acesso ao sistema de arquivos (exportações)

### macOS

**Integração:**
- Moldura de janela nativa
- Integração com Dock
- Atalhos macOS (Cmd em vez de Ctrl)
- Suporte à Touch Bar (se aplicável)

### Linux

**Integração:**
- Decorações padrão de janela
- Integração com ambientes desktop
- Atalhos padrão

### Web (se aplicável)

**Restrições do navegador:**
- API de tela cheia para imersão
- Armazenamento local para saves
- Service worker para offline
- Sem moldura nativa (usar customizada)

---

## 7.18 Resumo

**Filosofia de UI:**
- **Metáfora de desktop** para familiaridade
- **Design orientado ao conteúdo** (UI serve aos documentos)
- **Estética profissional** (tema escuro, tipografia limpa)
- **Acessibilidade intrínseca** (teclado, leitor de tela, alto contraste)

**Aplicativos centrais:**
1. **Email** - Briefings e notificações
2. **Arquivos do Caso** - Documentos, evidências, notas, linha do tempo
3. **Laboratório Forense** - Solicitar análises, ver laudos
4. **Enviar Solução** - Submissão final do caso

**Design visual:**
- Tema escuro (#1a1a1a de fundo, #4a9eff de destaque)
- Fonte Inter para UI, Georgia para documentos
- Alto contraste (WCAG AAA)
- Animações mínimas

**Experiência do usuário:**
- Gerenciamento de janelas (minimizar, maximizar, fechar)
- Atalhos de teclado em toda a experiência
- Progresso de perícias em tempo real
- Feedback claro e notificações
- Performance responsiva

---

**Próximo capítulo:** [08-TECNICO.md](08-TECNICO.md) – Arquitetura do sistema e implementação

**Documentos relacionados:**
- [03-MECANICAS.md](03-MECANICAS.md) – Implementação mecânica da UI
- [09-ESQUEMA-DE-DADOS.md](09-ESQUEMA-DE-DADOS.md) – Estruturas de dados por trás da UI
- [11-TESTES.md](11-TESTES.md) – Testes de UI e usabilidade

---

**Histórico de revisões:**

| Data | Versão | Mudanças | Autor |
|------|--------|----------|-------|
| 13/11/2025 | 1.0 | Tradução completa para PT-BR | Assistente de IA |
