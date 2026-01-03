# 🕵️ Case Zero — Cold Case Generator  
## ROADMAP.md

Este roadmap descreve a evolução do gerador de Cold Cases para torná-lo **mais consistente, investigável e determinístico**, alinhado aos níveis de dificuldade definidos (Rookie → Commander).

O foco não é “mais conteúdo”, mas **melhor raciocínio investigativo**, com ambiguidade controlada, evidências com papel claro e validações automáticas de jogabilidade.

---

## Execution Order Rationale

A ordem de execução dos épicos foi reorganizada para otimizar a integração e a eficiência do Copilot/LLM durante o desenvolvimento. Começamos pela fundação com o Epic 1 para estabelecer regras claras de dificuldade. Em seguida, abordamos a redução de ruído e padronização visual com Epic 2 e a política 7.1. Garantias determinísticas do Epic 6 asseguram qualidade estrutural antes de introduzir contradições planejadas no Epic 3. A validação de jogabilidade do Epic 4 vem depois para garantir casos solucionáveis, e finalmente, a higiene e fallback do Epic 5 com 7.2 fecham o ciclo, mantendo o sistema robusto e limpo.

---

# EPIC 1 — Foundation: Difficulty as Rules (não apenas volume)

### 1.0 Criar arquivo `CaseDifficultyProfile.json`
**Objetivo**  
Formalizar e disponibilizar o profile de dificuldade em formato JSON para uso em todas as fases.

**O que fazer**
- Definir e estruturar o arquivo JSON contendo:
  - Faixas: suspeitos, documentos, evidências, duração
  - Orçamento de papéis de evidência (`conclusive`, `supporting`, `ambiguous`, `red_herring`)
  - Requisitos de raciocínio (timeline, cross-doc, forense, inferência)
  - Limites: contradições, interpretações alternativas, variantes de mídia
  - Grau de determinismo de mídia
- Garantir que o arquivo seja serializável e facilmente acessível

**Critério de aceite**
- Profile disponível em JSON e utilizado de forma determinística

---

### 1.1 Criar `CaseDifficultyProfile`
**Objetivo**  
Formalizar o que cada nível de dificuldade exige em termos de:
- quantidade
- complexidade
- tipo de raciocínio

**O que fazer**
- Criar um modelo `CaseDifficultyProfile` contendo:
  - Faixas: suspeitos, documentos, evidências, duração
  - Orçamento de papéis de evidência (`conclusive`, `supporting`, `ambiguous`, `red_herring`)
  - Requisitos de raciocínio (timeline, cross-doc, forense, inferência)
  - Limites: contradições, interpretações alternativas, variantes de mídia
  - Grau de determinismo de mídia
- Criar um provider central que retorna o profile a partir do `difficulty`

**Critério de aceite**
- Para qualquer dificuldade, o profile é obtido de forma determinística e serializável em JSON

---

### 1.2 Injetar o `CaseDifficultyProfile` em todas as fases
**Objetivo**  
Garantir que Plan, Design, Generate e QA sigam as mesmas regras.

**O que fazer**
- Incluir o profile (ou subconjuntos dele) nos prompts de:
  - Plan
  - Design (doc e media)
  - Generate
  - QA
- Cada activity deve receber apenas o que é relevante para ela

**Critério de aceite**
- Logs mostram o profile (ou slice) presente em cada chamada LLM

---

# EPIC 2 — Evidências com papel investigativo explícito + EPIC 7.1 — Política clara: menos fotos alternativas

### 2.1 Adicionar `role`, `purpose` e regras de inferência às evidências
**Objetivo**  
Evidências devem existir para cumprir um papel investigativo claro.

**O que fazer**
- Expandir o schema de evidência para incluir:
  - `role`: conclusive | supporting | ambiguous | red_herring
  - `purpose`: ex. corroborar timeline, contradizer depoimento
  - `allowedInferences`
  - `forbiddenInferences`
- Distribuir os roles conforme o orçamento do profile

**Critério de aceite**
- Toda evidência tem papel definido
- Quantidade por papel respeita o profile

---

### 2.2 Implementar Evidence Canon (grupos canônicos)
**Objetivo**  
Evitar múltiplas representações visuais do mesmo objeto.

**O que fazer**
- Adicionar às evidências:
  - `canonical`
  - `canonicalGroup`
  - `maxVisualVariants`
- Alterar a geração de mídia para:
  - Nunca gerar mais variantes que o permitido
  - Nunca variar ângulo/contexto quando `maxVisualVariants = 1`

**Critério de aceite**
- Para um mesmo objeto, existe apenas uma representação visual no bundle final

---

