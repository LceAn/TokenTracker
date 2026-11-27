# build-fork-release.ps1  --  build the community fork's Windows release.
#
#   powershell -ExecutionPolicy Bypass -File TokenTrackerWin\scripts\build-fork-release.ps1
#   ... -SkipTests     # skip the test suites, when they were already run on this commit
#
# Mirrors .github/workflows/release-windows.yml, on the machine instead of CI,
# and adds the fork's identity: the version comes from package.json plus the
# revision in FORK_REVISION ("1.5.28" + "1" -> "1.5.28-fork.1").
#
# Output, in TokenTrackerWin\installer\Output\<fork version>\:
#   TokenTracker-Setup.exe    the per-user installer
#   TokenTracker-win-x64.zip  the same build as a portable zip
#   SHA256SUMS                sha256sum-format checksums of both
# The names are the ones UpdateReleaseClient looks for in a release.
#
# The script only builds. Publishing (tag + GitHub release) is a separate step.
# It refuses to run on a working tree with uncommitted changes, because the
# commit hash is embedded in the build and names the source it came from.

param(
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$ScriptDir  = Split-Path -Parent $MyInvocation.MyCommand.Path
$WinProjDir = Split-Path -Parent $ScriptDir
$RepoRoot   = Split-Path -Parent $WinProjDir
$ForkRepo   = 'LceAn/TokenTracker'

function Step([string]$Label) {
    Write-Host ''
    Write-Host "==> $Label" -ForegroundColor Cyan
}

function Run([string]$Command, [string[]]$Arguments) {
    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Command $($Arguments -join ' ') failed with exit code $LASTEXITCODE"
    }
}

