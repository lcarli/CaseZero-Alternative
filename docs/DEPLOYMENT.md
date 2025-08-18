# Guia de Deploy - Sistema CaseZero

## Overview

Este guia fornece instruções detalhadas para fazer deploy do sistema CaseZero em diferentes ambientes, desde desenvolvimento local até produção.

## Pré-requisitos

### Software Necessário

| Componente | Versão Mínima | Propósito |
|------------|---------------|-----------|
| Node.js | 18.x | Runtime para frontend |
| npm | 8.x | Gerenciador de pacotes |
| .NET SDK | 8.0 | Runtime para backend |
| Git | 2.x | Controle de versão |

### Opcional (para produção)
- Docker & Docker Compose
- Nginx (proxy reverso)
- SSL certificates
- Process manager (PM2, systemd)

---

## Deployment Local (Desenvolvimento)

### 1. Clone do Repositório

```bash
git clone https://github.com/lcarli/CaseZero-Alternative.git
cd CaseZero-Alternative
```

### 2. Setup do Backend

```bash
cd backend/CaseZeroApi

# Instalar dependências
dotnet restore

# Aplicar migrações (cria banco SQLite)
dotnet ef database update

# Executar em modo desenvolvimento
dotnet run
```

**Configuração de Desenvolvimento (`appsettings.Development.json`):**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=casezero_dev.db"
  },
  "JwtSettings": {
    "SecretKey": "DevSecretKeyThatShouldBeAtLeast32Characters!",
    "Issuer": "CaseZeroApi",
    "Audience": "CaseZeroFrontend",
    "ExpiryDays": 7
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

### 3. Setup do Frontend

```bash
cd frontend

# Instalar dependências
npm install

# Executar servidor de desenvolvimento
npm run dev
```

**Arquivo de configuração (`.env.development`):**
```env
VITE_API_BASE_URL=http://localhost:5000/api
VITE_ENV=development
```

### 4. Verificação

- Backend: http://localhost:5000
- Frontend: http://localhost:5173
- Usuário de teste: `john.doe@fic-police.gov` / `Password123!`
- **Novo sistema de registro**: Apenas nome, sobrenome e email pessoal necessários

---

## Deployment em Staging

### 1. Preparação do Ambiente

```bash
# Criar diretório do projeto
sudo mkdir -p /var/www/casezero
sudo chown $USER:$USER /var/www/casezero
cd /var/www/casezero

# Clone do código
git clone https://github.com/lcarli/CaseZero-Alternative.git .
```

### 2. Backend - Staging

```bash
cd backend/CaseZeroApi

# Configurar para staging
export ASPNETCORE_ENVIRONMENT=Staging

# Build da aplicação
dotnet publish -c Release -o publish

# Configurar banco de dados
dotnet ef database update --configuration Release
```

**Configuração de Staging (`appsettings.Staging.json`):**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=/var/data/casezero_staging.db"
  },
  "JwtSettings": {
    "SecretKey": "${JWT_SECRET_KEY}",
    "Issuer": "CaseZeroApi",
    "Audience": "CaseZeroFrontend",
    "ExpiryDays": 7
  },
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "CaseZeroApi": "Information"
    }
  },
  "AllowedHosts": "staging.casezero.com"
}
```

### 3. Frontend - Staging

```bash
cd frontend

# Configurar variáveis de ambiente
echo "VITE_API_BASE_URL=https://staging-api.casezero.com/api" > .env.staging

# Build para staging
npm run build
```

### 4. Configuração do Nginx

```nginx
# /etc/nginx/sites-available/casezero-staging
server {
    listen 80;
    server_name staging.casezero.com;
    return 301 https://$server_name$request_uri;
}

server {
    listen 443 ssl http2;
    server_name staging.casezero.com;

    ssl_certificate /etc/ssl/certs/casezero.crt;
    ssl_certificate_key /etc/ssl/private/casezero.key;

    # Frontend
    location / {
        root /var/www/casezero/frontend/dist;
        try_files $uri $uri/ /index.html;
        
        # Cache estático
        location ~* \.(js|css|png|jpg|jpeg|gif|ico|svg)$ {
            expires 1y;
            add_header Cache-Control "public, immutable";
        }
    }

    # API Backend
    location /api {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_cache_bypass $http_upgrade;
    }
}
```

### 5. Systemd Service para Backend

```ini
# /etc/systemd/system/casezero-api.service
[Unit]
Description=CaseZero API
After=network.target

