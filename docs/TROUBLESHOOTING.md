# Troubleshooting Guide - CaseZero System

## Overview

Este guia fornece soluções para problemas comuns que podem ocorrer durante desenvolvimento, deployment e operação do sistema CaseZero.

---

## 🚨 Problemas de Instalação e Setup

### Backend (.NET) não inicia

**Sintoma:** Erro ao executar `dotnet run`

**Possíveis Causas e Soluções:**

1. **SDK .NET não instalado ou versão incorreta**
   ```bash
   # Verificar versão
   dotnet --version
   
   # Deve retornar 8.x ou superior
   # Se não, instalar .NET 8 SDK
   ```

2. **Dependências não restauradas**
   ```bash
   cd backend/CaseZeroApi
   dotnet restore
   dotnet build
   ```

3. **Banco de dados não configurado**
   ```bash
   # Criar/atualizar banco
   dotnet ef database update
   
   # Se der erro, verificar connection string em appsettings.json
   ```

4. **Porta 5000 ocupada**
   ```bash
   # Verificar que está usando a porta
   netstat -ano | findstr :5000  # Windows
   lsof -i :5000                 # Linux/Mac
   
   # Matar processo ou alterar porta em launchSettings.json
   ```

### Testes do Backend falham

**Sintoma:** Erros durante `dotnet test` no projeto CaseZeroApi.Tests

**Possíveis Causas e Soluções:**

1. **Erro Entity Framework async em testes**
   ```bash
   # Sintoma: "The provider for the source 'IQueryable' doesn't implement 'IAsyncQueryProvider'"
   # Solução: Verificar se testes unitários estão usando InMemory database corretamente
   
   # No test setup, garantir configuração correta:
   services.AddDbContext<ApiContext>(options =>
       options.UseInMemoryDatabase(databaseName: "TestDatabase"));
   ```

2. **Mock incorreto do UserManager**
   ```bash
   # Sintoma: Testes de autenticação falhando
   # Solução: Verificar mocks do Identity no AuthControllerTests
   ```

3. **Estado de teste não limpo**
   ```bash
   # Limpar antes de executar testes
   cd backend/CaseZeroApi.Tests
   dotnet clean
   dotnet test
   ```

**Nota:** Se alguns testes estão falhando no desenvolvimento, isso é normal durante refatoração. Os testes devem ser corrigidos para refletir as mudanças no código.

### Frontend (React) não compila

**Sintoma:** Erro durante `npm run dev` ou `npm run build`

**Possíveis Causas e Soluções:**

1. **Node.js versão incompatível**
   ```bash
   # Verificar versão (deve ser 18+)
   node --version
   
   # Usar nvm para instalar versão correta
   nvm install 18
   nvm use 18
   ```

2. **Dependências não instaladas**
   ```bash
   cd frontend
   rm -rf node_modules package-lock.json
   npm install
   ```

3. **Erro de TypeScript em testes**
   ```bash
   # Se houver erro "Cannot find name 'global'" nos testes
   # Verificar se vitest.config.ts existe separado do vite.config.ts
   ls frontend/vitest.config.ts
   
   # Usar globalThis ao invés de global nos testes
   Object.defineProperty(globalThis, 'fetch', { value: mockFetch })
   ```

4. **Erro de configuração Vite/Vitest**
   ```bash
   # Verificar se existe vitest.config.ts separado para testes
   # vite.config.ts deve ser usado apenas para build
   # vitest.config.ts deve incluir configurações de teste
   ```

5. **Vulnerabilidades npm**
   ```bash
   # Verificar vulnerabilidades (comum ter algumas moderadas)
   npm audit
   
   # Corrigir apenas quebras críticas automaticamente
   npm audit fix
   
   # Para vulnerabilidades moderadas, avaliar se vale a pena
   # atualizar dependências que podem quebrar funcionalidades
   ```

6. **Erro de TypeScript**
   ```bash
   # Verificar erros de tipos
   npx tsc --noEmit
   
   # Instalar tipos em falta
   npm install @types/nome-do-pacote
   ```

