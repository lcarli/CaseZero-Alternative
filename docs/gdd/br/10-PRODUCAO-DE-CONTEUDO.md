# Capítulo 10 - Pipeline de Conteúdo & Produção

**Documento de Design de Jogo - CaseZero v3.0**  
**Última Atualização:** 14 de novembro de 2025  
**Status:** ✅ Completo

---

## 10.1 Visão Geral

Este capítulo define o **workflow de criação de casos, os sistemas de gestão de conteúdo e os processos de produção** para o CaseZero v3.0. Ele cobre todo o pipeline, do conceito ao caso publicado.

> **Status atual do repositório:** a geração de casos já está implementada em `functions/CaseGen.Functions/Services/CaseV2/` como um pipeline de Durable Functions que emite bundles canônicos de `case.json` v2 diretamente. O workflow editorial abaixo deve ser lido como orientação de design de conteúdo em torno desse gerador implementado, não como afirmação de que a geração ainda é apenas manual.

**Conceitos-chave:**

- Workflow editorial estruturado em torno do gerador implementado
- Fases de geração durável com gates de validação
- Gestão de assets e publicação ordenada
- Versionamento e aprovação de conteúdo
- Estratégia de localização implementada e planejada

---

## 10.2 Papéis do Time de Conteúdo

### Definição de papéis

#### Case Writer (Roteirista de Casos)

- **Responsabilidades:**
  - Desenvolver conceitos de caso
  - Escrever narrativas do caso
  - Criar documentos (relatórios policiais, depoimentos, entrevistas)
  - Elaborar a solução e a lista de evidências-chave
- **Habilidades necessárias:**
  - Escrita criativa
  - Conhecimento do gênero mistério/crime
  - Atenção aos detalhes
  - Capacidade de pesquisa

#### Evidence Designer (Designer de Evidências)

- **Responsabilidades:**
  - Projetar itens de evidência física
  - Criar descrições de evidência
  - Coordenar com o Forensic Writer
  - Especificar requisitos de fotografia das evidências
- **Habilidades necessárias:**
  - Conhecimento básico de perícia
  - Habilidades de pesquisa
  - Pensamento lógico
  - Orientação a detalhes

#### Forensic Writer (Redator Forense)

- **Responsabilidades:**
  - Escrever laudos de análise forense
  - Garantir precisão científica
  - Definir tipos de análise forense por evidência
  - Revisar a consistência do conteúdo forense
- **Habilidades necessárias:**
  - Escrita científica
  - Conhecimento de ciências forenses
  - Capacidade de pesquisa
  - Precisão técnica

#### Document Designer (Designer de Documentos)

- **Responsabilidades:**
  - Diagramar documentos em PDF
  - Criar cabeçalhos, formulários e templates
  - Garantir consistência visual
  - Aplicar as diretrizes de branding
- **Habilidades necessárias:**
  - Design gráfico
  - Adobe InDesign/Illustrator
  - Conhecimento de tipografia
  - Criação de templates

#### QA Tester (Testador de QA)

- **Responsabilidades:**
  - Resolver casos às cegas
  - Verificar se as pistas estão presentes
  - Checar contradições
  - Testar se a dificuldade é adequada
  - Reportar problemas
- **Habilidades necessárias:**
  - Pensamento analítico
  - Atenção aos detalhes
  - Leitura crítica
  - Documentação

#### Content Manager (Gerente de Conteúdo)

- **Responsabilidades:**
  - Distribuir casos entre os roteiristas
  - Acompanhar o status de produção
  - Revisar entregas
  - Aprovar casos finais
  - Coordenar o time
- **Habilidades necessárias:**
  - Gestão de projetos
  - Julgamento editorial
  - Comunicação
  - Organização

---

## 10.3 Workflow de Criação de Casos

### Fase 1: Desenvolvimento do conceito (2-3 dias)

**Etapa 1.1: Conceito inicial**

- Roteirista propõe a ideia do caso (pitch de 1 parágrafo)
- Define tipo de crime, ambientação e premissa básica
- Identifica um gancho exclusivo ou elemento interessante
- **Entrega:** Documento de conceito (200-300 palavras)

**Etapa 1.2: Revisão do conceito**

