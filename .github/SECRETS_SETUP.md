# GitHub Secrets Configuration Guide

## 🔐 Required GitHub Secrets

Para o deployment automatizado funcionar, você precisa configurar os seguintes secrets no GitHub:

---

## 1️⃣ Azure Credentials (Service Principal)

### Para Desenvolvimento

**Secret Name:** `AZURE_CREDENTIALS_DEV`

**Como obter:**

```bash
# 1. Login no Azure
az login

# 2. Criar Service Principal para DEV
az ad sp create-for-rbac \
  --name "casezero-github-dev" \
  --role "Contributor" \
  --scopes /subscriptions/{subscription-id} \
  --sdk-auth
```

**Formato do Secret:**
```json
{
  "clientId": "<client-id>",
  "clientSecret": "<client-secret>",
  "subscriptionId": "<subscription-id>",
  "tenantId": "<tenant-id>"
}
```

### Para Produção

**Secret Name:** `AZURE_CREDENTIALS_PROD`

```bash
# Criar Service Principal para PROD
az ad sp create-for-rbac \
  --name "casezero-github-prod" \
  --role "Contributor" \
  --scopes /subscriptions/{subscription-id} \
  --sdk-auth
```

**Formato:** Mesmo JSON acima

---

## 2️⃣ Teams Webhook (Opcional)

**Secret Name:** `TEAMS_WEBHOOK_URL`

**Como obter:**
1. No Microsoft Teams, vá ao canal desejado
2. Clique em **"..."** → **Connectors** → **Incoming Webhook**
3. Configure e copie a URL gerada

**Formato:**
```
https://outlook.office.com/webhook/...
```

**Uso:** Notificações de deployment no Teams (opcional)

---

## 📝 Como Adicionar Secrets no GitHub

### Via Interface Web

1. Vá para o repositório: https://github.com/lcarli/CaseZero-Alternative
2. Clique em **Settings** → **Secrets and variables** → **Actions**
3. Clique em **New repository secret**
4. Adicione cada secret:
   - Name: `AZURE_CREDENTIALS_DEV`
   - Secret: Cole o JSON completo
5. Repita para `AZURE_CREDENTIALS_PROD`

### Via GitHub CLI

```bash
# Instalar GitHub CLI (se necessário)
# https://cli.github.com/

# Login
gh auth login

# Adicionar secrets
gh secret set AZURE_CREDENTIALS_DEV < dev-credentials.json
gh secret set AZURE_CREDENTIALS_PROD < prod-credentials.json
gh secret set TEAMS_WEBHOOK_URL --body "https://outlook.office.com/webhook/..."
```

---

## 🔍 Verificar Secrets Configurados

```bash
# Listar secrets (não mostra valores)
gh secret list
```

Você deve ver:
```
AZURE_CREDENTIALS_DEV   Updated 2025-11-04
AZURE_CREDENTIALS_PROD  Updated 2025-11-04
TEAMS_WEBHOOK_URL       Updated 2025-11-04 (optional)
```

---

## ⚙️ Permissões Necessárias

O Service Principal precisa das seguintes permissões:

### Para Infraestrutura (main.bicep)
- ✅ **Contributor** no Subscription (ou Resource Group)
- ✅ **User Access Administrator** (para RBAC assignments)

### Comando para adicionar permissão extra:

```bash
# Adicionar role de User Access Administrator
az role assignment create \
  --assignee <service-principal-app-id> \
  --role "User Access Administrator" \
  --scope /subscriptions/{subscription-id}
```

---

## 🧪 Testar Configuração

Depois de adicionar os secrets, você pode testar:

1. Vá para **Actions** no GitHub
2. Selecione workflow **"🏗️ Deploy 3-Tier Infrastructure"**
3. Clique em **Run workflow**
4. Escolha:
   - Environment: `dev`
   - Action: `validate`
5. Clique em **Run workflow**

Se tudo estiver correto, o workflow vai:
- ✅ Fazer login no Azure
- ✅ Validar os templates Bicep
- ✅ Mostrar o que seria criado (What-If)

---

## 🔒 Segurança

### Boas Práticas

- ✅ **Use Service Principals separados** para dev e prod
- ✅ **Nunca commite** os credentials no código
- ✅ **Limite o escopo** do Service Principal ao mínimo necessário
- ✅ **Rotate secrets** periodicamente (ex: a cada 90 dias)
- ✅ **Use Azure Key Vault** para secrets de aplicação (já configurado nos templates)

### Renovar Service Principal

```bash
# Criar nova senha para o Service Principal
az ad sp credential reset \
  --name <app-id> \
  --append
```

---

## 📋 Checklist de Configuração

- [ ] Service Principal criado para DEV
- [ ] Service Principal criado para PROD
- [ ] `AZURE_CREDENTIALS_DEV` adicionado no GitHub
- [ ] `AZURE_CREDENTIALS_PROD` adicionado no GitHub
- [ ] `TEAMS_WEBHOOK_URL` adicionado (opcional)
- [ ] Permissões verificadas (Contributor + User Access Administrator)
- [ ] Teste executado com sucesso (validate action)

---

## 🆘 Troubleshooting

### Erro: "Authorization failed"

**Causa:** Service Principal sem permissões suficientes

**Solução:**
```bash
az role assignment create \
  --assignee <service-principal-app-id> \
  --role "Contributor" \
  --scope /subscriptions/{subscription-id}
```

### Erro: "Secret not found"

**Causa:** Secret não configurado ou nome errado

**Solução:** Verificar nome exato dos secrets (case-sensitive)

### Erro: "Invalid JSON format"

**Causa:** JSON do Service Principal com formato incorreto

**Solução:** Gerar novamente com `--sdk-auth` e copiar todo o output

---

## 📞 Comandos Úteis

```bash
# Ver subscription atual
az account show

# Listar Service Principals
az ad sp list --display-name "casezero-github"

# Ver roles de um Service Principal
az role assignment list \
  --assignee <service-principal-app-id> \
  --all

# Deletar Service Principal
az ad sp delete --id <app-id>
```

---

## 🎯 Próximos Passos

Depois de configurar os secrets:

1. ✅ Testar workflow com action `validate`
2. ✅ Deploy de dev com action `deploy`
3. ✅ Configurar secrets do Key Vault (JWT, Azure OpenAI)
4. ✅ Deploy das aplicações (.NET API, Functions, React)
5. ✅ Configurar custom domains (opcional)

---

**Status:** 🟡 **Aguardando Configuração**  
**Prioridade:** 🔥 **Alta** (necessário para deployment)  
**Tempo Estimado:** 10-15 minutos
