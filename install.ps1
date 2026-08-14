$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repository = if ([string]::IsNullOrWhiteSpace($env:OPTIMAL_GITHUB_REPOSITORY)) {
    'unquerys/Optimal'
} else {
    $env:OPTIMAL_GITHUB_REPOSITORY.Trim()
}
if ($repository -notmatch '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$') {
    throw 'OPTIMAL_GITHUB_REPOSITORY must use the owner/repository format.'
}
$apiUri = "https://api.github.com/repos/$repository/releases/latest"
$headers = @{ 'User-Agent' = 'Optimal-Installer' }
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("Optimal-Install-" + [Guid]::NewGuid().ToString('N'))

try {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    New-Item -ItemType Directory -Path $tempRoot | Out-Null

    Write-Host 'Finding the newest stable Optimal release...' -ForegroundColor Cyan
    $release = Invoke-RestMethod -Uri $apiUri -Headers $headers
    if ($release.prerelease -or $release.draft) {
        throw 'GitHub returned a draft or prerelease instead of the latest stable release.'
    }

    $installerAsset = @($release.assets) | Where-Object name -EQ 'Optimal-Setup.exe' | Select-Object -First 1
    $checksumAsset = @($release.assets) | Where-Object name -EQ 'Optimal-Setup.exe.sha256' | Select-Object -First 1
    if (-not $installerAsset -or -not $checksumAsset) {
        throw 'The stable release is missing the installer or its checksum.'
    }

    $installerPath = Join-Path $tempRoot 'Optimal-Setup.exe'
    $checksumPath = Join-Path $tempRoot 'Optimal-Setup.exe.sha256'
    Invoke-WebRequest -Uri $installerAsset.browser_download_url -Headers $headers -OutFile $installerPath
    Invoke-WebRequest -Uri $checksumAsset.browser_download_url -Headers $headers -OutFile $checksumPath

    $expected = ((Get-Content -LiteralPath $checksumPath -Raw).Trim() -split '\s+')[0].ToUpperInvariant()
    if ($expected -notmatch '^[A-F0-9]{64}$') {
        throw 'The published installer checksum is not a valid SHA-256 value.'
    }
    $actual = (Get-FileHash -LiteralPath $installerPath -Algorithm SHA256).Hash
    if ($actual -ne $expected) {
        throw 'Installer checksum verification failed. The installer will not run.'
    }

    Write-Host "Verified Optimal $($release.tag_name). Opening the installer..." -ForegroundColor Green
    $process = Start-Process -FilePath $installerPath -Verb RunAs -Wait -PassThru
    if ($process.ExitCode -notin 0, 5) {
        throw "Optimal Setup exited with code $($process.ExitCode)."
    }
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        $resolvedTemp = (Resolve-Path -LiteralPath $tempRoot).Path
        $expectedParent = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd('\')
        $actualParent = [System.IO.Path]::GetDirectoryName($resolvedTemp).TrimEnd('\')
        if ($actualParent -eq $expectedParent -and [System.IO.Path]::GetFileName($resolvedTemp).StartsWith('Optimal-Install-', [StringComparison]::Ordinal)) {
            Remove-Item -LiteralPath $resolvedTemp -Recurse -Force
        }
    }
}
