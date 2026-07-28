# Capítulo 06 - Progressão & Avanço

**Documento de Design de Jogo - CaseZero v3.0**  
**Última atualização:** 13 de novembro de 2025  
**Status:** ✅ Completo

---

> ### ⚠️ Status de implementação
>
> Este capítulo é a **visão de design original** para a progressão e é, em sua maior parte, **aspiracional** — a maior parte dele (a escada de 8 patentes, a fórmula de XP, a promoção baseada em XP, as estimativas de horas/casos) **não** foi implementada como descrito. Trate-o como uma **referência de meta de design / roadmap**, não como uma descrição do jogo em produção.
>
> **O que está realmente implementado hoje** (`backend/CaseZeroApi/Models/User.cs`, `backend/CaseZeroApi/Services/PromotionRules.cs`, `schemas/case.schema.json`):
> - **7 patentes/níveis de dificuldade**, compartilhados entre casos e detetives: `Rookie → Detective → Detective2 → Sergeant → Lieutenant → Captain → Commander`.
> - A promoção **não é baseada em XP**. Ela é dirigida pelo número **cumulativo de casos resolvidos com avaliação correta** desde a criação da conta, com limiares fixos: Rookie = 0, Detective = 3, Detective2 = 8, Sergeant = 16, Lieutenant = 28, Captain = 44, Commander = 65 casos resolvidos.
> - Não existe número de XP, fórmula de XP por dificuldade, nem sistema de bônus/penalidade de XP por tentativa no código atual.
>
> O restante deste capítulo (§6.3–§6.9) descreve o **sistema de 8 patentes/baseado em XP como originalmente concebido**. Ele é mantido para fins de histórico de design e iterações futuras; não trate os nomes específicos de patentes ("Detetive Sênior", "Detetive Mestre" etc.), os números de XP ou as estimativas de horas abaixo como fatos atuais.

## 6.1 Visão Geral

Este capítulo define o **sistema de progressão por patentes de detetive** — como os jogadores avançam na carreira, desbloqueiam novos casos e acompanham sua maestria investigativa. O objetivo é transmitir sensação de carreira real, não de “level” gamificado.

**Conceitos-chave:**

- 8 patentes (Recruta → Detetive Mestre)
- XP ganho ao solucionar casos
- Desbloqueio de casos por patente
- Acompanhamento de desempenho e estatísticas
- Nenhuma vantagem mecânica (apenas acesso a conteúdo)
- Metas de longo prazo para maestria

---

## 6.2 Filosofia da Progressão

### O que a Progressão É

**Avanço de Carreira:**

- Reflete expertise crescente
- Desbloqueia casos mais complexos
- Registra conquistas investigativas
- Mostra maestria ao longo do tempo

**Sistema de Reconhecimento:**

- Reconhece habilidade do jogador
- Gera senso de crescimento
- Cria metas de longo prazo
- Incentiva investigações completas

### O que a Progressão NÃO É

**Nada de Power Creep:**

- ❌ Sem perícias mais rápidas em patentes altas
- ❌ Sem tentativas extras de solução
- ❌ Sem dicas ou assistência liberadas
- ❌ Sem facilitar descoberta de evidências

**Sem Manipulação:**

- ❌ Sem bônus de login diário
- ❌ Sem sistemas de “energia”
- ❌ Sem progressão travada por tempo
- ❌ Sem pressão para subir rápido

**Princípio central:** Patentes medem habilidade, não tempo investido ou pagamento.

---

## 6.3 Estrutura de Patentes

### As 8 Patentes

```text
8. DETETIVE MESTRE        [18.000+ XP]     █████████████████████
                                            Lendário (Top 1%)

7. DETETIVE VETERANO      [12.000-18.000]  ████████████████░░░░░
                                            Elite

6. DETETIVE LÍDER         [8.000-12.000]   ████████████░░░░░░░░░
                                            Especialista

5. DETETIVE SÊNIOR        [5.000-8.000]    ████████░░░░░░░░░░░░░
                                            Altamente Habilidoso

4. DETETIVE I             [3.000-5.000]    ██████░░░░░░░░░░░░░░░
                                            Experiente

3. DETETIVE II            [1.500-3.000]    ████░░░░░░░░░░░░░░░░░
                                            Competente

2. DETETIVE III           [500-1.500]      ██░░░░░░░░░░░░░░░░░░░
                                            Em Desenvolvimento

1. RECRUTA                [0-500]          █░░░░░░░░░░░░░░░░░░░░
                                            Iniciante
```

