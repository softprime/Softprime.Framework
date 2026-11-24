
[CmdletBinding()]
param([Parameter(Mandatory)][string]$RunDirectory)

$ErrorActionPreference = 'Stop'
$run = (Resolve-Path -LiteralPath $RunDirectory).Path
$cache = Join-Path $run 'nuget-cache'
$feed = Join-Path $run 'feed'
$config = Join-Path $run 'nuget.config'
$version = (Get-Content -LiteralPath (Join-Path $run 'package-version.txt') -Raw).Trim()
$projectDirectory = Join-Path $PSScriptRoot 'PackageSmoke'
$project = Join-Path $projectDirectory 'PackageSmoke.csproj'
$previousCache = $env:NUGET_PACKAGES

Start-Transcript -Path (Join-Path $run 'package-assets.log') | Out-Null
try {
    $env:NUGET_PACKAGES = $cache
    $versionProperty = "-p:FrameworkPackageVersion=$version"
    & dotnet restore $project --configfile $config --no-http-cache $versionProperty
    if ($LASTEXITCODE) { throw 'Falha no restore do consumidor NuGet.' }

    $assets = Get-Content -Raw -LiteralPath (Join-Path $projectDirectory 'obj/project.assets.json') | ConvertFrom-Json
    $packages = @('Common', 'Api.Dto', 'Domain', 'DAL')
    foreach ($framework in 'net48', 'net8.0', 'net10.0') {
        $target = $assets.targets.PSObject.Properties[$framework].Value
        if (!$target) { throw "Target ausente dos assets: $framework." }
        foreach ($package in $packages) {
            $name = "Softprime.Framework.$package"
            $key = "$name/$version"
            $record = $target.PSObject.Properties[$key].Value
            $library = $assets.libraries.PSObject.Properties[$key].Value
            if ($record.type -ne 'package' -or $library.type -ne 'package') {
                throw "Pacote $key ausente em $framework."
            }
            $metadata = Get-Content -LiteralPath (Join-Path $cache "$($library.path)/.nupkg.metadata") -Raw | ConvertFrom-Json
            if ([IO.Path]::GetFullPath($metadata.source).TrimEnd('\', '/') -ne $feed.TrimEnd('\', '/')) {
                throw "Origem inesperada para $key em $framework."
            }
            $assetFramework = 'netstandard2.1'
            if ($framework -eq 'net48') {
                $assetFramework = if ($package -in 'Domain', 'DAL') { 'net48' } else { 'netstandard2.0' }
            }
            $expected = "lib/$assetFramework/$name.dll"
            foreach ($kind in 'compile', 'runtime') {
                $paths = @($record.$kind.PSObject.Properties.Name)
                if ($paths.Count -ne 1 -or $paths[0] -ne $expected) {
                    throw "Asset $kind inesperado de $key em $framework (esperado $expected, resolvido $paths)."
                }
                Write-Host "[ASSET] $framework $key $kind $expected"
            }
        }
        & dotnet build $project -c Release -f $framework --no-restore $versionProperty
        if ($LASTEXITCODE) { throw "Falha no build do consumidor $framework." }

        $binary = Join-Path $projectDirectory "bin/Release/$framework/PackageSmoke"
        if ($framework -eq 'net48') {
            & "$binary.exe"
        } else {
            & dotnet "$binary.dll" $cache $version
        }
        if ($LASTEXITCODE) { throw "Falha na execução do consumidor $framework." }
    }
} finally {
    $env:NUGET_PACKAGES = $previousCache
    Stop-Transcript | Out-Null
}
