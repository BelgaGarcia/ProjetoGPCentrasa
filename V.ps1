[CmdletBinding()]
param(
    [switch]$Coverage,

    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
$solution = Join-Path $PSScriptRoot 'CentraSA.sln'

function Invoke-DotNetStep {
    param(
        [Parameter(Mandatory)]
        [string]$Name,

        [Parameter(Mandatory)]
        [string[]]$Arguments
    )

    Write-Host "`n==> $Name" -ForegroundColor Cyan
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "A etapa '$Name' falhou com o codigo $LASTEXITCODE."
    }
}

Push-Location $PSScriptRoot
try {
    Invoke-DotNetStep 'Restaurar dependencias' @('restore', $solution)
    Invoke-DotNetStep 'Compilar com analisadores' @('build', $solution, '--configuration', $Configuration, '--no-restore')

    $testArguments = @('test', $solution, '--configuration', $Configuration, '--no-build', '--no-restore')
    if ($Coverage) {
        $testArguments += '--collect:XPlat Code Coverage'
    }

    Invoke-DotNetStep 'Executar testes' $testArguments
    Invoke-DotNetStep 'Verificar formatacao' @('format', $solution, '--verify-no-changes', '--no-restore')

    Write-Host "`nValidacao concluida com sucesso." -ForegroundColor Green
}
finally {
    Pop-Location
}
