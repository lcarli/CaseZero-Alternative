# 🤖 CaseZero Case Generator AI

> **📌 INFRAESTRUTURA INDEPENDENTE**: Para implantar APENAS a infraestrutura do Case Generator (sem o resto da aplicação), consulte [../infrastructure/case-generator-infrastructure-README.md](../infrastructure/case-generator-infrastructure-README.md).
> 
> **📌 INDEPENDENT INFRASTRUCTURE**: To deploy ONLY the Case Generator infrastructure (without the rest of the application), see [../infrastructure/case-generator-infrastructure-README.md](../infrastructure/case-generator-infrastructure-README.md).
> 
> **📌 INFRAESTRUCTURA INDEPENDIENTE**: Para implementar SOLO la infraestructura del Generador de Casos (sin el resto de la aplicación), consulte [../infrastructure/case-generator-infrastructure-README.md](../infrastructure/case-generator-infrastructure-README.md).
> 
> **📌 INFRASTRUCTURE INDÉPENDANTE**: Pour déployer UNIQUEMENT l'infrastructure du Générateur de Cas (sans le reste de l'application), consultez [../infrastructure/case-generator-infrastructure-README.md](../infrastructure/case-generator-infrastructure-README.md).

## Visão Geral

O CaseZero Case Generator AI é um sistema abrangente baseado em Azure Durable Functions que gera automaticamente casos de investigação detetivesca realistas usando inteligência artificial. O sistema fornece rastreamento de progresso em tempo real e produz pacotes de casos completos com documentos, evidências e materiais de investigação.

## 🏗️ Arquitetura

### Componentes Principais

- **Azure Durable Functions**: Orquestra o pipeline de geração de casos
- **Azure Storage Account**: Armazena casos gerados e pacotes
- **Azure Key Vault**: Gerencia segredos e configuração
- **Interface Frontend**: Rastreamento de progresso em tempo real e gerenciamento de casos
- **LLM Service**: Geração de conteúdo alimentada por IA

### Pipeline de Geração

O sistema segue um pipeline de 10 etapas para geração de casos:

1. **Plan** - Estrutura inicial e framework do caso
2. **Expand** - Suspeitos detalhados, evidências e cronologia
3. **Design** - Fluxo de investigação e mecânicas do jogo
4. **GenDocs** - Gerar documentos de investigação
5. **GenMedia** - Criar assets de mídia e prompts de imagem
6. **Normalize** - Padronizar conteúdo e formato
7. **Index** - Criar metadados pesquisáveis
8. **RuleValidate** - Verificações de garantia de qualidade
9. **RedTeam** - Validação de segurança e conteúdo
10. **Package** - Montagem final e armazenamento

## 🚀 Início Rápido

### Pré-requisitos

- Assinatura Azure com permissões apropriadas
- .NET 8 SDK
- Azure CLI
- Azure Functions Core Tools
- Visual Studio Code (recomendado)

### 1. Implantar Infraestrutura

Primeiro, implante a infraestrutura Azure necessária:

```bash
# Login no Azure
az login

# Definir sua assinatura
az account set --subscription "your-subscription-id"

# Implantar infraestrutura usando GitHub Actions
# Vá para: https://github.com/lcarli/CaseZero-Alternative/actions/workflows/infrastructure.yml
# Clique em "Run workflow" e selecione ambiente "development"
```

### 2. Configurar Segredos

Após a implantação da infraestrutura, configure os segredos necessários no Azure Key Vault:

```bash
# Obter nome do Key Vault da saída da implantação
KV_NAME="your-keyvault-name"

# Adicionar segredos necessários
az keyvault secret set --vault-name $KV_NAME --name "OpenAI-ApiKey" --value "your-openai-key"
az keyvault secret set --vault-name $KV_NAME --name "OpenAI-Endpoint" --value "your-openai-endpoint"
```

### 3. Implantar Functions

Implante as Case Generator Functions:

```bash
# Opção 1: Usando GitHub Actions (Recomendado)
# Vá para: https://github.com/lcarli/CaseZero-Alternative/actions/workflows/functions-deploy.yml
# Clique em "Run workflow" e selecione ambiente "development"

# Opção 2: Implantação manual
cd functions/CaseGen.Functions
func azure functionapp publish casezero-func-dev
```

