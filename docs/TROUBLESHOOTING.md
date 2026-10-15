# Guia de Solução de Problemas - Sistema CaseZero

## Overview

Este guia foi revisado contra o código atual do repositório. Ele cobre apenas os fluxos, portas, chaves de configuração e comandos que realmente existem hoje em `backend\CaseZeroApi`, `functions\CaseGen.Functions`, `frontend`, `scripts` e `.github\workflows`.

---

## 🚨 Problemas de Instalação e Setup

### Backend (.NET) não inicia

**Sintoma:** erro ao executar `dotnet run` em `backend\CaseZeroApi`.

**Possíveis causas e soluções:**

1. **SDK .NET incorreto**
   ```powershell
   dotnet --list-sdks
   ```
   O backend atual roda em **.NET 8**. Use um SDK 8.x instalado localmente.

2. **Dependências não restauradas**
   ```powershell
   dotnet restore .\CaseZero-Alternative.sln
   dotnet build .\CaseZero-Alternative.sln
   ```

3. **Connection string ausente ou com placeholder**
   O `Program.cs` falha explicitamente quando `ConnectionStrings:DefaultConnection` não está configurada ou ainda contém placeholders.

   Mensagens atuais esperadas:
   - `Database connection string 'DefaultConnection' is not configured.`
   - `Database connection string contains placeholder values.`

   Ajuste a connection string em um arquivo local como `backend\CaseZeroApi\appsettings.Local.json` ou por variável de ambiente. **Nunca copie segredos reais para a documentação**; use placeholders como:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=tcp:<server>.database.windows.net,1433;Database=<db>;User ID=<user>;Password=<password>;Encrypt=True;"
     }
   }
   ```

4. **Porta local divergente**
   Este projeto **não possui** `backend\CaseZeroApi\Properties\launchSettings.json`, então a porta da API não fica fixa no repositório.

   O frontend de desenvolvimento aponta para `http://localhost:5001/api`, então o caminho mais simples é subir a API nessa porta:
   ```powershell
   cd .\backend\CaseZeroApi
   dotnet run --urls http://localhost:5001
   ```

5. **SQLite local x Azure SQL**
   O código atual aceita desenvolvimento local com SQLite quando `UseSqlite=true` **ou** quando a connection string começa com `Data Source=`. Nesse modo, a API usa `EnsureCreated()`; fora dele, aplica `Migrate()` para SQL Server.

### Testes do Backend/Functions falham

**Sintoma:** falhas em `dotnet test`.

**Comandos reais do repositório:**
```powershell
dotnet test .\backend\CaseZeroApi.Tests\CaseZeroApi.Tests.csproj --no-build --configuration Release
dotnet test .\backend\CaseZeroApi.IntegrationTests\CaseZeroApi.IntegrationTests.csproj --no-build --configuration Release
dotnet test .\functions\CaseGen.Functions.Tests\CaseGen.Functions.Tests.csproj --no-build --configuration Release
```

**Possíveis causas e soluções:**

1. **Build não foi executado antes do `--no-build`**
   ```powershell
   dotnet restore .\CaseZero-Alternative.sln
   dotnet build .\CaseZero-Alternative.sln --configuration Release
   ```

2. **SDK das Functions incorreto**
   `functions\CaseGen.Functions` e `functions\CaseGen.Functions.Tests` devem continuar em **.NET 9**. Se os testes das Functions falharem por toolset, confira:
   ```powershell
   dotnet --list-sdks
   ```

3. **Suposição errada sobre dependências externas**
   Os testes de integração atuais usam ambiente `Testing`, banco em memória e desabilitam o uso de blob storage real. Se falharem, investigue a asserção específica do teste; não parta do pressuposto de que é falta de SQL Server ou Azurite.

### Frontend (React/Vite) não compila

**Sintoma:** erro em `npm run dev`, `npm run build` ou `npm run test:run`.