7. **Erro de linting**
   ```bash
   # Verificar e corrigir automaticamente
   npm run lint
   npm run lint -- --fix
   ```

---

## 🔌 Problemas de Conectividade

### Frontend não consegue conectar com Backend

**Sintoma:** Errors de CORS ou 404 nas chamadas de API

**Diagnóstico:**
```bash
# Verificar se backend está rodando
curl http://localhost:5000/health

# Verificar se frontend está configurado corretamente
cat frontend/.env.development
```

**Soluções:**

1. **CORS mal configurado**
   ```csharp
   // backend/Program.cs - verificar configuração CORS
   builder.Services.AddCors(options =>
   {
       options.AddPolicy("AllowFrontend",
           policy =>
           {
               policy.WithOrigins("http://localhost:5173") // Verificar porta
                     .AllowAnyHeader()
                     .AllowAnyMethod()
                     .AllowCredentials();
           });
   });
   ```

2. **URL da API incorreta**
   ```env
   # frontend/.env.development
   VITE_API_BASE_URL=http://localhost:5000/api
   ```

3. **Proxy do Vite mal configurado**
   ```typescript
   // frontend/vite.config.ts
   export default defineConfig({
     server: {
       proxy: {
         '/api': {
           target: 'http://localhost:5000',
           changeOrigin: true
         }
       }
     }
   });
   ```

### Problemas de Autenticação JWT

**Sintoma:** 401 Unauthorized em endpoints protegidos

**Diagnóstico:**
```bash
# Verificar se token está sendo enviado
# No browser DevTools > Network > Headers

# Verificar se token é válido
curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/api/cases
```

**Soluções:**

1. **Token expirado**
   ```typescript
   // Verificar no frontend se token está válido
   const isTokenExpired = (token: string) => {
     const payload = JSON.parse(atob(token.split('.')[1]));
     return payload.exp * 1000 < Date.now();
   };
   ```

2. **Secret key diferente entre ambientes**
   ```json
   // Verificar appsettings.json
   {
     "JwtSettings": {
       "SecretKey": "MesmoSecretEmTodosOsAmbientes"
     }
   }
   ```

---

## 🗄️ Problemas de Banco de Dados

### Entity Framework Migrations

**Sintoma:** Erro ao aplicar migrações

**Soluções:**

1. **Migration conflitante**
   ```bash
   # Remover migration problemática
   dotnet ef migrations remove
   
   # Criar nova migration
   dotnet ef migrations add FixDatabaseIssue
   dotnet ef database update
   ```

2. **Banco corrupto**
   ```bash
   # Backup atual
   cp casezero.db casezero_backup.db
   
   # Recriar banco
   rm casezero.db
   dotnet ef database update
   ```

3. **Schema mismatch**
   ```bash
   # Verificar diferenças
   dotnet ef migrations script
   
   # Aplicar manualmente se necessário
   sqlite3 casezero.db < migration.sql
   ```

### Problemas de Performance do Banco

**Sintoma:** Queries lentas, timeout

**Diagnóstico:**
```sql
-- Verificar queries lentas (se logging habilitado)
SELECT * FROM logs WHERE duration > 1000;

-- Verificar tamanho do banco
.dbinfo

-- Verificar índices
.indices
```

**Soluções:**

1. **Falta de índices**
   ```sql
   -- Adicionar índices para queries frequentes
   CREATE INDEX IX_CaseSessions_UserId_Status ON CaseSessions(UserId, Status);
   CREATE INDEX IX_Evidence_CaseId_Type ON Evidence(CaseId, Type);
   ```

2. **Banco fragmentado**
   ```sql
   -- Otimizar banco
   VACUUM;
   REINDEX;
   ANALYZE;
   ```

3. **Queries N+1**
   ```csharp
   // Evitar - causa N+1 queries
   var cases = await _context.Cases.ToListAsync();
   foreach (var case in cases)
   {
       var evidences = case.Evidences; // Lazy loading
   }
   
   // Correto - uma query com Include
   var cases = await _context.Cases
       .Include(c => c.Evidences)
       .ToListAsync();
   ```