- Content Manager avalia originalidade
- Confere contra casos existentes (evitar repetição)
- Verifica viabilidade da dificuldade pretendida
- **Decisão:** Aprovar, Revisar ou Rejeitar

**Etapa 1.3: Outline do caso**

- Roteirista expande para um outline completo
- Define vítima, 4-6 suspeitos e solução
- Identifica 10-15 evidências-chave
- Esboça a linha do tempo dos eventos
- **Entrega:** Outline do caso (1000-1500 palavras)

**Etapa 1.4: Aprovação do outline**

- Content Manager + Lead Writer revisam
- Checam consistência lógica
- Verificam se o caso é solucionável
- Garantem ausência de furos de roteiro
- **Decisão:** Aprovar para prosseguir ou solicitar revisões

---

### Fase 2: Pesquisa & planejamento (1-2 dias)

**Etapa 2.1: Pesquisa**

- Roteirista pesquisa tópicos relevantes:
  - Procedimentos de cena do crime
  - Capacidades forenses
  - Processos legais
  - Detalhes da ambientação (se localização específica)
  - Precisão histórica (se ambientado em outra época)
- **Entrega:** Documento de notas de pesquisa

**Etapa 2.2: Planejamento de evidências**

- Evidence Designer colabora com o roteirista
- Finaliza a lista de evidências (8-25 itens)
- Determina análises forenses disponíveis (3-8)
- Relaciona evidências às pistas
- **Entrega:** Planilha de manifesto de evidências

**Etapa 2.3: Planejamento de documentos**

- Lista todos os documentos necessários (8-25)
- Atribui tipos de documento:
  - Relatórios policiais (2-4)
  - Depoimentos de testemunhas (3-6)
  - Entrevistas de suspeitos (2-4)
  - Relatórios forenses (3-8)
  - Registros financeiros (0-3)
  - Cartas/Miscelânea (0-3)
- **Entrega:** Lista de documentos com tipos e autores

---

### Fase 3: Escrita (5-7 dias)

**Etapa 3.1: Desenvolvimento de personagens**

- Escrever perfis detalhados dos suspeitos
- Definir background da vítima
- Criar motivações dos personagens
- Elaborar estilos de fala para cada personagem
- **Entrega:** Dossiês de personagens

**Etapa 3.2: Construção da linha do tempo**

- Montar linha do tempo detalhada (10-30 eventos)
- Posicionar o crime como ponto central
- Adicionar álibis dos suspeitos
- Incluir observações de testemunhas
- Garantir consistência cronológica
- **Entrega:** Planilha de linha do tempo

**Etapa 3.3: Documentos – rodada 1**

- Escrever relatórios policiais (cena inicial, follow-up)
- Redigir depoimentos de testemunhas (3-6)
- Criar transcrições de entrevistas (2-4)
- **Entrega:** Primeiro lote de documentos (8-12)

**Etapa 3.4: Descrições de evidência**

- Escrever descrições detalhadas para cada evidência
- Incluir dados de coleta (local, hora, oficial)
- Notar características visíveis
- **Entrega:** Documento de descrições de evidência

**Etapa 3.5: Documentos – rodada 2**

- Escrever relatórios forenses (em colaboração com Forensic Writer)
- Criar registros financeiros, se aplicável
- Redigir cartas, e-mails e outros documentos
- **Entrega:** Segundo lote de documentos (5-10)

**Etapa 3.6: Revisão de posicionamento de pistas**

- Mapear todas as pistas para a solução
- Verificar presença de pistas críticas (3-5)
- Garantir clareza das pistas importantes (5-8)
- Distribuir pistas de apoio (8-12)
- Adicionar falsas pistas (5-10)
- **Entrega:** Planilha de matriz de pistas

---

### Fase 4: Produção de assets (3-4 dias)

**Etapa 4.1: Design de documentos**

- Document Designer diagrama todos os documentos
- Aplica templates (relatórios, depoimentos etc.)
- Adiciona cabeçalhos, logos e assinaturas
- Cria artefatos realistas (café, notas manuscritas opcionais)
- **Entrega:** PDFs de todos os documentos (8-25)

**Etapa 4.2: Fotografia de evidências**

