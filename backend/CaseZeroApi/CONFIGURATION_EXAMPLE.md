# Configuration Example for Azure OpenAI/Foundry

This example shows how to properly configure the AI Case Generation Service for production use.

## Environment Variables

Set these environment variables for secure configuration:

```bash
# Azure OpenAI Configuration
export AZURE_OPENAI_ENDPOINT="https://your-resource.openai.azure.com"
export AZURE_OPENAI_DEPLOYMENT="gpt-4"
export AZURE_OPENAI_API_VERSION="2024-02-15-preview"
export AZURE_OPENAI_API_KEY="your-actual-api-key"
```

## appsettings.Production.json

```json
{
  "AzureOpenAI": {
    "Endpoint": "${AZURE_OPENAI_ENDPOINT}",
    "Deployment": "${AZURE_OPENAI_DEPLOYMENT}",
    "ApiVersion": "${AZURE_OPENAI_API_VERSION}",
    "ApiKey": "${AZURE_OPENAI_API_KEY}",
    "ApiKeyHeaderName": "api-key"
  },
  "CasesBasePath": "/app/data/cases"
}
```

## Docker Configuration

```dockerfile
# Add to your Dockerfile
ENV AZURE_OPENAI_ENDPOINT=""
ENV AZURE_OPENAI_DEPLOYMENT="gpt-4"
ENV AZURE_OPENAI_API_VERSION="2024-02-15-preview" 
ENV AZURE_OPENAI_API_KEY=""

# Create cases directory
RUN mkdir -p /app/data/cases
VOLUME ["/app/data/cases"]
```

## Testing with Postman/curl

### Generate Complete Case

```bash
curl -X POST "https://your-api.com/api/CaseGeneration/generate" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Furto no Laboratório de Pesquisa",
    "location": "Universidade Federal, Campus Principal",
    "incidentDateTime": "2024-12-15T03:30:00-03:00",
    "pitch": "Equipamentos de alta tecnologia furtados durante a madrugada. Sistema de segurança desabilitado por dentro.",
    "twist": "Pesquisador com acesso privilegiado estava supostamente em viagem internacional.",
    "difficulty": "Alto",
    "targetDurationMinutes": 120,
    "constraints": "Ambiente universitário, sem violência física",
    "timezone": "America/Sao_Paulo",
    "generateImages": true
  }'
```

### Generate Case JSON Only

```bash
curl -X POST "https://your-api.com/api/CaseGeneration/generate-json" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Desaparecimento no Cruzeiro",
    "location": "Navio de Cruzeiro MS Atlantic",
    "incidentDateTime": "2024-11-20T22:15:00-04:00",
    "pitch": "Passageiro desaparece em alto mar durante festa. Última vez visto no deck superior.",
    "twist": "Câmeras de segurança mostram pessoa similar em outro deck no mesmo horário."
  }'
```

## Response Examples

### Successful Case Generation

```json
{
  "caseId": "CASE-2024-456",
  "caseJson": "{\"caseId\":\"CASE-2024-456\",\"title\":\"Furto no Laboratório de Pesquisa\",...}",
  "interrogatorios": [
    {
      "id": "DOC-INT-SUS-001",
      "fileName": "03_interrogatorios/INT-SUS-001-sessao-01.md",
      "content": "---\ndocType: interrogatorio\ncaseId: CASE-2024-456\n...",
      "kind": "Interrogatorio"
    }
  ],
  "relatorios": [
    {
      "id": "DOC-REL-CASE-2024-456",
      "fileName": "04_relatorios/REL-CASE-2024-456.md",
      "content": "# Relatório Investigativo\n## Caso: CASE-2024-456\n...",
      "kind": "Relatorio"
    }
  ],
  "laudos": [
    {
      "id": "ANL-014",
      "fileName": "05_laudos/ANL-014.md",
      "content": "# Laudo Pericial ANL-014\n...",
      "kind": "Laudo"
    }
  ],
  "evidenceManifest": {
    "id": "EVD-MANIFEST-CASE-2024-456",
    "fileName": "02_evidencias/EVIDENCIAS-CASE-2024-456.md",
    "content": "# Tabela-Mestra de Evidências\n...",
    "kind": "Manifest"
  },
  "imagePrompts": [
    {
      "evidenceId": "EVD-001",
      "title": "Laboratório após o furto",
      "intendedUse": "cenário",
      "prompt": "Interior de laboratório de pesquisa universitário após furto...",
      "negativePrompt": "pessoas visíveis, marcas identificáveis",
      "constraints": {
        "lighting": "iluminação fluorescente fria",
        "camera": "perspectiva ampla, lente 24mm",
        "style": "fotografia forense realista"
      }
    }
  ]
}
```

### Error Responses

```json
// Configuration Error
{
  "error": "Failed to generate case",
  "details": "AzureOpenAI:Endpoint not configured"
}

// API Error
{
  "error": "Failed to generate case",
  "details": "LLM error 401: Invalid API key"
}

// Generation Error
{
  "error": "Failed to generate case", 
  "details": "Failed to parse AI response as valid JSON"
}
```

## Monitoring and Logs

The service logs all operations at appropriate levels:

- **Information**: Case generation started/completed
- **Warning**: Non-critical issues (e.g., image prompt parsing failed)
- **Error**: Critical failures with full exception details

Example log entries:

```
2024-01-15 10:30:15 [Information] Starting case generation for "Furto no Laboratório de Pesquisa"
2024-01-15 10:30:18 [Information] Generated case.json for case CASE-2024-456
2024-01-15 10:30:45 [Information] Successfully generated and saved case CASE-2024-456
2024-01-15 10:30:45 [Information] Saved case package to /app/data/cases/CASE-2024-456
```

## File Structure

Generated cases are saved with this structure:

```
cases/
└── CASE-2024-456/
    ├── case.json                           # Main case definition
    ├── 02_evidencias/
    │   └── EVIDENCIAS-CASE-2024-456.md    # Evidence manifest
    ├── 03_interrogatorios/
    │   ├── INT-SUS-001-sessao-01.md       # Suspect 1 interrogation
    │   └── INT-SUS-002-sessao-01.md       # Suspect 2 interrogation
    ├── 04_relatorios/
    │   └── REL-CASE-2024-456.md           # Investigation report
    ├── 05_laudos/
    │   ├── ANL-014.md                      # CCTV analysis
    │   └── ANL-005.md                      # Access log analysis
    └── image_prompts.json                  # AI image generation prompts
```

## Performance Considerations

- **Generation Time**: 30-90 seconds per complete case
- **Token Usage**: ~15,000-25,000 tokens per case
- **File Size**: Generated cases typically 50-200KB total
- **Rate Limits**: Respect Azure OpenAI rate limits (RPM/TPM)

## Security Notes

- Never commit API keys to source control
- Use Azure Key Vault for production secrets
- Rotate API keys regularly
- Monitor API usage and costs
- Validate all generated content before serving to users