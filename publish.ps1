# =====================================================================
# publish.ps1 - Build and publish NotificationAPI for production
#
# Usage:
#   .\publish.ps1                          # Publish to .\publish\
#   .\publish.ps1 -OutputDir D:\deploy     # Custom output path
#   .\publish.ps1 -Zip                     # Zip after publish
#   .\publish.ps1 -Clean                   # Clean output before
# =====================================================================

param(
    [string]$OutputDir   = ".\publish",
    [string]$Configuration = "Release",
    [string]$Runtime      = "win-x64",
    [string]$Framework    = "net6.0",
    [switch]$Zip,
    [switch]$Clean,
    [switch]$SelfContained
)

$ErrorActionPreference = "Stop"
$startTime = Get-Date

$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectPath = Join-Path $ScriptRoot "NotificationAPI\NotificationAPI.csproj"
$OutputDir = if ([System.IO.Path]::IsPathRooted($OutputDir)) { $OutputDir } else { Join-Path $ScriptRoot $OutputDir }

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host " NotificationAPI Publish" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Project       : $ProjectPath"
Write-Host "Output        : $OutputDir"
Write-Host "Configuration : $Configuration"
Write-Host "Runtime       : $Runtime"
Write-Host "Framework     : $Framework"
Write-Host "SelfContained : $SelfContained"
Write-Host ""

# 1. Check prerequisites
if (-not (Test-Path $ProjectPath)) {
    Write-Host "ERROR: csproj not found at $ProjectPath" -ForegroundColor Red
    exit 1
}

$dllPath = Join-Path $ScriptRoot "DLL\XMUtility.dll"
if (-not (Test-Path $dllPath)) {
    Write-Host "WARNING: $dllPath does not exist - csproj HintPath is ..\DLL\, build will fail" -ForegroundColor Yellow
}

# 2. Git state
$commit = ""
try {
    $branch = (git -C $ScriptRoot rev-parse --abbrev-ref HEAD 2>$null)
    $commit = (git -C $ScriptRoot rev-parse --short HEAD 2>$null)
    $dirty  = (git -C $ScriptRoot status --porcelain 2>$null)
    Write-Host "Git branch    : $branch"
    Write-Host "Git commit    : $commit"
    if ($dirty) {
        Write-Host "Git status    : DIRTY (uncommitted changes - publishing uncommitted code!)" -ForegroundColor Yellow
    } else {
        Write-Host "Git status    : clean" -ForegroundColor Green
    }
    Write-Host ""
} catch {
    Write-Host "Git status    : (not a git repo or git not installed)" -ForegroundColor Yellow
    Write-Host ""
}

# 3. Clean
if ($Clean -and (Test-Path $OutputDir)) {
    Write-Host "[1/4] Clean output: $OutputDir" -ForegroundColor Cyan
    Remove-Item -Recurse -Force $OutputDir
    Write-Host "      done"
    Write-Host ""
}

# 4. Restore
Write-Host "[2/4] dotnet restore" -ForegroundColor Cyan
& dotnet restore $ProjectPath --nologo
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Restore failed" -ForegroundColor Red
    exit 1
}
Write-Host ""

# 5. Publish
$publishArgs = @(
    "publish", $ProjectPath,
    "-c", $Configuration,
    "-f", $Framework,
    "-r", $Runtime,
    "-o", $OutputDir,
    "--nologo",
    "/p:UseAppHost=true"
)
if ($SelfContained) {
    $publishArgs += "--self-contained", "true"
} else {
    $publishArgs += "--no-self-contained"
}

Write-Host "[3/4] dotnet publish" -ForegroundColor Cyan
& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Publish failed" -ForegroundColor Red
    exit 1
}
Write-Host ""

# 6. Verify output
Write-Host "[4/4] Verify output" -ForegroundColor Cyan
$requiredFiles = @(
    "NotificationAPI.dll",
    "NotificationAPI.exe",
    "appsettings.json",
    "XMUtility.dll",
    "XUtil.dll",
    "Data\users.json"
)
$missing = @()
foreach ($f in $requiredFiles) {
    $p = Join-Path $OutputDir $f
    if (-not (Test-Path $p)) {
        $missing += $f
    }
}

if ($missing.Count -gt 0) {
    Write-Host "WARNING: missing files in output:" -ForegroundColor Yellow
    $missing | ForEach-Object { Write-Host "         - $_" -ForegroundColor Yellow }
    Write-Host "         (appsettings.json / users.json are .gitignored - copy them manually on prod)" -ForegroundColor Yellow
} else {
    Write-Host "      all required files present" -ForegroundColor Green
}

$sizeMb = "{0:N1}" -f ((Get-ChildItem -Path $OutputDir -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB)
Write-Host "      Output size: $sizeMb MB"
Write-Host ""

# 7. Zip
if ($Zip) {
    Write-Host "[Zip]" -ForegroundColor Cyan
    $zipName = "NotificationAPI_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
    if ($commit) { $zipName += "_$commit" }
    $zipPath = Join-Path $ScriptRoot "$zipName.zip"
    if (Test-Path $zipPath) { Remove-Item $zipPath }
    Compress-Archive -Path "$OutputDir\*" -DestinationPath $zipPath -CompressionLevel Optimal
    $zipMb = "{0:N1}" -f ((Get-Item $zipPath).Length / 1MB)
    Write-Host "      $zipPath ($zipMb MB)" -ForegroundColor Green
    Write-Host ""
}

# 8. Summary
$elapsed = (Get-Date) - $startTime
Write-Host "============================================" -ForegroundColor Green
Write-Host " DONE in $([int]$elapsed.TotalSeconds)s" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Green
Write-Host "Output: $OutputDir"
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  1. Copy $OutputDir to prod server"
Write-Host "  2. Verify appsettings.json on prod has correct Couchbase config"
Write-Host "  3. Verify Data\users.json on prod has admin user"
Write-Host "  4. Restart IIS app pool or Windows Service"
Write-Host "  5. Tail logs\notification-api-<date>.log to confirm bootstrap success"
Write-Host ""