- Identificar evidências que precisam de fotos (10-40)
- Coordenar sessões de fotos ou buscar imagens em stock
- Capturar múltiplos ângulos (2-4 por item)
- Garantir alta resolução (mínimo 1920×1080)
- **Entrega:** Biblioteca de fotos de evidência

**Etapa 4.3: Edição de fotos**

- Editar fotos para consistência
- Adicionar etiquetas/selos de evidência
- Ajustar iluminação e contraste
- Exportar em formatos otimizados (JPEG/PNG)
- **Entrega:** Fotos de evidência editadas

**Etapa 4.4: Design de relatórios forenses**

- Diagramar relatórios forenses em PDF
- Usar templates de laboratório
- Incluir fotos de evidência nos relatórios
- Adicionar diagramas científicos quando necessário
- **Entrega:** PDFs de relatórios forenses (3-8)

**Etapa 4.5: Assets suplementares**

- Criar diagramas de cena (plantas, mapas)
- Produzir fotos de perfil dos suspeitos (quando originais)
- Gerar foto da vítima
- **Entrega:** Assets visuais suplementares

---

### Fase 5: Montagem do case.json (1 dia)

**Etapa 5.1: Entrada de dados**

- Compilar o conteúdo aprovado nas estruturas canônicas do **case.json v2**
- Inserir metadata (ID, título, tier de dificuldade/patente, locale etc.)
- Preencher vítima, crime, suspeitos, linha do tempo e solução conforme a spec v2
- Evitar nomes de campo deprecated de v0/v1, como `documents[]`, `unlockLogic` ou `forensicReports[]`, como estruturas canônicas
- **Entrega:** Bundle rascunho de `case.json` v2

**Etapa 5.2: Validação**

- Validar contra a [CASE JSON v2 spec](../../CASE_JSON_V2_SPEC.md) e o [JSON Schema](../../../schemas/case.schema.json)
- Checar integridade referencial e consistência entre entidades
- Verificar os gates de locale, evidência, desfecho forense e prova/lógica
- Testar caminhos de arquivos para os assets
- **Entrega:** Bundle validado de `case.json` v2

**Etapa 5.3: Organização de assets**

```
cases/CASE-YYYY-###/
├── case.json
├── documents/
│   ├── police-report-001.pdf
│   ├── witness-statement-001.pdf
│   ├── suspect-interview-001.pdf
│   └── ... (8-25 total)
├── evidence/
│   ├── ev001-1.jpg
│   ├── ev001-2.jpg
│   ├── ev002-1.jpg
│   └── ... (10-40 total)
├── forensics/
│   ├── dna-ev004.pdf
│   ├── ballistics-ev001.pdf
│   └── ... (3-8 total)
├── suspects/
│   ├── susp001.jpg
│   ├── susp002.jpg
│   └── ... (2-8 total)
└── scene-diagram.jpg (opcional)
```

---

### Fase 6: Garantia de qualidade (2-3 dias)

**Etapa 6.1: Auto-teste do roteirista**

- Roteirista resolve o próprio caso "às cegas"
- Verifica se todas as pistas são descobertas
- Checa para soluções não intencionais
- Testa tempo das análises forenses (se aplicável)
- **Entrega:** Relatório de QA do roteirista

**Etapa 6.2: Revisão por pares**

- Outro roteirista revisa o caso
- Lê todos os documentos
- Checa consistência
- Verifica qualidade da escrita
- Testa se é solucionável
- **Entrega:** Notas de revisão por pares

**Etapa 6.3: Playthrough do QA Tester**

- QA Tester joga o caso completamente às cegas
- Documenta o processo de solução
- Anota tempo gasto
- Identifica elementos confusos
- Checa contradições
- **Entrega:** Relatório de QA com issues

**Etapa 6.4: Revisões**

- Roteirista trata os problemas de QA
- Corrige contradições
- Esclarece pistas ambíguas
- Ajusta dificuldade se preciso
- Atualiza assets quando necessário
- **Entrega:** Arquivos de caso revisados

**Etapa 6.5: Validação final**

- Executar novamente o validador JSON
- Verificar aplicação de todas as revisões
- Conferir integridade dos arquivos
- Testar carregamento dos assets
- **Entrega:** Relatório de validação final

---

### Fase 7: Aprovação & publicação (1 dia)

**Etapa 7.1: Revisão do Content Manager**

