# Sistema de Perfis de Dificuldade (Difficulty Profile System)

## Visão Geral

A implementação atual de dificuldade de casos v2 não usa mais o modelo antigo com `Documents`, `Evidences`, `RedHerrings`, `EvidenceRoleBudget`, `MediaDeterminism` ou campos da antiga EPIC 1.1. O comportamento real está centralizado em `functions/CaseGen.Functions/Services/CaseV2/DifficultyProfileCatalog.cs` e é validado por `DifficultyTopologyValidator`, `PipelineStageValidator`, `EvidenceContractValidator`, `CaseBibleValidator` e `CaseV2FinalValidation`.

Hoje a dificuldade é composta por dois blocos:

1. `DifficultyProfile`: orçamento estrutural do caso.

2. `DifficultyTopology`: requisitos lógicos/topológicos do caminho de prova.

## Estrutura atual de `DifficultyProfile`

```csharp
public sealed record DifficultyProfile(
    string Name,
    int MinAssets,
    int MaxAssets,
    int MinInvestigativeAssets,
    int MaxInvestigativeAssets,
    int MinScenePhotos,
    int MaxScenePhotos,
    int MinFamilies,
    int MaxRarity,
    int MinSuspects,
    int MaxSuspects,
    int MinAnalyses,
    int MaxAnalyses,
    int MinIndependentCulpritSources,
    int MaxRepairIterations,
    bool AllEvidenceInitial)
{
    public DifficultyTopology Topology { get; init; } = new();
}
```

### Significado dos campos

- `MinAssets` / `MaxAssets`: total de assets no dossiê.

- `MinInvestigativeAssets` / `MaxInvestigativeAssets`: assets investigativos (`primary` ou `corroborative`), excluindo contextuais.

- `MinScenePhotos` / `MaxScenePhotos`: quantidade de fotos de cena.

- `MinFamilies`: diversidade mínima de famílias de evidência (`official`, `testimonial`, `digital`, etc.).

- `MaxRarity`: limite de raridade dos arquétipos usados como fillers.

- `MinSuspects` / `MaxSuspects`: faixa obrigatória de suspeitos.

- `MinAnalyses` / `MaxAnalyses`: quantidade de resultados forenses úteis (`findings == true`).

- `MinIndependentCulpritSources`: número mínimo de origens independentes sustentando a culpa.

- `MaxRepairIterations`: teto do loop de reparo/finalização.

- `AllEvidenceInitial`: se toda a evidência resolutiva precisa estar disponível desde o início.

## Estrutura atual de `DifficultyTopology`

```csharp
public sealed class DifficultyTopology
{
    public int MinDerivationDepth { get; init; }
    public int MaxDerivationDepth { get; init; } = int.MaxValue;
    public int MinIndependentProofPaths { get; init; } = 1;
    public int RequiredDecoyArcs { get; init; }
    public int MinForensicHops { get; init; }
    public int RedHerringResolutionDepth { get; init; } = 1;
    public int RequiredCrossSourceCorrelations { get; init; }
    public bool InitialSolutionAllowed { get; init; }
    public bool RequiresConflictingObservation { get; init; }
    public bool RequiresPartiallyOverlappingProofPaths { get; init; }
    public int MinOptionalAnalyses { get; init; }
    public int MinReliabilityLevels { get; init; } = 1;
    public bool RequiresMeaningfulInvestigationOrder { get; init; }
}
```

### Significado dos campos topológicos

- `MinDerivationDepth` / `MaxDerivationDepth`: profundidade mínima/máxima da prova contra o culpado.

- `MinIndependentProofPaths`: quantidade mínima de trilhas independentes de prova.

- `RequiredDecoyArcs`: mínimo de arcos de suspeita falsa (`decoy arcs`).

- `MinForensicHops`: número mínimo de passos forenses no caminho da prova.

- `RedHerringResolutionDepth`: profundidade mínima para resolver pistas falsas.

- `RequiredCrossSourceCorrelations`: correlações mínimas entre fontes distintas.

- `InitialSolutionAllowed`: permite ou não solução puramente com evidência inicial.

- `RequiresConflictingObservation`: exige ao menos uma observação/fato conflitante, mas explicável.

- `RequiresPartiallyOverlappingProofPaths`: exige caminhos de prova com sobreposição parcial.

- `MinOptionalAnalyses`: quantidade mínima de análises opcionais além das obrigatórias.

- `MinReliabilityLevels`: diversidade mínima de níveis de confiabilidade.

- `RequiresMeaningfulInvestigationOrder`: exige pré-requisitos reais na ordem de investigação.

## Perfis por nível

### Orçamento estrutural

| Nível | Assets | Investigativos | Fotos de cena | Famílias mín. | Raridade máx. | Suspeitos | Resultados forenses úteis | Fontes independentes do culpado | Reparo máx. | Toda evidência inicial? |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|
| Rookie | 10-14 | 5-7 | 1-2 | 3 | 1 | 3-4 | 0-0 | 2 | 3 | Sim |
| Detective | 12-16 | 6-8 | 1-2 | 5 | 2 | 3-4 | 1-2 | 2 | 3 | Não |
| Detective2 | 13-17 | 7-9 | 1-2 | 5 | 2 | 4-5 | 1-2 | 2 | 3 | Não |
| Sergeant | 14-18 | 8-10 | 2-3 | 6 | 3 | 4-5 | 1-3 | 2 | 3 | Não |
| Lieutenant | 15-19 | 8-11 | 2-3 | 6 | 3 | 4-6 | 2-3 | 3 | 3 | Não |
| Captain | 16-20 | 9-12 | 2-3 | 6 | 3 | 5-6 | 2-4 | 3 | 3 | Não |
| Commander | 17-22 | 10-13 | 2-4 | 6 | 3 | 5-6 | 2-4 | 3 | 3 | Não |

