# Enforce Windows UTF-8 BOM + CRLF on distributable files only.
# Git applies .gitignore rules (including nested files, wildcards and negations).
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$textExtensions = @('.cs', '.csproj', '.json', '.md', '.yml', '.yaml', '.sln', '.props', '.ps1', '.sh', '.txt', '.xml')
$textNames = @('.editorconfig', '.gitattributes', '.gitignore', 'LICENSE')
$checked = 0
$failures = @()

$git = Get-Command git -ErrorAction SilentlyContinue
if ($null -eq $git) {
    throw 'Git is required to apply .gitignore rules. Install Git for Windows and retry.'
}

# An empty temporary index treats tracked and untracked files alike, so even
# a tracked file matching .gitignore is excluded from the distribution check.
# The temporary repository is only needed when validating an extracted ZIP
# (which has no .git directory); it never changes the source directory.
$temp = Join-Path ([System.IO.Path]::GetTempPath()) ('driftguard-encoding-' + [guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $temp)
try {
    $temporaryRepository = $null
    if (-not (Test-Path -LiteralPath (Join-Path $root '.git'))) {
        $temporaryRepository = Join-Path $temp 'repository'
        & $git.Source init --quiet -- $temporaryRepository | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw 'Unable to initialize temporary Git metadata for .gitignore handling.'
        }
    }

    # -z preserves special characters in file names. --exclude-per-directory
    # considers .gitignore files at all levels without excluding files only
    # because of the developer's global Git ignore settings.
    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $git.Source
    $startInfo.Arguments = '-c core.longpaths=true ls-files --others --exclude-per-directory=.gitignore -z'
    $startInfo.WorkingDirectory = $root
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.StandardOutputEncoding = [System.Text.UTF8Encoding]::new($false)
    $startInfo.StandardErrorEncoding = [System.Text.UTF8Encoding]::new($false)
    $startInfo.EnvironmentVariables['GIT_INDEX_FILE'] = (Join-Path $temp 'empty-index')
    if ($null -ne $temporaryRepository) {
        $startInfo.EnvironmentVariables['GIT_DIR'] = (Join-Path $temporaryRepository '.git')
        $startInfo.EnvironmentVariables['GIT_WORK_TREE'] = $root
    } else {
        # Do not let inherited overrides point Git at an unrelated repository.
        [void]$startInfo.EnvironmentVariables.Remove('GIT_DIR')
        [void]$startInfo.EnvironmentVariables.Remove('GIT_WORK_TREE')
    }

    $process = [System.Diagnostics.Process]::Start($startInfo)
    try {
        $paths = $process.StandardOutput.ReadToEnd()
        $gitError = $process.StandardError.ReadToEnd()
        $process.WaitForExit()
        if ($process.ExitCode -ne 0) {
            throw "Unable to enumerate .gitignore-filtered files: $gitError"
        }
    } finally {
        $process.Dispose()
    }

    foreach ($relative in $paths.Split([char]0)) {
        if ([string]::IsNullOrEmpty($relative)) { continue }
        $extension = [System.IO.Path]::GetExtension($relative).ToLowerInvariant()
        $fileName = [System.IO.Path]::GetFileName($relative)
        if ($textExtensions -notcontains $extension -and $textNames -notcontains $fileName) { continue }

        $checked++
        $filePath = Join-Path $root $relative
        $bytes = [System.IO.File]::ReadAllBytes($filePath)
        if ($bytes.Length -lt 3 -or $bytes[0] -ne 0xEF -or $bytes[1] -ne 0xBB -or $bytes[2] -ne 0xBF) {
            $failures += "$relative : missing UTF-8 BOM"
            continue
        }
        for ($i = 3; $i -lt $bytes.Length; $i++) {
            if ($bytes[$i] -eq 0x0A -and $bytes[$i - 1] -ne 0x0D) {
                $failures += "$relative : found LF without CR"
                break
            }
            if ($bytes[$i] -eq 0x0D -and ($i + 1 -ge $bytes.Length -or $bytes[$i + 1] -ne 0x0A)) {
                $failures += "$relative : found CR without LF"
                break
            }
        }
    }
} finally {
    Remove-Item -LiteralPath $temp -Recurse -Force
}

if ($failures.Count -gt 0) {
    foreach ($failure in $failures) { [Console]::Error.WriteLine($failure) }
    exit 1
}
Write-Host "Validated Windows UTF-8 BOM + CRLF in $checked non-ignored source files."
