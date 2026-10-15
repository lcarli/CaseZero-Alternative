# Capítulo 03 - Mecânicas

**Documento de Design de Jogo - CaseZero v3.0**  
**Última atualização:** 13 de novembro de 2025  
**Status:** ✅ Completo

---

## 3.1 Visão Geral

Este capítulo detalha os **sistemas e mecânicas específicos** que sustentam a jogabilidade de CaseZero. Cada mecânica foi criada para apoiar os pilares centrais (Autenticidade, Autonomia, Análise, Paciência) enquanto mantém uma experiência investigativa realista.

**Sistemas centrais:**
1. Sistema de Visualização de Documentos
2. Sistema de Exame de Evidências
3. Sistema de Solicitação de Perícias
4. Sistema de Anotações
5. Sistema de Linha do Tempo
6. Sistema de Submissão da Solução
7. Gerenciamento de Sessões do Caso
8. Sistema de Progressão do Detetive

---

## 3.2 Sistema de Visualização de Documentos

O principal mecanismo de interação: ler documentos da investigação.

### Tipos de Documento

**1. Relatórios Policiais**
- **Formato:** PDF, 2-5 páginas
- **Conteúdo:** Relatório oficial do incidente, descrição da cena, achados iniciais
- **Estrutura:** Cabeçalho (data, oficial, nº do caso), narrativa, registro de evidências
- **Disponibilidade:** Sempre disponível desde o início do caso
- **Exemplo:** "Prédio Comercial do Centro - Relatório de Homicídio nº 2023-0315"

**2. Declarações de Testemunhas**
- **Formato:** PDF, 1-3 páginas
- **Conteúdo:** Entrevista transcrita (perguntas e respostas) ou declaração escrita
- **Estrutura:** Cabeçalho (nome da testemunha, data), corpo do depoimento, assinatura
- **Disponibilidade:** Disponível desde o início (na maioria dos casos)
- **Exemplo:** "Depoimento de João Silva, Vigia Noturno"

**3. Entrevistas com Suspeitos**
- **Formato:** PDF, 2-4 páginas
- **Conteúdo:** Transcrição de entrevista com o suspeito
- **Estrutura:** Cabeçalho, formato perguntas e respostas, anotações do entrevistador
- **Disponibilidade:** Disponível desde o início
- **Exemplo:** "Transcrição de Entrevista - Michael Torres, 16/03/2023"

**4. Laudos Forenses**
- **Formato:** PDF, 2-3 páginas
- **Conteúdo:** Resultados de análises técnicas
- **Estrutura:** Cabeçalho do laboratório, metodologia, achados, conclusões
- **Disponibilidade:** Apenas após solicitar e aguardar
- **Exemplo:** "Laudo de Análise de DNA - Evidência #EV-004"

**5. Documentos Pessoais**
- **Formato:** PDF ou imagem, 1-2 páginas
- **Conteúdo:** Cartas, e-mails, entradas de diário, bilhetes
- **Estrutura:** Variável (formato pessoal)
- **Disponibilidade:** Encontrados entre os pertences da vítima (disponíveis desde o início)
- **Exemplo:** "Troca de e-mails entre vítima e suspeito"

**6. Registros Financeiros**
- **Formato:** PDF, 1-2 páginas
- **Conteúdo:** Extratos bancários, logs de transações
- **Estrutura:** Tabelas com datas e valores
- **Disponibilidade:** Obtidos durante a investigação (disponíveis desde o início)
- **Exemplo:** "Extrato Bancário - Robert Chen, janeiro-março de 2023"

**7. Registros de Antecedentes**
- **Formato:** PDF, 1 página
- **Conteúdo:** Histórico criminal, registros de emprego, registros médicos
- **Estrutura:** Estilo de impressão de banco de dados
- **Disponibilidade:** Disponíveis desde o início
- **Exemplo:** "Consulta de Antecedentes Criminais - Michael Torres"

### Interface de Visualização

A leitura acontece dentro do aplicativo **File Viewer**, que já existe no desktop do jogador. Não criamos uma nova UI específica para PDFs; apenas abrimos o arquivo na janela padrão de arquivos, garantindo foco total no conteúdo.

**Layout essencial do File Viewer:**

```text
┌─────────────────────────────────────────────┐
│  File Viewer · Relatório Policial 2023-0315 │
├─────────────────────────────────────────────┤
│  Página 1 / 3      [Ajustar] [100%] [150%]  │
├─────────────────────────────────────────────┤
│                                             │
│        CONTEÚDO DO PDF RENDERIZADO          │
│                                             │
│   (scroll vertical padrão, fundo neutro)    │
│                                             │
└─────────────────────────────────────────────┘
```

**Recursos disponíveis (todos herdados do File Viewer):**

- Scroll vertical/inércia para passar páginas (ou Page Up/Down)
- Botões simples de zoom: Ajustar à largura, 100% e 150%
- Renderização nativa do PDF com antialiasing e fundo cinza-claro
- Copiar texto ou arrastar imagens diretamente para as anotações
- Modo escuro/claro seguindo a configuração global do jogo

**Não adicionamos:**

- Favoritos, marcações, busca, impressão ou qualquer HUD extra
- Indicadores automáticos de pistas ou resumos
- Sobreposição com dicas/contexto

> Objetivo: o jogador abre um documento no File Viewer, lê confortavelmente e volta aos outros aplicativos quando quiser. Mantemos o fluxo o mais próximo possível de "abrir um PDF no desktop" real.

### Metadados dos Documentos

Cada documento possui metadados ocultos (não exibidos ao jogador, mas que afetam a jogabilidade):

