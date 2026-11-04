[CmdletBinding()]
param(
    [string]$DatabasePath = (Join-Path $env:LOCALAPPDATA 'LeagueOfStats\leagueofstats.db')
)

$ErrorActionPreference = 'Stop'
$projectPath = Join-Path $PSScriptRoot 'tools/CreateDatabase/CreateDatabase.csproj'
$absolutePath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($DatabasePath)

& dotnet run --project $projectPath --configuration Release -- $absolutePath
if ($LASTEXITCODE -ne 0) {
    throw "No se pudo crear la base de datos (código $LASTEXITCODE)."
}
