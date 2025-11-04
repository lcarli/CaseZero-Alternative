# HTTP Request Tests

This folder contains HTTP request files for testing the CaseZero application APIs using REST Client extensions (e.g., VS Code REST Client or IntelliJ HTTP Client).

## Structure

```
http-requests/
├── test-casegen.http           # General case generation workflow tests
├── casegen-functions/          # Azure Functions specific tests
│   ├── test-real-pdf.http      # Real PDF generation tests
│   └── test-cover-page.http    # Cover page rendering tests
└── casezero-api/               # Web API tests
    └── CaseZeroApi.http        # API endpoint tests
```

## Usage

### VS Code REST Client

1. Install the [REST Client extension](https://marketplace.visualstudio.com/items?itemName=humao.rest-client)
2. Open any `.http` file
3. Click "Send Request" above each request

### Prerequisites

- **CaseGen.Functions**: Must be running locally or deployed to Azure
  - Local: `func start` in `backend/CaseGen.Functions/bin/Debug/net9.0`
  - Port: 7071 (default)

- **CaseZeroApi**: Must be running locally or deployed
  - Local: `dotnet run` in `backend/CaseZeroApi`
  - Port: 5000 (HTTP) or 5001 (HTTPS)

## Environment Variables

Some requests may require environment variables:
- `@hostname`: API base URL (e.g., `localhost:7071`)
- `@apiKey`: Azure Functions API key (if required)
- `@token`: Authentication token for CaseZeroApi

Configure these in the `.http` file or in `http-client.env.json`.

## File Descriptions

### test-casegen.http
General integration tests covering the full case generation workflow:
- Case planning
- Suspect/evidence expansion
- Design phase
- Document/media generation
- Validation and RedTeam analysis

### casegen-functions/test-real-pdf.http
Tests for PDF document rendering:
- Police reports
- Interview transcripts
- Forensic reports
- Evidence logs
- Witness statements

### casegen-functions/test-cover-page.http
Tests for cover page generation with metadata.

### casezero-api/CaseZeroApi.http
Tests for the main Web API endpoints:
- Authentication
- Case CRUD operations
- User management
- Game progression tracking