### 4. Configurar Segredos do GitHub

Configure os seguintes segredos no seu repositório GitHub:

- `AZURE_CREDENTIALS`: Credenciais do service principal (formato JSON)
- `AZURE_FUNCTIONAPP_PUBLISH_PROFILE_DEV`: Perfil de publicação do Function app para dev
- `AZURE_FUNCTIONAPP_PUBLISH_PROFILE_PROD`: Perfil de publicação do Function app para prod

## 📋 Guia de Configuração Passo a Passo

### Fase 1: Configuração de Recursos Azure

1. **Criar Grupos de Recursos**
   ```bash
   az group create --name casezero-dev-rg --location "East US 2"
   az group create --name casezero-prod-rg --location "East US 2"
   ```

2. **Implantar Infraestrutura**
   - Use o workflow do GitHub Actions: `🏗️ Deploy Infrastructure`
   - Selecione ambiente: `development` para testes
   - Verifique se os recursos foram criados corretamente

3. **Verificar Recursos Implantados**
   ```bash
   # Listar recursos implantados
   az resource list --resource-group casezero-dev-rg --output table
   ```

### Fase 2: Configurar Serviços Azure

1. **Configuração do Key Vault**
   ```bash
   # Definir o nome do seu Key Vault (da saída da implantação)
   KV_NAME=$(az resource list --resource-group casezero-dev-rg --resource-type "Microsoft.KeyVault/vaults" --query "[0].name" -o tsv)
   
   # Configurar segredos
   az keyvault secret set --vault-name $KV_NAME --name "OpenAI-ApiKey" --value "your-api-key"
   az keyvault secret set --vault-name $KV_NAME --name "OpenAI-Endpoint" --value "https://your-endpoint.openai.azure.com/"
   az keyvault secret set --vault-name $KV_NAME --name "OpenAI-DeploymentName" --value "gpt-4"
   ```

2. **Configuração da Conta de Armazenamento**
   ```bash
   # Obter nome da conta de armazenamento
   STORAGE_NAME=$(az resource list --resource-group casezero-dev-rg --resource-type "Microsoft.Storage/storageAccounts" --query "[?contains(name, 'genstr')].name" -o tsv)
   
   # Verificar se os contêineres foram criados
   az storage container list --account-name $STORAGE_NAME --auth-mode login
   ```

3. **Configuração do Function App**
   ```bash
   # Obter nome do Function App
   FUNC_NAME=$(az resource list --resource-group casezero-dev-rg --resource-type "Microsoft.Web/sites" --query "[?contains(name, 'func')].name" -o tsv)
   
   # Verificar se o Function App está executando
   az functionapp show --name $FUNC_NAME --resource-group casezero-dev-rg --query "state" -o tsv
   ```

### Fase 3: Implantar Functions

1. **Construir Functions Localmente**
   ```bash
   cd functions/CaseGen.Functions
   dotnet restore
   dotnet build --configuration Release
   dotnet publish --configuration Release --output ./publish
   ```

2. **Implantar no Azure**
   ```bash
   # Usando Azure Functions Core Tools
   func azure functionapp publish $FUNC_NAME --dotnet-isolated
   
   # Ou use o workflow do GitHub Actions
   # Navegue para Actions > 🚀 Deploy Case Generator Functions
   ```

3. **Verificar Implantação**
   ```bash
   # Listar functions implantadas
   az functionapp function list --name $FUNC_NAME --resource-group casezero-dev-rg --output table
   
   # Testar o endpoint de health
   curl "https://$FUNC_NAME.azurewebsites.net/api/status"
   ```

### Fase 4: Configurar Frontend

1. **Atualizar Variáveis de Ambiente**
   ```bash
   # No seu arquivo frontend/.env
   echo "VITE_FUNCTIONS_BASE_URL=https://$FUNC_NAME.azurewebsites.net" >> frontend/.env
   ```

2. **Construir e Implantar Frontend**
   ```bash
   cd frontend
   npm install
   npm run build
   
   # Implantar usando o pipeline CI/CD existente
   ```

## 🔧 Configuração