- Revisar o pacote completo do caso
- Checar qualidade de produção
- Verificar classificação de dificuldade
- Garantir consistência com a marca
- **Decisão:** Aprovar, Solicitar Ajustes Menores ou Rejeitar

**Etapa 7.2: Aprovação final**

- Assinatura do Lead Designer
- Marcar caso como "Publicado" no sistema
- Atribuir número oficial de caso
- Atualizar biblioteca de casos

**Etapa 7.3: Deploy**

- Fazer upload primeiro de todos os assets que não são `case.json` para o Azure Blob Storage
- Gravar o `case.json` **por último** como commit marker do bundle
- Expor o job na experiência admin de `/case-generation`, com estatísticas finais / status de retry
- Tratar o caso como live apenas depois que o blob final de `case.json` existir
- **Status:** Caso disponível em produção

---

## 10.4 Resumo do cronograma de produção

| Fase | Duração | Entregas principais |
|------|---------|---------------------|
| 1. Desenvolvimento do conceito | 2-3 dias | Conceito, Outline |
| 2. Pesquisa & planejamento | 1-2 dias | Notas de pesquisa, Manifesto de evidências, Lista de documentos |
| 3. Escrita | 5-7 dias | Dossiês, Linha do tempo, Documentos, Descrições de evidência |
| 4. Produção de assets | 3-4 dias | PDFs, Fotos, Relatórios forenses |
| 5. Montagem do case.json | 1 dia | case.json, Organização de assets |
| 6. Garantia de qualidade | 2-3 dias | Relatórios de QA, Revisões |
| 7. Aprovação & publicação | 1 dia | Aprovação final, Deploy |
| **TOTAL** | **15-21 dias** | Caso publicado completo |

**Velocidade:** 1-2 casos por mês por roteirista

---

## 10.5 Sistema de gerenciamento de conteúdo

### Workflow de status do caso

```
Draft → In Review → Approved → In Production → QA Testing → Final Review → Published → Archived
```

**Definições de status:**

- **Draft:** Roteirista trabalhando ativamente
- **In Review:** Submetido para revisão de conteúdo
- **Approved:** Conceito/outline aprovados, seguir para produção
- **In Production:** Assets em criação
- **QA Testing:** QA tester jogando o caso
- **Final Review:** Aprovação final do Content Manager
- **Published:** Disponível em produção
- **Archived:** Removido da biblioteca ativa (mas preservado)

### Banco de dados de acompanhamento de casos

**Campos:**

- Case ID
- Título
- Status
- Dificuldade
- Roteirista responsável
- Data de início
- Data-alvo de conclusão
- Fase atual
- Bloqueadores (se houver)
- Status de QA
- Data de aprovação

### Versionamento

**Estrutura do repositório Git:**

```
casezero-content/
├── cases/
│   ├── CASE-2024-001/
│   ├── CASE-2024-002/
│   └── ...
├── templates/
│   ├── police-report-template.indd
│   ├── witness-statement-template.indd
│   └── ...
├── guidelines/
│   ├── writing-guidelines.md
│   ├── forensic-accuracy.md
│   └── ...
└── tools/
    ├── case-validator.js
    ├── clue-checker.py
    └── ...
```

**Workflow Git:**

- Cada caso em branch de feature: `case/CASE-2024-XXX`
- Pull request para revisão de QA
- Merge em `main` após aprovação
- Tag de releases: `v3.0-CASE-2024-001`

---

## 10.6 Checklist de QA

### QA de conteúdo

**✅ História & lógica**

- [ ] Crime é solucionável com as evidências disponíveis
- [ ] Solução é lógica e comprovável
- [ ] Sem contradições na linha do tempo
- [ ] Álibis dos suspeitos são consistentes
- [ ] Falsas pistas são plausíveis, mas refutáveis

**✅ Pistas & evidências**

- [ ] 3-5 pistas críticas presentes
- [ ] Pistas críticas são descobríveis
- [ ] Cadeia de evidências está completa
- [ ] Resultados forenses fazem sentido
- [ ] Sem falsas pistas acidentais (enganos involuntários)

**✅ Qualidade de writing**

