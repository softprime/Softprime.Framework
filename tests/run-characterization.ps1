
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repository = Split-Path $PSScriptRoot -Parent
$reports = Join-Path $repository 'artifacts/sdk-ci/tests'
New-Item -ItemType Directory -Path $reports -Force | Out-Null

Start-Transcript -Path (Join-Path $reports 'validation.log') | Out-Null
Push-Location $repository
try {
    & git rev-parse HEAD
    if ($LASTEXITCODE) { throw 'Falha ao identificar o commit.' }
    & dotnet --info
    if ($LASTEXITCODE) { throw 'Falha ao identificar o SDK.' }

    & dotnet restore src/Softprime.Framework.All.sln
    if ($LASTEXITCODE) { throw 'Falha no restore da SDK.' }
    & dotnet build src/Softprime.Framework.All.sln -c Release --no-restore -p:GeneratePackageOnBuild=false
    if ($LASTEXITCODE) { throw 'Falha no build da SDK.' }

    $runs = 0
    $failed = @()
    foreach ($project in Get-ChildItem $PSScriptRoot -Recurse -File -Filter '*.Tests.csproj' | Sort-Object FullName) {
        $metadata = & dotnet msbuild $project.FullName -nologo -getProperty:TargetFramework,TargetFrameworks,IsTestProject
        if ($LASTEXITCODE) { throw "Falha na leitura de $($project.FullName)." }
        $properties = ($metadata | Out-String | ConvertFrom-Json).Properties
        if ($properties.IsTestProject -ne 'true') { throw "Projeto sem IsTestProject=true: $($project.FullName)." }

        $frameworks = if ($properties.TargetFrameworks) { $properties.TargetFrameworks } else { $properties.TargetFramework }
        if (!$frameworks) { throw "Projeto sem target: $($project.FullName)." }
        foreach ($framework in ($frameworks -split ';')) {
            $runs++
            Write-Host "[TEST] $($project.BaseName) $framework"
            & dotnet test $project.FullName -c Release -f $framework -p:GeneratePackageOnBuild=false `
                --results-directory (Join-Path $reports $framework) --logger "trx;LogFileName=$($project.BaseName).trx" `
                -- RunConfiguration.TreatNoTestsAsError=true
            if ($LASTEXITCODE) { $failed += "$($project.BaseName) ($framework)" }
        }
    }
    Write-Host "[TEST_SUMMARY] $runs combinações, $($failed.Count) com falha."
    if ($failed.Count) { throw "Falhas: $($failed -join ', ')." }
} finally {
    Pop-Location
    Stop-Transcript | Out-Null
}
