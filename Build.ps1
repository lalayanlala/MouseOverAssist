param([string]$DalamudPath)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$logPath = Join-Path $root 'BUILD_LOG.txt'
$transcribing = $false
try {
    Start-Transcript -LiteralPath $logPath -Force | Out-Null
    $transcribing = $true
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw '.NET 10 SDK (x64) is required. Install the SDK, not only the Runtime: https://dotnet.microsoft.com/download/dotnet/10.0'
    }
    $sdks = & dotnet --list-sdks
    if ($LASTEXITCODE -ne 0 -or -not ($sdks | Where-Object { $_ -match '^10\.' })) {
        throw '.NET 10 SDK (x64) was not found. See README_JA.txt for the official download link.'
    }
    if ([string]::IsNullOrWhiteSpace($DalamudPath)) {
        if (-not [string]::IsNullOrWhiteSpace($env:DALAMUD_HOME)) {
            $DalamudPath = $env:DALAMUD_HOME
        } else {
            $DalamudPath = Join-Path $env:APPDATA 'XIVLauncher\addon\Hooks\dev'
        }
    }
    if (-not (Test-Path -LiteralPath (Join-Path $DalamudPath 'Dalamud.dll'))) {
        throw "Dalamud.dll was not found at: $DalamudPath . Run FF14 with XIVLauncher/Dalamud once, or specify -DalamudPath."
    }
    $resolvedPath = (Resolve-Path -LiteralPath $DalamudPath).Path
    # SDK uses a trailing directory separator when resolving the local reference assemblies.
    $libPath = $resolvedPath.TrimEnd('\', '/') + '/'
    Write-Host 'Checking rule behavior, migration and sharing...' -ForegroundColor Cyan
    & dotnet run --project (Join-Path $root 'Tests\Tests.csproj') --configuration Release "-p:DalamudLibPath=$libPath"
    if ($LASTEXITCODE -ne 0) { throw 'Routing checks failed. Plugin was not built.' }

    $buildDir = Join-Path $root ('.build\' + [Guid]::NewGuid().ToString('N'))
    Write-Host 'Building MouseOverAssist V2.3.14 for FF14 patch 7.57 / Dalamud API 15...' -ForegroundColor Cyan
    & dotnet build (Join-Path $root 'MouseOverAssist\MouseOverAssist.csproj') --configuration Release `
        --output $buildDir "-p:DalamudLibPath=$libPath" '-p:EnableWindowsTargeting=true' '-p:TreatWarningsAsErrors=true' '--nologo'
    if ($LASTEXITCODE -ne 0) { throw 'Build failed. Send BUILD_LOG.txt to review the error.' }
    $dll = Join-Path $buildDir 'MouseOverAssist.dll'
    if (-not (Test-Path -LiteralPath $dll)) { throw 'Build completed without the expected plugin DLL.' }
    $manifest = Join-Path $buildDir 'MouseOverAssist.json'
    if (-not (Test-Path -LiteralPath $manifest)) {
        $manifest = Join-Path $root 'MouseOverAssist\MouseOverAssist.json'
    }
    $metadata = Get-Content -LiteralPath $manifest -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($metadata.DalamudApiLevel -ne 15) { throw 'Unexpected manifest API level. Output was not updated.' }
    $dllVersion = [System.Reflection.AssemblyName]::GetAssemblyName($dll).Version.ToString()
    if ($metadata.AssemblyVersion -ne $dllVersion) {
        throw "DLL/JSON versions disagree ($dllVersion vs $($metadata.AssemblyVersion)). Build output is not safe to publish."
    }
    $sourceManifestPath = Join-Path $root 'MouseOverAssist\MouseOverAssist.json'
    $sourceMetadata = Get-Content -LiteralPath $sourceManifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($sourceMetadata.AssemblyVersion -ne $dllVersion) {
        throw "Source manifest/DLL versions disagree ($($sourceMetadata.AssemblyVersion) vs $dllVersion). Update csproj and MouseOverAssist.json together."
    }
    Write-Host "Version check OK: $dllVersion" -ForegroundColor Green
    Write-Host 'Warning gate OK: 0 compiler warnings (warnings are treated as errors).' -ForegroundColor Green

    $outputDir = Join-Path $root 'Output'
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
    Copy-Item -LiteralPath $dll -Destination (Join-Path $outputDir 'MouseOverAssist.dll') -Force
    Copy-Item -LiteralPath $manifest -Destination (Join-Path $outputDir 'MouseOverAssist.json') -Force
    foreach ($name in @('MouseOverAssist.pdb', 'MouseOverAssist.deps.json')) {
        $source = Join-Path $buildDir $name
        if (Test-Path -LiteralPath $source) { Copy-Item -LiteralPath $source -Destination $outputDir -Force }
    }
    $location = Join-Path $outputDir 'MouseOverAssist.dll'
    Set-Content -LiteralPath (Join-Path $root 'PLUGIN_PATH.txt') -Value $location -Encoding UTF8
    Write-Host ''
    Write-Host 'SUCCESS - MouseOverAssist built:' -ForegroundColor Green
    Write-Host $location
    Write-Host 'For repository distribution, run PREPARE_REPOSITORY.bat next.'
    Write-Host 'In game: /moa'
    if ($transcribing) { Stop-Transcript | Out-Null; $transcribing = $false }
    Start-Process explorer.exe -ArgumentList ('"' + $outputDir + '"')
    exit 0
} catch {
    Write-Host ''
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host "Log: $logPath"
    if ($transcribing) { Stop-Transcript | Out-Null }
    exit 1
}
