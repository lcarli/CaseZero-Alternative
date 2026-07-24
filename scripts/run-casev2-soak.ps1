param(
    [ValidateSet("en-US", "pt-BR", "es-ES", "fr-FR")]
    [string]$Language = "en-US",
    [int]$CasesPerDifficulty = 3,
    [int]$CooldownSeconds = 60,
    [int]$MaxAttempts = 3,
    [string]$ReportPath = "casev2-soak-report.json",
    [switch]$KeepCases
)

$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repo "functions\CaseGen.Functions.Tests\CaseGen.Functions.Tests.csproj"
$resolvedReportPath = if ([System.IO.Path]::IsPathRooted($ReportPath)) {
    $ReportPath
} else {
    Join-Path $repo $ReportPath
}
$reportDirectory = Split-Path -Parent $resolvedReportPath
$logDirectory = Join-Path $reportDirectory "casev2-soak-logs"

New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null

$difficulties = @(
    "Rookie",
    "Detective",
    "Detective2",
    "Sergeant",
    "Lieutenant",
    "Captain",
    "Commander"
)
$themes = @(
    "museum archive theft",
    "waterfront cold case",
    "corporate financial fraud"
)

if (Test-Path $resolvedReportPath) {
    $report = Get-Content $resolvedReportPath -Raw | ConvertFrom-Json -AsHashtable
} else {
    $report = [ordered]@{
        startedAtUtc = [DateTime]::UtcNow.ToString("O")
        updatedAtUtc = [DateTime]::UtcNow.ToString("O")
        completedAtUtc = $null
        language = $Language
        casesPerDifficulty = $CasesPerDifficulty
        cooldownSeconds = $CooldownSeconds
        maxAttempts = $MaxAttempts
        runs = @()
    }
}

function Save-Report {
    $report.updatedAtUtc = [DateTime]::UtcNow.ToString("O")
    $report | ConvertTo-Json -Depth 20 | Set-Content -Path $resolvedReportPath -Encoding utf8
}

function Get-Run([string]$difficulty, [int]$seed) {
    return $report.runs | Where-Object {
        $_.difficulty -eq $difficulty -and [int]$_.seed -eq $seed
    } | Select-Object -First 1
}

function Get-ArtifactMetrics([string]$outputPath) {
    $caseDirectory = Split-Path -Parent $outputPath
    $case = Get-Content $outputPath -Raw | ConvertFrom-Json
    $graphPath = Join-Path $caseDirectory "case.graph.private.json"
    $solverPath = Join-Path $caseDirectory "solver-trace.private.json"
    $graph = if (Test-Path $graphPath) {
        Get-Content $graphPath -Raw | ConvertFrom-Json
    } else {
        $null
    }
    $solver = if (Test-Path $solverPath) {
        Get-Content $solverPath -Raw | ConvertFrom-Json
    } else {
        $null
    }

    return [ordered]@{
        suspects = @($case.suspects).Count
        assets = @($case.assets).Count
        emails = @($case.emails).Count
        images = @(Get-ChildItem $caseDirectory -Recurse -File -Filter "*.png").Count
        forensicOutcomes = @($case.forensicOutcomes).Count
        facts = if ($null -ne $graph) { @($graph.facts).Count } else { 0 }
        observations = if ($null -ne $graph) { @($graph.observations).Count } else { 0 }
        decoys = if ($null -ne $graph) { @($graph.decoyArcs).Count } else { 0 }
        solverCorrect = $solver.correct
        solverScore = $solver.score
    }
}

