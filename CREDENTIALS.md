# Credentials Documentation for CaseZero

## Test User Accounts

The following test accounts are automatically created when the database is initialized following the new registration pattern:

### Primary Test User
- **Police Email:** `john.doe@fic-police.gov`
- **Personal Email:** `john.doe.personal@example.com`
- **Password:** `Password123!`
- **Name:** John Doe
- **Department:** ColdCase
- **Position:** rook
- **Badge Number:** #4729
- **Rank:** Rook
- **Status:** Email verified and ready for login

### Secondary Test User  
- **Police Email:** `sarah.connor@fic-police.gov`
- **Personal Email:** `sarah.connor.personal@example.com`
- **Password:** `Inspector456!`
- **Name:** Sarah Connor
- **Department:** ColdCase
- **Position:** detective
- **Badge Number:** #1984
- **Rank:** Detective
- **Status:** Email verified and ready for login

## New Registration Process

1. **Registration**: Only requires firstName, lastName, and personalEmail
2. **Email Generation**: Police email auto-generated as `{firstname}.{lastname}@fic-police.gov`
3. **Auto-Assignment**: Department = "ColdCase", Position = "rook", Badge = unique 4-digit number
4. **Email Verification**: Verification email sent to personal email
5. **Account Activation**: User clicks verification link to activate account

## Email Service Configuration

To enable email sending, configure the following in `appsettings.json`:

```json
{
  "EmailSettings": {
    "SmtpServer": "smtp.your-provider.com",
    "SmtpPort": 587,
    "FromEmail": "noreply@your-domain.com",
    "FromName": "Sistema CaseZero",
    "SmtpUsername": "your-smtp-username",
    "SmtpPassword": "your-smtp-password",
    "EnableSsl": true
  }
}
```

**Note**: If email settings are not configured, the system will log email attempts but won't actually send emails. This allows development without email setup.

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