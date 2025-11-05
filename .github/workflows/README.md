# GitHub Workflows - Ordem de Execução

## 📋 Ordem Correta de Deploy

### 1️⃣ **Primeiro: Deploy da Infraestrutura**

```
infrastructure-3tier.yml
```

**O que faz:**
- Cria todos os recursos Azure (Storage, App Service, Functions, Static Web App)
- Deve ser executado **ANTES** de qualquer deploy de aplicação

**Como executar:**
- GitHub Actions → `infrastructure-3tier.yml` → Run workflow
- Escolher environment: `dev` ou `prod`
- Escolher layer: `all`, `shared`, `api`, `functions`, ou `frontend`

---

### 2️⃣ **Depois: Deploy das Aplicações**

#### DEV Environment:
```
cd-dev.yml
```
- Trigger: Push para branch `develop` (automático)
- Ou: Executar manualmente via GitHub Actions
- Deploy para: `app-casezero-api-dev` e `swa-casezero-dev`

#### PROD Environment:
```
cd-prod.yml
```
- Trigger: Push para branch `main` (automático)
- Ou: Executar manualmente (requer confirmação "CONFIRM")
- Deploy para: `app-casezero-api-prod` e `swa-casezero-prod`

---

### 3️⃣ **CI (Sempre Ativo)**

```
ci.yml
```
- Roda automaticamente em todos os PRs
- Valida: Build, testes, linting
- **Não faz deploy**

---

## ⚠️ **IMPORTANTE**

Se você ver o erro:
```
Error: Resource app-casezero-api-dev doesn't exist
```

**Solução:** Execute `infrastructure-3tier.yml` primeiro para criar os recursos!

---

## 🏗️ **Recursos Criados por Environment**

### DEV:
- `rg-casezero-shared-dev` - Shared resources
- `rg-casezero-api-dev` - Backend API
- `rg-casezero-functions-dev` - Azure Functions
- `rg-casezero-web-dev` - Static Web App

### PROD:
- `rg-casezero-shared-prod` - Shared resources
- `rg-casezero-api-prod` - Backend API
- `rg-casezero-functions-prod` - Azure Functions
- `rg-casezero-web-prod` - Static Web App