[Service]
Type=simple
User=www-data
WorkingDirectory=/var/www/casezero/backend/CaseZeroApi/publish
ExecStart=/usr/bin/dotnet CaseZeroApi.dll
Restart=always
RestartSec=10
Environment=ASPNETCORE_ENVIRONMENT=Staging
Environment=ASPNETCORE_URLS=http://localhost:5000

[Install]
WantedBy=multi-user.target
```

**Ativar e iniciar o serviço:**
```bash
sudo systemctl enable casezero-api
sudo systemctl start casezero-api
sudo systemctl status casezero-api
```

---

## Deployment em Produção

### 1. Preparação do Servidor

```bash
# Atualizar sistema
sudo apt update && sudo apt upgrade -y

# Instalar dependências
sudo apt install -y nginx certbot python3-certbot-nginx

# Instalar .NET 8
wget https://packages.microsoft.com/config/ubuntu/20.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
sudo apt update
sudo apt install -y dotnet-sdk-8.0

# Instalar Node.js
curl -fsSL https://deb.nodesource.com/setup_18.x | sudo -E bash -
sudo apt install -y nodejs
```

### 2. Backend - Produção

**Configuração de Produção (`appsettings.Production.json`):**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=/var/data/casezero.db"
  },
  "JwtSettings": {
    "SecretKey": "${JWT_SECRET_KEY}",
    "Issuer": "CaseZeroApi",
    "Audience": "CaseZeroFrontend",
    "ExpiryDays": 1
  },
  "EmailSettings": {
    "SmtpServer": "${SMTP_SERVER}",
    "SmtpPort": 587,
    "FromEmail": "${FROM_EMAIL}",
    "FromName": "Sistema CaseZero",
    "SmtpUsername": "${SMTP_USERNAME}",
    "SmtpPassword": "${SMTP_PASSWORD}",
    "EnableSsl": true
  },
  "Frontend": {
    "BaseUrl": "https://casezero.com"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "CaseZeroApi": "Information"
    }
  },
  "AllowedHosts": "casezero.com,www.casezero.com"
}
```

**Script de Deploy Backend:**
```bash
#!/bin/bash
# deploy-backend.sh

set -e

echo "🚀 Deploying CaseZero Backend to Production..."

# Variáveis
DEPLOY_DIR="/var/www/casezero"
SERVICE_NAME="casezero-api"
BACKUP_DIR="/var/backups/casezero"

# Criar backup do banco de dados
echo "📦 Creating database backup..."
sudo mkdir -p $BACKUP_DIR
sudo cp /var/data/casezero.db $BACKUP_DIR/casezero_$(date +%Y%m%d_%H%M%S).db

# Parar serviço
echo "⏹️ Stopping service..."
sudo systemctl stop $SERVICE_NAME

# Fazer backup da versão atual
echo "💾 Backing up current version..."
sudo cp -r $DEPLOY_DIR/backend/CaseZeroApi/publish $BACKUP_DIR/backend_$(date +%Y%m%d_%H%M%S)

# Pull do código atualizado
echo "📥 Pulling latest code..."
cd $DEPLOY_DIR
git pull origin main

# Build da nova versão
echo "🔨 Building application..."
cd backend/CaseZeroApi
dotnet publish -c Release -o publish

# Aplicar migrações
echo "🗄️ Applying database migrations..."
export ASPNETCORE_ENVIRONMENT=Production
dotnet ef database update --configuration Release

# Iniciar serviço
echo "▶️ Starting service..."
sudo systemctl start $SERVICE_NAME
sudo systemctl status $SERVICE_NAME

echo "✅ Backend deployment completed!"
```

### 3. Frontend - Produção

**Script de Deploy Frontend:**
```bash
#!/bin/bash
# deploy-frontend.sh

set -e

echo "🚀 Deploying CaseZero Frontend to Production..."

# Variáveis
DEPLOY_DIR="/var/www/casezero"
NGINX_ROOT="/var/www/casezero/frontend/dist"

# Fazer backup da versão atual
echo "💾 Backing up current version..."
sudo cp -r $NGINX_ROOT /var/backups/casezero/frontend_$(date +%Y%m%d_%H%M%S)

# Configurar variáveis de ambiente
echo "⚙️ Setting up environment..."
cd $DEPLOY_DIR/frontend
echo "VITE_API_BASE_URL=https://casezero.com/api" > .env.production

# Build da aplicação
echo "🔨 Building application..."
npm ci --production=false
npm run build

# Testar nginx config
echo "🔧 Testing nginx configuration..."
sudo nginx -t

# Reload nginx
echo "🔄 Reloading nginx..."
sudo systemctl reload nginx

echo "✅ Frontend deployment completed!"
```