### Descrição das Patentes

#### 1. Recruta (0-500 XP)

**Descrição:** Recém-alocado à Divisão de Casos Arquivados. Ainda aprendendo fundamentos da investigação.

**Acesso:**

- Caso tutorial (treinamento)
- Casos fáceis (2-3 suspeitos)
- 3-5 casos totais disponíveis

**Distintivo no Perfil:** 🔰 Detetive Recruta  
**Tempo estimado para subir:** 2-3 horas (1-2 casos fáceis)

**Jogador neste estágio:**

- Aprendendo mecânicas de investigação
- Entendendo leitura de documentos
- Primeiro contato com perícias
- Construindo confiança

---

#### 2. Detetive III (500-1.500 XP)

**Descrição:** Concluiu o primeiro caso real. Demonstra competência básica nos fundamentos.

**Acesso:**

- Todos os casos fáceis liberados
- Casos médios liberados (4-5 suspeitos)
- 8-12 casos totais disponíveis

**Distintivo no Perfil:** 👮 Detetive III  
**Tempo estimado para subir:** 8-12 horas (2-3 casos médios)

**Jogador neste estágio:**

- Confortável com as mecânicas
- Resolve casos diretos
- Lida com complexidade inicial
- Desenvolve análise

---

#### 3. Detetive II (1.500-3.000 XP)

**Descrição:** Investigador comprovado com múltiplos casos solucionados. Lida com investigações de complexidade média.

**Acesso:**

- Todos os casos fáceis e médios
- 15-20 casos totais disponíveis

**Distintivo no Perfil:** 👮‍♂️ Detetive II  
**Tempo estimado para subir:** 15-20 horas (3-4 casos médios, possivelmente 1 difícil)

**Jogador neste estágio:**

- Investigador confiante
- Raramente falha em casos fáceis
- Sucesso em 50%+ das tentativas iniciais em casos médios
- Pronto para desafios maiores

---

#### 4. Detetive I (3.000-5.000 XP)

**Descrição:** Detetive experiente. Encara casos complexos com forte capacidade analítica.

**Acesso:**

- Todos os casos fáceis e médios
- Casos difíceis liberados (6-7 suspeitos)
- 25-30 casos totais disponíveis

**Distintivo no Perfil:** 🕵️ Detetive I  
**Tempo estimado para subir:** 20-30 horas (4-5 casos difíceis)

**Jogador neste estágio:**

- Domina casos médios
- Confortável com alta complexidade
- Aprimora expertise
- Consegue lidar com ambiguidades

---

#### 5. Detetive Sênior (5.000-8.000 XP)

**Descrição:** Investigador altamente habilidoso. Reputação por resolver casos difíceis.

**Acesso:**

- Todos os casos fáceis, médios e difíceis
- 35-40 casos totais disponíveis

**Distintivo no Perfil:** 🎖️ Detetive Sênior  
**Tempo estimado para subir:** 30-40 horas (5-7 casos difíceis)

**Jogador neste estágio:**

- Desempenho consistente
- Altas taxas de sucesso
- Raramente fica travado
- Caminho para maestria

---

#### 6. Detetive Líder (8.000-12.000 XP)

**Descrição:** Investigador especialista. Trata os casos arquivados mais complexos com excelência.

**Acesso:**

- Todos os casos fáceis, médios e difíceis
- Casos especialistas liberados (8+ suspeitos)
- 45-50 casos totais disponíveis

**Distintivo no Perfil:** ⭐ Detetive Líder  
**Tempo estimado para subir:** 40-60 horas (6-8 casos especialistas)

**Jogador neste estágio:**