```json
{
  "id": "DOC-001",
  "type": "PoliceReport",
  "title": "Incident Report #2023-0315",
  "fileName": "police-report-2023-0315.pdf",
  "author": "Officer Sarah Martinez",
  "dateCreated": "2023-03-16T08:00:00Z",

  "tags": ["initial", "official", "scene"],
  "availableAt": "start",
  "relatedEvidence": ["EV-001", "EV-002"],
  "relatedSuspects": ["SUSP-001", "SUSP-002"],

  "contradicts": ["DOC-003"],
  "pageCount": 3,
  "importance": "critical"
}
```

**Observação:** Metadados servem apenas para design/testes. Jogadores descobrem relações lendo.

---

## 3.3 Sistema de Exame de Evidências

Visualização e análise de evidências físicas por meio de fotografias.

### Tipos de Evidência

**Evidência física:**
- Armas (armas de fogo, facas, objetos contundentes)
- Itens pessoais (carteiras, celulares, chaves)
- Roupas (manchadas de sangue, rasgadas etc.)
- Ferramentas (utilizadas no crime)

**Evidência biológica:**
- Amostras de sangue
- Fios de cabelo/fibras
- Fluidos corporais
- Tecidos

**Vestígios:**
- Impressões digitais
- Pegadas
- Marcas de pneus
- Lascas de tinta

**Evidência digital (futuro):**
- Registros telefônicos
- Arquivos de computadores
- Capturas de câmeras de segurança

**Documentos como evidência:**
- Bilhetes manuscritos
- Documentos falsificados
- Cartas de resgate
- Cartas ameaçadoras

### Apresentação das fotos de evidência

Cada evidência possui **apenas uma foto principal** em alta resolução. Nada de galerias com vários ângulos; o objetivo é ser direto como nos dossiês físicos que inspiram o jogo.

```text
┌─────────────────────────────────────────────┐
│ Evidência #EV-001: Arma de fogo (.38)       │
├─────────────────────────────────────────────┤
│                                             │
│        FOTO PRINCIPAL (com régua)           │
│                                             │
├─────────────────────────────────────────────┤
│ Tipo: Física - Arma                         │
│ Coletada: 16/03/2023 02h00                  │
│ Local: Cena do crime, perto da vítima       │
│ Coletada por: Equipe CSI Alpha              │
│                                             │
│ [🔬 Análises disponíveis: Balística,        │
│     Impressões digitais]                    │
│ [📋 Ver cadeia de custódia]                 │
└─────────────────────────────────────────────┘
```

Se o caso precisar mostrar um detalhe, ele aparece em um **recorte dentro do próprio documento** (por exemplo, no relatório policial). Mantemos o File Viewer livre de botões extras ou carrosséis.

### Metadados da evidência

```json
{
  "id": "EV-001",
  "name": "Firearm - .38 Caliber",
  "type": "Physical",
  "subtype": "Weapon",
  "description": "Smith & Wesson .38 Special revolver, found 3 feet from victim",
  "photos": [
    "evidence/ev001-overview.jpg",
    "evidence/ev001-closeup.jpg",
    "evidence/ev001-serial.jpg"
  ],
  "collectedBy": "CSI Team Alpha",
  "collectedAt": "2023-03-16T02:00:00Z",
  "collectedFrom": "Crime scene, 15th floor office",
  "chainOfCustody": ["CSI Alpha", "Evidence Room", "Forensics Lab"],
  "forensicAnalysisAvailable": [
    {
      "type": "Ballistics",
      "duration": 12,
      "durationUnit": "minutes"
    },
    {
      "type": "Fingerprints",
      "duration": 8,
      "durationUnit": "minutes"
    }
  ],
  "tags": ["weapon", "critical", "firearm"],
  "relatedEvidence": ["EV-002"],
  "availableAt": "start"
}
```

### Interação com a evidência

**O que os jogadores podem fazer:**
- ✅ Ver fotos em alta resolução
- ✅ Dar zoom e percorrer a foto arquivada única
- ✅ Ler a descrição da evidência
- ✅ Ver metadados de coleta
- ✅ Solicitar análise forense
- ✅ Confrontar com documentos

**O que os jogadores não podem fazer:**
- ❌ Girar modelo 3D (é foto, não modelo 3D)
- ❌ "Usar" evidência com outra evidência
- ❌ Coletar novas evidências
- ❌ Contaminar evidências
- ❌ Reexaminar na cena (objeto está no depósito)

---

## 3.4 Sistema de Solicitação de Perícias

A mecânica central baseada em tempo que cria ritmo e antecipação. No backend atual, isso é implementado como solicitações assíncronas baseadas em tempo decorrido, e não como trabalho laboratorial em streaming ao vivo.

### Tipos de análise forense

> **Mecânica de tempo acelerado (novo padrão):** todas as durações agora aparecem em minutos reais. Cada minuto corresponde a uma hora da cronologia do caso (ex.: 12 horas in-lore = 12 minutos reais de espera).

**Análise de DNA**
- **Duração acelerada:** 24 minutos (equivalem a 24h na cronologia do caso)
- **Aplicação em:** Sangue, cabelo, saliva, tecido
- **Resultados:** Perfil genético, possíveis correspondências com suspeitos/banco de dados
- **Custo:** Nenhum (solicitações ilimitadas)
- **Exemplo de resultado:** "Perfil de DNA corresponde a Michael Torres (99,7% de confiança)"

**Balística**
- **Duração acelerada:** 12 minutos (12h in-lore)
- **Aplicação em:** Armas de fogo, projéteis, cápsulas
- **Resultados:** Identificação da arma, trajetória, correspondência com projéteis
- **Exemplo de resultado:** "Projétil retirado da vítima foi disparado pela evidência #EV-001"

**Impressões digitais**
- **Duração acelerada:** 8 minutos (8h in-lore)
- **Aplicação em:** Impressões em superfícies, armas, objetos
- **Resultados:** Identificação das digitais, correspondências com suspeitos
- **Exemplo de resultado:** "Parcial encontrado na arma corresponde ao polegar direito de SUSP-002"

