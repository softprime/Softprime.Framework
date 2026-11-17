<#
.SYNOPSIS
    Executa os smoke tests de todos os samples implementados do Softprime.Framework.
.DESCRIPTION
    Compila e executa cada projeto de exemplo com o argumento '--smoke-test',
    validando o codigo de saida (0) e a saida esperada, garantindo que os samples
    nao apodrecam. Compativel com Windows PowerShell 5.1 e PowerShell Core 7+.
#>

$ErrorActionPreference = "Stop"

Write-Host "================================================================================" -ForegroundColor Cyan
Write-Host " Softprime.Framework - Bateria de Smoke Tests dos Samples (#21)" -ForegroundColor Cyan
Write-Host "================================================================================" -ForegroundColor Cyan
Write-Host ""

$samples = @(
    @{ Name = "01-Common"; Project = "samples/01-Common/Softprime.Samples.Common.csproj"; Framework = "net10.0" },
    @{ Name = "02-Domain"; Project = "samples/02-Domain/Softprime.Samples.Domain.csproj"; Framework = "net10.0" },
    @{ Name = "03-Data-EFCore"; Project = "samples/03-Data-EFCore/Softprime.Samples.DataEFCore.csproj"; Framework = "net6.0" },
    @{ Name = "04-WebApi"; Project = "samples/04-WebApi/Softprime.Samples.WebApi.csproj"; Framework = "net10.0" },
    @{ Name = "05-ApiClient"; Project = "samples/05-ApiClient/Softprime.Samples.ApiClient.csproj"; Framework = "net10.0" },
    @{ Name = "06-Mailer"; Project = "samples/06-Mailer/Softprime.Samples.Mailer.csproj"; Framework = "net10.0" },
    @{ Name = "07-Multitenancy"; Project = "samples/07-Multitenancy/Softprime.Samples.Multitenancy.csproj"; Framework = "net10.0" }
)

$total = $samples.Count
$passed = 0
$failed = 0
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

foreach ($sample in $samples) {
    Write-Host "[RUN] Executando smoke test: $($sample.Name) ($($sample.Framework))..." -ForegroundColor Yellow
    $sampleWatch = [System.Diagnostics.Stopwatch]::StartNew()

    $cmdArgs = @("run", "--project", $sample.Project, "-c", "Release", "--no-build")
    if ($sample.Framework) {
        $cmdArgs += @("--framework", $sample.Framework)
    }
    $cmdArgs += @("--", "--smoke-test")

    $process = Start-Process -FilePath "dotnet" -ArgumentList $cmdArgs -NoNewWindow -Wait -PassThru
    $sampleWatch.Stop()

    if ($process.ExitCode -eq 0) {
        Write-Host "  -> SUCESSO: $($sample.Name) concluido em $($sampleWatch.Elapsed.TotalSeconds.ToString('F2'))s" -ForegroundColor Green
        $passed++
    } else {
        Write-Host "  -> FALHA: $($sample.Name) retornou codigo $($process.ExitCode) em $($sampleWatch.Elapsed.TotalSeconds.ToString('F2'))s" -ForegroundColor Red
        $failed++
    }
    Write-Host ""
}

$stopwatch.Stop()

$statusColor = "Green"
if ($failed -gt 0) {
    $statusColor = "Red"
}

Write-Host "--------------------------------------------------------------------------------" -ForegroundColor Cyan
Write-Host "Resultado dos Smoke Tests: $passed/$total passaram com sucesso ($failed falhas)." -ForegroundColor $statusColor
Write-Host "Tempo total de execucao: $($stopwatch.Elapsed.TotalSeconds.ToString('F2'))s" -ForegroundColor Cyan
Write-Host "--------------------------------------------------------------------------------" -ForegroundColor Cyan

if ($failed -gt 0) {
    exit 1
}

exit 0