- Habilidades em nível especialista
- Resolve maioria dos casos na primeira tentativa
- Enfrenta dificuldade máxima
- Pertence ao topo do quadro investigativo

---

#### 7. Detetive Veterano (12.000-18.000 XP)

**Descrição:** Investigador de elite. Entre os melhores da divisão.

**Acesso:**

- Todos os casos liberados
- Casos raros/especiais (quando disponíveis)
- 55-60+ casos totais disponíveis

**Distintivo no Perfil:** 🏅 Detetive Veterano  
**Tempo estimado para subir:** 60-90 horas (8-12 casos especialistas)

**Jogador neste estágio:**

- Desempenho de elite
- Alta taxa de acerto na primeira tentativa mesmo no nível especialista
- Domínio profundo da investigação
- Respeitado pela comunidade

---

#### 8. Detetive Mestre (18.000+ XP)

**Descrição:** Status lendário. Patente máxima. Menos de 1% dos jogadores chegam aqui.

**Acesso:**

- Todo o conteúdo liberado
- Distintivo exclusivo “Master”
- Destaque no perfil

**Distintivo no Perfil:** 👑 Detetive Mestre  
**Conquista:** Permanente (não há patente superior)

**Jogador neste estágio:**

- Maestria completa
- Provavelmente 80%+ de sucesso na primeira tentativa
- Resolve qualquer caso
- Top 1% de toda a base

---

## 6.4 Sistema de Pontos de Experiência (XP)

### Fórmula de XP

**XP base por dificuldade:**

```javascript
const baseXP = {
  Easy: 150,
  Medium: 300,
  Hard: 600,
  Expert: 1200
};
```

**Modificadores:**

```javascript
function calculateXP(difficulty, attempt, bonuses) {
  let xp = baseXP[difficulty];
  
  // Penalidade por tentativa
  if (attempt === 2) {
    xp *= 0.75; // -25%
  } else if (attempt === 3) {
    xp *= 0.50; // -50%
  } else if (attempt > 3) {
    xp = 0; // Caso fracassado
  }
  
  // Bônus
  if (bonuses.firstAttempt) {
    xp *= 1.5; // +50% por resolver na primeira tentativa
  }
  
  if (bonuses.noForensics) {
    xp *= 1.25; // +25% por resolver sem perícias (raro)
  }
  
  if (bonuses.quickSolve) {
    xp *= 1.1; // +10% por resolver em menos de 2 horas
  }
  
  if (bonuses.thoroughExplanation) {
    xp *= 1.1; // +10% por explicação detalhada da solução
  }
  
  return Math.floor(xp);
}
```

### Exemplos de XP

**Caso Fácil (150 XP base):**

- Primeira tentativa, rápido: 150 × 1,5 × 1,1 = **248 XP**
- Segunda tentativa: 150 × 0,75 = **113 XP**
- Terceira tentativa: 150 × 0,5 = **75 XP**

**Caso Médio (300 XP base):**

- Primeira tentativa: 300 × 1,5 = **450 XP**
- Primeira tentativa + explicação detalhada: 300 × 1,5 × 1,1 = **495 XP**
- Segunda tentativa: 300 × 0,75 = **225 XP**
- Terceira tentativa: 300 × 0,5 = **150 XP**

**Caso Difícil (600 XP base):**

- Primeira tentativa: 600 × 1,5 = **900 XP**
- Primeira tentativa + todos os bônus: 600 × 1,5 × 1,1 × 1,1 = **1.089 XP**
- Segunda tentativa: 600 × 0,75 = **450 XP**
- Terceira tentativa: 600 × 0,5 = **300 XP**

**Caso Especialista (1.200 XP base):**

- Primeira tentativa: 1.200 × 1,5 = **1.800 XP**
- Primeira tentativa + bônus: 1.200 × 1,5 × 1,1 × 1,1 = **2.178 XP**
- Segunda tentativa: 1.200 × 0,75 = **900 XP**
- Terceira tentativa: 1.200 × 0,5 = **600 XP**

### Requisitos de XP por Patente