**Toxicologia**
- **Duração acelerada:** 36 minutos (36h in-lore)
- **Aplicação em:** Amostras de sangue, tecidos
- **Resultados:** Drogas, venenos, nível alcoólico
- **Exemplo de resultado:** "Toxicologia do sangue: 0,08% BAC, traços de sedativo"

**Análise de vestígios**
- **Duração acelerada:** 16 minutos (16h in-lore)
- **Aplicação em:** Fibras, cabelos, lascas de tinta
- **Resultados:** Identificação do material, possíveis origens
- **Exemplo de resultado:** "Fibra compatível com carpete do veículo do suspeito"

**Grafoscopia**
- **Duração acelerada:** 10 minutos (10h in-lore)
- **Aplicação em:** Documentos escritos
- **Resultados:** Identificação do autor, detecção de falsificação
- **Exemplo de resultado:** "Assinatura no documento provavelmente falsificada"

**Forense digital (futuro)**
- **Duração acelerada:** 48 minutos (48h in-lore)
- **Aplicação em:** Telefones, computadores, armazenamento
- **Resultados:** Arquivos excluídos, metadados, registros de comunicação

### Fluxo da solicitação

**Passo 1: Selecionar evidência**

```text
Laboratório Forense > Evidências disponíveis

EV-001: Arma de fogo (.38)
  [✓] Análise de balística (12 min)
  [✓] Impressões digitais (8 min)
  [ ] Solicitar análises selecionadas

EV-004: Amostra de sangue
  [✓] Análise de DNA (24 min)
  [✓] Toxicologia (36 min)
  [ ] Solicitar análises selecionadas
```

**Passo 2: Confirmar solicitação**

```text
┌─────────────────────────────────────────────┐
│ Confirmar solicitação de perícia            │
├─────────────────────────────────────────────┤
│ Evidência: EV-001 - Arma de fogo            │
│ Análise: Balística                          │
│ Duração: 12 minutos (equivalem a 12h in-lore)│
│                                             │
│ Previsão de conclusão: 17/03/2023 14h00     │
│  (em ~12 minutos reais)                     │
│                                             │
│ Observação: você pode continuar investigando│
│ enquanto aguarda o resultado.               │
│                                             │
│ [Cancelar] [Enviar solicitação]             │
└─────────────────────────────────────────────┘
```

**Passo 3: Período de espera**

```text
Laboratório Forense > Solicitações pendentes

EV-001 - Análise de balística
  Solicitada: 17/03/2023 02h00
  Status: Em andamento
  Conclusão: 17/03/2023 14h00 (restam 12 min reais / 12h in-lore)
  [Ver status]

EV-004 - Análise de DNA
  Solicitada: 17/03/2023 02h05
  Status: Em andamento
  Conclusão: 18/03/2023 02h05 (restam 24 min reais / 24h in-lore)
  [Ver status]
```

**Passo 4: Resultados disponíveis**

```text
Laboratório Forense > Análises concluídas

✓ EV-001 - Análise de balística
  Concluída: 17/03/2023 14h00
  [Ver laudo]  ← Abre o PDF com resultados

⏱ EV-004 - Análise de DNA
  Em andamento (restam 24 min reais / 24h in-lore)
```

Assim que o laboratório conclui uma perícia, o jogador recebe um **e-mail padrão do Laboratório Forense** (assunto "Laudo disponível", corpo curto com nome da evidência e link "Baixar relatório"). Ao clicar no link, o PDF é automaticamente copiado para o **File Viewer** dentro do jogo, abrindo o documento como se fosse qualquer outro arquivo digital do caso.

### Mecânicas de tempo

**Modo acelerado (padrão agora):**
- Interface mostra minutos reais equivalentes às horas in-lore (1 minuto = 1 hora na ficção)
- Análise de "12 horas" termina em 12 minutos reais
- Mantém a sensação procedural sem exigir espera longa
- Pode ser desativado nas configurações para quem quiser experiência prolongada

**Modo em tempo real (opcional):**
- Restaura o cronômetro original em horas reais
- Jogador pode fechar o jogo e voltar depois
- Progresso persiste no servidor
- Recomendado para sessões espaçadas ou streamings longos

**Modo instantâneo (acessibilidade):**
- Todas as análises terminam na hora
- Para jogadores com pouco tempo
- Rotulado claramente como "Modo História"
- Não concede progressão de patente nesse modo

### Múltiplas solicitações

**Processamento em paralelo:**
- ✅ É possível solicitar várias análises simultaneamente
- ✅ Cada uma possui seu próprio cronômetro
- ✅ Sem filas ou limites de slots
- ✅ Todas terminam no horário previsto

**Linha do tempo de exemplo:**

```text
02h00 in-lore (T+0 min reais)  - Solicitar Balística (12 min), DNA (24 min) e Impressões (8 min)
10h00 in-lore (T+8 min reais)  - Impressões prontas
14h00 in-lore (T+12 min reais) - Balística pronta
02h00 in-lore do dia seguinte (T+24 min reais) - DNA pronto
```

### Formato do laudo forense

**Estrutura do laudo (PDF):**
```
┌─────────────────────────────────────────────┐
│ LABORATÓRIO FORENSE METROPOLITANO           │
│ RELATÓRIO DE ANÁLISE BALÍSTICA              │
│                                             │
│ Caso: nº 2023-0315                          │
│ Evidência: EV-001 - Arma de fogo (.38)      │
│ Perito: Dr. James Chen, PhD                 │
│ Data: 17 de março de 2023                   │
│                                             │
│ ANÁLISES REALIZADAS:                        │
│ Exame da arma e comparação de projéteis     │
│                                             │
│ METODOLOGIA:                                │
│ Microscopia comparativa, análise de raiamento│
│                                             │
│ ACHADOS:                                    │
│ 1. Arma: Smith & Wesson .38 Special         │
│ 2. Número de série: [redigido]              │
│ 3. Projétil retirado da vítima (EV-002)     │
│    coincide com o raiamento de EV-001       │
│ 4. Resíduo de disparo presente no punho     │
│                                             │
│ CONCLUSÕES:                                 │
│ A evidência #EV-001 foi a arma usada no     │
│ disparo fatal. Correspondência de alta confiança.
│                                             │
│ [Assinatura: Dr. James Chen]                │
└─────────────────────────────────────────────┘
```

