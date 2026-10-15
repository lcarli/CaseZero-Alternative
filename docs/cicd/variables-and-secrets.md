# Variáveis e segredos de CI/CD

Esta referência contém somente valores usados pelos workflows atuais.

## Segredos GitHub

| Segredo | Workflow | Obrigatório |
|---|---|---|
| `AZURE_CREDENTIALS_DEV` | aplicação DEV e infraestrutura DEV | Sim |
| `AZURE_CREDENTIALS_PROD` | infraestrutura PROD | Para operações em PROD |
| `AZURE_STATIC_WEB_APPS_API_TOKEN_DEV` | deploy do frontend DEV | Sim |
| `VITE_API_URL` | build do frontend | Sim para o ambiente implantado |
| `SQL_ADMIN_LOGIN` | validação/deploy da infraestrutura | Sim |
| `SQL_ADMIN_PASSWORD` | validação/deploy da infraestrutura | Sim |
| `GITHUB_TOKEN` | Static Web Apps | Fornecido automaticamente pelo GitHub |

Os workflows atuais não usam:

- publish profiles da API ou da Function;
- `AZURE_RESOURCE_GROUP_DEV`/`PROD`;
- webhook do Teams;
- tokens de Static Web Apps para PROD;
- connection strings de banco como GitHub secrets;
- credenciais do Azure Foundry diretamente no workflow.

## Formato das credenciais Azure

`azure/login@v2` recebe um JSON semelhante a:

```json
{
  "clientId": "<application-id>",
  "clientSecret": "<secret>",
  "subscriptionId": "<subscription-id>",
  "tenantId": "<tenant-id>"
}
```

Não inclua esse JSON em arquivos versionados, issues, logs ou documentação.

## Variáveis dos workflows

### `cd-dev.yml`

```yaml
AZURE_WEBAPP_NAME: casezero-api-dev
AZURE_STATIC_WEB_APP_NAME: casezero-web-dev
AZURE_STATIC_WEB_APP_RG: casezero-web-dev-rg
AZURE_RESOURCE_GROUP: casezero-api-dev-rg
AZURE_FUNCTIONAPP_NAME: casegen-func-dev
AZURE_FUNCTIONAPP_RG: casezero-func-dev-rg
DOTNET_VERSION: 8.0.x
DOTNET_FUNCTIONS_VERSION: 9.0.x
NODE_VERSION: 20
```

### `infrastructure-3tier.yml`

```yaml
BICEP_FILE_PATH: infrastructure/main.bicep
```

O workflow usa `infrastructure/parameters.dev.json` ou
`infrastructure/parameters.prod.json` conforme o ambiente.

## Configurações implantadas pela infraestrutura

### API

As principais app settings incluem:

- `ASPNETCORE_ENVIRONMENT`;
- `JwtSettings__SecretKey`, por referência ao Key Vault;
- `JwtSettings__Issuer`;
- `JwtSettings__Audience`;
- `JwtSettings__ExpirationInMinutes`;
- `APPLICATIONINSIGHTS_CONNECTION_STRING`;
- `CaseGenerator__FunctionBaseUrl`;
- `CaseGeneratorStorage__AccountName`;
- `CaseGeneratorStorage__BundlesContainer`;
- `Cors__AllowedOrigins__*`.

### CaseGen.Functions

- `FUNCTIONS_EXTENSION_VERSION=~4`;
- `FUNCTIONS_WORKER_RUNTIME=dotnet-isolated`;
- `AzureWebJobsStorage`;
- `TaskHub=CaseGeneratorHub`;
- `APPLICATIONINSIGHTS_CONNECTION_STRING`;
- `CaseGeneratorStorage__AccountName`;
- `CaseGeneratorStorage__ConnectionString`;
- `CaseGeneratorStorage__BundlesContainer=bundles`;
- `AzureFoundry__Endpoint`;
- `AzureFoundry__ApiKey`;
- `AzureFoundry__ModelName`;
- `AzureFoundry__ImageDeploymentName`.

As configurações `AzureFoundry__*` são referências a segredos do Key Vault, não
valores gravados no template.

## Segredos do Key Vault

A infraestrutura atual referencia:

| Segredo | Consumidor |
|---|---|
| `jwt-signing-key` | API |
| `azure-foundry-endpoint` | Function |
| `azure-foundry-api-key` | Function |
| `azure-foundry-model-name` | Function |
| `azure-foundry-image-deployment-name` | Function |

Os parâmetros SQL são fornecidos ao workflow durante a implantação e devem ser
tratados como credenciais sensíveis.

## Criar ou rotacionar credenciais do workflow

Prefira federação OIDC quando o workflow for modernizado. Enquanto os workflows
usarem `creds`, crie um service principal e armazene o JSON somente no GitHub:

```powershell
az ad sp create-for-rbac `
  --name "casezero-dev-github" `
  --role Contributor `
  --scopes "/subscriptions/<subscription-id>" `
  --sdk-auth
```

O escopo de assinatura é necessário porque o IaC realiza deployments em nível
de assinatura e cria resource groups. Depois, reduza permissões adicionais
sempre que a arquitetura permitir.

Para obter o token da Static Web App DEV:

```powershell
az staticwebapp secrets list `
  --name casezero-web-dev `
  --resource-group casezero-web-dev-rg `
  --query properties.apiKey `
  --output tsv
```

## Checklist

- [ ] `AZURE_CREDENTIALS_DEV` autentica na assinatura correta.
- [ ] `AZURE_CREDENTIALS_PROD` existe antes de operar PROD.
- [ ] `AZURE_STATIC_WEB_APPS_API_TOKEN_DEV` pertence a `casezero-web-dev`.
- [ ] `VITE_API_URL` aponta para a API, incluindo o prefixo esperado pelo frontend.
- [ ] Credenciais SQL não aparecem em parâmetros versionados.
- [ ] Referências do Key Vault aparecem como `Resolved`.
- [ ] Identidades gerenciadas possuem somente os roles necessários.
- [ ] Nenhum `local.settings.json`, token ou connection string está versionado.

## Diagnóstico seguro

Liste apenas nomes de secrets, nunca valores:

```powershell
gh secret list
```

Confira a identidade autenticada:

```powershell
az account show --query "{subscription:id,tenant:tenantId,user:user.name}"
```

Confira roles sem exibir credenciais:

```powershell
az role assignment list `
  --assignee-object-id <principal-id> `
  --query "[].{role:roleDefinitionName,scope:scope}"
```