### 4. SSL Certificate com Let's Encrypt

```bash
# Obter certificado SSL
sudo certbot --nginx -d casezero.com -d www.casezero.com

# Configurar renovação automática
sudo crontab -e
# Adicionar linha:
0 12 * * * /usr/bin/certbot renew --quiet
```

### 5. Configuração de Segurança

**Firewall (UFW):**
```bash
sudo ufw enable
sudo ufw allow 22    # SSH
sudo ufw allow 80    # HTTP
sudo ufw allow 443   # HTTPS
sudo ufw deny 5000   # Bloquear acesso direto à API
```

**Nginx Security Headers:**
```nginx
# Adicionar ao server block
add_header X-Frame-Options "SAMEORIGIN" always;
add_header X-XSS-Protection "1; mode=block" always;
add_header X-Content-Type-Options "nosniff" always;
add_header Referrer-Policy "no-referrer-when-downgrade" always;
add_header Content-Security-Policy "default-src 'self' http: https: data: blob: 'unsafe-inline'" always;
add_header Strict-Transport-Security "max-age=31536000; includeSubDomains" always;
```

---

## Configuração do Serviço de Email

### 1. Configuração de Email para Produção

O CaseZero utiliza um sistema de verificação por email. Para configurar o envio de emails:

**Variáveis de Ambiente Necessárias:**
```bash
# Email Service Configuration
export SMTP_SERVER="smtp.gmail.com"          # Servidor SMTP
export SMTP_USERNAME="your-email@gmail.com"   # Usuário SMTP
export SMTP_PASSWORD="your-app-password"      # Senha do app (Gmail)
export FROM_EMAIL="noreply@your-domain.com"   # Email remetente
```

**Para Gmail:**
1. Ativar autenticação de 2 fatores
2. Gerar senha de app específica
3. Usar `smtp.gmail.com:587` com SSL

**Para SendGrid:**
```bash
export SMTP_SERVER="smtp.sendgrid.net"
export SMTP_USERNAME="apikey"
export SMTP_PASSWORD="your-sendgrid-api-key"
export FROM_EMAIL="noreply@your-domain.com"
```

**Para AWS SES:**
```bash
export SMTP_SERVER="email-smtp.us-east-1.amazonaws.com"
export SMTP_USERNAME="your-aws-access-key-id"
export SMTP_PASSWORD="your-aws-secret-access-key"
export FROM_EMAIL="noreply@your-verified-domain.com"
```

### 2. Testando Email Service

**Verificar configuração em desenvolvimento:**
```bash
# Logs para verificar se emails estão sendo processados
docker logs casezero-api | grep -i email

# Testar registro sem email real
curl -X POST "http://localhost:5000/api/auth/register" \
  -H "Content-Type: application/json" \
  -d '{
    "firstName": "Test", 
    "lastName": "User",
    "personalEmail": "test@example.com",
    "password": "TestPassword123!"
  }'
```

### 3. Fallback sem Email

Se não configurar o serviço de email:
- Sistema funciona normalmente em desenvolvimento
- Logs mostram tentativas de envio
- Contas podem ser verificadas manualmente via banco de dados:

```sql
-- Verificar conta manualmente (desenvolvimento)
UPDATE Users SET EmailVerified = 1 WHERE Email = 'user@fic-police.gov';
```

---

## Deployment com Docker

### 1. Dockerfile do Backend

```dockerfile
# backend/CaseZeroApi/Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["CaseZeroApi.csproj", "."]
RUN dotnet restore "./CaseZeroApi.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "CaseZeroApi.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "CaseZeroApi.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Instalar Entity Framework tools
RUN dotnet tool install --global dotnet-ef
ENV PATH="$PATH:/root/.dotnet/tools"

ENTRYPOINT ["dotnet", "CaseZeroApi.dll"]
```

### 2. Dockerfile do Frontend