| Patente | XP Necessário | XP Acumulado | Equivalente em Casos Fáceis | Equivalente em Casos Especialistas |
|---------|---------------|--------------|------------------------------|------------------------------------|
| Recruta → Detetive III | 500 | 500 | 2-3 casos | 1 caso |
| Detetive III → Detetive II | 1.000 | 1.500 | 4-5 casos | 1-2 casos |
| Detetive II → Detetive I | 1.500 | 3.000 | 6-7 casos | 2-3 casos |
| Detetive I → Sênior | 2.000 | 5.000 | 8-10 casos | 3-4 casos |
| Sênior → Líder | 3.000 | 8.000 | 12-15 casos | 5-6 casos |
| Líder → Veterano | 4.000 | 12.000 | 18-20 casos | 7-9 casos |
| Veterano → Mestre | 6.000 | 18.000 | 25-30 casos | 10-12 casos |

**Total até Mestre:** 18.000 XP (~60-120 horas de jogo, 40-80 casos)

---

## 6.5 Sistema de Desbloqueio de Casos

### Regras de Desbloqueio

**Gate por dificuldade:**

```javascript
const caseUnlockRequirements = {
  Easy: "Rookie", // Patente 1+
  Medium: "Detective III", // Patente 2+
  Hard: "Detective I", // Patente 4+
  Expert: "Lead Detective" // Patente 6+
};
```

**Desbloqueio progressivo:**

- Todos os casos fáceis disponíveis como Recruta
- Médios liberam em Detetive III (após primeiro caso resolvido)
- Difíceis liberam em Detetive I (após demonstrar competência)
- Especialistas liberam em Detetive Líder (conteúdo de elite)

### Por que fazer gating de dificuldade?

**Evita Frustração:**

- Novatos não encaram especialistas de imediato
- Garante evolução de habilidades antes de casos pesados
- Cria sensação de progressão

**Mantém Desafio:**

- Sempre há dificuldade adequada disponível
- Casos difíceis parecem conquistas merecidas
- Evita burnout por conteúdo muito complexo cedo demais

**Não bloqueia conteúdo:**

- Patentes altas ainda podem jogar casos fáceis
- Sem “passar do nível” de casos
- Todo caso continua relevante

---

## 6.6 Recompensas de Progressão

### O que você ganha ao subir de patente

**1. Acesso a conteúdo (recompensa primária)**

- Novo nível de dificuldade liberado
- Mais casos disponíveis
- Casos raros/especiais (nas patentes mais altas)

**2. Distintivo no perfil**

- Indicador visual da patente
- Exibido no perfil
- Reconhecimento na comunidade

**3. Título**

- "Detetive Recruta"
- "Detetive III/II/I"
- "Detetive Sênior"
- "Detetive Líder"
- "Detetive Veterano"
- "Detetive Mestre"

**4. Estatísticas liberadas**

- Estatísticas mais detalhadas
- Histórico de casos
- Taxas de sucesso
- Comparação com média da patente

### O que você NÃO ganha

**Sem vantagens mecânicas:**

- ❌ Perícias não ficam mais rápidas
- ❌ Sem tentativas extras
- ❌ Sem dicas ou destaque de pistas
- ❌ Sem versões “mais fáceis” dos casos

**Sem cosméticos (mantemos simples):**

- ❌ Sem customização de avatar
- ❌ Sem decoração de escritório
- ❌ Sem temas de perfil

**Filosofia:** Progressão serve para reconhecimento de habilidade, não poder ou vaidade.

---

## 6.7 Estatísticas & Acompanhamento de Desempenho

### Estatísticas centrais

**Estatísticas gerais:**