### Variáveis de Ambiente

#### Configurações do Function App
```
FUNCTIONS_EXTENSION_VERSION=~4
FUNCTIONS_WORKER_RUNTIME=dotnet-isolated
AzureWebJobsStorage=<storage-connection-string>
CaseGeneratorStorage__ConnectionString=<storage-connection-string>
CaseGeneratorStorage__CasesContainer=cases
CaseGeneratorStorage__BundlesContainer=bundles
KeyVault__VaultUri=<keyvault-uri>
TaskHub=CaseGeneratorHub
APPLICATIONINSIGHTS_CONNECTION_STRING=<app-insights-connection>
```

#### Segredos do Key Vault
- `OpenAI-ApiKey`: Sua chave da API OpenAI
- `OpenAI-Endpoint`: Endpoint do serviço OpenAI
- `OpenAI-DeploymentName`: Nome da implantação do modelo

### Configuração CORS

Se acessar de uma aplicação web, configure CORS no Function App:

```bash
az functionapp cors add --name $FUNC_NAME --resource-group casezero-dev-rg --allowed-origins "https://your-domain.com"
```

## 🔌 Referência da API

### Iniciar Geração de Caso

**Endpoint v2 (via backend proxy)**: `POST /api/casegeneration/generate`

**Corpo da Requisição**:
```json
{
  "title": "Roubo em Empresa de Tecnologia",
  "location": "São Paulo, SP",
  "difficulty": "Rookie",
  "requiredRank": "Rookie",
  "language": "pt-BR",
  "writeToDisk": true
}
```

> **Imagens no pipeline v2:** não existe o campo `generateImages`. O gerador
> produz imagens somente quando o portfólio do caso inclui evidências do tipo
> `photo` ou `image`. Um caso com apenas PDFs/documentos pode terminar
> corretamente com `AssetsRenderedImages = 0` e sem erros de renderização.

**Resposta**:
```json
{
  "jobId": "casev2-...",
  "status": "queued",
  "statusUri": "/api/casegeneration/jobs/casev2-..."
}
```

### Obter Status da Geração

**Endpoint**: `GET /api/status/{instanceId}`

**Resposta**:
```json
{
  "instanceId": "abc123...",
  "runtimeStatus": "Running",
  "createdAt": "2024-01-01T10:00:00Z",
  "lastUpdatedAt": "2024-01-01T10:05:00Z",
  "customStatus": {
    "caseId": "CASE-20240101-1000",
    "status": "Running",
    "currentStep": "GenDocs",
    "completedSteps": ["Plan", "Expand", "Design"],
    "progress": 40.0
  }
}
```

## 🧪 Testes

### Testes Unitários

```bash
cd functions/CaseGen.Functions
dotnet test
```

### Testes de Integração

```bash
# Testar functions individuais
curl -X POST "https://$FUNC_NAME.azurewebsites.net/api/StartCaseGeneration" \
  -H "Content-Type: application/json" \
  -d '{"title":"Caso de Teste","location":"Local de Teste","difficulty":"Iniciante"}'
```

### Testes de Carga

Para ambientes de produção, execute testes de carga:

```bash
# Instalar Artillery
npm install -g artillery

# Executar teste de carga
artillery run load-test-config.yml
```

## 📊 Monitoramento

### Application Insights

Monitore suas Functions usando Application Insights:

1. **Visualizar no Portal Azure**
   - Navegue para seu Function App
   - Clique em "Application Insights"
   - Monitore performance, falhas e dependências

2. **Métricas Chave para Monitorar**
   - Contagem de execução de functions
   - Duração de execução de functions
   - Falhas de functions
   - Operações de storage
   - Acesso ao Key Vault

### Alertas

Configure alertas para cenários críticos:

```bash
# Criar alerta para falhas de function
az monitor metrics alert create \
  --name "CaseGenerator-Failures" \
  --resource-group casezero-dev-rg \
  --scopes "/subscriptions/$SUBSCRIPTION_ID/resourceGroups/casezero-dev-rg/providers/Microsoft.Web/sites/$FUNC_NAME" \
  --condition "count Microsoft.Web/sites/functions/requests" \
  --threshold 10
```