```dockerfile
# frontend/Dockerfile
FROM node:18-alpine AS build

WORKDIR /app
COPY package*.json ./
RUN npm ci

COPY . .
RUN npm run build

FROM nginx:alpine AS production
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/nginx.conf

EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
```

### 3. Docker Compose

```yaml
# docker-compose.yml
version: '3.8'

services:
  backend:
    build: 
      context: ./backend/CaseZeroApi
      dockerfile: Dockerfile
    ports:
      - "5000:80"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ConnectionStrings__DefaultConnection=Data Source=/data/casezero.db
      - JwtSettings__SecretKey=${JWT_SECRET_KEY}
    volumes:
      - casezero_data:/data
      - ./cases:/app/cases:ro
    depends_on:
      - db
    restart: unless-stopped

  frontend:
    build:
      context: ./frontend
      dockerfile: Dockerfile
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./nginx/ssl:/etc/nginx/ssl:ro
    depends_on:
      - backend
    restart: unless-stopped

  nginx:
    image: nginx:alpine
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./nginx/nginx.conf:/etc/nginx/nginx.conf:ro
      - ./nginx/ssl:/etc/nginx/ssl:ro
      - ./frontend/dist:/usr/share/nginx/html:ro
    depends_on:
      - backend
      - frontend
    restart: unless-stopped

volumes:
  casezero_data:
    driver: local

networks:
  default:
    name: casezero-network
```

### 4. Deploy com Docker

```bash
# Criar variáveis de ambiente
echo "JWT_SECRET_KEY=YourProductionSecretKey" > .env

# Build e execução
docker-compose up -d --build

# Verificar logs
docker-compose logs -f

# Aplicar migrações
docker-compose exec backend dotnet ef database update
```

---

## Monitoramento e Logs

### 1. Configuração de Logs

**Serilog (Backend):**
```csharp
// Program.cs
builder.Host.UseSerilog((context, configuration) =>
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .WriteTo.Console()
        .WriteTo.File("/var/log/casezero/api-.log", 
            rollingInterval: RollingInterval.Day)
        .WriteTo.Seq("http://localhost:5341") // Opcional: Seq server
);
```

**Frontend Logging:**
```typescript
// src/services/logger.ts
class Logger {
  private apiUrl = import.meta.env.VITE_API_BASE_URL;

  error(message: string, error?: Error) {
    console.error(message, error);
    
    // Enviar para backend em produção
    if (import.meta.env.PROD) {
      this.sendToBackend('error', message, error?.stack);
    }
  }

  private async sendToBackend(level: string, message: string, stack?: string) {
    try {
      await fetch(`${this.apiUrl}/logs`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ level, message, stack, timestamp: new Date() })
      });
    } catch (e) {
      // Ignore logging errors
    }
  }
}
```

### 2. Health Checks

**Backend Health Check:**
```csharp
// Program.cs
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>()
    .AddCheck("cases-directory", () => 
        Directory.Exists("./cases") 
            ? HealthCheckResult.Healthy() 
            : HealthCheckResult.Unhealthy());

app.MapHealthChecks("/health");
```

**Script de Monitoramento:**
```bash
#!/bin/bash
# monitor.sh

API_URL="https://casezero.com"
SLACK_WEBHOOK="your-slack-webhook-url"

# Verificar saúde da API
if ! curl -f -s "$API_URL/health" > /dev/null; then
    echo "❌ API health check failed"
    curl -X POST -H 'Content-type: application/json' \
        --data '{"text":"🚨 CaseZero API is down!"}' \
        $SLACK_WEBHOOK
    exit 1
fi

# Verificar espaço em disco
DISK_USAGE=$(df /var/data | awk 'NR==2 {print $5}' | sed 's/%//')
if [ $DISK_USAGE -gt 90 ]; then
    curl -X POST -H 'Content-type: application/json' \
        --data "{\"text\":\"⚠️ Disk usage is at ${DISK_USAGE}%\"}" \
        $SLACK_WEBHOOK
fi

# Verificar logs de erro
ERROR_COUNT=$(grep -c "ERROR" /var/log/casezero/api-$(date +%Y%m%d).log)
if [ $ERROR_COUNT -gt 10 ]; then
    curl -X POST -H 'Content-type: application/json' \
        --data "{\"text\":\"⚠️ High error count: ${ERROR_COUNT} errors today\"}" \
        $SLACK_WEBHOOK
fi

echo "✅ All checks passed"
```

---

## Backup e Restore

### 1. Script de Backup