```text
┌─────────────────────────────────────────────┐
│ PERFIL DO DETETIVE                          │
├─────────────────────────────────────────────┤
│ Patente: Detetive Líder ⭐                  │
│ XP: 9.450 / 12.000                          │
│                                             │
│ ESTATÍSTICAS DE CARREIRA:                   │
│ Casos Resolvidos: 28                        │
│ Casos Fracassados: 3                        │
│ Casos Ativos: 2                             │
│ Taxa de Sucesso: 90,3%                      │
│                                             │
│ Sucesso na 1ª Tentativa: 42,9%              │
│ Tentativas Médias: 1,6                      │
│ Tempo Total de Investigação: 87,5 horas     │
│ Tempo Médio por Caso: 3,1 horas             │
│                                             │
│ POR DIFICULDADE:                            │
│ Fácil:   12 resolvidos, 100% sucesso        │
│ Médio:  10 resolvidos, 90% sucesso          │
│ Difícil:  5 resolvidos, 80% sucesso         │
│ Especialista: 1 resolvido, 50% sucesso      │
└─────────────────────────────────────────────┘
```

**Estatísticas por caso:**

```text
CASE-2024-001: Homicídio no Escritório Central
Status: ✓ Resolvido (Primeira Tentativa)
Tempo Gasto: 4,5 horas
Tentativas: 1/3
XP Ganho: 450 (+50% bônus)
Data da Solução: 18 de março de 2025
```

### Métricas acompanhadas

**Desempenho:**

- Total de casos resolvidos
- Casos fracassados (todas as tentativas esgotadas)
- Taxa de sucesso (%)
- Sucesso na primeira tentativa (%)
- Tentativas médias por caso
- Tempo médio por caso

**Por Dificuldade:**

- Casos resolvidos por dificuldade
- Taxa de sucesso por dificuldade
- XP ganho por dificuldade

**Conquistas especiais (ocultas):**

- Resolver sem perícias
- Resolver em menos de 2 horas
- Explicação perfeita (alta qualidade)
- Resolver caso especialista na primeira tentativa

### O que NÃO é registrado

**Respeito à privacidade:**

- ❌ Tempo individual de leitura por documento
- ❌ Movimentos de mouse
- ❌ Número de vezes que cada evidência foi aberta
- ❌ Conteúdo das anotações (privadas)

**Filosofia:** Medimos resultados, não comportamentos.

---

## 6.8 Ritmo de Progressão

### Tempo investido por patente

**Horas estimadas (jogador habilidoso):**

| Patente | Horas para alcançar | Horas acumuladas | Casos necessários |
|---------|---------------------|------------------|-------------------|
| Recruta | 0 | 0 | 0 (patente inicial) |
| Detetive III | 2-4 | 2-4 | 2-3 fáceis |
| Detetive II | 8-12 | 10-16 | +3-4 médios |
| Detetive I | 15-20 | 25-36 | +4-5 médios/difíceis |
| Detetive Sênior | 20-30 | 45-66 | +5-7 difíceis |
| Detetive Líder | 30-40 | 75-106 | +6-8 difíceis |
| Detetive Veterano | 40-60 | 115-166 | +8-12 especialistas |
| Detetive Mestre | 60-90 | 175-256 | +12-18 especialistas |

**Total até Mestre:** 175-256 horas (varia conforme habilidade e dificuldade escolhida)

### Desenho da curva de progressão

**Patentes iniciais (1-3): rápidas**

- Vitórias rápidas geram confiança
- 2-4 horas por patente
- Sensação de recompensa
- Mantém novos jogadores engajados

**Patentes intermediárias (4-6): moderadas**

- 20-40 horas por patente
- Progressão significativa
- Acompanha habilidade crescente
- Ritmo sustentável

**Patentes finais (7-8): lentas**

- 40-90 horas por patente
- Conquista de elite
- Metas de longo prazo
- Apenas para top 1%

**Filosofia:** Início rápido, meio consistente, final prolongado para maestria.

---

## 6.9 Casos Fracassados & Sistema de Retentativas

### Regras de fracasso

**Quando você falha:**

- Consumiu 3 tentativas de solução
- Caso marcado como “Não Resolvido (Revisado)”
- Pode ver a solução correta
- Ganha 0 XP
- Caso permanece acessível

**Requisitos para retentar:**

- Resolver 2 outros casos antes do retry
- Pode retentar ilimitadas vezes (após resolver 2 outros)
- Tentativas renovadas (volta a 3)
- Pode ganhar o XP total na nova solução

