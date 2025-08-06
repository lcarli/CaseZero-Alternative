using CaseZeroApi.Data;
using CaseZeroApi.Models;
using System.Text.Json;

namespace CaseZeroApi.Services
{
    public class DataSeedingService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DataSeedingService> _logger;

        public DataSeedingService(ApplicationDbContext context, ILogger<DataSeedingService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task SeedGDDDataAsync()
        {
            // Check if we already have GDD data
            if (_context.Evidences.Any() || _context.Suspects.Any())
            {
                _logger.LogInformation("GDD data already exists, skipping seeding");
                return;
            }

            _logger.LogInformation("Seeding GDD-aligned data...");

            await SeedCase001DataAsync();
            await SeedCase002DataAsync();
            await SeedInitialEmailsAsync();

            await _context.SaveChangesAsync();
            _logger.LogInformation("GDD data seeding completed");
        }

        private async Task SeedCase001DataAsync()
        {
            var case001 = await _context.Cases.FindAsync("CASE-2024-001");
            if (case001 == null) return;

            // Add suspects
            var suspects = new[]
            {
                new Suspect
                {
                    CaseId = "CASE-2024-001",
                    Name = "Marcus Silva",
                    Alias = "O Fantasma",
                    Age = 34,
                    Description = "Ex-funcionário do banco, especialista em sistemas de segurança",
                    BackgroundInfo = "Trabalhou no banco por 8 anos antes de ser demitido por insubordinação. Conhece todos os sistemas de segurança.",
                    Motive = "Conhecimento interno e necessidades financeiras após demissão",
                    Alibi = "Afirma que estava em casa assistindo TV na noite do crime",
                    HasAlibiVerified = false,
                    Status = SuspectStatus.PersonOfInterest,
                    IsActualCulprit = true,
                    ContactInfo = "marcus.silva@email.com, (11) 98765-4321",
                    LastKnownLocation = "Rua das Flores, 123, Vila Madalena"
                },
                new Suspect
                {
                    CaseId = "CASE-2024-001",
                    Name = "Ana Rodriguez",
                    Alias = null,
                    Age = 28,
                    Description = "Atual gerente de TI do banco",
                    BackgroundInfo = "Funcionária exemplar com acesso privilegiado aos sistemas. Sem histórico criminal.",
                    Motive = "Acesso privilegiado aos sistemas de segurança",
                    Alibi = "Estava em reunião familiar documentada por fotos e testemunhas",
                    HasAlibiVerified = true,
                    Status = SuspectStatus.PersonOfInterest,
                    IsActualCulprit = false,
                    ContactInfo = "ana.rodriguez@bancoxyz.com, (11) 91234-5678",
                    LastKnownLocation = "Av. Paulista, 1000, Centro"
                },
                new Suspect
                {
                    CaseId = "CASE-2024-001",
                    Name = "Roberto Santos",
                    Alias = "Rob",
                    Age = 41,
                    Description = "Consultor de segurança eletrônica contratado",
                    BackgroundInfo = "Especialista em segurança com 15 anos de experiência. Trabalhou na instalação do sistema atual.",
                    Motive = "Conhecia as vulnerabilidades do sistema de segurança",
                    Alibi = "Estava trabalhando em outro projeto com horários documentados",
                    HasAlibiVerified = false,
                    Status = SuspectStatus.PersonOfInterest,
                    IsActualCulprit = false,
                    ContactInfo = "roberto@securitytech.com, (11) 94567-8901",
                    LastKnownLocation = "Rua da Consolação, 456, Centro"
                }
            };

            _context.Suspects.AddRange(suspects);

            // Add evidence with dependencies
            var evidences = new[]
            {
                new Evidence
                {
                    CaseId = "CASE-2024-001",
                    Name = "Briefing Inicial",
                    Type = "text",
                    Description = "Relatório inicial do incidente com detalhes básicos",
                    FilePath = "/cases/CASE-2024-001/case-files/briefing_inicial.txt",
                    Category = EvidenceCategory.Document,
                    Priority = EvidencePriority.High,
                    IsUnlocked = true,
                    RequiresAnalysis = false
                },
                new Evidence
                {
                    CaseId = "CASE-2024-001",
                    Name = "Planta Baixa do Banco",
                    Type = "pdf",
                    Description = "Planta arquitetônica completa do banco com sistemas de segurança",
                    FilePath = "/cases/CASE-2024-001/case-files/planta_baixa_banco.pdf",
                    Category = EvidenceCategory.Document,
                    Priority = EvidencePriority.Medium,
                    IsUnlocked = true,
                    RequiresAnalysis = false
                },
                new Evidence
                {
                    CaseId = "CASE-2024-001",
                    Name = "Gravação Câmera 01",
                    Type = "video",
                    Description = "Filmagem da câmera de segurança principal durante o crime",
                    FilePath = "/cases/CASE-2024-001/security/footage_camera_01.mp4",
                    Category = EvidenceCategory.Digital,
                    Priority = EvidencePriority.Critical,
                    IsUnlocked = true,
                    RequiresAnalysis = true
                },
                new Evidence
                {
                    CaseId = "CASE-2024-001",
                    Name = "Logs de Acesso",
                    Type = "data",
                    Description = "Registros de acesso ao sistema de segurança",
                    FilePath = "/cases/CASE-2024-001/security/access_logs.csv",
                    Category = EvidenceCategory.Digital,
                    Priority = EvidencePriority.High,
                    IsUnlocked = false,
                    RequiresAnalysis = false,
                    DependsOnEvidenceIds = JsonSerializer.Serialize(new[] { "3" }) // Depends on camera footage analysis
                },
                new Evidence
                {
                    CaseId = "CASE-2024-001",
                    Name = "Impressões Digitais do Cofre",
                    Type = "image",
                    Description = "Impressões digitais coletadas na maçaneta do cofre principal",
                    FilePath = "/cases/CASE-2024-001/forensics/fingerprints_vault.jpg",
                    Category = EvidenceCategory.Biological,
                    Priority = EvidencePriority.Critical,
                    IsUnlocked = true,
                    RequiresAnalysis = true
                },
                new Evidence
                {
                    CaseId = "CASE-2024-001",
                    Name = "Análise Forense Digital",
                    Type = "text",
                    Description = "Relatório detalhado da análise dos sistemas digitais",
                    FilePath = "/cases/CASE-2024-001/forensics/digital_analysis.txt",
                    Category = EvidenceCategory.Technical,
                    Priority = EvidencePriority.High,
                    IsUnlocked = false,
                    RequiresAnalysis = false,
                    DependsOnEvidenceIds = JsonSerializer.Serialize(new[] { "4" }) // Depends on access logs
                }
            };

            _context.Evidences.AddRange(evidences);
        }

        private async Task SeedCase002DataAsync()
        {
            var case002 = await _context.Cases.FindAsync("CASE-2024-002");
            if (case002 == null) return;

            // Add suspects for case 002
            var suspects = new[]
            {
                new Suspect
                {
                    CaseId = "CASE-2024-002",
                    Name = "Carlos Mendes",
                    Age = 45,
                    Description = "CFO da TechCorp",
                    BackgroundInfo = "CFO há 3 anos, sob pressão por resultados financeiros devido à competição no mercado.",
                    Motive = "Pressão extrema por resultados financeiros positivos",
                    Alibi = "Estava em viagem de negócios documentada",
                    HasAlibiVerified = true,
                    Status = SuspectStatus.PersonOfInterest,
                    IsActualCulprit = false
                },
                new Suspect
                {
                    CaseId = "CASE-2024-002",
                    Name = "Ana Rodriguez",
                    Age = 32,
                    Description = "Analista financeira sênior",
                    BackgroundInfo = "Funcionária modelo com acesso direto aos sistemas contábeis. Recentemente passou por dificuldades financeiras pessoais.",
                    Motive = "Acesso privilegiado aos sistemas contábeis e pressões financeiras pessoais",
                    Alibi = "Afirma que estava trabalhando até tarde no escritório",
                    HasAlibiVerified = false,
                    Status = SuspectStatus.PersonOfInterest,
                    IsActualCulprit = true
                }
            };

            _context.Suspects.AddRange(suspects);

            // Add evidence for case 002
            var evidences = new[]
            {
                new Evidence
                {
                    CaseId = "CASE-2024-002",
                    Name = "Relatório Inicial",
                    Type = "text",
                    Description = "Relatório inicial da auditoria interna",
                    FilePath = "/cases/CASE-2024-002/case-files/case002.txt",
                    Category = EvidenceCategory.Document,
                    Priority = EvidencePriority.Medium,
                    IsUnlocked = true,
                    RequiresAnalysis = false
                },
                new Evidence
                {
                    CaseId = "CASE-2024-002",
                    Name = "Relatórios Financeiros",
                    Type = "spreadsheet",
                    Description = "Planilhas financeiras dos últimos 2 anos",
                    FilePath = "/cases/CASE-2024-002/case-files/financial_reports.xlsx",
                    Category = EvidenceCategory.Document,
                    Priority = EvidencePriority.High,
                    IsUnlocked = true,
                    RequiresAnalysis = true
                },
                new Evidence
                {
                    CaseId = "CASE-2024-002",
                    Name = "Análise Forense Digital",
                    Type = "text",
                    Description = "Análise forense dos sistemas computacionais",
                    FilePath = "/cases/CASE-2024-002/forensics/digital_forensics.txt",
                    Category = EvidenceCategory.Technical,
                    Priority = EvidencePriority.High,
                    IsUnlocked = false,
                    RequiresAnalysis = false,
                    DependsOnEvidenceIds = JsonSerializer.Serialize(new[] { "8" }) // Depends on financial reports analysis
                }
            };

            _context.Evidences.AddRange(evidences);
        }

        private async Task SeedInitialEmailsAsync()
        {
            // Get the test user
            var testUser = _context.Users.FirstOrDefault(u => u.Email == "detective@police.gov");
            if (testUser == null) return;

            // Create a system user for system emails
            var systemUser = _context.Users.FirstOrDefault(u => u.UserName == "system");
            if (systemUser == null)
            {
                systemUser = new User
                {
                    UserName = "system",
                    Email = "system@police.gov",
                    FirstName = "System",
                    LastName = "Admin",
                    Department = "System",
                    Position = "System",
                    BadgeNumber = "0000",
                    IsApproved = true,
                    Rank = DetectiveRank.Commander
                };
                _context.Users.Add(systemUser);
                await _context.SaveChangesAsync();
            }

            var emails = new[]
            {
                new Email
                {
                    ToUserId = testUser.Id,
                    FromUserId = systemUser.Id,
                    CaseId = "CASE-2024-001",
                    Subject = "URGENTE: Designação de Caso #CASE-2024-001",
                    Content = @"BRIEFING DO CASO: Roubo no Banco Central

DETALHES DO INCIDENTE:
- Localização: Banco Central - Centro da Cidade
- Data do Incidente: 15/01/2024 02:30
- Prioridade: Alta
- Nível de Dificuldade: 7/10

DESCRIÇÃO:
Roubo milionário ocorreu durante a madrugada. Sistema de segurança foi comprometido de forma sofisticada. Suspeitos utilizaram conhecimento interno do banco. Não houve feridos, mas perdas financeiras são significativas.

SUSPEITOS INICIAIS:
- Marcus Silva (ex-funcionário)
- Ana Rodriguez (gerente de TI) 
- Roberto Santos (consultor de segurança)

INSTRUÇÕES:
1. Revise todos os arquivos de evidência disponíveis
2. Solicite análises forenses conforme necessário
3. Identifique suspeitos e verifique álibis
4. Colete evidências suficientes para fundamentar uma acusação
5. Submeta seu relatório final com suspeito e evidências

Este caso requer rank mínimo: Detetive Sênior

Bom trabalho, detetive.

Chefe Johnson
Departamento de Polícia Metropolitana",
                    Preview = "Nova designação de caso de alta prioridade: Roubo no Banco Central. Requer experiência em crimes financeiros...",
                    Type = EmailType.CaseBriefing,
                    Priority = EmailPriority.High,
                    IsSystemGenerated = true,
                    Attachments = JsonSerializer.Serialize(new[]
                    {
                        new { name = "briefing_inicial.txt", size = "3.2 KB", type = "text" },
                        new { name = "planta_baixa_banco.pdf", size = "856 KB", type = "pdf" }
                    })
                },
                new Email
                {
                    ToUserId = testUser.Id,
                    FromUserId = systemUser.Id,
                    CaseId = "CASE-2024-002",
                    Subject = "Designação de Caso #CASE-2024-002 - Fraude Corporativa",
                    Content = @"BRIEFING DO CASO: Fraude Corporativa TechCorp

DETALHES DO INCIDENTE:
- Localização: TechCorp Headquarters  
- Data do Incidente: 01/02/2024
- Prioridade: Média
- Nível de Dificuldade: 5/10

DESCRIÇÃO:
Auditoria interna detectou irregularidades nos relatórios financeiros dos últimos 2 anos. Suspeita de manipulação de dados contábeis para inflacionar lucros. Possível envolvimento da alta gerência.

INSTRUÇÕES:
Investigue as irregularidades financeiras e identifique os responsáveis.

Chefe Johnson",
                    Preview = "Caso de fraude corporativa na TechCorp. Auditoria detectou irregularidades financeiras...",
                    Type = EmailType.CaseBriefing,
                    Priority = EmailPriority.Normal,
                    IsSystemGenerated = true
                }
            };

            _context.Emails.AddRange(emails);
        }
    }
}