### Requisitos topológicos

| Nível | Profundidade | Trilhas independentes | Decoys | Hops forenses | Prof. resolução de red herring | Correlações cross-source | Solução inicial? | Conflito obrigatório? | Sobreposição parcial? | Análises opcionais mín. | Níveis de confiabilidade | Ordem significativa? |
|---|---|---:|---:|---:|---:|---:|---|---|---|---:|---:|---|
| Rookie | 1-2 | 2 | 2 | 0 | 1 | 0 | Sim | Não | Não | 0 | 1 | Não |
| Detective | 3-4 | 2 | 2 | 1 | 1 | 1 | Não | Não | Não | 0 | 1 | Sim |
| Detective2 | 4-5 | 2 | 3 | 1 | 2 | 1 | Não | Sim | Sim | 1 | 2 | Sim |
| Sergeant | 4-6 | 2 | 3 | 1 | 2 | 1 | Não | Sim | Sim | 1 | 2 | Sim |
| Lieutenant | 5-7 | 3 | 3 | 2 | 2 | 2 | Não | Sim | Não | 1 | 3 | Sim |
| Captain | 5-8 | 3 | 4 | 2 | 2 | 2 | Não | Sim | Não | 1 | 3 | Sim |
| Commander | 6-9 | 3 | 4 | 3 | 2 | 2 | Não | Sim | Não | 1 | 3 | Sim |

## Efeito real no pipeline

### 1. Case Bible / blueprint

`CaseBibleTask` injeta no prompt:

- dificuldade canônica

- faixa de suspeitos

- mínimo de proof paths independentes

- mínimo de origens não forenses para a culpa

- mínimo de `decoyArcs`

- mínimo de `forensicOpportunities`

- flag `all_evidence_initial`

- exigência de observação conflitante

Além disso, `CaseBibleValidator` e `PipelineStageValidator` impõem regras extras por dificuldade:

- sempre deve haver exatamente **um** clue decisivo contra o culpado;

- em `Rookie`, nenhum clue/oportunidade forense é permitido;

- fora de `Rookie`, o clue decisivo do culpado deve ser forense;

- fora de `Rookie`, a cadeia do culpado precisa incluir pelo menos um clue de `identity`, um de `action` e um decisivo de `forensicAttribution`.

### 2. Asset plan / dossiê

`EvidenceArchetypeCatalog` usa o profile para definir:

- total de assets;

- total de assets investigativos;

- quantidade de fotos de cena;

- diversidade mínima de famílias de evidência;

- raridade máxima de arquétipos filler.

Regras estruturais atualmente obrigatórias:

- sempre existe um `incident_report` inicial;

- cada suspeito recebe exatamente **um** retrato (`suspect_portrait`) e **uma** entrevista/transcrição;

- assets `contextual` não podem carregar clues resolutivos nem virar `requiredEvidenceIds`.

### 3. Forense

`CaseV2GeneratorService` pula toda a etapa forense quando o caso é `Rookie`.

Para os demais níveis:

- `ForensicsPlanTask` calcula a quantidade desejada de outcomes com base em `MinAnalyses`, `MaxAnalyses` e `Topology.MinOptionalAnalyses`;

- `PipelineStageValidator.ValidateForensics` exige que a quantidade de outcomes úteis fique dentro da faixa do profile;

- `SolutionSkeletonTask` reserva pelo menos `MinOptionalAnalyses` como opcionais, para não tornar toda análise obrigatória na solução.

### 4. Topologia e validação final

`DifficultyTopologyValidator` valida, no grafo final:

- profundidade real da prova do culpado;

- quantidade de proof paths independentes;

- quantidade de decoys;

- hops forenses reais;

- correlação entre fontes;

- existência de conflitos explicáveis;

- sobreposição parcial (quando exigida);

- número mínimo de níveis de confiabilidade;

- ordem de investigação com pré-requisitos.

`CaseV2FinalValidation` sempre executa essa checagem; para `Rookie`, ainda roda `RookieRegressionValidator`.

## Contrato especial de Rookie

`Rookie` é o único nível com comportamento funcional especial:

- sem workflow forense;

- `requiredAnalysisIds` deve ficar vazio;

- toda evidência necessária à solução deve estar acessível desde o início;

- `unlockMode` público deve ser `all_initial`;

- regras `forensics_complete` são removidas do JSON final.

Em outras palavras: `Rookie` não é só “mais fácil”; é um modo sem dependência de laboratório.

## Resumo prático

- A dificuldade atual é orientada por **orçamento estrutural + topologia de prova**, não por contadores genéricos de documentos.

- O principal salto entre níveis altos não é apenas “mais assets”, mas sim:

  - mais suspeitos;

  - mais caminhos independentes;

  - mais decoys;

  - mais hops forenses;

  - mais correlação entre fontes;

  - maior profundidade lógica;

  - maior necessidade de ordem investigativa real.

- `MaxRepairIterations` está em **3** para todos os níveis no estado atual do código.
