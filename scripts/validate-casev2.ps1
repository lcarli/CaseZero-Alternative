param(
    [ValidateSet("Goldens", "Artifact", "Generate", "DirectGenerate", "Batch", "Full")]
    [string]$Mode = "Goldens",
    [string]$CaseDirectory,
    [string]$BaseUrl = "http://localhost:7071",
    [ValidateSet("Rookie", "Detective", "Detective2", "Sergeant", "Lieutenant", "Captain", "Commander")]
    [string]$Difficulty = "Rookie",
    [string]$Theme = "office theft",
    [int]$Seed = 1,
    [ValidateSet("en-US", "pt-BR", "es-ES", "fr-FR")]
    [string]$Language = "en-US",
    [string]$CaseId,
    [int[]]$Seeds = @(1, 2, 3),
    [string[]]$Themes = @("office theft", "financial fraud"),
    [string]$BatchReportPath = "batch-report.json"
)

$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repo "functions\CaseGen.Functions.Tests\CaseGen.Functions.Tests.csproj"

function Invoke-TestFilter([string]$filter) {
    & dotnet test $project --no-restore --nologo --filter $filter
    if ($LASTEXITCODE -ne 0) { throw "Validation tests failed." }
}

function Validate-Artifact([string]$directory) {
    if (-not (Test-Path $directory)) { throw "Case directory not found: $directory" }
    $env:CASEZERO_CASEV2_ARTIFACT_DIR = (Resolve-Path $directory).Path
    try {
        Invoke-TestFilter "FullyQualifiedName~GoldenArtifactValidationTests.ValidateArtifactDirectory"
    }

    finally {
        Remove-Item Env:\CASEZERO_CASEV2_ARTIFACT_DIR -ErrorAction SilentlyContinue
    }
}

function Invoke-Generation([string]$difficulty, [string]$theme, [int]$seed) {
    $body = @{
        difficulty = $difficulty
        requiredRank = $difficulty
        theme = $theme
        seed = $seed
        language = $Language
        writeToDisk = $true
    } | ConvertTo-Json
    $start = Invoke-RestMethod -Method Post -Uri "$($BaseUrl.TrimEnd('/'))/api/cases/v2/generate" -ContentType "application/json" -Body $body
    $statusUrl = if ($start.statusUri -match "^https?://") { $start.statusUri } else { "$($BaseUrl.TrimEnd('/'))$($start.statusUri)" }
    do {
        Start-Sleep -Seconds 5
        $status = Invoke-RestMethod -Method Get -Uri $statusUrl
        Write-Host "difficulty=$difficulty theme=$theme seed=$seed status=$($status.status) phase=$($status.currentPhase)"
    } while ($status.status -notin @("done", "failed"))
    if ($status.status -eq "failed") { throw "Generation failed: $($status.error)" }
    return $status.result
}

