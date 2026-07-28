# Case Generator Setup

Este guia descreve como configurar, validar e implantar o gerador Case v2 em
`functions/CaseGen.Functions`.

Documentos relacionados:

- [`CASE_GENERATION_PIPELINE.md`](CASE_GENERATION_PIPELINE.md): arquitetura, fases, agentes, retries e gates de qualidade;
- [`RUNNING_FUNCTIONS_LOCALLY.md`](RUNNING_FUNCTIONS_LOCALLY.md): execução local detalhada;
- [`API_COMPLETE.md`](API_COMPLETE.md): contratos HTTP;
- [`CASE_JSON_V2_SPEC.md`](CASE_JSON_V2_SPEC.md): contrato público de `case.json`;
- [`DEPLOYMENT.md`](DEPLOYMENT.md): deploy do sistema completo.

## Arquitetura operacional

`CaseGen.Functions` é uma Azure Function isolada em .NET 9 com Durable
Functions. O fluxo publicado é:

```text
Frontend
  → CaseZeroApi autenticada (ADMIN)
  → POST /api/casegeneration/generate
  → POST /api/cases/v2/generate na Function App
  → Durable orchestration
  → até cinco tentativas completas por padrão
  → validação, solver, renderização e publicação
  → container bundles
```

O frontend não deve conhecer chaves nem chamar a Function App diretamente. Em
produção, ele usa `VITE_API_URL` e os endpoints autenticados do `CaseZeroApi`.

As rotas HTTP da Function App usam autorização `Anonymous` no código. O acesso
direto deve ser protegido pela topologia de rede e nunca tratado como uma API
pública para clientes.

## Pré-requisitos

| Ferramenta | Versão/uso |
|---|---|
| .NET SDK | 9.x para `CaseGen.Functions` e `CaseGen.Functions.Tests` |
| Node.js | 20.x ou superior |
| Azure Functions Core Tools | v4 |
| Azurite | Storage local para Functions e Durable Task |
| Azure CLI | autenticação, infraestrutura e diagnóstico |
| Azure Foundry | endpoint, deployment de texto, deployment de imagem e chave |

O backend principal continua em .NET 8. Não altere o target framework dos
projetos da Function e dos testes para uma versão diferente de .NET 9.

## Configuração local

Crie `functions/CaseGen.Functions/local.settings.json`. O arquivo contém
segredos e não deve ser commitado.

Exemplo mínimo:

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "CaseGeneratorStorage__ConnectionString": "UseDevelopmentStorage=true",
    "CaseGeneratorStorage__BundlesContainer": "bundles",
    "AzureFoundry__Endpoint": "https://<resource>.openai.azure.com/openai/v1",
    "AzureFoundry__ModelName": "<text-deployment>",
    "AzureFoundry__ImageDeploymentName": "<image-deployment>",
    "AzureFoundry__ApiKey": "<secret>",
    "CaseGenV2__DisableBlobPublishing": "false",
    "CaseGenV2__JobMaxAttempts": "5"
  }
}
```

As chaves com `__` são carregadas como seções .NET. Por exemplo,
`AzureFoundry__ModelName` corresponde a `AzureFoundry:ModelName`.

### Configurações principais

| Chave | Finalidade |
|---|---|
| `AzureFoundry__Endpoint` | Endpoint HTTPS compatível com a API OpenAI v1 |
| `AzureFoundry__ModelName` | Deployment do modelo de texto estruturado |
| `AzureFoundry__ImageDeploymentName` | Deployment do modelo de imagem |
| `AzureFoundry__ApiKey` | Credencial do Foundry |
| `CaseGeneratorStorage__AccountName` | Conta usada com identidade gerenciada no Azure |
| `CaseGeneratorStorage__ConnectionString` | Storage local ou fallback |
| `CaseGeneratorStorage__BundlesContainer` | Container publicado; padrão `bundles` |
| `CaseGenV2__CasesBasePath` | Diretório local de saída |
| `CaseGenV2__DisableBlobPublishing` | Desabilita publicação no Blob |
| `CaseGenV2__CaseGraphEnabled` | Seleciona o contrato público compilado pelo grafo |
| `CaseGenV2__JobMaxAttempts` | Tentativas completas; padrão `5`, máximo `10` |
| `CaseGenV2__RepairMaxIterations` | Sobrescreve o limite de reparos da dificuldade |
| `CaseGenV2__RookieMaxMediumFindings` | Tolerância de achados médios para Rookie |

## Executar localmente

Os scripts preparam Azurite, schema, build e Functions host.

Windows:

```powershell
.\scripts\run-functions.ps1
```

macOS/Linux:

```bash
./scripts/run-functions.sh
```

O host fica disponível em `http://localhost:7071`.

