# Windows PowerShell twin of scripts/lab-tools: run a lab command in the shared tools container.
#   .\scripts\lab-tools.ps1 <command> [arguments...]
# Run it from inside the Team Notes checkout, with the app already started (docker compose up -d).
# The checkout is mounted at the same /c/... path that Git Bash uses, so this wrapper and
# ./scripts/lab-tools in Git Bash share one lab-tools container. Works on Windows PowerShell 5.1
# and PowerShell 7. Piped input is forwarded to the command (for example JSON for python3 -c).
# Quote a literal -- ('--'): PowerShell itself removes a bare -- before the script sees it.
# Continue, not Stop: Windows PowerShell 5.1 hosts (ISE, VS Code) turn native stderr into errors.
$ErrorActionPreference = 'Continue'
$OutputEncoding = New-Object System.Text.UTF8Encoding $false  # piped input reaches the command as UTF-8

function Write-Fail([string]$Message, [int]$Code) {
  [Console]::Error.WriteLine($Message)
  exit $Code
}

$sep = [IO.Path]::DirectorySeparatorChar
$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..') -ErrorAction Stop).ProviderPath.TrimEnd($sep)
$labCwd = (Get-Location).ProviderPath.TrimEnd($sep)
if (-not ($labCwd + $sep).StartsWith($repoRoot + $sep, [StringComparison]::OrdinalIgnoreCase)) {
  Write-Fail 'Run scripts/lab-tools.ps1 from inside the Team Notes checkout.' 2
}
if ($args.Count -eq 0) {
  Write-Fail 'Usage: .\scripts\lab-tools.ps1 <command> [arguments...]' 2
}

# C:\Users\me\team-notes -> /c/Users/me/team-notes (a valid Linux path inside the container).
# PowerShell on Linux/macOS keeps the path as it is, like ./scripts/lab-tools.
function ConvertTo-ContainerPath([string]$Path) {
  if ($Path.StartsWith('/')) { return $Path }
  if ($Path -notmatch '^([A-Za-z]):\\?(.*)$') { Write-Fail "Unsupported checkout path: $Path (use a drive path such as C:\...)" 2 }
  $rest = $Matches[2] -replace '\\', '/'
  if ($rest) { return '/' + $Matches[1].ToLower() + '/' + $rest }
  return '/' + $Matches[1].ToLower()
}

# Windows PowerShell 5.1 (and 7.0-7.2) rebuild a native command line that drops or splits quotes
# inside arguments (JSON bodies, jq filters). The command and its arguments therefore travel as
# base64 JSON in LAB_ARGV, and python3 in the tools image starts them exactly as typed.
$launcher = "import os,json,base64;a=json.loads(base64.b64decode(os.environ.pop('LAB_ARGV')));os.execvp(a[0],a)"
function ConvertTo-LabArgv([object[]]$Values) {
  $json = ConvertTo-Json -InputObject @($Values | ForEach-Object { [string]$_ }) -Compress
  return [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($json))
}

# The container paths come from one conversion so the workdir matches the mount exactly
# (Linux paths are case-sensitive even though Windows paths are not).
$containerRepo = ConvertTo-ContainerPath $repoRoot
$containerCwd = $containerRepo + ($labCwd.Substring($repoRoot.Length) -replace '\\', '/')
$composeFile = Join-Path $repoRoot 'docker-compose.yml'
$compose = @('compose', '--project-directory', $repoRoot, '-f', $composeFile)
$saved = @{ LAB_REPO_DIR = $env:LAB_REPO_DIR; LAB_REPO_SOURCE = $env:LAB_REPO_SOURCE }
$env:LAB_REPO_DIR = $containerRepo
$env:LAB_REPO_SOURCE = $repoRoot -replace '\\', '/'

function Get-ComposeId([string[]]$PsArgs) {
  $id = & docker @compose ps @PsArgs
  if ($LASTEXITCODE -ne 0) { Write-Fail 'docker compose ps failed; is Docker Desktop running?' 1 }
  $first = $id | Select-Object -First 1
  if ($null -eq $first) { return '' }  # no such container yet
  return ([string]$first).Trim()
}