try {
    foreach ($difficultyIndex in 0..($difficulties.Count - 1)) {
        $difficulty = $difficulties[$difficultyIndex]
        foreach ($caseIndex in 1..$CasesPerDifficulty) {
            $seed = (($difficultyIndex + 1) * 1000) + 100 + $caseIndex
            $theme = $themes[($caseIndex - 1) % $themes.Count]
            $existingRun = Get-Run $difficulty $seed
            if ($null -ne $existingRun -and $existingRun.status -eq "passed") {
                Write-Host "Skipping completed case: difficulty=$difficulty seed=$seed"
                continue
            }

            if ($null -eq $existingRun) {
                $existingRun = [ordered]@{
                    difficulty = $difficulty
                    seed = $seed
                    theme = $theme
                    status = "pending"
                    attempts = @()
                    successfulAttempt = $null
                    durationSeconds = $null
                    inputTokens = $null
                    outputTokens = $null
                    metrics = $null
                    error = $null
                }
                $report.runs += $existingRun
            }

            $runStarted = [DateTime]::UtcNow
            $existingRun.status = "running"
            $existingRun.error = $null
            Save-Report

            for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
                $caseId = "case_soak_$($difficulty.ToLowerInvariant())_${seed}_a$attempt"
                $resultPath = Join-Path $logDirectory "$caseId-result.json"
                $logPath = Join-Path $logDirectory "$caseId.log"
                Remove-Item -LiteralPath $resultPath -ErrorAction SilentlyContinue
                $attemptStarted = [DateTime]::UtcNow

                $env:CASEZERO_RUN_REAL_GENERATION = "1"
                $env:CASEZERO_DIFFICULTY = $difficulty
                $env:CASEZERO_THEME = $theme
                $env:CASEZERO_SEED = $seed.ToString()
                $env:CASEZERO_LANGUAGE = $Language
                $env:CASEZERO_CASE_ID = $caseId
                $env:CASEZERO_DIRECT_RESULT_FILE = $resultPath

                try {
                    Write-Host "Generating difficulty=$difficulty seed=$seed attempt=$attempt"
                    & dotnet test $project --no-restore --nologo --filter "FullyQualifiedName~DirectCaseGenerationIntegrationTests.DirectHarness_GeneratesAndValidatesRealCase" 2>&1 |
                        Tee-Object -FilePath $logPath
                    $exitCode = $LASTEXITCODE
                } finally {
                    Remove-Item Env:\CASEZERO_RUN_REAL_GENERATION -ErrorAction SilentlyContinue
                    Remove-Item Env:\CASEZERO_DIFFICULTY -ErrorAction SilentlyContinue
                    Remove-Item Env:\CASEZERO_THEME -ErrorAction SilentlyContinue
                    Remove-Item Env:\CASEZERO_SEED -ErrorAction SilentlyContinue
                    Remove-Item Env:\CASEZERO_LANGUAGE -ErrorAction SilentlyContinue
                    Remove-Item Env:\CASEZERO_CASE_ID -ErrorAction SilentlyContinue
                    Remove-Item Env:\CASEZERO_DIRECT_RESULT_FILE -ErrorAction SilentlyContinue
                }

                $attemptDuration = [Math]::Round(([DateTime]::UtcNow - $attemptStarted).TotalSeconds, 1)
                $logText = Get-Content $logPath -Raw
                $attemptRecord = [ordered]@{
                    attempt = $attempt
                    startedAtUtc = $attemptStarted.ToString("O")
                    durationSeconds = $attemptDuration
                    exitCode = $exitCode
                    rateLimited = $logText -match "(?i)\b429\b|rate.?limit|too many requests"
                    logPath = $logPath
                }
                $existingRun.attempts += $attemptRecord

                if ($exitCode -eq 0 -and (Test-Path $resultPath)) {
                    $result = Get-Content $resultPath -Raw | ConvertFrom-Json
                    if ($result.finalValidationPassed -and $result.solverSucceeded -and $result.validationErrorCount -eq 0) {
                        $existingRun.status = "passed"
                        $existingRun.successfulAttempt = $attempt
                        $existingRun.durationSeconds = [Math]::Round(([DateTime]::UtcNow - $runStarted).TotalSeconds, 1)
                        $existingRun.inputTokens = $result.inputTokens
                        $existingRun.outputTokens = $result.outputTokens
                        $existingRun.metrics = Get-ArtifactMetrics $result.outputPath
                        $existingRun.error = $null
                        Save-Report

                        if (-not $KeepCases) {
                            $caseDirectory = Split-Path -Parent $result.outputPath
                            if ((Split-Path -Leaf $caseDirectory) -eq $caseId) {
                                Remove-Item -LiteralPath $caseDirectory -Recurse -Force
                            }
                        }
                        break
                    }
                }

                $existingRun.error = ($logText -split "\r?\n" | Select-Object -Last 30) -join [Environment]::NewLine
                Save-Report
                if ($attempt -lt $MaxAttempts) {
                    $backoff = if ($attemptRecord.rateLimited) {
                        300 * [Math]::Pow(2, $attempt - 1)
                    } else {
                        60 * [Math]::Pow(2, $attempt - 1)
                    }
                    Write-Host "Attempt failed; retrying in $backoff second(s)."
                    Start-Sleep -Seconds $backoff
                }
            }

            if ($existingRun.status -ne "passed") {
                $existingRun.status = "failed"
                $existingRun.durationSeconds = [Math]::Round(([DateTime]::UtcNow - $runStarted).TotalSeconds, 1)
                Save-Report
            }

            Start-Sleep -Seconds $CooldownSeconds
        }
    }
} finally {
    $finishedRuns = @($report.runs | Where-Object { $_.status -in @("passed", "failed") })
    $report.summary = [ordered]@{
        total = @($report.runs).Count
        passed = @($finishedRuns | Where-Object status -eq "passed").Count
        failed = @($finishedRuns | Where-Object status -eq "failed").Count
        rateLimitedAttempts = @($report.runs.attempts | Where-Object rateLimited).Count
        totalInputTokens = ($finishedRuns | Measure-Object inputTokens -Sum).Sum
        totalOutputTokens = ($finishedRuns | Measure-Object outputTokens -Sum).Sum
    }
    if (@($finishedRuns).Count -eq ($difficulties.Count * $CasesPerDifficulty)) {
        $report.completedAtUtc = [DateTime]::UtcNow.ToString("O")
    }
    Save-Report
}
