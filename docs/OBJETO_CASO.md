# Sistema Objeto Caso - Documentação Completa

## Visão Geral

O **Sistema Objeto Caso** é uma implementação completa e modular para criação, gerenciamento e execução de casos investigativos no jogo CaseZero. Cada caso é representado por uma estrutura de arquivos organizada que permite experiências narrativas controladas e progressivas.

## Estrutura do Sistema

### 1. Arquivo Principal: `case.json`

O arquivo `case.json` é o coração de cada caso, contendo toda a lógica, dados e configurações necessárias:

```json
{
  "caseId": "Case001",
  "metadata": { ... },
  "evidences": [ ... ],
  "suspects": [ ... ],
  "forensicAnalyses": [ ... ],
  "temporalEvents": [ ... ],
  "timeline": [ ... ],
  "solution": { ... },
  "unlockLogic": { ... },
  "gameMetadata": { ... }
}
```

### 2. Estrutura de Pastas

```
CaseXXX/
├── case.json                    # Configuração principal do caso
├── evidence/                    # Evidências físicas e digitais
│   ├── documento.pdf
│   ├── foto.jpg
│   └── video.mp4
├── suspects/                    # Arquivos descritivos dos suspeitos
│   ├── suspeito1.txt
│   └── suspeito2.txt
├── forensics/                   # Resultados de análises forenses
│   ├── dna_resultado.pdf
│   └── digitais_resultado.pdf
├── memos/                       # Memorandos temporais
│   └── memo_chefe.txt
└── witnesses/                   # Depoimentos de testemunhas
    └── testemunha.pdf
```

## Componentes do Sistema

### 📋 Metadados (Metadata)

Informações básicas do caso:
- **Identificação**: Título, descrição, localização
- **Temporalidade**: Data/hora de início e do incidente
- **Dificuldade**: Nível (1-10) e tempo estimado
- **Requisitos**: Rank mínimo necessário
- **Vítima**: Informações completas sobre a vítima

### 🔍 Sistema de Evidências

Cada evidência possui:
- **Identificação única** (EVD001, EVD002, etc.)
- **Metadados**: Nome, tipo, categoria, prioridade
- **Localização**: Caminho do arquivo físico
- **Estado**: Desbloqueada ou bloqueada
- **Dependências**: Outras evidências necessárias
- **Análises**: Tipos de análise forense requeridas
- **Condições de desbloqueio**: Lógica temporal e condicional

#### Tipos de Evidência:
- `document` - Documentos (PDFs, textos)
- `image` - Imagens (fotografias, capturas)
- `video` - Vídeos (câmeras de segurança)
- `audio` - Áudios (gravações, chamadas)
- `physical` - Objetos físicos (armas, materiais)
- `digital` - Dados digitais (logs, imagens forenses)

#### Categorias:
- `Document` - Documentos e relatórios
- `Digital` - Mídia digital e eletrônica
- `Physical` - Evidências físicas
- `Biological` - Materiais biológicos
- `Communication` - Comunicações
- `Technical` - Análises técnicas

### 👤 Sistema de Suspeitos

Perfis completos incluindo:
- **Dados pessoais**: Nome, idade, profissão, apelido
- **Relacionamento**: Com a vítima e o caso
- **Motivo**: Razões para cometer o crime
- **Álibi**: Alegação de paradeiro + verificação
- **Comportamento**: Durante interrogatórios
- **Background**: Histórico pessoal relevante
- **Evidências ligadas**: Quais evidências os conectam
- **Status**: Se é o culpado real (interno do sistema)
- **Desbloqueio**: Condições para aparecer na investigação

### 🔬 Sistema de Análises Forenses

Simulação realista de laboratório:
- **Tipos disponíveis**: DNA, Impressões Digitais, Balística, Toxicologia, etc.
- **Tempo de resposta**: Simulação realista (60-240 minutos)
- **Arquivos de resultado**: Relatórios técnicos detalhados
- **Precedência**: Algumas análises dependem de outras