### Por que esse sistema?

**Incentiva seguir adiante:**

- Jogador não fica preso em um caso
- Experimenta outros conteúdos
- Aprende com variedade
- Evita frustração

**Permite aprendizado:**

- Visualiza a solução após falhar
- Entende o que perdeu
- Aplica lições em outros casos
- Volta com perspectiva nova

**Sem penalidade permanente:**

- Pode eventualmente resolver tudo
- Sem conteúdo “perdido”
- É possível recuperar XP
- Compatível com perfis completistas

### Exemplo de fluxo

```text
1. Tenta CASE-2024-001 (Difícil)
   → Falha após 3 tentativas (0 XP)
   → Vê a solução
   
2. Resolve CASE-2024-002 (Médio)
   → Sucesso! (+300 XP)
   
3. Resolve CASE-2024-003 (Médio)
   → Sucesso! (+300 XP)
   
4. Retenta CASE-2024-001 (Difícil)
   → Sucesso na primeira tentativa do retry! (+900 XP)
   → XP total concedido
```

---

## 6.10 Desempenho Comparativo

### Médias por patente (benchmarks)

**Detetive III (Patente 2):**

- Taxa de sucesso: 60-70%
- Primeira tentativa: 30-40%
- Tempo médio: 4-5 horas/caso

**Detetive I (Patente 4):**

- Taxa de sucesso: 75-85%
- Primeira tentativa: 40-50%
- Tempo médio: 3-4 horas/caso

**Detetive Líder (Patente 6):**

- Taxa de sucesso: 85-95%
- Primeira tentativa: 50-65%
- Tempo médio: 3-4 horas/caso

**Detetive Mestre (Patente 8):**

- Taxa de sucesso: 90-98%
- Primeira tentativa: 65-80%
- Tempo médio: 2,5-3,5 horas/caso

### Comparação de desempenho (opcional)

**No perfil do jogador:**

```text
SEU DESEMPENHO vs. MÉDIA DA PATENTE:

Sucesso na primeira tentativa:
Você: 42%  ▓▓▓▓▓▓▓▓░░░░
Média: 50%  ▓▓▓▓▓▓▓▓▓▓░░

Taxa de sucesso:
Você: 90%  ▓▓▓▓▓▓▓▓▓░░
Média: 87%  ▓▓▓▓▓▓▓▓▓░░

Você está acima da média da sua patente!
```

**Filosofia:** Comparativo opcional, não competitivo. Ajuda a avaliar habilidade.

---

## 6.11 Conquistas Especiais (Ocultas)

### Distintivos secretos

**"Detetive Perspicaz" 🧠**

- Resolver 5 casos na primeira tentativa
- Recompensa: distintivo de perfil

**"Investigador Paciente" ⏳**

- Resolver um caso sem acelerar perícias
- Recompensa: distintivo de perfil

**"Mente Intuitiva" 💡**

- Resolver um caso sem solicitar perícias (raro)
- Recompensa: distintivo + bônus de XP

**"Leitor Veloz" ⚡**

- Resolver um caso médio em menos de 2 horas
- Recompensa: distintivo

**"Analista Mestre" 🎯**

- Resolver um caso especialista na primeira tentativa
- Recompensa: distintivo + respeito

**"Especialista em Casos" 📁**

- Resolver 50 casos no total
- Recompensa: título liberado

**"Detetive Lendário" 👑**

- Alcançar patente Mestre
- Recompensa: título permanente + destaque comunitário

**Filosofia:** Conquistas ocultas recompensam excelência sem gerar pressão.

---

## 6.12 Reset de Patente & Prestígio (Não Planejado)

### Por que não teremos sistema de prestígio?

**Argumentos contra:**

1. **Sem mudanças mecânicas:** Como patentes não dão poder, prestígio não significa nada
2. **Perda de acesso a conteúdo:** Bloquearia casos difíceis já conquistados
3. **Moagem artificial:** Estenderia tempo de jogo sem novo conteúdo
4. **Modelo premium:** Jogadores pagaram pelo conteúdo, não devem refazer tudo