---

## 🎮 Problemas de Casos e Conteúdo

### Caso não carrega

**Sintoma:** Erro 404 ou caso vazio

**Diagnóstico:**
```bash
# Verificar se arquivo case.json existe
ls -la cases/CASE-2024-001/case.json

# Validar JSON
cat cases/CASE-2024-001/case.json | jq .

# Usar script de validação
./validate_case.sh CASE-2024-001
```

**Soluções:**

1. **JSON malformado**
   ```bash
   # Validar e formatar JSON
   cat case.json | jq . > case_formatted.json
   mv case_formatted.json case.json
   ```

2. **Arquivos em falta**
   ```bash
   # Verificar todos os arquivos referenciados
   find cases/CASE-2024-001 -name "*.pdf" -o -name "*.jpg" -o -name "*.mp4"
   ```

3. **Permissões incorretas**
   ```bash
   # Corrigir permissões
   chmod -R 644 cases/CASE-2024-001/*
   chmod 755 cases/CASE-2024-001/
   ```

### Evidências não desbloqueiam

**Sintoma:** Progresso do jogo não avança

**Diagnóstico:**
```javascript
// No browser console, verificar estado do jogo
console.log(caseSession.unlockedContent);
console.log(caseSession.progress);
```

**Soluções:**

1. **Lógica de desbloqueio incorreta**
   ```json
   // Verificar unlockRequirements no case.json
   {
     "unlockRequirements": ["evidence_001", "forensic_002"]
   }
   ```

2. **Estado da sessão desatualizado**
   ```csharp
   // Forçar atualização da sessão
   await _caseSessionService.UpdateProgressAsync(sessionId, newUnlockedContent);
   ```

---

## 🚀 Problemas de Deploy

### Docker não builda

**Sintoma:** Erro durante `docker build` ou `docker-compose up`

**Soluções:**

1. **Dockerfile incorreto**
   ```dockerfile
   # Verificar se paths estão corretos
   COPY ["CaseZeroApi.csproj", "."]
   # Não usar COPY . . muito cedo
   ```

2. **Context incorreto**
   ```bash
   # Build com context correto
   docker build -t casezero-backend -f backend/Dockerfile backend/
   ```

3. **Dependências não encontradas**
   ```dockerfile
   # Certificar que restore acontece antes do copy do código
   COPY ["*.csproj", "./"]
   RUN dotnet restore
   COPY . .
   ```

### SSL/HTTPS Issues

**Sintoma:** Erros de certificado ou conexão insegura

**Soluções:**

1. **Certificado Let's Encrypt expirado**
   ```bash
   # Verificar status
   sudo certbot certificates
   
   # Renovar
   sudo certbot renew
   sudo systemctl reload nginx
   ```

2. **Configuração nginx incorreta**
   ```nginx
   # Verificar config
   sudo nginx -t
   
   # Verificar certificados
   openssl x509 -in /etc/ssl/certs/casezero.crt -text -noout
   ```

### Performance em Produção

**Sintoma:** Sistema lento, timeouts

**Diagnóstico:**
```bash
# Verificar recursos do servidor
top
df -h
free -m

# Verificar logs
tail -f /var/log/casezero/api-$(date +%Y%m%d).log
tail -f /var/log/nginx/access.log
```

**Soluções:**

1. **Falta de recursos**
   ```bash
   # Adicionar swap se necessário
   sudo fallocate -l 2G /swapfile
   sudo chmod 600 /swapfile
   sudo mkswap /swapfile
   sudo swapon /swapfile
   ```

2. **Nginx mal configurado**
   ```nginx
   # Otimizar nginx.conf
   worker_processes auto;
   worker_connections 1024;
   
   # Habilitar gzip
   gzip on;
   gzip_types text/plain application/json application/javascript text/css;
   ```

---

## 🔧 Ferramentas de Diagnóstico

### Logs Importantes

