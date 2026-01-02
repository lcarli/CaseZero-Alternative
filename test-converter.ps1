# Script para testar a conversão v2-hierarchical → case.json v1.0
# Usage: ./test-converter.ps1

Write-Host "🔧 Testing case format converter..." -ForegroundColor Cyan

$caseId = "CASE-20260102-e6dcc658"
$testOutputPath = "./test-output/bundles/$caseId"

if (!(Test-Path $testOutputPath)) {
    Write-Host "❌ Test case not found at: $testOutputPath" -ForegroundColor Red
    exit 1
}

Write-Host "✅ Found test case: $caseId" -ForegroundColor Green

# Check files
Write-Host "`n📁 Files in case:" -ForegroundColor Cyan
Write-Host "  - normalized_case.json: $(Test-Path "$testOutputPath/normalized_case.json")" -ForegroundColor Yellow

$docCount = (Get-ChildItem "$testOutputPath/documents" -Filter *.json -ErrorAction SilentlyContinue).Count
Write-Host "  - documents/: $docCount files" -ForegroundColor Yellow

$mediaCount = (Get-ChildItem "$testOutputPath/media" -Filter *.json -ErrorAction SilentlyContinue).Count
Write-Host "  - media/: $mediaCount files" -ForegroundColor Yellow

Write-Host "`n✅ Converter should load:" -ForegroundColor Green
Write-Host "  1. normalized_case.json (v2-hierarchical index)" -ForegroundColor White
Write-Host "  2. All $docCount document files from /documents" -ForegroundColor White
Write-Host "  3. All $mediaCount media files from /media" -ForegroundColor White
Write-Host "  4. Extract suspects from interviews" -ForegroundColor White
Write-Host "  5. Build assets[] with visibility" -ForegroundColor White
Write-Host "  6. Generate emails[] and rules[]" -ForegroundColor White
Write-Host "  7. Output case.json v1.0 format" -ForegroundColor White

Write-Host "`n🚀 Ready to test! Run Azure Functions and generate a case to see the converter in action." -ForegroundColor Green
Write-Host "   The converter will automatically create case.json v1.0 in the bundles container." -ForegroundColor Gray
