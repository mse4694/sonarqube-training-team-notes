# Windows PowerShell twin of scripts/zap-scan (Workshop 12): run a ZAP baseline (spider + passive
# scan, no active attack) against the running Team Notes web service and keep zap.json / zap.html in
# a new folder .lab-workspaces\security-tools.XXXXXX\zap (path saved to .lab-workspaces\zap.env.ps1).
#   .\scripts\zap-scan.ps1
# Run it from the Team Notes checkout with the app already started (docker compose up -d).
# Works on Windows PowerShell 5.1 and PowerShell 7.
# Continue, not Stop: Windows PowerShell 5.1 hosts (ISE, VS Code) turn native stderr into errors.
$ErrorActionPreference = 'Continue'

function Stop-Scan([string]$Message) {
  [Console]::Error.WriteLine("Stop: $Message")
  exit 1
}

$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..') -ErrorAction Stop).ProviderPath
$workspaces = Join-Path $repoRoot '.lab-workspaces'
$labTools = Join-Path $PSScriptRoot 'lab-tools.ps1'
$target = 'http://web:3000/login'
$zapImage = 'ghcr.io/zaproxy/zaproxy:stable'
$onWindows = ($PSVersionTable.PSEdition -eq 'Desktop') -or $IsWindows
Set-Location -LiteralPath $repoRoot

# lab-tools shares the api container's network, so it reaches the web service by name.
$loginHttp = & $labTools curl --connect-timeout 5 --max-time 15 -sS -o /dev/null -w '%{http_code}' $target
if ($LASTEXITCODE -ne 0 -or -not $loginHttp) { $loginHttp = 'connection_error' }
else { $loginHttp = ([string]($loginHttp | Select-Object -Last 1)).Trim() }
Write-Output "login HTTP $loginHttp ($target)"
if ($loginHttp -ne '200') {
  Stop-Scan 'start the existing Team Notes app (docker compose up -d) until login returns HTTP 200, then run this again'
}

& docker pull -q $zapImage | Out-Null
if ($LASTEXITCODE -ne 0) { Stop-Scan "could not pull $zapImage" }
$digest = ([string](& docker image inspect --format '{{index .RepoDigests 0}}' $zapImage | Select-Object -First 1)).Trim()
Write-Output "ZAP image: $digest"
$composeFile = Join-Path $repoRoot 'docker-compose.yml'
$webId = ([string](& docker compose --project-directory $repoRoot -f $composeFile ps -q web | Select-Object -First 1)).Trim()
if (-not $webId) { Stop-Scan 'the web service is not running' }
$webNet = ([string](& docker inspect --format '{{range $name, $cfg := .NetworkSettings.Networks}}{{println $name}}{{end}}' $webId | Select-Object -First 1)).Trim()

New-Item -ItemType Directory -Force -Path $workspaces | Out-Null
$suffix = -join ((1..6) | ForEach-Object { 'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789'[(Get-Random -Maximum 62)] })
$toolsDir = Join-Path $workspaces "security-tools.$suffix"
$zapDir = Join-Path $toolsDir 'zap'
New-Item -ItemType Directory -Path $zapDir | Out-Null
# The ZAP container writes as its own user; Docker Desktop on Windows already lets it write.
if (-not $onWindows) { & chmod 777 $zapDir }
Set-Content -LiteralPath (Join-Path $workspaces 'zap.env.ps1') -Value ('$TOOLS_DIR = ''' + ($toolsDir -replace "'", "''") + '''') -Encoding ASCII
Write-Output "Target: $target (network $webNet, no login session)"

# Only the report folder is mounted: no Docker socket, credentials or source. --mount keeps the
# drive colon of C:\... out of the -v source:target:mode parsing.
& docker run --rm --network $webNet --mount "type=bind,source=$zapDir,target=/zap/wrk" `
  $zapImage zap-baseline.py -t $target -m 1 -T 5 -J zap.json -r zap.html
$zapExit = $LASTEXITCODE

switch ($zapExit) {
  0 { $meaning = 'no WARN/FAIL in this scope' }
  1 { $meaning = 'at least one FAIL' }
  2 { $meaning = 'WARN only, no FAIL (a scan result, not an error)' }
  3 { $meaning = 'ZAP could not complete the scan' }
  default { $meaning = 'Docker could not start ZAP' }
}
Write-Output "ZAP exit=$($zapExit): $meaning"
Write-Output "Reports: $(Join-Path $zapDir 'zap.json') and zap.html (saved in .lab-workspaces\zap.env.ps1)"
$report = Join-Path $zapDir 'zap.json'
if ($zapExit -gt 2 -or -not (Test-Path -LiteralPath $report) -or (Get-Item -LiteralPath $report).Length -eq 0) {
  Stop-Scan 'no usable report; fix the setup and run again (do not read this as zero findings)'
}
exit 0
