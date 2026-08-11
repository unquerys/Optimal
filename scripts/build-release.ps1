param(
    [switch]$SkipInstaller,
    [ValidateSet('artifacts', 'release-output')]
    [string]$OutputDirectoryName = 'artifacts'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$artifacts = Join-Path $repositoryRoot $OutputDirectoryName
$portable = Join-Path $artifacts 'package'
$singleFile = Join-Path $artifacts 'single-file'
$appProject = Join-Path $repositoryRoot 'Optimal.App\Optimal.App.csproj'
$testsProject = Join-Path $repositoryRoot 'Optimal.Tests\Optimal.Tests.csproj'
$installerScript = Join-Path $repositoryRoot 'installer\Optimal.iss'

function Reset-ReleaseDirectory {
    param([Parameter(Mandatory)][string]$Path)

    $root = [System.IO.Path]::GetFullPath($repositoryRoot).TrimEnd('\')
    $target = [System.IO.Path]::GetFullPath($Path).TrimEnd('\')
    $allowedTargets = @(
        (Join-Path $root 'artifacts'),
        (Join-Path $root 'release-output')
    )
    if ($target -notin $allowedTargets) {
        throw "Refusing to clean an unexpected release directory: $target"
    }

    if (Test-Path -LiteralPath $target) {
        $item = Get-Item -LiteralPath $target -Force
        if ($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) {
            throw 'Refusing to clean an artifacts directory that is a reparse point.'
        }
        Remove-Item -LiteralPath $target -Recurse -Force
    }
    New-Item -ItemType Directory -Path $target | Out-Null
}

function Write-Checksum {
    param([Parameter(Mandatory)][string]$Path)

    $hash = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
    $line = "$hash  $([System.IO.Path]::GetFileName($Path))`n"
    [System.IO.File]::WriteAllText("$Path.sha256", $line, [System.Text.UTF8Encoding]::new($false))
}

Push-Location $repositoryRoot
try {
    Reset-ReleaseDirectory -Path $artifacts

    dotnet restore 'Optimal.sln'
    if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed.' }

    dotnet restore $appProject -r win-x64
    if ($LASTEXITCODE -ne 0) { throw 'Windows x64 runtime restore failed.' }

    dotnet test $testsProject -c Release --no-restore --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }

    dotnet publish $appProject -c Release -r win-x64 --self-contained true --no-restore -o $portable `
        -p:DebugType=None `
        -p:DebugSymbols=false
    if ($LASTEXITCODE -ne 0) { throw 'Portable publish failed.' }

    dotnet publish $appProject -c Release -r win-x64 --self-contained true --no-restore -o $singleFile `
        -p:PublishSingleFile=true `
        -p:IncludeAllContentForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -p:DebugType=None `
        -p:DebugSymbols=false
    if ($LASTEXITCODE -ne 0) { throw 'Standalone publish failed.' }

    $standaloneExe = Join-Path $artifacts 'Optimal.exe'
    Copy-Item -LiteralPath (Join-Path $singleFile 'Optimal.exe') -Destination $standaloneExe

    $portableZip = Join-Path $artifacts 'Optimal-win-x64.zip'
    Compress-Archive -Path (Join-Path $portable '*') -DestinationPath $portableZip -CompressionLevel Optimal

    if (-not $SkipInstaller) {
        $isccCandidates = @(
            (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
            (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'),
            (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
        ) | Where-Object { $_ -and (Test-Path -LiteralPath $_) }
        $iscc = $isccCandidates | Select-Object -First 1
        if (-not $iscc) {
            throw 'Inno Setup 6 was not found. Install it or run with -SkipInstaller.'
        }
        & $iscc "/O$artifacts" "/DPackageDir=$portable" $installerScript
        if ($LASTEXITCODE -ne 0) { throw 'Installer build failed.' }
    }

    Write-Checksum -Path $standaloneExe
    Write-Checksum -Path $portableZip
    $installer = Join-Path $artifacts 'Optimal-Setup.exe'
    if (Test-Path -LiteralPath $installer) {
        Write-Checksum -Path $installer
    }

    Write-Output "Release artifacts are ready in $artifacts"
}
finally {
    Pop-Location
}
