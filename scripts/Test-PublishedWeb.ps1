[CmdletBinding()]
param([Parameter(Mandatory = $true)][string]$ArtifactPath, [switch]$RootMode, [int]$Port = 17196)
$ErrorActionPreference = 'Stop'
$repository = Split-Path -Parent $PSScriptRoot
$artifact = [IO.Path]::GetFullPath($(if ([IO.Path]::IsPathRooted($ArtifactPath)) { $ArtifactPath } else { Join-Path $repository $ArtifactPath }))
$dll = Join-Path $artifact 'MiniLogistics.Web.dll'
if (-not (Test-Path -LiteralPath $dll)) { throw 'Published Web assembly is missing.' }
$variables = @{
    ASPNETCORE_ENVIRONMENT = 'BrowserTests'; BrowserSpa__Enabled = 'true'; BrowserSpa__ServeAtRoot = $RootMode.IsPresent.ToString().ToLowerInvariant()
    BannerImages__RootPath = (Join-Path $repository '.agent/published-smoke-images')
    ConnectionStrings__DefaultConnection = 'Server=(localdb)\MSSQLLocalDB;Database=MiniLogisticsPublishedSmoke;Trusted_Connection=True;TrustServerCertificate=True'
    Kestrel__Certificates__Default__Path = (Join-Path $repository '.agent/https/localhost.pem')
    Kestrel__Certificates__Default__KeyPath = (Join-Path $repository '.agent/https/localhost.key')
    MINILOGISTICS_E2E_BASE_URL = "https://localhost:$Port/$(if (-not $RootMode) { 'app/' })"
    MINILOGISTICS_E2E_ROOT = $(if ($RootMode) { '1' } else { '0' })
    MINILOGISTICS_E2E_SENSITIVE = '1'
}
$saved = @{}
$process = $null
try {
    foreach ($name in $variables.Keys) { $saved[$name] = [Environment]::GetEnvironmentVariable($name, 'Process'); [Environment]::SetEnvironmentVariable($name, $variables[$name], 'Process') }
    $process = Start-Process dotnet -ArgumentList @("`"$dll`"", '--urls', "https://localhost:$Port") -WorkingDirectory $artifact -WindowStyle Hidden -PassThru
    $ready = $false
    for ($attempt = 0; $attempt -lt 120; $attempt++) {
        if ($process.HasExited) { throw 'Published server exited.' }
        & curl.exe --fail --silent --cacert (Join-Path $repository '.agent/https/localhost.pem') "https://localhost:$Port/health/live" *> $null
        if ($LASTEXITCODE -eq 0) { $ready = $true; break }
        Start-Sleep -Milliseconds 250
    }
    if (-not $ready) { throw 'Published server did not become healthy.' }
    if ($RootMode) { npm --prefix (Join-Path $repository 'src/MiniLogistics.Frontend') run test:e2e -- e2e/root-cutover.spec.ts e2e/public-pages.spec.ts }
    else { npm --prefix (Join-Path $repository 'src/MiniLogistics.Frontend') run test:e2e }
    if ($LASTEXITCODE) { throw 'Published browser smoke failed.' }
}
finally {
    if ($process -and -not $process.HasExited) { Stop-Process -Id $process.Id }
    foreach ($name in $saved.Keys) { [Environment]::SetEnvironmentVariable($name, $saved[$name], 'Process') }
}