### Justificativa de design

**Por que perícias em tempo real?**
1. **Cria antecipação:** há algo pelo que esperar
2. **Imita a realidade:** perícias levam tempo de verdade
3. **Incentiva múltiplas sessões:** pontos naturais de pausa
4. **Evita spam:** não é possível solicitar tudo instantaneamente
5. **Dá peso:** torna o resultado mais significativo

**Por que solicitações ilimitadas?**
1. **Sem escassez artificial:** evita sensação "gamificada"
2. **Liberdade ao jogador:** peça o que achar importante
3. **Sem punição:** nenhuma penalidade por explorar
4. **Realista:** policiais podem solicitar quantas perícias precisarem

---

## 3.5 Sistema de Anotações

Ferramenta simples, porém essencial, para a análise conduzida pelo jogador.

### Interface do caderno

**Editor de texto simples:**
```
┌─────────────────────────────────────────────┐
│ Caderno do Detetive                          │
├─────────────────────────────────────────────┤
│ [Nova nota] [Notas do caso] [Teorias] [Perg.]│
├─────────────────────────────────────────────┤
│                                             │
│ Caso nº 2023-0315 - Minha investigação      │
│ ────────────────────────────────────────    │
│                                             │
│ SUSPEITOS:                                  │
│ - Michael Torres: sócio, disputa financeira,│
│   álibi fraco                               │
│ - Linda Chen: esposa, seguro de vida,       │
│   mas parece genuinamente abalada           │
│ - David Park: ex-funcionário, tinha acesso  │
│                                             │
│ LINHA DO TEMPO:                             │
│ 22h00 - Vítima entra no prédio (CFTV)       │
│ 23h15 - Torres visto entrando (registro)    │
│ 23h30 - Hora estimada da morte              │
│                                             │
│ PERGUNTAS:                                  │
│ - Álibi de Torres não fecha                 │
│ - Por que vítima estava lá tão tarde?       │
│ - Onde estão os US$ 500 mil faltantes?      │
│                                             │
└─────────────────────────────────────────────┘
```

**Recursos disponíveis:**
- ✅ Texto livre
- ✅ Salvamento automático a cada 30 segundos
- ✅ Persistência entre sessões
- ✅ Múltiplas notas
- ✅ Copiar/colar a partir dos documentos
- ✅ Formatação básica (negrito, itálico, listas)
- ✅ Exportar para arquivo de texto

**Recursos ausentes:**
- ❌ Sem preenchimento automático de pistas
- ❌ Sem sistema de sugestões
- ❌ Sem integração de destaque/marcação
- ❌ Sem resumo por IA
- ❌ Sem mapa de conexões (seria prescritivo demais)

### Organização das notas

**Sistema de abas:**
- **Notas do caso:** observações gerais
- **Suspeitos:** notas sobre cada pessoa
- **Evidências:** observações sobre evidências
- **Linha do tempo:** notas cronológicas
- **Perguntas:** dúvidas em aberto
- **Teoria:** hipótese atual

**Ou:** o jogador pode organizar como preferir (texto livre)

### Filosofia de design

**"Uma página em branco, não um template"**
- Oferece espaço para pensar
- Não impõe a forma de raciocinar
- Permite que o jogador crie o próprio método
- Anotar é algo pessoal

**Por que não oferecer recursos avançados?**
- Queremos que o jogador engaje mentalmente, não dependa da ferramenta
- As conexões devem acontecer na cabeça do jogador
- Sistematização excessiva reduz a sensação de descoberta
- Simplicidade mantém o foco no caso

---

## 3.6 Sistema de Linha do Tempo

Representação visual dos eventos do caso (a partir dos documentos).

### Visualização da linha do tempo

**Exibição cronológica:**
```
┌─────────────────────────────────────────────┐
│ Linha do tempo do caso - 15 de março de 2023 │
├─────────────────────────────────────────────┤
│                                             │
│ 22h00 ─────┐                                │
│            └─ Vítima entra no prédio        │
│               (CFTV, Registro de segurança) │
│                                             │
│ 22h30 ─────┐                                │
│            └─ Última vez vista com vida     │
│               (Testemunha: vigia noturno)   │
│                                             │
│ 23h15 ─────┐                                │
│            └─ Suspeito A entra no prédio    │
│               (Registro de segurança)       │
│                                             │
│ 23h30 ─────┐                                │
│            └─ Hora estimada da morte        │
│               (Laudo forense)               │
│                                             │
│ 23h45 ─────┐                                │
│            └─ Suspeito A sai do prédio      │
│               (CFTV)                        │
│                                             │
│ 00h30 ─────┐                                │
│            └─ Corpo encontrado              │
│               (Vigia de segurança)          │
│                                             │
└─────────────────────────────────────────────┘
```

### Fonte de dados da linha do tempo

**Preenchida automaticamente a partir de:**
- Timestamps dos documentos
- Horários de depoimentos
- Estimativas forenses
- Logs de segurança
- Dados de CFTV

**Jogadores não podem:**
- ❌ Adicionar eventos personalizados (dados precisam vir dos arquivos)
- ❌ Editar descrições de eventos
- ❌ Alterar horários

