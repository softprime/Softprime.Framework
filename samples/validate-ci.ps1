<# Validacao completa, sem publicacao. Requer Windows e SDKs 6, 8 e 10. #>
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
function Invoke-Dotnet {
    param([string[]]$Arguments)
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw "dotnet falhou ($LASTEXITCODE): $($Arguments -join ' ')" }
}
$root = Split-Path $PSScriptRoot -Parent
$run = Join-Path $root ('artifacts/samples-ci/' + [guid]::NewGuid().ToString('N'))
$feed = Join-Path $run 'feed'
$cache = Join-Path $run 'nuget-cache'
$version = '2.0.0-ci.' + [guid]::NewGuid().ToString('N')
$config = Join-Path $run 'nuget.config'
$previousCache = $env:NUGET_PACKAGES
New-Item -ItemType Directory -Path $feed, $cache -Force | Out-Null
Start-Transcript -Path (Join-Path $run 'validation.log') | Out-Null
Push-Location $root
try {
    $solution = 'samples/Softprime.Framework.Samples.sln'
    Invoke-Dotnet @('restore', $solution, '-p:UseInstalledPackages=false')
    Invoke-Dotnet @('build', $solution, '-c', 'Release', '--no-restore', '-p:UseInstalledPackages=false')
    & (Join-Path $PSScriptRoot 'run-smoke-tests.ps1')
    if ($LASTEXITCODE -ne 0) { throw 'Smoke tests com ProjectReference falharam.' }

    # Versao exclusiva e cache vazio impedem consumir pacote antigo.
    Invoke-Dotnet @('pack', 'src/Softprime.Framework.All.sln', '-c', 'Release', '-o', $feed, "-p:Version=$version", "-p:PackageVersion=$version")
    $settings = New-Object System.Xml.XmlDocument
    $settings.LoadXml('<configuration><packageSources><clear /></packageSources><packageSourceMapping /></configuration>')
    foreach ($source in @(@('local', $feed), @('nuget.org', 'https://api.nuget.org/v3/index.json'))) {
        $add = $settings.CreateElement('add'); $add.SetAttribute('key', $source[0]); $add.SetAttribute('value', $source[1])
        [void]$settings.configuration.packageSources.AppendChild($add)
        $mapping = $settings.CreateElement('packageSource'); $mapping.SetAttribute('key', $source[0])
        $package = $settings.CreateElement('package'); $pattern = if ($source[0] -eq 'local') { 'Softprime.Framework.*' } else { '*' }
        $package.SetAttribute('pattern', $pattern); [void]$mapping.AppendChild($package)
        [void]$settings.SelectSingleNode('/configuration/packageSourceMapping').AppendChild($mapping)
    }
    $settings.Save($config)
    $env:NUGET_PACKAGES = $cache
    $props = @('-p:UseInstalledPackages=true', "-p:FrameworkPackageVersion=$version")
    Invoke-Dotnet (@('restore', $solution, '--configfile', $config, '--no-http-cache') + $props)
    foreach ($project in Get-ChildItem $PSScriptRoot -Recurse -Filter *.csproj) {
        $assets = Get-Content (Join-Path $project.DirectoryName 'obj/packages/project.assets.json') -Raw | ConvertFrom-Json
        $resolved = @($assets.libraries.PSObject.Properties | Where-Object { $_.Name -like 'Softprime.Framework.*' })
        if ($resolved.Count -eq 0) { throw "Sem pacotes Softprime: $($project.Name)" }
        foreach ($library in $resolved) {
            if ($library.Value.type -ne 'package' -or !$library.Name.EndsWith("/$version")) { throw "Referencia inesperada: $($library.Name)" }
            $metadata = Join-Path $cache ($library.Value.path + '/.nupkg.metadata')
            $origin = (Get-Content -LiteralPath $metadata -Raw | ConvertFrom-Json).source
            if ([IO.Path]::GetFullPath($origin).TrimEnd('\', '/') -ne [IO.Path]::GetFullPath($feed).TrimEnd('\', '/')) { throw "Origem inesperada: $origin" }
        }
    }
    Invoke-Dotnet (@('build', $solution, '-c', 'Release', '--no-restore') + $props)
    & (Join-Path $PSScriptRoot 'run-smoke-tests.ps1') -UseInstalledPackages
    if ($LASTEXITCODE -ne 0) { throw 'Smoke tests com PackageReference falharam.' }
    & (Join-Path $PSScriptRoot 'audit-packaging.ps1') -OutputDirectory (Join-Path $run 'audit') -MSBuildProperties $props
    Write-Host "[CI_PASSED] Duas modalidades validadas. Evidencias: $run"
} finally {
    $env:NUGET_PACKAGES = $previousCache
    Pop-Location
    Stop-Transcript | Out-Null
}