**Filosofia:** Maestria é o objetivo final, não grind infinito.

**Alternativa:** Pacotes de casos (DLC) ampliam conteúdo sem resetar progresso.

---

## 6.13 Conteúdo Sazonal (Consideração futura)

### Casos sazonais (pós-lançamento)

**Conceito:**

- Casos especiais de tempo limitado (1-2 meses)
- Disponíveis para todas as patentes (sem gating)
- Temas únicos (feriados, históricos)
- Concedem distintivos temáticos
- Casos arquivados, mas rejogáveis após a temporada

**Exemplo:**

```text
TEMPORADA DE INVERNO 2025: "O Golpe de Fim de Ano"
Período: 1º dez – 31 jan
Casos: 3 novos (Fácil, Médio, Difícil)
Tema: Crimes com temática de feriados
Recompensa: Distintivo sazonal
```

**Por que sazonais?**

- Mantém a comunidade engajada
- Conteúdo fresco regularmente
- Opcional (não bloqueia jogo base)
- Rejogáveis após o fim da temporada

**Nota:** Apenas se o lançamento for bem-sucedido. Não faz parte do MVP.

---

## 6.14 Perfil & Identidade

### Exibição pública do perfil

**O que é mostrado:**

```text
┌─────────────────────────────────────────────┐
│ Perfil do Detetive                          │
├─────────────────────────────────────────────┤
│ Usuário: Alex_Martinez                      │
│ Patente: Detetive Líder ⭐                  │
│ XP: 9.450 / 12.000                          │
│                                             │
│ Entrou em: 15 de janeiro de 2025            │
│ Casos Resolvidos: 28                        │
│ Taxa de Sucesso: 90,3%                      │
│                                             │
│ DISTINTIVOS:                                │
│ 🧠 Detetive Perspicaz                       │
│ 💡 Mente Intuitiva                          │
│ ⚡ Leitor Veloz                              │
│                                             │
│ CASOS RECENTES:                             │
│ ✓ CASE-2024-015 (Especialista) - 1ª tentativa │
│ ✓ CASE-2024-014 (Difícil) - 2ª tentativa      │
│ ✓ CASE-2024-013 (Difícil) - 1ª tentativa      │
└─────────────────────────────────────────────┘
```

### Configurações de privacidade

**O que o jogador pode ocultar:**

- Taxa de sucesso
- Total de casos resolvidos
- Histórico recente de casos
- Distintivos conquistados

**O que permanece visível:**

- Nome de usuário
- Patente atual
- Data de ingresso

**Filosofia:** Recursos sociais são opt-in, não obrigatórios.

---

## 6.15 Onboarding & XP do Tutorial

### Caso tutorial

**Caso de treinamento:**

- Caso simples de furto (1 suspeito, solução óbvia)
- 5 documentos, 2 evidências
- Dura 15-20 minutos
- Concede **50 XP** (10% caminho até Detetive III)

**Propósito:**

- Ensina as mecânicas
- Garante primeira vitória
- Constrói confiança
- Entrega conquista rápida

### Bônus do primeiro caso real

**Primeiro caso resolvido:**

- Bônus extra de +100 XP (único)
- Total: ~250-350 XP (caso fácil + bônus)
- Leva o jogador a 50-70% do caminho até Detetive III
- Incentiva continuar jogando

---

## 6.16 Casos-limite & Cenários Especiais

### Resolver dificuldade alta cedo

```text
Cenário: jogador Recruta resolve caso Difícil
```

**Tratamento:**

- ✅ Ganha XP completo (900+ na primeira tentativa)
- ✅ Pode subir várias patentes de uma vez
- ✅ Desbloqueia o conteúdo correspondente
- 🎖️ Reconhecimento especial (“Overachiever”)

**Filosofia:** Habilidade merece recompensa, sem travas artificiais.

### “Farmar” dificuldade baixa

```text
Cenário: detetive de alta patente resolve apenas casos fáceis
```

**Tratamento:**