```bash
#!/bin/bash
# backup.sh

BACKUP_DIR="/var/backups/casezero"
DB_PATH="/var/data/casezero.db"
APP_DIR="/var/www/casezero"
DATE=$(date +%Y%m%d_%H%M%S)

echo "🗄️ Starting backup process..."

# Criar diretório de backup
mkdir -p $BACKUP_DIR/$DATE

# Backup do banco de dados
echo "📦 Backing up database..."
cp $DB_PATH $BACKUP_DIR/$DATE/casezero.db

# Backup do código
echo "📁 Backing up application files..."
tar -czf $BACKUP_DIR/$DATE/application.tar.gz -C $APP_DIR .

# Backup dos cases
echo "📋 Backing up cases..."
tar -czf $BACKUP_DIR/$DATE/cases.tar.gz -C $APP_DIR/cases .

# Upload para S3 (opcional)
if [ ! -z "$AWS_S3_BUCKET" ]; then
    echo "☁️ Uploading to S3..."
    aws s3 cp $BACKUP_DIR/$DATE s3://$AWS_S3_BUCKET/backups/$DATE --recursive
fi

# Limpeza de backups antigos (manter últimos 30 dias)
find $BACKUP_DIR -type d -mtime +30 -exec rm -rf {} \;

echo "✅ Backup completed: $BACKUP_DIR/$DATE"
```

### 2. Script de Restore

```bash
#!/bin/bash
# restore.sh

if [ -z "$1" ]; then
    echo "Usage: $0 <backup_date>"
    echo "Available backups:"
    ls -la /var/backups/casezero/
    exit 1
fi

BACKUP_DATE=$1
BACKUP_DIR="/var/backups/casezero/$BACKUP_DATE"

echo "🔄 Starting restore process from $BACKUP_DATE..."

# Parar serviços
echo "⏹️ Stopping services..."
sudo systemctl stop casezero-api
sudo systemctl stop nginx

# Restore do banco de dados
echo "🗄️ Restoring database..."
sudo cp $BACKUP_DIR/casezero.db /var/data/casezero.db

# Restore da aplicação
echo "📁 Restoring application..."
cd /var/www
sudo rm -rf casezero
sudo tar -xzf $BACKUP_DIR/application.tar.gz

# Iniciar serviços
echo "▶️ Starting services..."
sudo systemctl start casezero-api
sudo systemctl start nginx

echo "✅ Restore completed!"
```

---

## Troubleshooting

### 1. Problemas Comuns

**Backend não inicia:**
```bash
# Verificar logs
sudo journalctl -u casezero-api -f

# Verificar permissões
sudo chown -R www-data:www-data /var/www/casezero
sudo chmod -R 755 /var/www/casezero

# Verificar banco de dados
sudo sqlite3 /var/data/casezero.db ".tables"
```

**Frontend não carrega:**
```bash
# Verificar build
cd /var/www/casezero/frontend
npm run build

# Verificar nginx
sudo nginx -t
sudo systemctl status nginx

# Verificar permissões
sudo chown -R www-data:www-data /var/www/casezero/frontend/dist
```

### 2. Performance Tuning

**Nginx Optimization:**
```nginx
# nginx.conf
worker_processes auto;
worker_connections 1024;

http {
    # Gzip compression
    gzip on;
    gzip_vary on;
    gzip_types text/plain text/css application/json application/javascript text/xml application/xml application/xml+rss text/javascript;

    # Caching
    location ~* \.(css|js|png|jpg|jpeg|gif|ico|svg)$ {
        expires 1y;
        add_header Cache-Control "public, immutable";
    }
}
```

**Backend Optimization:**
```csharp
// Program.cs
builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = 10485760; // 10MB
});

// Response compression
builder.Services.AddResponseCompression(options =>
{
    options.Providers.Add<GzipCompressionProvider>();
    options.EnableForHttps = true;
});
```

### 3. Security Checklist

- [ ] SSL/TLS configurado (A+ rating no SSL Labs)
- [ ] Firewall configurado adequadamente
- [ ] JWT secret keys são seguros e únicos
- [ ] Backup automático configurado
- [ ] Logs de auditoria habilitados
- [ ] Rate limiting implementado
- [ ] Headers de segurança configurados
- [ ] Dependências atualizadas
- [ ] Monitoramento ativo
- [ ] Procedimentos de resposta a incidentes documentados