**Possíveis causas e soluções:**

1. **Node.js fora do baseline da equipe/CI**
   O workflow atual usa **Node 20**. Verifique:
   ```powershell
   node --version
   ```

2. **Dependências não instaladas corretamente**
   ```powershell
   cd .\frontend
   npm ci
   ```

3. **Scripts incorretos**
   Os scripts válidos hoje são:
   - `npm run dev`
   - `npm run build`
   - `npm run lint`
   - `npm run preview`
   - `npm run test`
   - `npm run test:ui`
   - `npm run test:run`

4. **Variável de ambiente errada**
   O frontend atual usa **`VITE_API_URL`**. Não use `VITE_API_BASE_URL`.

5. **Configuração de teste desatualizada**
   O repositório atual **não depende de `vitest.config.ts` separado**. A configuração de teste fica dentro de `frontend\vite.config.ts`.

---

## 🔌 Problemas de Conectividade

### Frontend não consegue conectar com Backend

**Sintoma:** 401, 404, CORS ou `Failed to fetch` no navegador.

**Diagnóstico:**
```powershell
Get-Content .\frontend\.env.development
```

Verifique se `VITE_API_URL` aponta para a mesma porta em que a API está rodando. O valor versionado hoje é:
```env
VITE_API_URL=http://localhost:5001/api
```

**Soluções:**

1. **A API não está na porta esperada pelo frontend**
   Como não há `launchSettings.json`, faça a API subir em `5001` ou ajuste `frontend\.env.development` para a porta real.

2. **CORS mal configurado por ambiente**
   O backend lê `Cors:AllowedOrigins` da configuração. Se nada for informado, o fallback atual é:
   - `http://localhost:5173`
   - `https://localhost:5173`

   Em Azure/App Settings, use chaves como:
   - `Cors__AllowedOrigins__0=https://<frontend-host>`
   - `Cors__AllowedOrigins__1=http://localhost:5173`

3. **Diagnóstico usando endpoint inexistente**
   O repositório atual **não expõe um health check real em `/api/health`**. Para validar que a API respondeu em desenvolvimento, prefira:
   - `http://localhost:5001/swagger/index.html`
   - ou um endpoint real da API, como `POST /api/auth/login`

4. **Suposição errada sobre proxy do Vite**
   `frontend\vite.config.ts` atual não define proxy. As chamadas são diretas para `VITE_API_URL`.

### Backend não consegue acionar o Case Generator

**Sintoma:** erro 503/502 ao usar endpoints de geração de caso.

**Comportamento atual:** `backend\CaseZeroApi\Controllers\CaseGenerationController.cs` faz proxy para a Function App.

**Erros atuais relevantes:**
- `Case generator not configured (CaseGenerator:FunctionBaseUrl missing).`
- `Case generator unreachable`

**Soluções:**

1. **Configurar a URL base das Functions**
   Defina no backend:
   ```json
   {
     "CaseGenerator": {
       "FunctionBaseUrl": "http://localhost:7071",
       "FunctionKey": "<optional-local-key>"
     }
   }
   ```

2. **Subir o host das Functions na porta esperada**
   ```powershell
   powershell -ExecutionPolicy Bypass -File .\scripts\run-functions.ps1
   ```
   O script e os logs atuais assumem `http://localhost:7071`.

3. **Conferir a rota correta**
   O backend chama:
   - `POST {FunctionBaseUrl}/api/cases/v2/generate`
   - `GET  {FunctionBaseUrl}/api/cases/v2/jobs/{jobId}`

### Functions/Azurite não iniciam

**Sintoma:** `func start` falha, a geração trava em storage, ou aparece erro de configuração de blob.

**Caminho suportado hoje:**
```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\run-functions.ps1
```

**O script atual faz o seguinte:**
- exige **.NET 9** para build das Functions;
- exige **Node 20+** para `func`/Azurite;
- instala `azure-functions-core-tools@4` e `azurite` se faltarem;
- inicia o Azurite usando a pasta raiz **`AzuriteConfig`**;
- sobe o host em **`http://localhost:7071`**.

