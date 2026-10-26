[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$OutputPath,
    [switch]$SkipInstall
)
$ErrorActionPreference = 'Stop'
$repository = Split-Path -Parent $PSScriptRoot
$frontend = Join-Path $repository 'src/MiniLogistics.Frontend'
$output = [IO.Path]::GetFullPath($(if ([IO.Path]::IsPathRooted($OutputPath)) { $OutputPath } else { Join-Path $repository $OutputPath }))
$source = Join-Path $repository 'src'
if ($output -eq $repository -or $output -eq $source -or $output.StartsWith($source + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Publish output must be a separate artifact directory, outside source.'
}
Push-Location $repository
try {
    if ((node --version) -ne 'v24.22.1') { throw 'Use pinned Node 24.22.1.' }
    if ((npm --version) -ne '11.19.1') { throw 'Use pinned npm 11.19.1.' }
    if (-not $SkipInstall) { npm --prefix $frontend ci; if ($LASTEXITCODE) { throw 'npm ci failed.' } }
    foreach ($step in @('typecheck', 'lint', 'test', 'build')) {
        npm --prefix $frontend run $step
        if ($LASTEXITCODE) { throw "Frontend $step failed." }
    }
    dotnet publish src/MiniLogistics.Web/MiniLogistics.Web.csproj -c Release -p:PublishBrowserSpa=true -o $output
    if ($LASTEXITCODE) { throw 'Web publish failed.' }
    $revision = git rev-parse HEAD
    @{ revision = $revision; sourceHasChanges = [bool](git status --porcelain); createdAtUtc = [DateTimeOffset]::UtcNow.ToString('O'); ui = 'React'; node = '24.22.1'; npm = '11.19.1'; assemblySha256 = (Get-FileHash (Join-Path $output 'MiniLogistics.Web.dll')).Hash; indexSha256 = (Get-FileHash (Join-Path $output 'frontend/index.html')).Hash } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'release-manifest.json') -Encoding utf8
    Write-Output "Published artifact: $output. Configure persistent BannerImages, DataProtection, SQL/Redis and BrowserSpa mode for the target environment before starting it."
}
finally { Pop-Location }
