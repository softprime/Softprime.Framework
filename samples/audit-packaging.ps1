<# Avalia e tenta empacotar cada sample; nao depende do PackageId. #>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$OutputDirectory,
    [string[]]$Projects = @((Get-ChildItem $PSScriptRoot -Recurse -Filter *.csproj).FullName),
    [string[]]$MSBuildProperties = @()
)
$ErrorActionPreference = 'Stop'
foreach ($project in $Projects) {
    $evaluated = & dotnet msbuild $project '-getProperty:IsPackable' @MSBuildProperties
    if ($LASTEXITCODE -ne 0 -or ($evaluated -join '').Trim() -ne 'false') { throw "Sample empacotavel: $project" }
    $output = Join-Path $OutputDirectory ([guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $output | Out-Null
    & dotnet pack $project -c Release --no-build --no-restore -o $output @MSBuildProperties
    if ($LASTEXITCODE -ne 0) { throw "Pack falhou: $project" }
    if (Get-ChildItem $output -Recurse -File | Where-Object Extension -in '.nupkg', '.snupkg') { throw "Sample produziu pacote: $project" }
}
Write-Host "[AUDIT_PASSED] $($Projects.Count) samples fora do empacotamento."