**Chaves atuais de `functions\CaseGen.Functions\local.settings.json` (nomes apenas):**
- `AzureWebJobsStorage`
- `FUNCTIONS_WORKER_RUNTIME`
- `CaseGeneratorStorage__ConnectionString`
- `CaseGeneratorStorage__BundlesContainer`
- `LLM__UseAzureFoundry`
- `AzureFoundry__Endpoint`
- `AzureFoundry__ModelName`
- `AzureFoundry__ImageDeploymentName`
- `AzureFoundry__ApiKey`
- `ASPNETCORE_ENVIRONMENT`

**Soluções:**

1. **Storage local não configurado**
   O factory atual aceita `CaseGeneratorStorage:AccountName` (managed identity) ou `CaseGeneratorStorage:ConnectionString` / `CaseGeneratorStorage__ConnectionString` / `AzureWebJobsStorage`.

   Para desenvolvimento local com Azurite, use placeholders como:
   ```json
   {
     "Values": {
       "AzureWebJobsStorage": "UseDevelopmentStorage=true",
       "CaseGeneratorStorage__ConnectionString": "UseDevelopmentStorage=true",
       "CaseGeneratorStorage__BundlesContainer": "bundles"
     }
   }
   ```

2. **Azurite preso por PID antigo**
   O helper script grava `AzuriteConfig\azurite.pid`. Se houver PID órfão, remova o arquivo e rode o script novamente.

3. **Pasta errada de diagnóstico**
   O fluxo atual usa **`AzuriteConfig`** como `--location` do Azurite. Não assuma `AzuriteRuntime` como origem principal dos dados/logs locais.

4. **Logs do Azurite**
   Consulte `AzuriteConfig\azurite.log` quando houver falha de storage local.

---

## 🗄️ Problemas de Dados e Casos

### Falha de schema em `case.json`

**Sintoma:** falha em CI ou nos testes de validação de casos.

**Comportamento atual de CI:** o workflow `cd-dev.yml` valida `cases/*/case.json` contra `schemas/case.schema.json` com `ajv`.

**Validação local suportada hoje:**
```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\validate-casev2.ps1 -Mode Artifact -CaseDirectory .\cases\<case-id>
```

Outros modos reais do script incluem `Goldens`, `Generate`, `DirectGenerate`, `Batch` e `Full`.

**Correções comuns:**
1. confirmar que o diretório do caso existe;
2. validar o `case.json` contra `schemas\case.schema.json`;
3. se o erro ocorreu após geração, usar `-Mode Generate` para gerar e validar no mesmo fluxo.

### Caso gerado sem nenhuma imagem

**Sintoma:** o resultado ou diretório do caso não contém imagens.

**Comportamento esperado:** o portfólio Case v2 atual exige fotografias de cena e um retrato para cada suspeito. Falhas nesses visuais são bloqueantes: o caso incompleto não deve ser publicado nem reportado como sucesso.

**Diagnóstico atual:**
1. Verifique `AssetsRenderedImages` e `AssetRenderingErrors` no resultado do job.
2. Em logs das Functions, procure por mensagens como:
   - `Rendered {Pdfs} PDFs and {Imgs} images...`
   - `Image render failed`
   - `Mandatory visual render failed`

**Interpretação rápida:**
- `AssetsRenderedImages = 0` em um job concluído: investigue métricas antigas, bundle desatualizado ou resposta inconsistente;
- `Mandatory visual render failed`: a tentativa deve falhar e pode iniciar um retry completo;
- outros erros em `AssetRenderingErrors`: verifique se pertencem a imagens opcionais de objeto ou vigilância.

---

## 🚀 Problemas de CI e Deploy

### CI falha no GitHub Actions

**Workflow principal atual:** `.github\workflows\cd-dev.yml`