**Backend:**
```bash
# Logs da aplicação
tail -f /var/log/casezero/api-$(date +%Y%m%d).log

# Logs do systemd service
sudo journalctl -u casezero-api -f

# Logs do Entity Framework (se habilitado)
grep "Executed DbCommand" /var/log/casezero/api-$(date +%Y%m%d).log
```

**Frontend:**
```javascript
// Browser console
console.log('Environment:', import.meta.env);
console.log('API URL:', import.meta.env.VITE_API_BASE_URL);

// Network tab para verificar requests
```

**Nginx:**
```bash
# Access logs
tail -f /var/log/nginx/access.log

# Error logs
tail -f /var/log/nginx/error.log
```

### Health Checks

**API Health Check:**
```bash
curl http://localhost:5000/health
```

**Database Check:**
```bash
sqlite3 casezero.db "SELECT COUNT(*) FROM AspNetUsers;"
```

**File System Check:**
```bash
# Verificar casos
ls -la cases/
du -sh cases/*

# Verificar permissões
find /var/www/casezero -type f ! -readable
```

### Performance Monitoring

**Backend Metrics:**
```csharp
// Adicionar ao Program.cs para monitoramento
app.Use(async (context, next) =>
{
    var sw = Stopwatch.StartNew();
    await next();
    sw.Stop();
    
    if (sw.ElapsedMilliseconds > 1000)
    {
        _logger.LogWarning("Slow request: {Path} took {Duration}ms", 
            context.Request.Path, sw.ElapsedMilliseconds);
    }
});
```

**Frontend Performance:**
```javascript
// Usar Performance API
performance.mark('case-load-start');
// ... código de carregamento
performance.mark('case-load-end');
performance.measure('case-load', 'case-load-start', 'case-load-end');
console.log(performance.getEntriesByName('case-load'));
```

---

## 📞 Escalação de Problemas

### Quando Escalar

1. **Dados corrompidos** - Backup/restore necessário
2. **Falha de segurança** - Acesso não autorizado
3. **Performance crítica** - Sistema inutilizável
4. **Perda de dados** - Backup falhou

### Informações para Incluir

1. **Contexto:**
   - Quando o problema começou?
   - O que mudou recentemente?
   - Quantos usuários afetados?

2. **Logs:**
   - Logs de erro relevantes
   - Timestamps exatos
   - Stack traces completos

3. **Ambiente:**
   - Versão do sistema
   - Sistema operacional
   - Recursos disponíveis

4. **Passos para Reproduzir:**
   - Sequência exata de ações
   - Dados de entrada
   - Resultado esperado vs atual

---

## 🔄 Recovery Procedures

### Backup e Restore

```bash
# Backup completo
./scripts/backup.sh

# Restore específico
./scripts/restore.sh 20240115_120000

# Backup manual do banco
cp /var/data/casezero.db /backup/casezero_$(date +%Y%m%d_%H%M%S).db
```

### Rollback de Deploy

```bash
# Via Git (se deploy direto)
git checkout previous-working-commit
./scripts/deploy.sh

# Via Docker
docker-compose down
docker tag casezero:current casezero:broken
docker tag casezero:previous casezero:current
docker-compose up -d
```

### Recuperação de Dados

```sql
-- Recuperar dados de backup
.restore /backup/casezero_backup.db

-- Verificar integridade
PRAGMA integrity_check;

-- Reconstruir índices se necessário
REINDEX;
```

---

## 📚 Recursos Adicionais

### Documentação de Referência
- [.NET Troubleshooting](https://docs.microsoft.com/en-us/dotnet/core/diagnostics/)
- [React DevTools](https://reactjs.org/blog/2019/08/15/new-react-devtools.html)
- [SQLite Documentation](https://www.sqlite.org/docs.html)
- [Nginx Troubleshooting](https://nginx.org/en/docs/debugging_log.html)

### Ferramentas Úteis
- **Postman/Insomnia** - Teste de APIs
- **React DevTools** - Debug de componentes
- **Chrome DevTools** - Performance e network
- **DB Browser for SQLite** - Visualização do banco
- **htop/top** - Monitoramento de sistema