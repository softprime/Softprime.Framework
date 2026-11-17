<# Executa os binarios Release previamente compilados em todos os targets. #>
[CmdletBinding()]
param([switch]$UseInstalledPackages)
$ErrorActionPreference = 'Stop'
$mode = if ($UseInstalledPackages) { 'packages' } else { 'projects' }
$passed = 0
$failed = 0
foreach ($project in Get-ChildItem $PSScriptRoot -Recurse -Filter *.csproj | Sort-Object FullName) {
    [xml]$xml = Get-Content -LiteralPath $project.FullName
    $frameworks = @($xml.Project.PropertyGroup.TargetFrameworks, $xml.Project.PropertyGroup.TargetFramework) | Where-Object { $_ }
    foreach ($framework in (($frameworks -join ';') -split ';')) {
        $binary = Join-Path $project.DirectoryName "bin/$mode/Release/$framework/$($project.BaseName).dll"
        if (!(Test-Path -LiteralPath $binary)) { throw "Binario ausente: $binary. Compile antes de executar." }
        Write-Host "[RUN] $($project.BaseName) $framework ($mode)"
        & dotnet $binary --smoke-test
        if ($LASTEXITCODE -ne 0) { $failed++ } else { $passed++ }
    }
}
Write-Host "[SMOKE_SUMMARY] $mode : $passed aprovados; $failed falhas"
if ($failed -gt 0 -or $passed -eq 0) { exit 1 }
exit 0