**Jogadores podem:**
- ✅ Ver eventos cronologicamente
- ✅ Filtrar por fonte (CFTV, testemunhas etc.)
- ✅ Clicar no evento para ver o documento de origem
- ✅ Identificar lacunas na linha do tempo

### Finalidade de design

**Por que incluir a linha do tempo:**
- Ajuda a visualizar a sequência de eventos
- Destaca lacunas (onde falta informação)
- Útil para encontrar conflitos de álibi
- Reduz sobrecarga de memorizar horários

**Por que mantê-la simples:**
- Não é um quebra-cabeça
- Apenas um auxílio visual
- Dados vêm dos documentos (sem info escondida)
- Análise continua nas mãos do jogador

---

## 3.7 Sistema de Submissão da Solução

O ápice de alto risco da investigação.

### Formulário de submissão

**Envio com múltiplas partes:**
```
┌─────────────────────────────────────────────┐
│ Enviar solução do caso                       │
├─────────────────────────────────────────────┤
│                                             │
│ QUEM COMETEU O CRIME?                       │
│ [Selecionar suspeito ▼]                     │
│ ├─ Michael Torres                           │
│ ├─ Linda Chen                               │
│ ├─ David Park                               │
│ └─ Outro/Desconhecido                       │
│                                             │
│ QUAL FOI O MOTIVO?                          │
│ ┌───────────────────────────────────────┐   │
│ │ Disputa financeira por US$ 500 mil.   │   │
│ │ Torres precisava do dinheiro e estava │   │
│ │ sendo removido da empresa...          │   │
│ └───────────────────────────────────────┘   │
│                                             │
│ COMO O CRIME FOI COMETIDO?                  │
│ ┌───────────────────────────────────────┐   │
│ │ Torres usou sua chave para entrar no  │   │
│ │ escritório à noite. Disparou o .38    │   │
│ │ coletado. Tentou simular um roubo...  │   │
│ └───────────────────────────────────────┘   │
│                                             │
│ EVIDÊNCIAS-CHAVE:                           │
│ [Selecionar evidências]                     │
│ ☑ EV-001 - Arma de fogo (balística confere) │
│ ☑ EV-004 - DNA (Torres na cena)             │
│ ☑ DOC-007 - Registros financeiros           │
│                                             │
│ Tentativas restantes: 3/3                   │
│                                             │
│ [Cancelar] [Enviar solução]                 │
└─────────────────────────────────────────────┘
```

### Sistema de validação

**Verificações automáticas:**
1. **Culpado selecionado?** (obrigatório)
2. **Motivo explicado?** (mínimo de 50 palavras)
3. **Método descrito?** (mínimo de 50 palavras)
4. **Ao menos uma evidência indicada?** (obrigatório)

**Se faltar algo:**
```
⚠️ Submissão incompleta
Informe:
- Explicação do motivo (mínimo 50 palavras)
- Pelo menos uma evidência de suporte

[Voltar]
```

### Avaliação da solução

**Comparação do lado do servidor:**

```typescript
function evaluateSolution(submission, correctSolution) {
  // 1. Verifica culpado
  const culpritCorrect = submission.culprit === correctSolution.culprit;
  
  // 2. Checa se citou evidência-chave
  const keyEvidenceCited = submission.evidence.some(e => 
    correctSolution.keyEvidence.includes(e)
  );
  
  // 3. Analisa qualidade da explicação (futuro: baseado em ML)
  const explanationLength = submission.motive.length + submission.method.length;
  const thoughtfulExplanation = explanationLength > 200;
  
  return {
    isCorrect: culpritCorrect,
    hadKeyEvidence: keyEvidenceCited,
    wasThoughtful: thoughtfulExplanation,
    score: calculateScore(culpritCorrect, keyEvidenceCited, thoughtfulExplanation)
  };
}
```

### Tela de feedback

**Solução correta:**
```
┌─────────────────────────────────────────────┐
│ ✓ CASO RESOLVIDO                            │
├─────────────────────────────────────────────┤
│                                             │
│ Excelente trabalho, Detetive!               │
│                                             │
│ Você identificou corretamente Michael Torres│
│ como culpado e demonstrou ótima habilidade  │
│ analítica ao conectar as evidências.        │
│                                             │
│ SUA ANÁLISE:                                │
│ • Identificou o culpado correto ✓           │
│ • Citou evidências-chave ✓                  │
│ • Explicou o motivo de forma sólida ✓       │
│                                             │
│ REGISTROS (servidor):                       │
│ • Tempo registrado: 18 min totais           │
│ • Casos resolvidos: 5/6 para Detetive I     │
│ • Status do caso: RESOLVIDO                 │
│                                             │
│ [Ver solução completa] [Próximo caso]       │
└─────────────────────────────────────────────┘
```

**Solução incorreta (ainda há tentativas):**
```
┌─────────────────────────────────────────────┐
│ ✗ INCORRETO                                 │
├─────────────────────────────────────────────┤
│                                             │
│ Sua conclusão não corresponde às evidências.│
│                                             │
│ FEEDBACK:                                   │
│ • O suspeito escolhido tem álibi sólido para│
│   o horário do crime.                       │
│ • Reexamine os laudos, especialmente o DNA. │
│ • A linha do tempo mostra discrepância nos  │
│   depoimentos – investigue mais.            │
│                                             │
│ Tentativas restantes: 2/3                   │
│                                             │
│ Vá com calma. Revise as evidências e tente  │
│ novamente quando estiver pronto.            │
│                                             │
│ [Voltar à investigação]                     │
└─────────────────────────────────────────────┘
```