- [ ] Documentos soam realistas
- [ ] Tom consistente por tipo de documento
- [ ] Sem anacronismos (se em outra época)
- [ ] Gramática e ortografia corretas
- [ ] Voz dos personagens é distinta

**✅ Classificação de dificuldade**

- [ ] Condiz com o tier pretendido (Easy/Medium/Hard/Expert)
- [ ] Pistas com nível adequado de ocultação/obviedade
- [ ] Exige o nível correto de inferência
- [ ] Tempo estimado é preciso

### QA técnico

**✅ Validação do case.json**

- [ ] Sintaxe JSON válida
- [ ] Versão do schema correta
- [ ] Todos os campos obrigatórios presentes
- [ ] IDs únicos e seguindo o padrão
- [ ] Referências válidas (culprit → suspect etc.)
- [ ] Cardinalidade respeitada (2-8 suspeitos, 8-25 evidências etc.)

**✅ QA de assets**

- [ ] Todos os caminhos em case.json são válidos
- [ ] PDFs abrem corretamente (sem corrupção)
- [ ] Imagens carregam e exibem corretamente
- [ ] Resolução mínima cumprida (1920×1080)
- [ ] Tamanho dos arquivos adequado (<5MB por PDF, <2MB por imagem)
- [ ] Convenção de nomes seguida

**✅ Metadata**

- [ ] Case ID no formato CASE-YYYY-###
- [ ] Título único e descritivo
- [ ] Dificuldade alinhada com a pontuação
- [ ] Tempo estimado razoável (1-12 horas)
- [ ] Tags relevantes

---

## 10.7 Templates & ferramentas

### Templates de documentos

**Template de relatório policial**

- Cabeçalho com logo do departamento
- Campo de número do caso
- Campos de data/hora
- Nome do oficial/número de crachá
- Seções estruturadas: Resumo, Detalhes, Evidências, Assinatura
- Fonte: Arial 11pt, margens de 1"

**Template de depoimento de testemunha**

- Cabeçalho com título "Witness Statement"
- Seção de informações da testemunha
- Área para relato narrativo
- Linha de assinatura com data
- Fonte: Times New Roman 12pt

**Template de transcrição de entrevista**

- Cabeçalho "Interview Transcript"
- Informações do entrevistado
- Data/hora/local
- Formato de perguntas e respostas com identificação dos interlocutores
- Rodapé com numeração de páginas
- Fonte: Courier New 11pt (estilo máquina de escrever)

**Template de relatório forense**

- Cabeçalho do laboratório
- IDs do relatório e da evidência
- Tipo de análise destacado
- Seções: Metodologia, Achados, Conclusões
- Assinatura do perito
- Fonte: Arial 10pt, estilo técnico

### Ferramentas de software

**Criação de conteúdo:**

- **Adobe InDesign:** Diagramação de documentos
- **Adobe Photoshop:** Edição de fotos
- **Adobe Illustrator:** Diagramas, ícones
- **Microsoft Word:** Redação de texto
- **Google Sheets:** Controle de linha do tempo/evidências

**Validação:**

- **JSON Validator:** Ferramenta online/CLI para sintaxe
- **Custom Case Validator:** Confere integridade referencial
- **Clue Checker Script:** Verifica se a solução é comprovável

**Gestão de assets:**

- **Git:** Controle de versão
- **Azure Blob Storage:** Armazenamento em produção
- **CDN:** Distribuição de assets

---

## 10.8 Estratégia de localização

### Fase 1: Localização de UI já implementada

**Suporte atual na UI:**

- O frontend já oferece **quatro locales implementados**: `en-US`, `pt-BR`, `es-ES`, `fr-FR`
- A página admin `/case-generation` inclui seletor de locale
- As strings de UI vivem no sistema de i18n do frontend, e não em texto solto por página

### Fase 2: Pipeline de locale da geração já implementado

**Configuração técnica atual:**

- O gerador com Durable Functions aceita um locale-alvo
- Os bundles gerados precisam continuar em conformidade com a [CASE JSON v2 spec](../../CASE_JSON_V2_SPEC.md)
- A saída por locale é validada antes da publicação, junto com os gates de schema, lógica, solver e parity

**Lembrete do bundle canônico:**

```json
{
  "locale": "en-US",
  "caseId": "CASE-2024-001"
}
```

