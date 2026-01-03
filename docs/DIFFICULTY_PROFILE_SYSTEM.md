# Sistema de Perfis de Dificuldade (Difficulty Profile System)

## Visão Geral

O sistema `CaseDifficultyProfile` formaliza as regras de geração de casos baseadas em níveis de dificuldade, garantindo que cada nível tenha requisitos específicos de quantidade, complexidade e tipo de raciocínio necessário.

## Estrutura do DifficultyProfile

### Campos Originais
- **Description**: Descrição textual do nível
- **Suspects**: Faixa (min, max) de suspeitos
- **Documents**: Faixa (min, max) de documentos
- **Evidences**: Faixa (min, max) de evidências
- **ComplexityFactors**: Array de fatores de complexidade
- **EstimatedDurationMinutes**: Duração estimada (min, max)
- **RedHerrings**: Quantidade de pistas falsas
- **GatedDocuments**: Documentos com gating
- **ForensicsComplexity**: Nível de complexidade forense

### Novos Campos (EPIC 1.1)

#### 1. EvidenceRoles (Orçamento de Papéis de Evidência)
Define quantas evidências de cada tipo devem existir:
- **Conclusive**: Evidências conclusivas que provam algo definitivamente
- **Supporting**: Evidências de apoio que corroboram outras evidências
- **Ambiguous**: Evidências ambíguas que podem ter múltiplas interpretações
- **RedHerring**: Pistas falsas que desviam a investigação

```csharp
EvidenceRoles = new EvidenceRoleBudget
{
    Conclusive = (2, 3),
    Supporting = (1, 2),
    Ambiguous = (0, 0),
    RedHerring = (0, 0)
}
```

#### 2. ReasoningRequirements (Requisitos de Raciocínio)
Lista os tipos de raciocínio investigativo necessários:
- `timeline_basic`: Análise básica de linha do tempo
- `cross_document`: Correlação entre documentos
- `evidence_correlation`: Correlação de evidências
- `forensic_analysis`: Análise forense
- `deep_inference`: Inferência profunda
- `adversarial_reasoning`: Raciocínio adversarial

#### 3. PlannedContradictions (Contradições Planejadas)
Faixa de contradições intencionais que devem ser resolvidas:
```csharp
PlannedContradictions = (0, 1)  // 0 a 1 contradição planejada
```

#### 4. AlternativeInterpretations (Interpretações Alternativas)
Faixa de interpretações alternativas válidas:
```csharp
AlternativeInterpretations = (0, 1)
```

#### 5. MaxMediaVariantsPerEvidence (Variantes de Mídia)
Número máximo de representações visuais diferentes para a mesma evidência:
```csharp
MaxMediaVariantsPerEvidence = 1  // Apenas uma foto por evidência
```

#### 6. MediaDeterminism (Determinismo de Mídia)
Controla o grau de criatividade na geração de mídia:

```csharp
public enum MediaDeterminismLevel
{
    High,        // Sem variação, sem detalhes extras (Rookie/Detective)
    Medium,      // Variação controlada (Detective2/Sergeant)
    Controlled   // Variação apenas se role=ambiguous (Lieutenant+)
}
```

## Perfis por Nível

### Rookie
- **Evidências**: Maioria conclusiva, zero ambíguas
- **Raciocínio**: Timeline básico, evidência direta
- **Contradições**: 0
- **Determinismo**: Alto (sem variação)

### Detective
- **Evidências**: Mix conclusivo/suporte, até 1 ambígua, 1-2 red herrings
- **Raciocínio**: Timeline, cross-document, verificação de testemunhas
- **Contradições**: 0-1
- **Determinismo**: Alto

### Detective2
- **Evidências**: Balanceado, 1-2 ambíguas, 2-3 red herrings
- **Raciocínio**: Análise de timeline, correlação, lógica ramificada
- **Contradições**: 1-2
- **Determinismo**: Médio

### Sergeant
- **Evidências**: Mix variado, 2-3 ambíguas, 3-4 red herrings
- **Raciocínio**: Correlação multi-fonte, análise forense, avaliação de confiabilidade
- **Contradições**: 2-3
- **Determinismo**: Médio
- **Variantes**: Até 2 por evidência

### Lieutenant
- **Evidências**: Alta complexidade, 2-4 ambíguas, 4-5 red herrings
- **Raciocínio**: Timeline em camadas, dependências, análise técnica, testes de hipóteses
- **Contradições**: 3-4
- **Determinismo**: Controlado
- **Variantes**: Até 2 por evidência

### Captain
- **Evidências**: Muito complexo, 3-5 ambíguas, 5-6 red herrings
- **Raciocínio**: Inferência profunda, contrainteligência, análise adversarial
- **Contradições**: 4-5
- **Determinismo**: Controlado
- **Variantes**: Até 3 por evidência

### Commander
- **Evidências**: Extremamente complexo, 4-6 ambíguas, 7-8 red herrings
- **Raciocínio**: Padrões seriais, correlação global, jurisdição internacional
- **Contradições**: 5-7
- **Determinismo**: Controlado
- **Variantes**: Até 3 por evidência

## Uso no Sistema

### Obter Profile
```csharp
var profile = DifficultyLevels.GetProfile(difficulty);
```

### Acessar Orçamento de Evidências
```csharp
var (minConclusive, maxConclusive) = profile.EvidenceRoles.Conclusive;
```

### Verificar Requisitos de Raciocínio
```csharp
if (profile.ReasoningRequirements.Contains("forensic_analysis"))
{
    // Incluir análise forense
}
```

### Aplicar Determinismo de Mídia
```csharp
switch (profile.MediaDeterminism)
{
    case MediaDeterminismLevel.High:
        // Sem variação nos prompts
        break;
    case MediaDeterminismLevel.Medium:
        // Variação controlada
        break;
    case MediaDeterminismLevel.Controlled:
        // Variação apenas para evidências ambíguas
        break;
}
```

## Próximos Passos (EPIC 1.2)

- [ ] Injetar o profile nos prompts de Plan
- [ ] Injetar o profile nos prompts de Design
- [ ] Injetar o profile nos prompts de Generate
- [ ] Injetar o profile nos prompts de QA
- [ ] Adicionar logging do profile em cada chamada LLM

## Validações (EPIC 6)

O Normalizer deve validar:
- Contagem real de evidências por role vs budget do profile
- Número de contradições dentro da faixa permitida
- Número de interpretações alternativas dentro da faixa
- Variantes de mídia não excedem o máximo permitido

## Benefícios

1. **Previsibilidade**: Cada nível segue regras formais
2. **Consistência**: Todas as fases usam as mesmas definições
3. **Qualidade**: Ambiguidade é intencional e controlada
4. **Jogabilidade**: Casos são solucionáveis por raciocínio lógico
5. **Serializável**: Profile pode ser exportado como JSON para debugging