**Falha (sem tentativas restantes):**
```
┌─────────────────────────────────────────────┐
│ CASO PERMANECE NÃO SOLUCIONADO              │
├─────────────────────────────────────────────┤
│                                             │
│ Todas as tentativas foram utilizadas.       │
│                                             │
│ Este caso era particularmente desafiador.   │
│ Você pode revisar a solução agora para      │
│ entender o que faltou ou retornar depois de │
│ resolver mais 2 casos.                      │
│                                             │
│ REGISTRO:                                   │
│ • Tempo registrado: 24 min (para referência) │
│ • Status do caso: NÃO RESOLVIDO (Revisado)  │
│                                             │
│ [Ver solução] [Voltar ao painel]            │
└─────────────────────────────────────────────┘
```

### Justificativa para limite de tentativas

**Por que 3 tentativas?**
1. **Evita chutes:** não é possível testar todos os suspeitos
2. **Adiciona peso:** torna o envio significativo
3. **Estimula rigor:** jogadores investigam totalmente antes de enviar
4. **Realista:** detetives precisam de evidências sólidas antes de acusar

**Por que não ilimitadas?**
- Incentivaria tentativa e erro em vez de dedução
- Removeria a tensão do envio
- Jogadores não levariam o processo a sério

**Por que não só 1 tentativa?**
- Punição excessiva para erros honestos
- Não permite aprendizado com o erro
- Geraria frustração

---

## 3.8 Gerenciamento de Sessões do Caso

Sistema de bastidores que controla o progresso do jogador.

### Dados de sessão

**O que é rastreado:**
```json
{
  "sessionId": "uuid-123",
  "userId": "user-456",
  "caseId": "CASE-2024-001",
  "startedAt": "2023-11-13T14:00:00Z",
  "lastAccessAt": "2023-11-13T15:30:00Z",
  "status": "Active",
  "progress": {
    "documentsRead": ["DOC-001", "DOC-002", "DOC-005"],
    "evidenceViewed": ["EV-001", "EV-004"],
    "forensicRequests": [
      {
        "id": "req-1",
        "evidenceId": "EV-001",
        "analysisType": "Ballistics",
        "requestedAt": "2023-11-13T14:30:00Z",
        "completedAt": "2023-11-14T02:30:00Z",
        "status": "Pending"
      }
    ],
    "notebookEntries": ["Suspect analysis", "Timeline notes"],
    "submissionAttempts": 0,
    "timeSpent": 5400
  }
}
```

**O que NÃO é rastreado:**
- ❌ Ordem de leitura dos documentos (sem ordem imposta)
- ❌ Tempo gasto em cada documento (sem métrica de velocidade)
- ❌ Movimento do mouse ou mapas de calor
- ❌ Número de visualizações de evidência (irrelevante)

### Sistema de salvamento

**Auto salvamento:**
- A cada 30 segundos
- Ao perder foco da janela
- Ao fechar o app
- Ao navegar para fora do caso

**Salvar manualmente:**
- Desnecessário (salva sempre)
- Sem "slots" (um save por caso por usuário)

**Retomar:**
- Abre exatamente onde parou
- Temporizadores de perícia continuam no servidor
- Anotações preservadas
- Posições das janelas são salvas (opcional)

---

## 3.9 Sistema de Progressão do Detetive

Avanço de longo prazo por meio de patentes.

### Estrutura de patentes

**Escada oficial de patente / dificuldade (7 níveis):** a base viva do produto usa a mesma progressão de sete níveis tanto para dificuldade dos casos quanto para patente do detetive. As promoções dependem de **resoluções corretas e avaliadas de forma cumulativa** — não de XP, nem de cotas por dificuldade.

1. **Rookie**
  - Patente inicial
  - Dificuldade de entrada
  - **Marco de promoção:** 0 resoluções corretas avaliadas

2. **Detetive**
  - Primeira promoção
  - **Marco de promoção:** 3 resoluções corretas avaliadas

3. **Detetive Sênior** (`Detective2`)
  - Investigador já consistente
  - **Marco de promoção:** 8 resoluções corretas avaliadas

4. **Sargento**
  - Investigador sênior de campo
  - **Marco de promoção:** 16 resoluções corretas avaliadas

5. **Tenente**
  - Acesso a casos avançados
  - **Marco de promoção:** 28 resoluções corretas avaliadas

6. **Capitão**
  - Acesso a casos de alta complexidade
  - **Marco de promoção:** 44 resoluções corretas avaliadas

7. **Comandante**
  - Patente máxima atualmente implementada
  - **Marco de promoção:** 65 resoluções corretas avaliadas

### Registro de casos e tempo

- Progressão não usa XP nem multiplicadores: **ela segue resoluções corretas avaliadas de forma cumulativa**.
- Ao enviar o relatório final, o backend registra o resultado do caso junto com:
  - `caseId`
  - `difficulty`
  - `resolvedAt`
  - `elapsedMinutes` (tempo total gasto naquele caso)
- Promoções são calculadas a partir das resoluções corretas avaliadas; resultados incorretos ou não avaliados não avançam a patente.
- O `elapsedMinutes` continua alimentando comparativos internos de tempo e resumos pós-caso.
- O servidor mantém um histórico por caso resolvido; não existe contador acumulado de XP.

### Fluxo de promoção

1. O jogador resolve um caso arquivado.
2. O sistema grava o tempo total e registra se aquela resolução contou como correta e avaliada.
3. O serviço de promoção compara o total cumulativo correto com os marcos ativos (3 / 8 / 16 / 28 / 44 / 65).
4. Se um marco for alcançado, a patente do perfil sobe imediatamente e o acesso a casos de patente superior pode ser liberado.
5. Apenas o histórico por caso + o tempo total permanecem salvos; não persistimos ledger de XP nem cotas por dificuldade.

### Benefícios das patentes

**O que as patentes desbloqueiam:**
- ✅ Acesso a casos mais difíceis (gating)
- ✅ Novas categorias de caso (quando existirem)
- ✅ Badge/título no perfil

