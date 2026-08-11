$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$project = Join-Path $projectRoot 'Optimal.App\Optimal.App.csproj'

Push-Location $projectRoot
try {
    dotnet run --project $project -c Release -- --onboarding
    if ($LASTEXITCODE -ne 0) {
        throw "Optimal exited with code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}
