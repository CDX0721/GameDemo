param(
    [string]$ProjectRoot = "E:\QQ Files\游戏\GameDemo"
)

$source = Join-Path $ProjectRoot "Assets\ConfigSource\策划案.xlsx"
$tempOutput = Join-Path $ProjectRoot "Temp\ConfigExportValidation\Output"
$logDir = Join-Path $ProjectRoot "TestLogs\ConfigExport"
$exportLogPath = Join-Path $logDir "planning_xlsx_export.log"
$validateLogPath = Join-Path $logDir "planning_xlsx_validation.log"
$manifestPath = Join-Path $logDir "planning_xlsx_export_manifest.json"
$exportScript = Join-Path $ProjectRoot "Tools\ConfigExport\export_planning_xlsx.py"
$validateScript = Join-Path $ProjectRoot "Tools\ConfigExport\validate_planning_export.py"

New-Item -ItemType Directory -Force -Path $tempOutput | Out-Null
New-Item -ItemType Directory -Force -Path $logDir | Out-Null

python $exportScript --source $source --output $tempOutput --log $exportLogPath --manifest $manifestPath
$exportExitCode = $LASTEXITCODE

if ($exportExitCode -eq 0) {
    python $validateScript --output $tempOutput --log $validateLogPath
    $validateExitCode = $LASTEXITCODE
}
else {
    $validateExitCode = 99
}

if (Test-Path $manifestPath) {
    Remove-Item -LiteralPath $manifestPath -Force
}

if (Test-Path $tempOutput) {
    Remove-Item -LiteralPath $tempOutput -Recurse -Force
}

if ($exportExitCode -ne 0) {
    exit $exportExitCode
}

exit $validateExitCode