Use o [JSON Schema](../../../schemas/case.schema.json) compartilhado como contrato estrutural autoritativo para todos os locales.

### Fase 3: Expansão planejada além dos 4 locales atuais

**Planejado:**
1. Avaliar locales adicionais além de `en-US`, `pt-BR`, `es-ES` e `fr-FR`
2. Adicionar diretrizes de adaptação cultural quando nomes, instituições ou convenções documentais precisarem mudar
3. Ampliar a cobertura de QA com falantes nativos por locale

**Processo de tradução (expansão planejada):**
1. Exportar o texto voltado ao jogador a partir da fonte canônica em case.json v2
2. Tradução / revisão profissional
3. Adaptação cultural (nomes, locais se necessário)
4. Recriar documentos ou imagens específicas do locale quando necessário
5. QA por falante nativo
6. Publicar o bundle localizado pelo mesmo fluxo de validação + publicação

---

## 10.9 Gestão da biblioteca de conteúdo

### Categorização de casos

**Por dificuldade (enum implementado):**

- Rookie
- Detective
- Detective2
- Sergeant
- Lieutenant
- Captain
- Commander

**Por tipo de crime:**

- Homicide
- Assault
- Theft
- Fraud
- Arson
- Other

**Por tags:**

- Financial (fraude, desvio de dinheiro)
- Workplace (crime corporativo)
- Domestic (família/relacionamentos)
- Organized Crime (gangues, máfia)
- Historical (casos de época)
- Serial (múltiplas vítimas)

### Política de aposentadoria de casos

**Quando arquivar:**

- Caso disponível há 3+ anos
- Taxa de conclusão muito alta (>90%)
- Caso substituído por versão aprimorada
- Conteúdo não atende mais aos padrões de qualidade

**Processo de arquivamento:**

- Alterar status para "Archived"
- Remover da lista ativa de casos
- Preservar arquivos na pasta de arquivo
- Usuários que iniciaram ainda podem concluir
- Novos jogadores não podem iniciar

---

## 10.10 Ferramentas de produção de conteúdo

### Case Validator Tool

**Implementação atual:** o gerador live do repositório **não** usa o pseudocódigo legado em estilo v1 que antes aparecia aqui como fonte de verdade. Em vez disso, o pipeline com Durable Functions valida a saída canônica de `case.json` v2 contra a [CASE JSON v2 spec](../../CASE_JSON_V2_SPEC.md) e o [JSON Schema](../../../schemas/case.schema.json), e depois aplica gates de nível do gerador para:

- Consistência do Case Bible
- Consistência de referências cruzadas no CaseGraph
- Validação de evidência / prova / desfecho forense
- Validação de dificuldade + locale
- Gate de score do solver (**rejeita abaixo de 0,90**)
- Specialist/expert review, rookie regression e parity checks

### Clue Checker Tool

**Implementação atual:** a solvabilidade das pistas é garantida principalmente pelo **SolverTask** automatizado do pipeline de geração, que tenta resolver o caso usando apenas evidências visíveis ao jogador. Trate exemplos antigos que mencionam nomes de campo deprecated (por exemplo `documents[]` como estrutura canônica de topo) apenas como esboços históricos, não como contrato atual do repositório.

---

## 10.11 Documento de diretrizes para roteiristas

### Princípios de escrita (resumo)

Consulte [05-NARRATIVA.md](05-NARRATIVA.md) para as diretrizes completas.

**Pontos-chave:**

- Autenticidade acima do drama
- Mostre, não conte
- Respeite a inteligência do jogador
- Pistas sutis, não óbvias
- Procedimentos policiais realistas
- Sem anacronismos
- Vozes de personagem consistentes

### Erros comuns a evitar

**❌ Explicar demais:**

"O suspeito mentiu sobre o álibi porque na verdade estava na cena do crime matando a vítima."

- Muito direto, remove o mistério

**✅ Mostrar evidências:**

"Suspeito afirma que estava em casa às 23h. Câmeras de segurança o mostram saindo do prédio às 22h45."

- Jogador faz a conexão

**❌ Linha do tempo contraditória:**

"Vítima vista pela última vez às 22h. Crime ocorreu às 21h."

- Impossível, quebra a lógica

**✅ Linha do tempo consistente:**

