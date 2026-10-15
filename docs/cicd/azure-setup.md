# Configuração Azure para CI/CD

Este guia prepara a assinatura para os workflows atuais. A infraestrutura é
declarada em `infrastructure/main.bicep`; evite criar manualmente recursos que o
template já administra.

## Pré-requisitos

- Azure CLI autenticado;
- permissão para deployments em nível de assinatura;
- permissão para criar resource groups e atribuições RBAC;
- GitHub Actions habilitado;
- secrets descritos em [`variables-and-secrets.md`](variables-and-secrets.md).

## 1. Selecionar a assinatura

```powershell
az login
az account set --subscription "<subscription-id>"
az account show --query "{name:name,id:id,tenantId:tenantId}"
```

## 2. Registrar providers

```powershell
$providers = @(
  "Microsoft.Authorization",
  "Microsoft.CognitiveServices",
  "Microsoft.Insights",
  "Microsoft.KeyVault",
  "Microsoft.Network",
  "Microsoft.OperationalInsights",
  "Microsoft.Sql",
  "Microsoft.Storage",
  "Microsoft.Web"
)

foreach ($provider in $providers) {
  az provider register --namespace $provider
}
```

## 3. Preparar a identidade do GitHub

O workflow de infraestrutura usa deployment em nível de assinatura. A
identidade precisa criar resource groups, recursos e role assignments
declarados no Bicep.

```powershell
az ad sp create-for-rbac `
  --name "casezero-dev-github" `
  --role Contributor `
  --scopes "/subscriptions/<subscription-id>" `
  --sdk-auth
```

Armazene a saída em `AZURE_CREDENTIALS_DEV`. Repita para PROD somente quando
esse ambiente for utilizado.

Permissões para criar role assignments podem exigir `User Access Administrator`
ou `Role Based Access Control Administrator`. Conceda apenas se o deployment
falhar nessa etapa e limite o escopo à assinatura usada pelo projeto.

## 4. Configurar secrets GitHub

Configure:

- `AZURE_CREDENTIALS_DEV`;
- `AZURE_CREDENTIALS_PROD`, se aplicável;
- `AZURE_STATIC_WEB_APPS_API_TOKEN_DEV`;
- `VITE_API_URL`;
- `SQL_ADMIN_LOGIN`;
- `SQL_ADMIN_PASSWORD`.

Não armazene segredos do Azure Foundry no GitHub. A Function os resolve pelo Key
Vault criado pela infraestrutura.

## 5. Revisar parâmetros

Use:

```text
infrastructure/parameters.dev.json
infrastructure/parameters.prod.json
```

Confirme especialmente:

- `environment`;
- região;
- nomes/prefixos;
- origens CORS;
- opção de Azure SQL;
- SKUs e redundância;
- rede e subnets.

Nunca grave senha SQL diretamente nesses arquivos.

## 6. Validar infraestrutura

No GitHub:

1. abra **Deploy 3-Tier Infrastructure**;
2. escolha o ambiente;
3. selecione `validate`;
4. revise a compilação Bicep e o deployment validation.

Localmente:

```powershell
az bicep build --file infrastructure\main.bicep

az deployment sub validate `
  --location canadacentral `
  --template-file infrastructure\main.bicep `
  --parameters "@infrastructure\parameters.dev.json" `
  --parameters sqlAdminLogin="<login>" `
  --parameters sqlAdminPassword="<password>"
```

## 7. Implantar

Execute o workflow novamente com `action=deploy`. Ele gera `what-if` antes da
implantação e usa um nome de deployment versionado por timestamp.

Os outputs incluem URLs do frontend, API e Function App. O arquivo completo de
output é publicado como artifact; ele não deve conter valores de secrets.

## 8. Configurar o token da Static Web App

Depois que `casezero-web-dev` existir:

```powershell
az staticwebapp secrets list `
  --name casezero-web-dev `
  --resource-group casezero-web-dev-rg `
  --query properties.apiKey `
  --output tsv
```

Armazene o resultado em `AZURE_STATIC_WEB_APPS_API_TOKEN_DEV`.

## 9. Validar a topologia

Confira:

- API e Function com identidades gerenciadas;
- integração de VNet;
- private endpoints aprovados;
- zonas DNS privadas vinculadas à VNet;
- Storage com acesso público desabilitado;
- API com leitura de blobs e envio de mensagens para Queue;
- Function com acesso aos blobs e aos secrets do Key Vault;
- referências do Key Vault em estado `Resolved`;
- frontend configurado com a URL da API.

Exemplos:

```powershell
az webapp identity show `
  --name casezero-api-dev `
  --resource-group casezero-api-dev-rg

az functionapp identity show `
  --name casegen-func-dev `
  --resource-group casezero-func-dev-rg

az network private-endpoint list --output table
```

## 10. Implantar a aplicação

Após a infraestrutura:

1. abra **Deploy to DEV Environment**;
2. execute manualmente para `development`, ou faça push elegível;
3. confirme build e testes;
4. confirme deploy da API, Function e frontend.

## Atualizações

Para alterações de infraestrutura:

1. modifique os arquivos Bicep;
2. gere novamente os JSONs compilados usados pelo repositório;
3. execute `validate`;
4. revise `what-if`;
5. execute `deploy`.

Não aplique mudanças manuais permanentes no Portal sem refletir a configuração
no IaC.

## Destruição

`action=destroy` exige `confirm_destroy=CONFIRM` e remove resource groups
encontrados pelas tags de ambiente.

Antes de usar:

- revise a consulta de resource groups;
- faça backup dos dados necessários;
- confirme o ambiente;
- verifique locks e recursos compartilhados;
- trate a ação como irreversível.

## Troubleshooting

### `No subscriptions found`

O JSON do service principal pode estar expirado, apontando para outro tenant ou
sem acesso à assinatura. Teste a autenticação e rotacione a credencial.

### Falha ao criar role assignment

Conceda temporariamente a permissão mínima necessária para atribuir roles e
remova privilégios excessivos após o deployment.

### Storage inacessível

Não habilite acesso público como solução permanente. Verifique VNet, DNS
privado, private endpoint e RBAC da identidade chamadora.

### Referência do Key Vault não resolvida

Confirme:

- nome exato do segredo;
- URI gerada pelo Bicep;
- role `Key Vault Secrets User`;
- conectividade privada e DNS;
- estado da referência nas app settings.

### Azure Policy alterou uma propriedade

Compare o estado real com o `what-if` e consulte as policy assignments. Não
presuma que um comando bem-sucedido permaneceu aplicado.
