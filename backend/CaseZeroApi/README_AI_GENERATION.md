# AI Case Generation Service

This service provides AI-powered case generation capabilities for the CaseZero detective game using Azure Foundry (Azure OpenAI).

## Overview

The `CaseGenerationService` uses Azure OpenAI's GPT models to generate complete detective case packages including:

- **Case JSON structure**: Complete case definition with suspects, evidence, timeline, etc.
- **Interrogation documents**: Realistic police interrogation transcripts
- **Investigation reports**: Official police investigation reports
- **Forensic reports**: Technical forensic analysis documents
- **Evidence manifest**: Chain of custody documentation
- **Image prompts**: Detailed prompts for AI image generation

## Configuration

Add the following to your `appsettings.json`:

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://your-resource.openai.azure.com",
    "Deployment": "gpt-4",
    "ApiVersion": "2024-02-15-preview",
    "ApiKey": "your-api-key-here",
    "ApiKeyHeaderName": "api-key"
  }
}
```

## Usage

### Service Registration

The service is automatically registered in `Program.cs`:

```csharp
builder.Services.AddHttpClient<LlmClient>();
builder.Services.AddSingleton<LlmOptions>(/* configuration */);
builder.Services.AddScoped<ICaseGenerationService, CaseGenerationService>();
```

### API Endpoints

#### Generate Complete Case
```http
POST /api/CaseGeneration/generate
Content-Type: application/json

{
  "title": "Roubo na Clínica Saint-Émile",
  "location": "Québec, QC",
  "incidentDateTime": "2025-07-29T18:51:00-04:00",
  "pitch": "Subtração de numerário e medicamentos controlados; microabertura do cofre coincidente com queda de CFTV.",
  "twist": "Cartão de acesso de funcionária usado enquanto ela alega estar em trânsito.",
  "difficulty": "Médio-Alto",
  "targetDurationMinutes": 90,
  "constraints": "Sem violência gráfica; nomes e marcas fictícias.",
  "generateImages": true
}
```

#### Generate Case JSON Only
```http
POST /api/CaseGeneration/generate-json
Content-Type: application/json

{
  "title": "Test Case",
  "location": "Test Location",
  "incidentDateTime": "2024-01-15T10:00:00Z",
  "pitch": "Brief description of the case",
  "twist": "Unexpected element in the case"
}
```

### Programmatic Usage

```csharp
public class ExampleUsage
{
    private readonly ICaseGenerationService _caseGenerationService;

    public ExampleUsage(ICaseGenerationService caseGenerationService)
    {
        _caseGenerationService = caseGenerationService;
    }

    public async Task<CasePackage> GenerateExampleCase()
    {
        var seed = new CaseSeed(
            Title: "Murder at Corporate Tower",
            Location: "Downtown Business District",
            IncidentDateTime: DateTimeOffset.Parse("2024-01-15T22:30:00-05:00"),
            Pitch: "Executive found dead in locked office during business hours",
            Twist: "Security badge used while victim was supposedly alone",
            Difficulty: "Hard",
            TargetDurationMinutes: 120,
            Constraints: "No graphic violence; fictional names only"
        );

        var options = new GenerationOptions
        {
            GenerateImages = true
        };

        return await _caseGenerationService.GenerateCaseAsync(seed, options);
    }
}
```

## Generated Files

The service automatically saves generated cases to the `cases` directory with the following structure:

```
cases/
└── CASE-2024-XXX/
    ├── case.json                    # Main case definition
    ├── 02_evidencias/
    │   └── EVIDENCIAS-CASE-2024-XXX.md
    ├── 03_interrogatorios/
    │   ├── INT-SUS-001-sessao-01.md
    │   └── INT-SUS-002-sessao-01.md
    ├── 04_relatorios/
    │   └── REL-CASE-2024-XXX.md
    ├── 05_laudos/
    │   ├── ANL-014.md
    │   └── ANL-005.md
    └── image_prompts.json           # AI image generation prompts
```

## Models

### CaseSeed
Defines the initial parameters for case generation:
- `Title`: Case title
- `Location`: Where the incident occurred
- `IncidentDateTime`: When the incident happened
- `Pitch`: Brief description of the case
- `Twist`: Unexpected element or complication
- `Difficulty`: Case difficulty level
- `TargetDurationMinutes`: Expected time to solve
- `Constraints`: Content restrictions
- `Timezone`: Timezone for dates/times

### CasePackage
Contains all generated content:
- `CaseId`: Unique case identifier
- `CaseJson`: Main case definition
- `Interrogatorios`: List of interrogation documents
- `Relatorios`: Investigation reports
- `Laudos`: Forensic analysis reports
- `EvidenceManifest`: Evidence chain of custody
- `ImagePrompts`: AI image generation prompts

## Authentication

All endpoints require authentication. Include a valid JWT token in the Authorization header:

```http
Authorization: Bearer <your-jwt-token>
```

## Error Handling

The service includes comprehensive error handling and logging. Common errors:

- **Configuration errors**: Missing Azure OpenAI configuration
- **Authentication errors**: Invalid API keys
- **Generation errors**: AI service failures
- **File system errors**: Unable to save generated files

All errors are logged and return appropriate HTTP status codes with error details.