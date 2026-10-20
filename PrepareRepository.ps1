param([string]$RepositoryUrl, [string]$OutputPath)
$ErrorActionPreference = 'Stop'
try {
    if ([string]::IsNullOrWhiteSpace($OutputPath)) { $OutputPath = Join-Path $PSScriptRoot 'Output' }
    $dllPath = Join-Path $OutputPath 'MouseOverAssist.dll'
    $jsonPath = Join-Path $OutputPath 'MouseOverAssist.json'
    if (-not (Test-Path -LiteralPath $dllPath) -or -not (Test-Path -LiteralPath $jsonPath)) {
        throw 'Put these tools beside the Output folder from the successful build. Output must contain MouseOverAssist.dll and MouseOverAssist.json.'
    }
    if ([string]::IsNullOrWhiteSpace($RepositoryUrl)) {
        $RepositoryUrl = Read-Host 'Paste the GitHub repository URL (https://github.com/OWNER/REPOSITORY)'
    }
    $RepositoryUrl = $RepositoryUrl.Trim().TrimEnd('/')
    if ($RepositoryUrl -notmatch '^https://github\.com/([A-Za-z0-9-]+)/([A-Za-z0-9_.-]+)$') {
        throw 'Enter the repository page URL, not a file, settings, or release URL.'
    }
    $ownerName = $Matches[1]
    $repositoryName = $Matches[2]
    $manifest = Get-Content -LiteralPath $jsonPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($manifest.InternalName -ne 'MouseOverAssist' -or $manifest.DalamudApiLevel -ne 15) {
        throw 'The manifest is not a MouseOverAssist API 15 build.'
    }
    $dllVersion = [System.Reflection.AssemblyName]::GetAssemblyName($dllPath).Version.ToString()
    if ($dllVersion -ne $manifest.AssemblyVersion) {
        throw "DLL/JSON versions disagree ($dllVersion vs $($manifest.AssemblyVersion)). Run BUILD.bat again."
    }
    if ($dllVersion -notmatch '^\d+\.\d+\.\d+\.\d+$') { throw "Unexpected DLL version format: $dllVersion" }
    $releaseTag = 'v' + $dllVersion
    $downloadUrl = "$RepositoryUrl/releases/download/$releaseTag/latest.zip"
    $rawUrl = "https://raw.githubusercontent.com/$ownerName/$repositoryName/main/pluginmaster.json"
    $releaseDir = Join-Path $PSScriptRoot 'Release'
    $stageDir = Join-Path $PSScriptRoot ('.release-stage-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $stageDir -Force | Out-Null
    New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null
    $utf8 = New-Object System.Text.UTF8Encoding($false)
    # Distribute our own files; do not redistribute Dalamud/game reference assemblies.
    Copy-Item -LiteralPath $dllPath -Destination (Join-Path $stageDir 'MouseOverAssist.dll')
    foreach ($notice in @('LICENSE.txt', 'THIRD_PARTY_NOTICES.txt')) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot $notice) -Destination $stageDir
    }
    $deps = Join-Path $OutputPath 'MouseOverAssist.deps.json'
    if (Test-Path -LiteralPath $deps) { Copy-Item -LiteralPath $deps -Destination $stageDir }
    $manifest | Add-Member -NotePropertyName RepoUrl -NotePropertyValue $RepositoryUrl -Force
    [IO.File]::WriteAllText((Join-Path $stageDir 'MouseOverAssist.json'), ($manifest | ConvertTo-Json -Depth 20), $utf8)
    # Full matching source accompanies the binary Release.
    $sourceStage = Join-Path $PSScriptRoot ('.source-stage-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $sourceStage | Out-Null
    foreach ($sourceName in @('MouseOverAssist','Tests','BUILD.bat','Build.ps1','global.json','nuget.config','PREPARE_REPOSITORY.bat','PrepareRepository.ps1','README_JA.txt','V2.3_CHANGES.txt','V2.3.1_CHANGES.txt','V2.3.2_CHANGES.txt','V2.3.3_CHANGES.txt','V2.3.4_CHANGES.txt','V2.3.5_CHANGES.txt','V2.3.6_CHANGES.txt','V2.3.7_CHANGES.txt','V2.3.8_CHANGES.txt','V2.3.9_CHANGES.txt','V2.3.10_CHANGES.txt','V2.3.11_CHANGES.txt','V2.3.12_CHANGES.txt','V2.3.13_CHANGES.txt','V2.3.14_CHANGES.txt','V2.3.15_CHANGES.txt','VERIFICATION.txt','LICENSE.txt','THIRD_PARTY_NOTICES.txt')) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot $sourceName) -Destination $sourceStage -Recurse
    }
    Get-ChildItem -LiteralPath $sourceStage -Directory -Recurse | Where-Object { $_.Name -in @('bin','obj') } | Remove-Item -Recurse -Force
    Compress-Archive -Path (Join-Path $sourceStage '*') -DestinationPath (Join-Path $releaseDir 'MouseOverAssist_V2_Source.zip') -Force
    Remove-Item -LiteralPath $sourceStage -Recurse -Force
    $zipPath = Join-Path $releaseDir 'latest.zip'
    $stageFiles = @(Get-ChildItem -LiteralPath $stageDir -File | ForEach-Object { $_.FullName })
    Compress-Archive -LiteralPath $stageFiles -DestinationPath $zipPath -Force
    Remove-Item -LiteralPath $stageDir -Recurse -Force

    $manifest | Add-Member -NotePropertyName DownloadLinkInstall -NotePropertyValue $downloadUrl -Force
    $manifest | Add-Member -NotePropertyName DownloadLinkUpdate -NotePropertyValue $downloadUrl -Force
    $manifest | Add-Member -NotePropertyName IsHide -NotePropertyValue $false -Force
    $manifest | Add-Member -NotePropertyName IsTestingExclusive -NotePropertyValue $false -Force
    $manifest | Add-Member -NotePropertyName LastUpdate -NotePropertyValue ([DateTimeOffset]::UtcNow.ToUnixTimeSeconds().ToString()) -Force
    # Explicit brackets are required even for a repository containing just one plugin.
    $entryJson = $manifest | ConvertTo-Json -Depth 20
    [IO.File]::WriteAllText((Join-Path $releaseDir 'pluginmaster.json'), "[`r`n$entryJson`r`n]", $utf8)
    [IO.File]::WriteAllText((Join-Path $releaseDir 'REPOSITORY_URL.txt'), $rawUrl + "`r`n", $utf8)
    # Keep this script ASCII-only so Windows PowerShell 5.1 can parse it reliably.
    $stepsLines = @(
        "1. Create a new GitHub Release. Use tag: $releaseTag",
        '2. Attach latest.zip and MouseOverAssist_V2_Source.zip to that Release, then publish it.',
        '3. Upload Release\pluginmaster.json to the repository root on the main branch as pluginmaster.json.',
        '4. Refresh the custom plugin repositories in Dalamud and update MouseOverAssist.',
        '',
        'Custom repository URL:',
        $rawUrl,
        '',
        'Keep older Releases. This tool only prepares files; it does not publish to GitHub.'
    )
    $steps = [string]::Join("`r`n", $stepsLines) + "`r`n"
    [IO.File]::WriteAllText((Join-Path $releaseDir 'UPLOAD_STEPS.txt'), $steps, $utf8)
    Write-Host "SUCCESS: $releaseDir" -ForegroundColor Green
    Write-Host "Release tag: $releaseTag"
    Write-Host "Repository URL (after upload): $rawUrl"
    exit 0
} catch {
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}