#### Análises Suportadas:
- `DNA` - Análise genética
- `Fingerprint` - Impressões digitais
- `DigitalForensics` - Perícia digital
- `Ballistics` - Balística
- `Toxicology` - Toxicologia
- `HandwritingAnalysis` - Grafotécnica
- `VoiceAnalysis` - Análise de voz
- `Trace` - Vestígios (fibras, tintas)

### ⏰ Eventos Temporais

Progressão narrativa controlada:
- **Memos do chefe**: Pressão por resultados
- **Novas testemunhas**: Depoimentos adicionais
- **Atualizações do laboratório**: Status das análises
- **Novos documentos**: Evidências que chegam com tempo
- **Telefonemas/emails**: Comunicações programadas

### 📅 Timeline do Crime

Reconstrução cronológica:
- **Eventos ordenados**: Sequência temporal precisa
- **Fontes verificadas**: Origem de cada informação
- **Horários críticos**: Álibis, oportunidades, evidências

### ✅ Sistema de Solução

Definição da resposta correta:
- **Culpado**: Nome do criminoso real
- **Evidência chave**: Prova definitiva
- **Evidências de apoio**: Conjunto probatório
- **Explicação**: Como o crime aconteceu
- **Pontuação mínima**: Score necessário para sucesso

### 🔓 Lógica de Desbloqueio

Sistema complexo de progressão:

#### Regras de Progressão:
```json
{
  "condition": "evidenceExamined",
  "target": "EVD001",
  "unlocks": ["EVD004"],
  "delay": 30
}
```

#### Tipos de Condição:
- `evidenceExamined` - Evidência foi examinada
- `analysisComplete` - Análise foi concluída
- `suspectInterrogated` - Suspeito foi interrogado
- `timeElapsed` - Tempo específico passou

#### Regras de Análise:
```json
{
  "evidenceId": "EVD003",
  "analysisType": "DNA",
  "unlocks": ["solution.keyEvidence"],
  "critical": true
}
```

## API REST

### Endpoints Disponíveis

#### Listagem e Carregamento
- `GET /api/caseobject` - Lista casos disponíveis
- `GET /api/caseobject/{caseId}` - Carrega caso completo
- `GET /api/caseobject/{caseId}/validate` - Valida estrutura

#### Componentes Específicos
- `GET /api/caseobject/{caseId}/metadata` - Metadados apenas
- `GET /api/caseobject/{caseId}/evidences` - Lista evidências
- `GET /api/caseobject/{caseId}/suspects` - Lista suspeitos
- `GET /api/caseobject/{caseId}/timeline` - Timeline do crime

#### Arquivos
- `GET /api/caseobject/{caseId}/files/{fileName}` - Arquivo específico

### Autenticação

Todas as rotas requerem autenticação JWT:
```bash
curl -H "Authorization: Bearer {token}" \
  http://localhost:5000/api/caseobject/{caseId}
```

## Criação de Novos Casos

### Passo a Passo

1. **Criar estrutura de pastas**:
```bash
mkdir -p CaseXXX/{evidence,suspects,forensics,memos,witnesses}
```

2. **Criar case.json** baseado no template

3. **Adicionar arquivos de evidência** nas subpastas

4. **Testar com validação**:
```bash
curl -H "Authorization: Bearer {token}" \
  http://localhost:5000/api/caseobject/CaseXXX/validate
```

### Template Básico

```json
{
  "caseId": "CaseXXX",
  "metadata": {
    "title": "Título do Caso",
    "description": "Descrição detalhada",
    "startDateTime": "2024-01-01T08:00:00Z",
    "location": "Local do crime",
    "incidentDateTime": "2024-01-01T02:00:00Z",
    "victimInfo": {
      "name": "Nome da Vítima",
      "age": 35,
      "occupation": "Profissão",
      "causeOfDeath": "Causa da morte"
    },
    "briefing": "Briefing inicial para o detetive",
    "difficulty": 5,
    "estimatedDuration": "2-3 horas",
    "minRankRequired": "Detective"
  },
  "evidences": [ /* evidências */ ],
  "suspects": [ /* suspeitos */ ],
  "forensicAnalyses": [ /* análises */ ],
  "temporalEvents": [ /* eventos */ ],
  "timeline": [ /* timeline */ ],
  "solution": { /* solução */ },
  "unlockLogic": { /* lógica */ },
  "gameMetadata": { /* metadados do jogo */ }
}
```