Para detalhes sobre processos, arquivos persistidos e troubleshooting local,
consulte [`RUNNING_FUNCTIONS_LOCALLY.md`](RUNNING_FUNCTIONS_LOCALLY.md).

## Gerar um caso local

Inicie o job:

```powershell
$body = @{
  caseId = "case_setup_example"
  theme = "Homicídio em um armazém portuário"
  location = "Santos, SP"
  difficulty = "Detective"
  requiredRank = "Detective"
  language = "pt-BR"
  seed = 423
  writeToDisk = $true
} | ConvertTo-Json

$job = Invoke-RestMethod `
  -Method Post `
  -Uri "http://localhost:7071/api/cases/v2/generate" `
  -ContentType "application/json" `
  -Body $body
```

Consulte o progresso:

```powershell
Invoke-RestMethod `
  -Uri "http://localhost:7071/api/cases/v2/jobs/$($job.jobId)"
```

O status retorna a tentativa atual, retries, fase interna e estágio público. Os
valores terminais são `done` e `failed`.

Em uma aplicação cliente, use o proxy da API:

```text
POST /api/casegeneration/generate
GET  /api/casegeneration/jobs/{jobId}
```

Esses endpoints exigem JWT com papel `ADMIN`.

## Persistência e publicação

Quando `writeToDisk=true` e todos os gates passam:

```text
cases/<caseId>/case.json
cases/<caseId>/assets/*
```

Quando o publisher está configurado:

```text
bundles/<caseId>/assets/*
bundles/<caseId>/case.json
```

Os assets são enviados primeiro. `case.json` é enviado por último e funciona
como marcador de commit do bundle. Uma falha de publicação configurada é
propagada; o job não pode reportar sucesso com um bundle incompleto.

Somente casos com validação bloqueante zerada, solver aprovado com score mínimo
de `0.90` e renderização visual obrigatória concluída podem ser persistidos.

## Validação

Build e testes da Function:

```powershell
dotnet build functions\CaseGen.Functions\CaseGen.Functions.csproj -c Release
dotnet test functions\CaseGen.Functions.Tests\CaseGen.Functions.Tests.csproj -c Release
```

Teste direcionado do Case v2:

```powershell
dotnet test functions\CaseGen.Functions.Tests\CaseGen.Functions.Tests.csproj `
  -c Release `
  --filter "FullyQualifiedName~CaseGen.Functions.Tests.CaseV2"
```

Geração real direta:

```powershell
.\scripts\validate-casev2.ps1 `
  -Mode DirectGenerate `
  -Difficulty Detective `
  -Seed 423 `
  -Theme "harbor warehouse homicide" `
  -Language en-US `
  -CaseId case_setup_validation_423
```

Soak sequencial:

```powershell
.\scripts\run-casev2-soak.ps1 `
  -CasesPerDifficulty 3 `
  -CooldownSeconds 60 `
  -MaxAttempts 5 `
  -Language en-US `
  -ReportPath casev2-soak-report.json
```

Gerações reais são lentas e não determinísticas. Testes unitários e goldens
validam estrutura rapidamente; mudanças de comportamento do gerador devem ser
confirmadas com geração real.

## Infraestrutura Azure

A infraestrutura completa é definida por `infrastructure/main.bicep` e
implantada pelo workflow:

```text
.github/workflows/infrastructure-3tier.yml
```

O workflow suporta `validate`, `deploy` e `destroy` para `dev` e `prod`. Ele usa
os arquivos `infrastructure/parameters.<environment>.json` e executa a
implantação em `canadacentral`.

O ambiente da Function inclui:

- Function App Linux com runtime .NET 9 isolated;
- Durable Functions e Storage de runtime;
- Application Insights;
- identidade gerenciada;
- integração com VNet;
- Storage com acesso público desabilitado e private endpoints;
- containers `cases`, `bundles`, `case-context` e `logs`;
- referências do Azure Foundry resolvidas pelo Key Vault.

No ambiente DEV, os nomes operacionais usados pelo pipeline são:

| Recurso | Nome |
|---|---|
| Function App | `casegen-func-dev` |
| Resource group | `casezero-func-dev-rg` |
| API Web App | `casezero-api-dev` |

Não dependa de nomes derivados da conta de Storage ou do Key Vault. Obtenha-os
pelos outputs da implantação ou pelo Azure Resource Graph.

### Segredos do Key Vault

A infraestrutura referencia:

- `azure-foundry-endpoint`;
- `azure-foundry-api-key`;
- `azure-foundry-model-name`;
- `azure-foundry-image-deployment-name`.

A identidade gerenciada da Function App precisa ler esses segredos. O acesso ao
Storage deve usar RBAC e conectividade privada conforme definido pela
infraestrutura.

## Deploy da aplicação

O build, os testes e o deploy de DEV são feitos por:

```text
.github/workflows/cd-dev.yml
```

O workflow:

1. instala .NET 8 para a API e .NET 9 para Functions;
2. constrói frontend, API e Function;
3. executa os testes dos três projetos de backend e do frontend;
4. valida os casos versionados contra o schema;
5. publica os artefatos;
6. implanta API, Function App e Static Web App.

O deploy usa `AZURE_CREDENTIALS_DEV`. A Function é publicada com
`Azure/functions-action`, preservando o diretório `.azurefunctions` necessário
ao pacote de runtime.

Alterações somente em Markdown ou `docs/` são ignoradas no gatilho automático
de push do workflow. Execute o workflow manualmente apenas quando for necessário
reimplantar a aplicação sem uma mudança de código.

## Monitoramento

Use o Application Insights para acompanhar:

- falhas por tentativa e erro terminal;
- duração de fases e do job;
- dependências do Azure Foundry;
- respostas `408`, `429` e `5xx`;
- consumo de tokens;
- falhas de renderização;
- quantidade de blobs publicados;
- retries e score do solver.

O endpoint de status também expõe `currentAttempt`, `maxAttempts`,
`nextRetryAt`, `retryReason`, `currentPhase`, `currentStageId`, histórico de
estágios e resultado final.

## Troubleshooting

### Foundry não configurado

Erros como `AzureFoundry:Endpoint not configured` indicam variável ausente. No
Azure, confira também se as referências do Key Vault estão com status
`Resolved`.

### Endpoint inválido

`AzureFoundry:Endpoint` deve ser uma URI HTTPS absoluta. Não use o nome do
recurso isoladamente.

### Storage inacessível

No Azure, confirme:

1. integração da Function com a VNet;
2. resolução DNS do private endpoint;
3. RBAC da identidade gerenciada;
4. existência do container `bundles`;
5. configuração `CaseGeneratorStorage__AccountName`.

Em desenvolvimento local, confirme que o Azurite está ativo e que
`AzureWebJobsStorage` usa `UseDevelopmentStorage=true`.

### Job retorna `409`

Existe outra geração ativa. Consulte o `jobId` retornado e aguarde o término. O
controle de concorrência é best-effort e permite somente uma geração ativa.

### Job faz retry

Retries são esperados no MVP. O objetivo é publicar somente casos válidos e
jogáveis, não garantir sucesso na primeira tentativa. Consulte `retryReason`,
`nextRetryAt` e o histórico de tentativas.

### Caso não aparece no site

Confirme que:

1. o job terminou com `status=done`;
2. `blobsPublished` é maior que zero;
3. existe `bundles/<caseId>/case.json`;
4. todos os assets referenciados existem;
5. a API possui acesso de leitura ao Storage;
6. o cache da API já expirou ou foi renovado.

## Regras de segurança

- Nunca commite `local.settings.json`, chaves, connection strings ou tokens.
- Nunca envie credenciais do Foundry ao frontend.
- Use o proxy `ADMIN` do backend para geração iniciada pela interface.
- Mantenha o Storage privado e prefira identidade gerenciada.
- Trate `case.json` como marcador de publicação e não o envie antes dos assets.
- Não considere um diretório local válido como prova de publicação no Azure.