# A zip whose entry names use forward slashes, unlike Compress-Archive in
# Windows PowerShell 5.1, so every unzipper sees the folder structure.
function New-Zip([string]$SourceDir, [string]$ZipPath) {
    if (Test-Path -LiteralPath $ZipPath) { Remove-Item -LiteralPath $ZipPath -Force }
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $resolved = (Resolve-Path -LiteralPath $SourceDir).Path.TrimEnd('\')
    $prefixLength = $resolved.Length + 1
    $zip = [System.IO.Compression.ZipFile]::Open($ZipPath, 'Create')
    try {
        foreach ($file in [System.IO.Directory]::EnumerateFiles($resolved, '*', 'AllDirectories')) {
            $entryName = $file.Substring($prefixLength).Replace('\', '/')
            [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $zip, $file, $entryName, [System.IO.Compression.CompressionLevel]::Optimal)
        }
    } finally {
        $zip.Dispose()
    }
}

Push-Location $RepoRoot
$savedEnvironment = @{}
try {
    # ── identity ──────────────────────────────────────────────────────
    Step 'Version'
    $baseVersion = (Get-Content package.json -Raw | ConvertFrom-Json).version
    if ($baseVersion -notmatch '^\d+\.\d+\.\d+$') { throw "package.json has no stable x.y.z version: $baseVersion" }
    if (-not (Test-Path FORK_REVISION)) { throw 'FORK_REVISION is missing from the repository root.' }
    $forkRevision = (Get-Content FORK_REVISION -Raw).Trim()
    if ($forkRevision -notmatch '^\d+$') { throw "FORK_REVISION must be a number, found '$forkRevision'." }
    $forkVersion = "$baseVersion-fork.$forkRevision"
    $forkLabel = "-fork.$forkRevision"
    $outDir = Join-Path $WinProjDir "installer\Output\$forkVersion"

    $sourceSha = & git rev-parse HEAD
    if ($LASTEXITCODE -ne 0) { throw 'git rev-parse HEAD failed.' }
    $dirty = & git status --porcelain
    if ($LASTEXITCODE -ne 0) { throw 'git status failed.' }
    if ($dirty) {
        throw "Working tree has uncommitted changes, so the build could not be traced to one commit. Commit or stash them first:`n$($dirty -join "`n")"
    }
    Write-Host "Fork version : $forkVersion (upstream $baseVersion, revision $forkRevision)"
    Write-Host "Source commit: $sourceSha"

    # ── tests ─────────────────────────────────────────────────────────
    if (-not $SkipTests) {
        Step 'Validators'
        foreach ($validator in 'validate:versions', 'validate:copy', 'validate:locale', 'validate:ui-hardcode', 'validate:guardrails') {
            Run npm @('run', '-s', $validator)
        }
        Step 'Windows app unit tests'
        Run dotnet @('test', 'TokenTrackerWin.Tests/TokenTrackerWin.Tests.csproj', '-nologo', '-v', 'q')
        Step 'Dashboard tests'
        Run npm @('--prefix', 'dashboard', 'run', '-s', 'test')
    }

    # ── dashboard ─────────────────────────────────────────────────────
    Step "Dashboard ($forkVersion)"
    $savedEnvironment['TOKENTRACKER_BUILD_PET'] = $env:TOKENTRACKER_BUILD_PET
    $savedEnvironment['VITE_APP_VERSION'] = $env:VITE_APP_VERSION
    $savedEnvironment['VITE_RELEASE_REPO'] = $env:VITE_RELEASE_REPO
    # The pet and quota pages ship only in the desktop apps' builds.
    $env:TOKENTRACKER_BUILD_PET = '1'
    # Shown as the version everywhere, and what the browser-mode update check compares.
    $env:VITE_APP_VERSION = $forkVersion
    # Where the browser-mode "check for updates" looks for releases.
    $env:VITE_RELEASE_REPO = $ForkRepo
    Run npm @('run', '-s', 'dashboard:build')

    $bundles = Get-ChildItem dashboard/dist/assets -Filter *.js -File | Select-Object -ExpandProperty FullName
    if (-not (Select-String -Path $bundles -SimpleMatch -Pattern $forkVersion -List)) {
        throw "The dashboard bundle does not carry the version '$forkVersion'."
    }
    # The browser-mode update check builds its request URL from the repository
    # named at build time, so read the repository from the very file that makes
    # that request (looking for the name anywhere would be satisfied by its
    # mere mention in the UI copy).
    $updateChunk = Get-ChildItem dashboard/dist/assets -Filter *.js -File | Where-Object {
        [System.IO.File]::ReadAllText($_.FullName) -match 'api\.github\.com/repos/.{1,16}?/releases/latest'
    } | Select-Object -First 1
    if (-not $updateChunk) { throw 'The dashboard bundle has no browser-mode update check to verify.' }
    if (-not ([System.IO.File]::ReadAllText($updateChunk.FullName)).Contains('"' + $ForkRepo + '"')) {
        throw "The browser-mode update check in $($updateChunk.Name) does not look in '$ForkRepo'."
    }
    Write-Host "Browser-mode update check follows: $ForkRepo"

    # ── bundled Node runtime + CLI ────────────────────────────────────
    Step 'Embedded server (Node runtime + CLI + dashboard)'
    & (Join-Path $ScriptDir 'bundle-node.ps1')

    # ── self-contained app ────────────────────────────────────────────
    Step 'Publish (self-contained win-x64)'
    if (Test-Path TokenTrackerWin/publish) { Remove-Item -Recurse -Force TokenTrackerWin/publish }
    Run dotnet @('publish', 'TokenTrackerWin/TokenTrackerWin.csproj', '-c', 'Release', '-r', 'win-x64',
        '--self-contained', 'true', '-o', 'TokenTrackerWin/publish', '-nologo', '-v', 'q')
    # dotnet publish does not include EmbeddedServer (it lives outside the
    # .csproj); the app resolves it from next to its exe.
    Copy-Item TokenTrackerWin/EmbeddedServer TokenTrackerWin/publish/EmbeddedServer -Recurse -Force

    Step 'Package verification'
    Run node @('scripts/verify-windows-package.cjs', 'TokenTrackerWin/publish', (Join-Path $env:TEMP "tokentracker-win-package-$forkVersion.json"))
    $productVersion = (Get-Item TokenTrackerWin/publish/TokenTracker.dll).VersionInfo.ProductVersion
    if (-not $productVersion.StartsWith($forkVersion, [StringComparison]::Ordinal)) {
        throw "The published app reports '$productVersion', expected it to start with '$forkVersion'."
    }
    Write-Host "App version resource: $productVersion"

    # ── release assets ────────────────────────────────────────────────
    Step 'Portable zip'
    if (Test-Path $outDir) { Remove-Item -Recurse -Force $outDir }
    New-Item -ItemType Directory -Force -Path $outDir | Out-Null
    $zipPath = Join-Path $outDir 'TokenTracker-win-x64.zip'
    New-Zip 'TokenTrackerWin/publish' $zipPath

    Step 'Installer (Inno Setup)'
    $iscc = @(
        $(if ($env:INNO_SETUP_DIR) { Join-Path $env:INNO_SETUP_DIR 'ISCC.exe' }),
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
    ) | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -First 1
    if (-not $iscc) { throw 'ISCC.exe (Inno Setup 6) was not found. Install Inno Setup or set INNO_SETUP_DIR.' }
    Push-Location $WinProjDir
    try {
        # AppVersion stays numeric for Windows; the fork label only tags the file name.
        Run $iscc @("/DMyAppVersion=$baseVersion", "/DForkLabel=$forkLabel", 'installer\TokenTracker.iss')
    } finally {
        Pop-Location
    }
    $setupBuilt = Join-Path $WinProjDir "installer\Output\TokenTracker-Setup-v$baseVersion$forkLabel.exe"
    if (-not (Test-Path $setupBuilt)) { throw "The installer was not produced at $setupBuilt." }
    $setupPath = Join-Path $outDir 'TokenTracker-Setup.exe'
    Copy-Item $setupBuilt $setupPath -Force

    Step 'Checksums'
    $sums = Get-ChildItem $outDir -File | Where-Object { $_.Name -ne 'SHA256SUMS' } | Sort-Object Name | ForEach-Object {
        $hash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        "$hash  $($_.Name)"
    }
    [System.IO.File]::WriteAllText((Join-Path $outDir 'SHA256SUMS'), (($sums -join "`n") + "`n"), (New-Object System.Text.UTF8Encoding($false)))

    # ── summary ───────────────────────────────────────────────────────
    Step "Built $forkVersion"
    Write-Host "Release tag: v$forkVersion"
    Write-Host "Assets in  : $outDir"
    Get-ChildItem $outDir -File | Sort-Object Name | ForEach-Object {
        Write-Host ("  {0,-28} {1,8:N1} MB" -f $_.Name, ($_.Length / 1MB))
    }
} finally {
    foreach ($name in $savedEnvironment.Keys) {
        Set-Item -Path "Env:$name" -Value $savedEnvironment[$name]
    }
    Pop-Location
}