## 🚨 Solução de Problemas

### Problemas Comuns

1. **Function App Não Inicia**
   ```bash
   # Verificar logs
   az functionapp log tail --name $FUNC_NAME --resource-group casezero-dev-rg
   
   # Reiniciar o app
   az functionapp restart --name $FUNC_NAME --resource-group casezero-dev-rg
   ```

2. **Problemas de Conexão com Storage**
   ```bash
   # Verificar acesso à conta de storage
   az storage account show --name $STORAGE_NAME --resource-group casezero-dev-rg
   
   # Verificar connection string
   az functionapp config appsettings list --name $FUNC_NAME --resource-group casezero-dev-rg
   ```

3. **Problemas de Acesso ao Key Vault**
   ```bash
   # Verificar identidade do Function App
   az functionapp identity show --name $FUNC_NAME --resource-group casezero-dev-rg
   
   # Verificar políticas de acesso do Key Vault
   az keyvault show --name $KV_NAME --resource-group casezero-dev-rg
   ```

### Modo de Debug

Habilitar logging de debug:

```bash
az functionapp config appsettings set \
  --name $FUNC_NAME \
  --resource-group casezero-dev-rg \
  --settings "AZURE_FUNCTIONS_ENVIRONMENT=Development"
```

## 🔄 Pipeline CI/CD

### Implantação Automatizada

O sistema inclui pipelines CI/CD abrangentes:

1. **Pipeline de Infraestrutura**: `infrastructure.yml`
   - Implanta todos os recursos Azure
   - Valida templates BICEP
   - Suporta múltiplos ambientes

2. **Pipeline de Functions**: `functions-deploy.yml`
   - Constrói e testa Functions
   - Implanta em ambientes dev/prod
   - Inclui verificações de saúde

3. **Pipeline Frontend**: `cd-dev.yml` / `cd-prod.yml`
   - Constrói e implanta frontend
   - Atualiza endpoints da API

### Implantação Manual

Para implantações de emergência:

```bash
# Implantação rápida de function
cd functions/CaseGen.Functions
func azure functionapp publish $FUNC_NAME --force
```

## 📈 Escalabilidade

### Otimização de Performance

1. **Escalabilidade do Function App**
   ```bash
   # Configurar plano premium para produção
   az functionapp plan update \
     --name casezero-func-asp-prod \
     --resource-group casezero-prod-rg \
     --max-burst 20
   ```

2. **Otimização de Storage**
   - Usar storage premium para alto IOPS
   - Habilitar CDN para conteúdo gerado
   - Implementar políticas de ciclo de vida do blob

### Otimização de Custos

1. **Ambiente de Desenvolvimento**
   - Usar plano de consumo
   - Implementar políticas de pausa automática
   - Limpeza regular de dados de teste

2. **Ambiente de Produção**
   - Monitorar padrões de uso
   - Implementar alertas de custo
   - Usar instâncias reservadas para cargas previsíveis

## 🔒 Segurança

### Melhores Práticas

1. **Controle de Acesso**
   - Usar identidades gerenciadas
   - Implementar RBAC adequadamente
   - Revisões regulares de acesso

2. **Proteção de Dados**
   - Criptografar dados em repouso
   - Usar HTTPS para todas as comunicações
   - Implementar políticas de retenção de dados

3. **Monitoramento**
   - Habilitar Azure Security Center
   - Monitorar atividades suspeitas
   - Avaliações regulares de segurança

## 📚 Recursos Adicionais

- [Documentação Azure Durable Functions](https://docs.microsoft.com/en-us/azure/azure-functions/durable/)
- [Guia Azure Functions .NET Isolated](https://docs.microsoft.com/en-us/azure/azure-functions/dotnet-isolated-process-guide)
- [Integração Azure Key Vault](https://docs.microsoft.com/en-us/azure/azure-functions/functions-bindings-key-vault)
- [Application Insights para Functions](https://docs.microsoft.com/en-us/azure/azure-functions/functions-monitoring)

## 🤝 Contribuindo

1. Faça fork do repositório
2. Crie uma branch de feature
3. Faça suas alterações
4. Adicione testes se aplicável
5. Submeta um pull request

## 📄 Licença

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.