# Raw Go-template strings (backticks) keep this format free of double quotes on every PowerShell.
$inspectFormat = '{{println .State.Running .HostConfig.NetworkMode}}{{range .Config.Env}}{{println (print `env:` .)}}{{end}}{{range .Mounts}}{{if eq .Type `bind`}}{{println (print `bind:` .Source `|` .Destination `|` .RW)}}{{end}}{{end}}'
$script:toolsState = ''
$script:toolsCompatible = $false
function Read-ToolsContainer([string]$Id) {
  $lines = @(& docker inspect --format $inspectFormat $Id | ForEach-Object { ([string]$_).TrimEnd() })
  if ($LASTEXITCODE -ne 0) { Write-Fail 'docker inspect of the lab-tools container failed.' 1 }
  $script:toolsState = ([string]$lines[0]).Trim()
  $workspaceOk = @($lines | Where-Object { $_ -ceq "env:LAB_WORKSPACE=$containerRepo" }).Count -gt 0
  # Docker Desktop reports the bind source in its own form; the target has to be the checkout path.
  $bindOk = @($lines | Where-Object {
      $f = $_ -split '\|'
      $f.Count -eq 3 -and $f[0].StartsWith('bind:') -and $f[1] -ceq $containerRepo -and $f[2] -ceq 'true'
    }).Count -gt 0
  $script:toolsCompatible = $workspaceOk -and $bindOk
}

try {
  $apiId = Get-ComposeId @('--status', 'running', '-q', 'api')
  if (-not $apiId) { Write-Fail 'Start the existing Team Notes app with docker compose up -d before running lab tools.' 1 }
  # A running tools container can retain the old API's network namespace after
  # Compose recreates the API. Compare container identities before every exec.
  $toolsId = Get-ComposeId @('--all', '-q', 'lab-tools')
  if ($toolsId) { Read-ToolsContainer $toolsId }
  if ($script:toolsState -ne "true container:$apiId" -or -not $script:toolsCompatible) {
    # Build and startup chatter goes to stderr, never into captured or redirected command output.
    # docker images prints nothing (exit 0) for a missing image; redirecting native stderr
    # would turn it into a terminating error on Windows PowerShell 5.1.
    $image = & docker images -q team-notes-lab-tools:latest
    if (-not $image) {
      & docker @compose build --quiet lab-tools | ForEach-Object { [Console]::Error.WriteLine($_) }
      if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
    $recreate = @()
    if ($toolsId -and ($script:toolsState -ne "false container:$apiId" -or -not $script:toolsCompatible)) { $recreate = @('--force-recreate') }
    & docker @compose up -d --no-deps --no-build --pull never @recreate lab-tools | ForEach-Object { [Console]::Error.WriteLine($_) }
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    $toolsId = Get-ComposeId @('--all', '-q', 'lab-tools')
    if (-not $toolsId) { Write-Fail 'The lab-tools container did not start.' 1 }
    Read-ToolsContainer $toolsId
    $currentApi = Get-ComposeId @('--status', 'running', '-q', 'api')
    if ($currentApi -ne $apiId -or $script:toolsState -ne "true container:$apiId" -or -not $script:toolsCompatible) {
      Write-Fail 'The tools workspace or API network does not match; rerun the command after the app is ready.' 1
    }
  }
  # exec does not run an image's ENTRYPOINT. Invoke it explicitly for each command
  # so HOME, UID/GID and no-new-privileges match the mounted checkout owner.
  $execArgs = @($compose) + @('exec', '-T', '-e', ('LAB_ARGV=' + (ConvertTo-LabArgv $args)), '--workdir', $containerCwd,
    'lab-tools', '/usr/local/bin/lab-tools-entrypoint', 'python3', '-c', $launcher)
  if ($MyInvocation.ExpectingInput) { $input | & docker @execArgs } else { & docker @execArgs }
  exit $LASTEXITCODE
} finally {
  $env:LAB_REPO_DIR = $saved.LAB_REPO_DIR
  $env:LAB_REPO_SOURCE = $saved.LAB_REPO_SOURCE
}
