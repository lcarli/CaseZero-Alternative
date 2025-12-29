# CaseZero Case Linter

Ferramenta de validação para arquivos `case.json` v1.0 do CaseZero.

## 📦 Instalação

```bash
cd tools
npm install
```

## 🚀 Uso

### Validação básica

```bash
node case-linter.js ../cases/samples/case.sample.json
```

### Com schema customizado

```bash
node case-linter.js case.json --schema ./custom-schema.json
```

### Verificar arquivos físicos

```bash
node case-linter.js case.json --check-files --base-path /path/to/project
```

## ✅ O que é validado

### 1. JSON Schema
- Estrutura do JSON
- Tipos de dados
- Campos obrigatórios
- Valores de enum
- Formatos (email, date-time, etc.)

### 2. IDs Duplicados
- `assetId` únicos
- `emailId` únicos
- `suspectId` únicos
- `ruleId` únicos

### 3. Referências
- Attachments em emails devem referenciar `assetId` existentes
- `linkedAssets` em suspects devem referenciar `assetId` existentes
- Triggers de rules devem referenciar IDs existentes
- Actions de rules devem referenciar IDs existentes

### 4. Visibilidade Inicial
- Pelo menos 1 email com `visibility: "initial"` (obrigatório)
- Pelo menos 1 asset com `visibility: "initial"` (recomendado)
- Pelo menos 1 suspect com `visibility: "initial"` (recomendado)

### 5. Caminhos de Arquivos (opcional)
- Verifica se os arquivos referenciados em `filePath` existem fisicamente
- Útil para validar casos antes de deploy

## 🔧 Integração

### No package.json do projeto

```json
{
  "scripts": {
    "lint:case": "node tools/case-linter.js cases/samples/case.sample.json",
    "validate:all": "find cases -name '*.json' -exec node tools/case-linter.js {} \\;"
  }
}
```

### Pre-commit hook

```bash
#!/bin/bash
# .git/hooks/pre-commit

for file in $(git diff --cached --name-only --diff-filter=ACM | grep 'cases/.*\\.json$'); do
  node tools/case-linter.js "$file" --check-files
  if [ $? -ne 0 ]; then
    echo "❌ Case validation failed for $file"
    exit 1
  fi
done
```

### CI/CD (GitHub Actions)

```yaml
- name: Validate cases
  run: |
    cd tools && npm install
    node case-linter.js ../cases/samples/case.sample.json
```

## 📋 Output de Exemplo

```
🔍 CaseZero Case Linter v1.0

ℹ️  Validando: case.sample.json
ℹ️  Schema: case.schema.json

1. Carregando arquivo...
✅ Arquivo carregado e parseado com sucesso

2. Validando contra JSON Schema...
✅ Schema válido

3. Verificando IDs duplicados...
✅ Nenhum ID duplicado encontrado

4. Validando referências...
✅ Todas as referências são válidas

5. Verificando visibilidade inicial...
✅ Visibilidade configurada corretamente

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
✅ Validação completa: Nenhum erro ou warning encontrado!
```

## 🐛 Exemplo de Erros

```
❌ /emails/0/attachments/0: string must match pattern "^asset\.[a-z0-9_]+$"
❌ Email "email.briefing" referencia asset inexistente: "asset.nonexistent"
❌ Asset duplicado: "asset.photo_001" (índice 3)
⚠️  Nenhum email com visibility="initial" encontrado
```

## 📚 Referências

- [CASE_JSON_V1_SPEC.md](../docs/CASE_JSON_V1_SPEC.md) - Especificação completa
- [case.schema.json](../schemas/case.schema.json) - JSON Schema formal