## Melhores Práticas

### 🎯 Design de Casos

1. **Complexidade progressiva**: Comece simples, aumente gradualmente
2. **Múltiplos suspeitos**: Pelo menos 3 com motivos plausíveis
3. **Evidências interconectadas**: Crie dependências lógicas
4. **Red herrings**: Evidências que distraem mas não são definitivas
5. **Solução única**: Uma resposta correta clara e bem fundamentada

### 📝 Narrativa

1. **Briefing claro**: Contexto suficiente para começar
2. **Progressão lógica**: Cada descoberta leva naturalmente à próxima
3. **Timing realista**: Análises e eventos com tempos convincentes
4. **Detalhes consistentes**: Informações que se complementam
5. **Conclusão satisfatória**: Solução que amarra todas as pontas

### 🔧 Técnico

1. **Validação completa**: Sempre testar com endpoint de validação
2. **Arquivos presentes**: Garantir que todos os arquivos existem
3. **IDs únicos**: Evidências, suspeitos e eventos com identificadores únicos
4. **Paths relativos**: Usar caminhos relativos à pasta do caso
5. **JSON válido**: Verificar sintaxe antes de testar

### 🎮 Gameplay

1. **Dificuldade balanceada**: Nem muito fácil, nem impossível
2. **Múltiplos caminhos**: Diferentes ordens de investigação possíveis
3. **Feedback claro**: Sistema deve indicar progresso
4. **Recompensas**: Desbloqueios satisfatórios
5. **Rejogabilidade**: Detalhes que podem ser perdidos na primeira vez

## Exemplo: Case001

O **Case001** ("Homicídio no Edifício Corporativo") serve como exemplo completo e template para novos casos. Inclui:

- ✅ **6 evidências** com progressão complexa
- ✅ **3 suspeitos** com perfis completos
- ✅ **4 análises forenses** com resultados realistas
- ✅ **3 eventos temporais** para progressão narrativa
- ✅ **Timeline detalhada** para reconstrução
- ✅ **Solução clara** com evidências definitivas
- ✅ **Lógica de desbloqueio** sofisticada

### Progressão do Case001:
1. Examinar evidências iniciais (relatório, fotos, arma)
2. Aguardar desbloqueio dos logs de acesso (30 min)
3. Analisar logs → desbloqueia câmeras (45 min)
4. Análise das câmeras → desbloqueia laptop (60 min)
5. Análises forenses da arma → confirma culpado
6. Eventos temporais adicionam pressão e contexto
7. Solução: Marina Silva com evidências de DNA e digitais

## Configuração do Sistema

### Backend (.NET)

1. **Service**: `CaseObjectService` gerencia carregamento e validação
2. **Controller**: `CaseObjectController` expõe API REST
3. **Models**: `CaseObject` e classes relacionadas para JSON
4. **Configuração**: `appsettings.json` define caminho dos casos

### Configuração em `appsettings.json`:
```json
{
  "CasesBasePath": "../../cases"
}
```

### Registro no `Program.cs`:
```csharp
builder.Services.AddScoped<ICaseObjectService, CaseObjectService>();
```

## Expansibilidade

O sistema foi projetado para fácil expansão:

### 🆕 Novos Tipos de Evidência
Adicionar ao enum `EvidenceCategory` e ajustar lógica de handling

### 🔬 Novas Análises Forenses  
Estender `ForensicAnalysisType` e implementar lógica específica

### ⏰ Novos Tipos de Eventos
Ampliar `TemporalEventType` para incluir novos tipos de eventos

### 🎮 Novas Mecânicas
Sistema de unlock logic permite condições complexas e customizadas

### 🌐 Internacionalização
Estrutura JSON facilita tradução de conteúdo para outros idiomas

O Sistema Objeto Caso fornece uma base sólida e flexível para criar experiências investigativas ricas e envolventes no CaseZero, mantendo a modularidade e facilidade de expansão que permite o crescimento orgânico da biblioteca de casos.