- ✅ Continua ganhando XP (150 base)
- ✅ Sem redução ou penalidade
- 📊 Estatísticas mostram distribuição por dificuldade
- 💭 Comunidade pode notar (se leaderboard opcional exibir mix)

**Filosofia:** Jogadores escolhem como jogar; estatísticas revelam nível de desafio.

### Histórico perfeito

```text
Cenário: jogador resolve todos os casos na primeira tentativa
```

**Tratamento:**

- 🏆 Conquista “Detetive Perfeito”
- 👑 Distintivo/título especial
- 📈 Topo do leaderboard (se implementado)
- 🎉 Reconhecimento da comunidade

**Filosofia:** Excelência deve ser celebrada publicamente.

---

## 6.17 Transparência de Progressão

### Comunicação clara

**Exibição no jogo:**

```text
┌─────────────────────────────────────────────┐
│ PROGRESSO DE PATENTE                        │
├─────────────────────────────────────────────┤
│ Atual: Detetive Líder ⭐                    │
│ Próxima: Detetive Veterano 🏅               │
│                                             │
│ XP: 9.450 / 12.000                          │
│ Progresso: ▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓░░░░ 79%         │
│                                             │
│ XP para próxima patente: 2.550              │
│ ~3-4 casos Difíceis, ou                     │
│ ~2 casos Especialistas                      │
│                                             │
│ [Ver estrutura completa de patentes]        │
└─────────────────────────────────────────────┘
```

**Após cada caso:**

```text
┌─────────────────────────────────────────────┐
│ CASO RESOLVIDO! ✓                           │
├─────────────────────────────────────────────┤
│ CASE-2024-014: A Conspiração do Porto       │
│ Dificuldade: Difícil                        │
│ Tentativas: 1/3 (Primeira tentativa!)       │
│                                             │
│ XP GANHO:                                   │
│ XP base:        600                         │
│ Primeira tentativa: +300 (bônus 50%)        │
│ Resolução rápida:  +90 (bônus 10%)          │
│ ─────────────────                           │
│ XP total:      990                          │
│                                             │
│ Progresso de patente: 9.450 → 10.440 / 12.000 │
│ [+990 XP]  ▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓░░░ 87%         │
│                                             │
│ Faltam 1.560 XP para Detetive Veterano!     │
└─────────────────────────────────────────────┘
```

---

## 6.18 Resumo

**Sistema de Progressão:**

- **8 patentes:** Recruta → Detetive Mestre
- **Ganhos de XP:** 150 (Fácil) até 1.200+ (Especialista) por caso
- **Total até o máximo:** 18.000 XP (~175-256 horas, 40-80 casos)
- **Desbloqueios:** Novas dificuldades nas patentes 2, 4, 6

**Princípios centrais:**

- 🎯 **Baseado em habilidade:** patentes refletem maestria, não tempo
- 🚫 **Sem power creep:** apenas acesso a conteúdo, sem vantagem mecânica
- 📈 **Transparente:** requisitos e progresso claros
- ⏱️ **Respeitoso:** sem manipulação, grind ou pressão

**Recompensas:**

- Acesso a conteúdo (novos tiers de dificuldade)
- Distintivos e títulos no perfil
- Estatísticas de desempenho
- Reconhecimento na comunidade

**Filosofia:** Progressão celebra habilidade investigativa, respeitando o tempo e a inteligência do jogador.

---

**Próximo capítulo:** [07-INTERFACE-DO-USUARIO.md](07-INTERFACE-DO-USUARIO.md) – Metáfora de desktop e design de UI

**Documentos relacionados:**

- [02-JOGABILIDADE.md](02-JOGABILIDADE.md) – Como a progressão integra com a jogabilidade
- [04-ESTRUTURA-DE-CASO.md](04-ESTRUTURA-DE-CASO.md) – Fatores de dificuldade dos casos
- [11-TESTES.md](11-TESTES.md) – Testes de balanceamento da progressão

---

**Histórico de revisões:**

| Data | Versão | Mudanças | Autor |
|------|--------|----------|-------|
| 13/11/2025 | 1.0 | Tradução completa para PT-BR | Assistente de IA |