**O que as patentes NÃO concedem:**
- ❌ Vantagens mecânicas
- ❌ Perícias mais rápidas
- ❌ Dicas melhores (não existem dicas)
- ❌ Tentativas extras de submissão
- ❌ Casos mais fáceis

**Filosofia:** patentes refletem maestria, não poder.

---

## 3.10 Sistema de Suspeitos

Como suspeitos são apresentados e investigados.

### Perfil do suspeito

**Informações exibidas:**
```
┌─────────────────────────────────────────────┐
│ PERFIL DO SUSPEITO: Michael Torres          │
├─────────────────────────────────────────────┤
│ [Foto: retrato profissional]                │
│                                             │
│ IDADE: 38                                   │
│ OCUPAÇÃO: Sócio da TechCorp                 │
│ RELACIONAMENTO: Sócio de negócios da vítima │
│                                             │
│ HISTÓRICO:                                  │
│ Torres cofundou a TechCorp com a vítima em │
│ 2018. Acionista minoritário (30%). Tensões  │
│ recentes sobre os rumos da empresa. MBA pela│
│ Universidade Estadual.                      │
│                                             │
│ MOTIVO:                                     │
│ Disputa financeira - devia US$ 500 mil à    │
│ vítima. Corria risco de perder suas ações.  │
│                                             │
│ ÁLIBI:                                      │
│ Alega estar em casa assistindo TV das 21h à │
│ meia-noite. Sem testemunhas.                │
│                                             │
│ ANTECEDENTES CRIMINAIS:                     │
│ Nenhum                                      │
│                                             │
│ TRANSCRIÇÃO DA ENTREVISTA: [Ver DOC-004]    │
│ EVIDÊNCIAS RELACIONADAS: [EV-001, EV-004]   │
└─────────────────────────────────────────────┘
```

### Diretrizes de quantidade de suspeitos

Diretriz ampla de design na escada atual de sete níveis de dificuldade:

- **Rookie:** 2-3 suspeitos
- **Detetive / Detetive Sênior:** 4-5 suspeitos
- **Sargento / Tenente:** 6-7 suspeitos
- **Capitão / Comandante:** 8+ suspeitos

### Pistas falsas

**Suspeitos inocentes devem:**
- Ser plausíveis como culpados
- Ter álibis fracos ou comportamento suspeito
- Ter algum vínculo com a vítima
- Possuir motivos aparentes

**Mas, por fim:**
- Evidências os inocentam
- Álibi se confirma após análise
- Motivo é menos sólido do que parecia

**Exemplo:**
- **Linda Chen (esposa):** Ganha com o seguro, mas DNA a coloca em casa durante o crime; CFTV confirma que não saiu.

---

## 3.11 Mecânicas do Tutorial

Onboarding mínimo que ensina o essencial.

### Estrutura do tutorial (enxuta)

**Tela 1: Boas-vindas (10 segundos)**
```
Bem-vindo à Divisão de Casos Arquivados

Você é um detetive investigando casos antigos.
Leia documentos, examine evidências, resolva crimes.

[Continuar]
```

**Tela 2: Tour pela área de trabalho (20 segundos)**
```
[Desktop exibido com 3 apps]

Esta é sua estação de trabalho. Você tem três ferramentas:

📧 E-MAIL - Briefings dos casos
📁 ARQUIVOS DO CASO - Documentos e evidências
🧪 LABORATÓRIO FORENSE - Solicitar perícias

Clique em E-MAIL para começar.
```

**Tela 3: Briefing do caso de treinamento (leitura)**
```
[Aplicativo de e-mail abre automaticamente]

Leia este briefing para conhecer seu primeiro caso.

[Briefing do caso tutorial exibido - caso simples de furto]
```

**Tela 4: Explorar arquivos (prompt)**
```
Agora abra ARQUIVOS DO CASO para examinar as evidências.

[Jogador abre o app e vê 2 documentos simples]
```

**Tela 5: Enviar solução (guiado)**
```
Você já viu as evidências. Hora de enviar sua conclusão.

Abra o APLICATIVO DE SUBMISSÃO e identifique o ladrão.

[Jogador envia e recebe feedback imediato]
```

**Tela 6: Conclusão (10 segundos)**
```
Treinamento concluído!

Você está pronto para casos reais.
Lembre-se: investigue a fundo antes de enviar.

[Iniciar primeiro caso real]
```

**Tempo total:** 3-5 minutos

### Descoberta pós-tutorial

**Descoberta das perícias:**
- Primeiro caso real traz evidência que claramente precisa de análise
- O app do laboratório ganha badge "Novo!"
- Ao clicar na evidência aparece o botão "Solicitar análise"
- Depois da primeira solicitação, o jogador entende a mecânica

**Sem mãozinha após o tutorial:**
- Sem pop-ups de dica
- Sem marcadores de objetivo
- Sem mensagens "você deveria fazer X agora"

---

## 3.12 Recursos de Qualidade de Vida

Pequenas mecânicas que melhoram a experiência.

### Sistema de favoritos
- Marcar documentos/evidências como "importantes"
- Acesso rápido a itens marcados
- Ferramenta pessoal de organização

### Função de busca
- Pesquisar palavras-chave em todos os documentos
- Encontrar menções a nomes, locais, itens
- Exibir resultados com contexto

### Painel de casos
- Visualizar todos os casos
- Filtrar por: status (ativo, resolvido, não resolvido), dificuldade
- Acompanhar estatísticas gerais

### Comparação de evidências
- Ver duas evidências lado a lado
- Útil para encontrar semelhanças/diferenças
- A análise continua sendo do jogador

### Impressão de documentos
- "Imprimir" documentos para pasta pessoal
- Permite consultar sem abrir o original
- Funciona como "salvar para depois"

### Gerenciamento de janelas
- Minimizar/maximizar apps
- Organizar janelas
- Lembrar posições entre sessões

---

## 3.13 Mecânicas de Acessibilidade

