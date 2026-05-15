-- Diagnostic queries para verificar visibilidade de emails

-- 1. Verificar sessões criadas
SELECT TOP 5 Id, UserId, CaseId, Status, SessionStart, SessionEnd
FROM CaseSessions
ORDER BY SessionStart DESC;

-- 2. Verificar assets visíveis
SELECT UserId, CaseId, AssetId, UnlockedAt
FROM CaseSessionVisibleAssets
ORDER BY UnlockedAt DESC;

-- 3. Verificar emails visíveis (PROBLEMA AQUI)
SELECT UserId, CaseId, EmailId, UnlockedAt
FROM CaseSessionVisibleEmails
ORDER BY UnlockedAt DESC;

-- 4. Ver estados de emails
SELECT UserId, CaseId, EmailId, ReadAt, OpenCount
FROM CaseSessionEmailStates;

-- 5. Contar registros
SELECT 
    'Sessions' as TableName, COUNT(*) as Count FROM CaseSessions
UNION ALL
SELECT 
    'VisibleAssets', COUNT(*) FROM CaseSessionVisibleAssets
UNION ALL
SELECT 
    'VisibleEmails', COUNT(*) FROM CaseSessionVisibleEmails
UNION ALL
SELECT 
    'EmailStates', COUNT(*) FROM CaseSessionEmailStates;