Ele faz hoje:
1. setup de **.NET 8.x + .NET 9.x**;
2. setup de **Node 20**;
3. `npm ci` e `npm run build` no frontend;
4. `dotnet restore` e `dotnet build` da solução;
5. testes de backend unitários;
6. testes de backend de integração;
7. testes das Functions;
8. `npm run test:run` no frontend;
9. validação de `cases/*/case.json` com `ajv`.

**Observações importantes:**
- mudanças apenas em `docs/**` e arquivos Markdown são ignoradas por esse workflow em `push`;
- o build do frontend recebe `VITE_API_URL` via secret do GitHub Actions.

**Se quiser reproduzir localmente, use os mesmos comandos do workflow.**

### Deploy de infraestrutura falha

**Workflow atual:** `.github\workflows\infrastructure-3tier.yml`

**Regras atuais do workflow:**
- é **manual** (`workflow_dispatch`);
- aceita `validate`, `deploy` e `destroy`;
- `destroy` exige `confirm_destroy=CONFIRM`;
- o deploy pode habilitar SQL Database via `deploy_sql_database`.

Se a falha for de infraestrutura, investigue os logs do próprio workflow e os parâmetros enviados; não use passos antigos de Docker Compose ou Nginx, porque eles não representam o fluxo atual versionado neste repositório.

### Docker/Nginx/backup scripts antigos

As orientações antigas para `Dockerfile`, `docker-compose`, `nginx`, `scripts\backup.sh`, `scripts\restore.sh` e `scripts\deploy.sh` **não correspondem ao estado atual do repositório**.

Hoje:
- não há `Dockerfile` versionado na raiz/escopo principal deste repositório;
- não há `docker-compose.yml` versionado para o fluxo principal;
- não existem `scripts\backup.sh`, `scripts\restore.sh` ou `scripts\deploy.sh` neste repositório.

Se precisar de recuperação/rollback, siga o processo operacional real do ambiente Azure correspondente, não esses comandos antigos.

---

## 🔧 Ferramentas de Diagnóstico

### Verificações rápidas

**Backend:**
```powershell
cd .\backend\CaseZeroApi
dotnet run --urls http://localhost:5001
```
Depois abra:
```text
http://localhost:5001/swagger/index.html
```

**Frontend:**
```powershell
cd .\frontend
npm ci
npm run dev
```
Confirme no browser console:
```javascript
console.log(import.meta.env.VITE_API_URL)
```

**Functions + Azurite:**
```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\run-functions.ps1
```

### Logs úteis

- `AzuriteConfig\azurite.log` — falhas de storage local/Azurite.
- `functions\CaseGen.Functions\func-host.log` — histórico de endpoints expostos/local host.
- saída do `dotnet run`/`func start` — principal fonte para exceptions atuais de startup.

---

## 📞 Escalação de Problemas

Escalone quando houver:
1. falha persistente com credenciais/configuração corretas;
2. divergência entre ambiente local e CI já reproduzida com os mesmos comandos do workflow;
3. erro de geração de caso com `AssetRenderingErrors` ou falha do orchestrator sem causa óbvia;
4. problema de infraestrutura Azure identificado no workflow manual.

Ao escalar, inclua:
- comando executado;
- arquivo/configuração relevante (sem segredos);
- mensagem exata de erro;
- se o problema ocorreu em backend, frontend, functions, Azurite ou GitHub Actions.

---

## 📚 Recursos Adicionais

- [.NET Diagnostics](https://learn.microsoft.com/dotnet/core/diagnostics/)
- [Vite Documentation](https://vite.dev/guide/)
- [Vitest Documentation](https://vitest.dev/guide/)
- [Azure Functions Core Tools](https://learn.microsoft.com/azure/azure-functions/functions-run-local)
- [Azurite Documentation](https://learn.microsoft.com/azure/storage/common/storage-use-azurite)