### 2.3 Determinismo de mídia por dificuldade
**Objetivo**  
Reduzir criatividade excessiva nos níveis baixos.

**O que fazer**
- Definir no profile: `MediaDeterminism = High | Medium | Controlled`
- Ajustar prompts de mídia:
  - Rookie/Detective: sem variação, sem detalhes extras
  - Sergeant+: variação permitida apenas se `role=ambiguous`
- Proibir “false positives” sem justificativa no Design

**Critério de aceite**
- Mídias são estáveis, previsíveis e alinhadas ao papel da evidência

---

### 7.1 Política padrão: 1 evidência = 1 mídia
**Objetivo**  
Evitar duplicação e ruído visual.

**O que fazer**
- Garantir no Design que cada evidenceId gera no máximo um MediaSpec
- Permitir exceções apenas se forem tipos diferentes (ex.: doc_scan + cctv)

**Critério de aceite**
- Rookie/Detective nunca têm múltiplas fotos do mesmo objeto

---

# EPIC 6 — Validações determinísticas no Normalizer

### 6.1 Validar budgets do profile
**Objetivo**  
Garantir que o output respeite a dificuldade.

**O que fazer**
- No Normalize/Validate:
  - Checar contagem real vs ranges do profile
  - Gerar issue High se fora do range
- Correções sugeridas entram no Fix

**Critério de aceite**
- Nenhum caso fora das faixas definidas

---

### 6.2 Garantir integridade de IDs e referências
**Objetivo**  
Eliminar referências quebradas.

**O que fazer**
- Extrair IDs citados em documentos
- Comparar com o index do manifest
- Gerar issues para qualquer referência inválida

**Critério de aceite**
- Zero referências inexistentes no bundle final

---

# EPIC 3 — Contradições planejadas e resolvíveis

### 3.1 Planejar contradições no Design
**Objetivo**  
Contradições devem ser parte intencional do puzzle.

**O que fazer**
- Adicionar `plannedContradictions` no Design:
  - Documentos envolvidos
  - Tipo de contradição
  - Evidências/documentos que resolvem
  - Dificuldade mínima
- Limitar quantidade conforme o profile

**Critério de aceite**
- Nenhuma contradição sem resolução definida

---

### 3.2 Garantir que documentos implementem contradições planejadas
**Objetivo**  
Evitar que o LLM “conserte” contradições sozinho.

**O que fazer**
- Injetar contradições relevantes no prompt de geração do documento
- Adicionar regra explícita:
  - “Se este documento participa de uma contradição planejada, implemente-a exatamente”

**Critério de aceite**
- Contradições aparecem corretamente nos documentos finais

---

# EPIC 4 — QA focado em jogabilidade

### 4.1 Criar Activity de simulação de raciocínio do jogador
**Objetivo**  
Validar se o caso é solucionável sem chute.

**O que fazer**
- Criar uma activity que:
  - Assume o papel de um detetive experiente
  - Lista passos mínimos de raciocínio
  - Identifica dead ends e ambiguidade excessiva
- Retornar JSON estruturado com o diagnóstico

**Critério de aceite**
- Casos insolúveis são detectados automaticamente

---

### 4.2 Integrar simulação ao loop de QA/Fix
**Objetivo**  
Corrigir casos não solucionáveis.

**O que fazer**
- Converter falhas da simulação em issues High
- Estender o Fix para:
  - Criar links faltantes
  - Reduzir ambiguidade
  - Inserir evidência de apoio

**Critério de aceite**
- Casos problemáticos entram no loop e melhoram antes do package

---

# EPIC 5 — Contexto mínimo e previsível por activity + EPIC 7.2 — Fallback no Normalizer para consolidação

### 5.1 Padronizar Context Slices
**Objetivo**  
Reduzir invenções por falta de contexto.

**O que fazer**
- Criar um builder de contexto mínimo por tipo de activity
- Garantir formato consistente e previsível
- Evitar passar JSONs gigantes desnecessários

**Critério de aceite**
- Prompts menores, mais estáveis e menos inventivos

---

### 7.2 Fallback no Normalizer para consolidação
**Objetivo**  
Corrigir deslizes do LLM automaticamente.

**O que fazer**
- Agrupar mídias por `canonicalGroup`
- Manter apenas a principal
- Atualizar manifest e referências

**Critério de aceite**
- Bundle final sempre limpo e consistente

---

## ✅ Resultado Esperado Final

- Casos menores **mais claros**
- Casos grandes **mais inteligentes**
- Ambiguidade **intencional**
- Evidências com função clara
- Jogador resolve casos por **raciocínio**, não por sorte

---