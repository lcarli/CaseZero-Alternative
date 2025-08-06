# Test Credentials for CaseZero

## Seeded User Accounts

The following test accounts are automatically created when the database is initialized:

### Primary Test User
- **Email:** `detective@police.gov`
- **Password:** `Password123!`
- **Name:** John Doe
- **Department:** Investigation Division
- **Position:** Detective
- **Badge Number:** #4729
- **Rank:** Detective2
- **Status:** Approved and ready for login

### Secondary Test User  
- **Email:** `inspector@police.gov`
- **Password:** `Inspector456!`
- **Name:** Sarah Connor
- **Department:** Homicide Division
- **Position:** Inspector
- **Badge Number:** #1984
- **Rank:** Sergeant
- **Status:** Approved and ready for login

## Usage

1. Start the backend server: `cd backend/CaseZeroApi && dotnet run`
2. Start the frontend server: `cd frontend && npm run dev`
3. Navigate to `http://localhost:5173`
4. Click "Entrar no Sistema" 
5. Use any of the credentials above to login
6. Access the dashboard to view cases and start investigating

## Features Available After Login

- **Dashboard**: View case statistics, available cases, and weekly objectives
- **Cases**: Access to seeded investigation cases including:
  - CASE-2024-001: Roubo no Banco Central (High Priority)
  - CASE-2024-002: Fraude Corporativa TechCorp (Medium Priority)  
  - CASE-2024-003: Homicídio no Porto (Resolved)
- **Evidence System**: Examine and analyze evidence pieces
- **Suspect Profiles**: Review suspect information and alibis
- **Forensic Analysis**: Request and review forensic reports
- **Email System**: Receive case briefings and updates

## Database

The application uses SQLite with the database file `casezero.db` created automatically on first run with all necessary seed data including users, cases, evidence, suspects, and emails.