"Vítima vista pela última vez às 22h. Crime ocorreu entre 22h30 e 23h, segundo perícia."

- Lógico, permite investigação

**❌ Falsas pistas injustificadas:**

"Suspeito B tem motivo e oportunidade, mas não cometeu o crime. Sem explicação."

- Frustrante, soa trapaceiro

**✅ Falsas pistas refutáveis:**

"Suspeito B tem motivo, mas o DNA prova que ele não estava na cena."

- Jogador descobre a verdade investigando

---

## 10.12 Calibração de dificuldade

### Fórmula de pontuação de dificuldade

**Implementação atual:** o repositório live usa o enum compartilhado de sete níveis para dificuldade/patente:
`Rookie`, `Detective`, `Detective2`, `Sergeant`, `Lieutenant`, `Captain`, `Commander`.

**Nota histórica de design (não é o enum live):** a fórmula de quatro tiers abaixo foi preservada apenas como orientação original de balanceamento.

(De [04-ESTRUTURA-DE-CASO.md](04-ESTRUTURA-DE-CASO.md))

```
Score = (suspects × 5) + (evidence × 3) + (documents × 4) + (forensics × 5) + (red_herrings × 2) + (timeline_complexity × 10)
```

**Tiers históricos:**

- Easy: 0-45
- Medium: 46-90
- Hard: 91-135
- Expert: 136-180

### Testes de calibração

**Processo:**

1. Calcular pontuação inicial de dificuldade
2. QA tester resolve o caso, registrando tempo
3. Comparar tempo real com o estimado
4. Ajustar complexidade se houver diferença:
   - Fácil demais? Adicionar falsas pistas, esconder pistas melhor
   - Difícil demais? Esclarecer pistas críticas, reduzir enganos
5. Re-testar com segundo QA tester
6. Atribuir classificação final de dificuldade

**Meta de tempo:**

- Easy: 1,5-3 horas
- Medium: 3-6 horas
- Hard: 6-9 horas
- Expert: 9-12 horas

---

## 10.13 Pipeline de conteúdo pós-lançamento

> **Nota de roadmap histórico:** esta seção registra premissas originais de planejamento de lançamento (por exemplo o mix Easy/Medium/Hard/Expert). Leia como histórico de planejamento, não como o sistema de dificuldade implementado atualmente no repositório.

### Conteúdo de lançamento (MVP)

**Biblioteca inicial:**

- 3 casos Easy
- 3 casos Medium
- 2 casos Hard
- 1 caso Expert
- **Total:** 9 casos

**Cronograma de produção pré-lançamento:**

- 6 meses antes do lançamento
- 1-2 roteiristas ativos
- 1 caso concluído a cada 3 semanas
- Tempo de folga para polimento

### Cadência pós-lançamento

**Meses 1-3 (estabilização):**

- Foco em correções de bugs
- Sem novos casos (usar backlog se necessário)
- 1 caso em produção (lançamento no mês 4)

**Meses 4-6 (estado estável):**

- 1 novo caso por mês
- 2 casos simultaneamente no pipeline
- Contratar roteirista adicional se a demanda aumentar

**Meses 7-12 (expansão):**

- 1-2 novos casos por mês
- Introduzir casos sazonais/temáticos
- Iniciar localização dos 3 casos mais populares

**Ano 2+:**

- 12-18 casos por ano
- Expandir para 3-4 roteiristas
- Pipeline de localização completo
- Considerar editor de casos UGC (futuro)

---

## 10.14 Métricas & analytics de conteúdo

### Métricas de sucesso

**Por caso:**

- **Completion Rate:** % de jogadores que iniciam e concluem
- **Success Rate:** % de casos concluídos com solução correta
- **Average Time:** tempo médio até a conclusão
- **First Attempt Success:** % que resolve na primeira tentativa
- **Forensic Usage:** % que usa perícia, média de solicitações
- **Difficulty Rating:** dificuldade reportada (jogador) vs planejada

**Saúde do conteúdo:**

- **Case Availability:** casos por tier de dificuldade
- **Backlog:** casos em produção
- **Velocity:** casos concluídos por mês
- **Quality Score:** média de QA (escala 1-10)

### Coleta de dados

**Do gameplay:**