switch ($Mode) {
    "Goldens" {
        Invoke-TestFilter "FullyQualifiedName~GoldenArtifactValidationTests.PersistedGoldenArtifacts"
    }
    "Artifact" {
        if ([string]::IsNullOrWhiteSpace($CaseDirectory)) { throw "-CaseDirectory is required." }
        Validate-Artifact $CaseDirectory
    }
    "Generate" {
        $result = Invoke-Generation $Difficulty $Theme $Seed
        if ($result.validationErrorsCount -gt 0) {
            throw "Generation was blocked by $($result.validationErrorsCount) validation error(s): $($result.errorMessage)"
        }
        if ([string]::IsNullOrWhiteSpace($result.outputPath)) {
            throw "Generation completed without a persisted output path."
        }
        Validate-Artifact (Split-Path -Parent $result.outputPath)
    }
    "DirectGenerate" {
        $resultFile = Join-Path $repo "direct-generation-result.json"
        Remove-Item $resultFile -ErrorAction SilentlyContinue
        $env:CASEZERO_RUN_REAL_GENERATION = "1"
        $env:CASEZERO_DIFFICULTY = $Difficulty
        $env:CASEZERO_THEME = $Theme
        $env:CASEZERO_SEED = $Seed.ToString()
        $env:CASEZERO_LANGUAGE = $Language
        $env:CASEZERO_CASE_ID = if ([string]::IsNullOrWhiteSpace($CaseId)) {
            "case_direct_$($Difficulty.ToLowerInvariant())_$Seed"
        } else {
            $CaseId
        }
        $env:CASEZERO_DIRECT_RESULT_FILE = $resultFile
        try {
            Invoke-TestFilter "FullyQualifiedName~DirectCaseGenerationIntegrationTests.DirectHarness_GeneratesAndValidatesRealCase"
            if (-not (Test-Path $resultFile)) {
                throw "Direct generation completed without a result file."
            }
            $result = Get-Content $resultFile -Raw | ConvertFrom-Json
            if ($result.validationErrorCount -gt 0 -or -not $result.finalValidationPassed) {
                throw "Direct generation failed deterministic validation."
            }
            Write-Host "DIRECT_GENERATION_RESULT_PATH=$($result.outputPath)"
            Validate-Artifact (Split-Path -Parent $result.outputPath)
        }
        finally {
            Remove-Item Env:\CASEZERO_RUN_REAL_GENERATION -ErrorAction SilentlyContinue
            Remove-Item Env:\CASEZERO_DIFFICULTY -ErrorAction SilentlyContinue
            Remove-Item Env:\CASEZERO_THEME -ErrorAction SilentlyContinue
            Remove-Item Env:\CASEZERO_SEED -ErrorAction SilentlyContinue
            Remove-Item Env:\CASEZERO_LANGUAGE -ErrorAction SilentlyContinue
            Remove-Item Env:\CASEZERO_CASE_ID -ErrorAction SilentlyContinue
            Remove-Item Env:\CASEZERO_DIRECT_RESULT_FILE -ErrorAction SilentlyContinue
            Remove-Item $resultFile -ErrorAction SilentlyContinue
        }
    }
    "Batch" {
        $results = @()
        foreach ($themeItem in $Themes) {
            foreach ($seedItem in $Seeds) {
                $results += Invoke-Generation $Difficulty $themeItem $seedItem
            }
        }
        $count = $results.Count
        $categories = @{}
        $layouts = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
        foreach ($result in $results) {
            if ($null -ne $result.specialistFindingsByCategory) {
                foreach ($property in $result.specialistFindingsByCategory.PSObject.Properties) {
                    if (-not $categories.ContainsKey($property.Name)) { $categories[$property.Name] = 0 }
                    $categories[$property.Name] += [int]$property.Value
                }
            }
            foreach ($layout in $result.evidenceLayouts) { [void]$layouts.Add([string]$layout) }
        }
        $report = [ordered]@{
            difficulty = $Difficulty
            totalCases = $count
            graphValidationPassRate = if ($count) { ($results.Where({$_.graphValidationPassed}).Count / $count) } else { 0 }
            firstPassSuccessRate = if ($count) { ($results.Where({$_.firstPassSuccess}).Count / $count) } else { 0 }
            averageRepairOperations = if ($count) { ($results | Measure-Object repairOperationCount -Average).Average } else { 0 }
            repairPlateauRate = if ($count) { ($results.Where({$_.repairPlateauCount -gt 0}).Count / $count) } else { 0 }
            solverSuccessRate = if ($count) { ($results.Where({$_.solverSucceeded}).Count / $count) } else { 0 }
            specialistFindingsByCategory = $categories
            averageLatencyMs = if ($count) { (($results | ForEach-Object { ($_.stageLatencyMs.PSObject.Properties.Value | Measure-Object -Sum).Sum }) | Measure-Object -Average).Average } else { 0 }
            totalInputTokens = ($results | Measure-Object inputTokens -Sum).Sum
            totalOutputTokens = ($results | Measure-Object outputTokens -Sum).Sum
            evidenceLayoutDiversity = $layouts.Count
            cases = $results
        }
        $resolvedReport = Join-Path $repo $BatchReportPath
        $report | ConvertTo-Json -Depth 20 | Set-Content -Path $resolvedReport -Encoding utf8
        Write-Host "Batch report: $resolvedReport"
        $report | ConvertTo-Json -Depth 6
    }
    "Full" {
        Invoke-TestFilter "FullyQualifiedName~CaseGen.Functions.Tests.CaseV2"
    }
}