Garantindo que mais pessoas possam jogar.

### Acessibilidade visual
- Modo alto contraste
- Ajuste de tamanho de fonte (apenas na UI, não nos PDFs)
- Paleta compatível com daltonismo
- Suporte a leitores de tela na UI

### Acessibilidade de tempo
- Opções de velocidade das perícias (tempo real, acelerado, instantâneo)
- Pausar cronômetro de perícia
- Sem pressão de tempo em qualquer lugar

### Acessibilidade de leitura
- Texto para fala do navegador funciona nos documentos
- Fonte amigável para dislexia (UI)
- Linguagem clara e direta na UI

### Acessibilidade motora
- Navegação completa por teclado
- Sem inputs dependentes de tempo
- Sem necessidade de cliques rápidos
- Áreas clicáveis amplas

---

## 3.14 Antipadrões (o que evitamos)

Mecânicas deliberadamente NÃO incluídas:

### ❌ Sistema de dicas
**Motivo:** inviabiliza autonomia e dedução

### ❌ Lista de objetivos
**Motivo:** transforma a investigação em roteiro prescritivo

### ❌ Marcadores no mapa
**Motivo:** remove o desafio de "descobrir sozinho"

### ❌ Minijogos
**Motivo:** quebra imersão e a sensação de investigação autêntica

### ❌ Testes de habilidade/atributos
**Motivo:** a inteligência real do jogador é a "estatística"

### ❌ Gerenciamento de inventário
**Motivo:** todas as evidências estão no laboratório; nada de coletar/carregar

### ❌ Escassez de recursos
**Motivo:** perícias ilimitadas, sem sistema de dinheiro, sem limites artificiais

### ❌ Energia/estamina
**Motivo:** investigue o quanto quiser

### ❌ Missões diárias
**Motivo:** padrão de engajamento manipulativo

### ❌ Pressão social
**Motivo:** nada de "seu amigo resolveu mais rápido"

---

## 3.15 Tratamento de casos limite

Como os sistemas lidam com comportamentos incomuns:

### Jogador nunca solicita perícias
**Resposta do sistema:**
- ✅ Ainda pode enviar a solução
- ✅ Pode resolver sem perícias (se conseguir)
- ❌ Não recebe avisos insistentes
- 🎖️ Conquista "Intuição de Detetive" se acertar

### Jogador solicita tudo imediatamente
**Resposta do sistema:**
- ✅ Todas as solicitações são aceitas
- ⏱️ Todos os cronômetros rodam em paralelo
- Sem punições ou penalidades

### Jogador leva 6 meses para concluir
**Resposta do sistema:**
- ✅ Todo o progresso fica salvo
- ✅ Perícias foram concluídas há tempos
- 📝 Botão "Resumo do caso" para refrescar memória
- Sem penalidades por pausas longas

### Jogador copia nome do suspeito sem ler
**Resposta do sistema:**
- ⚠️ Campo de explicação obrigatório (evita chute cego)
- É preciso descrever motivo e método
- Sistema detecta copy/paste (futuro: sinalizar submissões suspeitas)

### Jogador abandona o tutorial
**Resposta do sistema:**
- ✅ Pode pular direto para os casos
- ✅ Documentação de ajuda disponível
- ❌ Nenhum tutorial forçado novamente

---

## 3.16 Métricas e Telemetria

O que medimos (com respeito ao jogador):

### Rastreadas anonimamente:
- ✅ Taxas de conclusão de casos
- ✅ Tempo médio até a solução
- ✅ Distribuição de tentativas de submissão
- ✅ Taxas de uso de perícias
- ✅ Contagem de visualizações de documentos (agregado)

### NÃO rastreadas:
- ❌ Tempo individual em cada documento
- ❌ Comportamentos exatos do jogador
- ❌ Movimentos/cliques do mouse
- ❌ Notas pessoais da investigação
- ❌ Qualquer coisa invasiva

### Finalidade:
- Balancear dificuldade
- Identificar casos confusos
- Melhorar o design
- NÃO para manipular engajamento

---

## 3.17 Resumo

**Mecânicas centrais:**
1. **Visualização de documentos** – ler PDFs, examinar fotos
2. **Perícias** – solicitar análises, aguardar resultados (tempo real ou acelerado)
3. **Anotações** – notas pessoais em texto livre
4. **Linha do tempo** – cronologia visual de eventos
5. **Solução** – enviar culpado + explicação (3 tentativas)
6. **Progressão** – Patentes sobem ao resolver casos; salvamos apenas o tempo total

**Princípios mecânicos:**
- 🎯 Autêntico (perícias realistas, sem gamificação)
- 🧠 Conduzido pelo jogador (sem mão segurando)
- 📚 Foco na análise (ler é a jogabilidade)
- ⏳ Paciente (tempo real gera antecipação)

**O que faz funcionar:**
- Mecânicas simples, conteúdo profundo
- Sem barreiras ou manipulação artificiais
- Respeito à inteligência do jogador
- Dedução acima de ação

---

**Próximo capítulo:** [04-ESTRUTURA-DE-CASO.md](04-ESTRUTURA-DE-CASO.md) - Construção detalhada dos casos

**Documentos relacionados:**
- [02-JOGABILIDADE.md](02-JOGABILIDADE.md) - Como as mecânicas geram a jogabilidade
- [07-INTERFACE-DO-USUARIO.md](07-INTERFACE-DO-USUARIO.md) - Como as mecânicas são apresentadas
- [09-ESQUEMA-DE-DADOS.md](09-ESQUEMA-DE-DADOS.md) - Estruturas de dados das mecânicas

---

**Histórico de revisões:**

| Data | Versão | Mudanças | Autor |
|------|--------|----------|-------|
| 13/11/2025 | 1.0 | Tradução completa para PT-BR | Assistente de IA |