- Horários de início/fim da sessão
- Tentativas de submissão
- Solicitações de perícia
- Documentos visualizados (opcional, com privacidade)
- Correção da solução

**De pesquisas:**

- Nota de dificuldade pós-caso (1-5 estrelas)
- Nota de diversão (1-5 estrelas)
- Feedback aberto

### Iteração baseada em dados

**Se Completion Rate < 50%:**

- Caso muito difícil ou longo
- Revisar clareza
- Considerar ajustes ou aposentadoria

**Se First Attempt Success > 80%:**

- Caso muito fácil
- Adicionar complexidade ou mudar de tier

**Se Forensic Usage < 30%:**

- Perícia pouco relevante ou dispensável
- Revisar integração forense

---

## 10.15 Orçamento & recursos

### Custos de produção (por caso)

**Mão de obra:**

- Roteirista: 80-100 horas @ US$ 40-60/h = US$ 3.200-6.000
- Document Designer: 24-32 horas @ US$ 30-50/h = US$ 720-1.600
- QA Tester: 16-24 horas @ US$ 25-40/h = US$ 400-960
- Content Manager: 8-12 horas @ US$ 50-75/h = US$ 400-900
- **Total mão de obra:** US$ 4.720-9.460 por caso

**Assets:**

- Fotos de stock (se necessário): US$ 0-500
- Fotografia customizada: US$ 500-2.000 (se necessária)
- Assets gráficos: US$ 200-500
- **Total assets:** US$ 700-3.000 por caso

**Custo total por caso:** US$ 5.420-12.460

**Orçamento anual de conteúdo (18 casos/ano):**

- Baixo: US$ 97.560
- Alto: US$ 224.280
- **Média:** ~US$ 160.000/ano

### Tamanho do time

**Time de lançamento:**

- 2 Case Writers (tempo integral)
- 1 Document Designer (tempo integral)
- 1 Forensic Writer (meio período/consultor)
- 1 QA Tester (tempo integral)
- 1 Content Manager (tempo integral)
- **Total:** 5-6 pessoas

**Time no ano 2:**

- 3-4 Case Writers
- 1-2 Document Designers
- 1-2 QA Testers
- 1 Content Manager
- 1 Localization Coordinator (novo)
- **Total:** 7-10 pessoas

---

## 10.16 Resumo

**Pipeline de conteúdo:**

- Workflow em 7 fases, do conceito à publicação
- 15-21 dias por caso
- Velocidade: 1-2 casos/mês por roteirista

**Garantia de qualidade:**

- Auto-teste do roteirista
- Revisão por pares
- Playthrough cego do QA tester
- Aprovação do Content Manager
- Validação técnica

**Ferramentas & sistemas:**

- Git para versionamento
- Case Validator para integridade
- Clue Checker para solucionabilidade
- Templates de documentos para consistência
- CMS para acompanhamento

**Produção:**

- Lançamento: 9 casos (3 Easy, 3 Medium, 2 Hard, 1 Expert)
- Pós-lançamento: 1-2 casos por mês
- Ano 2: 12-18 casos por ano
- Localização começa no mês 12

**Orçamento:**

- US$ 5.420-12.460 por caso
- ~US$ 160.000/ano para 18 casos
- Time: 5-6 pessoas no lançamento, 7-10 no ano 2

**Saúde do conteúdo:**

- Monitorar taxa de conclusão, sucesso, tempo
- Iterar com base nos dados dos jogadores
- Aposentar casos após 3+ anos
- Manter backlog de 2-4 semanas

---

**Próximo capítulo:** [11-TESTES.md](11-TESTES.md) – Estratégia de testes e processos de QA

**Documentos relacionados:**

- [04-ESTRUTURA-DE-CASO.md](04-ESTRUTURA-DE-CASO.md) – Requisitos dos casos
- [05-NARRATIVA.md](05-NARRATIVA.md) – Diretrizes de escrita
- [09-ESQUEMA-DE-DADOS.md](09-ESQUEMA-DE-DADOS.md) – Validação de dados

---

**Histórico de revisões:**

| Data | Versão | Mudanças | Autor |
|------|--------|----------|-------|
| 14/11/2025 | 1.0 | Tradução completa para PT-BR | Assistente de IA |
