$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$project = Join-Path $projectRoot 'Optimal.App\Optimal.App.csproj'
$output = Join-Path $projectRoot 'Optimal.App\bin\Release\net9.0-windows10.0.19041.0'
$executable = Join-Path $output 'Optimal.exe'
$logDirectory = Join-Path $projectRoot '.logs'
$log = Join-Path $logDirectory 'launcher.log'

New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
"[$(Get-Date -Format o)] Building current source" | Set-Content -Path $log

$existing = Get-Process -Name 'Optimal' -ErrorAction SilentlyContinue
if ($existing) {
    $ids = ($existing.Id -join ', ')
    throw "Optimal is already running (PID $ids). End that process in Task Manager once, then run Run.bat again."
}

Push-Location $projectRoot
try {
    dotnet build $project -c Release 2>&1 | Tee-Object -FilePath $log -Append
    if ($LASTEXITCODE -ne 0) {
        throw "Optimal failed to build. See $log"
    }

    if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
        throw "Build completed but Optimal.exe was not found at $executable"
    }

    "[$(Get-Date -Format o)] Requesting elevation for $executable" | Add-Content -Path $log
    $process = Start-Process -FilePath $executable -ArgumentList '--onboarding' -WorkingDirectory $output -Verb RunAs -PassThru
    $process.WaitForExit()
    "[$(Get-Date -Format o)] Optimal exited with code $($process.ExitCode)" | Add-Content -Path $log
    if ($process.ExitCode -ne 0) {
        throw "Optimal exited with code $($process.ExitCode). See $log and the startup log under LocalAppData."
    }
}
finally {
    Pop-Location
}
