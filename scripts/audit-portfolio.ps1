[CmdletBinding()]
param(
    [switch]$RequireClean
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path $PSScriptRoot -Parent

Push-Location $repositoryRoot
try {
    $trackedFiles = @(& git ls-files)
    if ($LASTEXITCODE -ne 0) {
        throw 'Nao foi possivel consultar os arquivos versionados.'
    }

    $publishableFiles = @(& git ls-files --cached --others --exclude-standard)
    if ($LASTEXITCODE -ne 0) {
        throw 'Nao foi possivel consultar os arquivos publicaveis.'
    }

    $sensitivePaths = $publishableFiles | Where-Object {
        $_ -match '(?i)(^|/)(data|backups?)/' -or
        $_ -match '(?i)\.(db|sqlite|sqlite3|pfx|p12|pem|key|env)$' -or
        $_ -match '(?i)appsettings\..*\.local\.json$'
    }
    if ($sensitivePaths) {
        throw "Arquivos sensiveis versionados:`n$($sensitivePaths -join "`n")"
    }

    $historyObjects = @(& git rev-list --objects --all)
    if ($LASTEXITCODE -ne 0) {
        throw 'Nao foi possivel consultar o historico do Git.'
    }
    $sensitiveHistory = $historyObjects | Where-Object {
        $_ -match '(?i)\s.*\.(db|sqlite|sqlite3|pfx|p12|pem|key|env)$' -or
        $_ -match '(?i)\s(.*/)?(data|backups?)/'
    }
    if ($sensitiveHistory) {
        throw "Caminhos sensiveis encontrados no historico:`n$($sensitiveHistory -join "`n")"
    }

    $scanFiles = $publishableFiles |
        Where-Object {
            ($_ -match '(?i)\.(cs|cshtml|json|md|props|targets|ps1|mjs|cmd|yml|yaml|csproj|sln|config)$' -or
                $_ -in @('.gitignore', 'LICENSE')) -and
            $_ -ne 'scripts/audit-portfolio.ps1' -and
            $_ -notmatch '^src/CentraSA.Web/wwwroot/lib/'
        } |
        ForEach-Object { Get-Item -LiteralPath $_ }
    $privacyPattern = 't001521|TOTVS|Protheus|Pendente DSM|plano de corte|documento fiscal|ICMS'
    $privacyFindings = $scanFiles | Select-String -Pattern $privacyPattern
    if ($privacyFindings) {
        throw "Referencias nao sanitizadas encontradas:`n$($privacyFindings -join "`n")"
    }

    $secretPattern = '-----BEGIN [A-Z ]*PRIVATE KEY-----|AKIA[0-9A-Z]{16}|gh[pousr]_[A-Za-z0-9]{20,}|sk-[A-Za-z0-9]{20,}'
    $secretFindings = $scanFiles | Select-String -Pattern $secretPattern
    if ($secretFindings) {
        throw "Possiveis segredos encontrados:`n$($secretFindings -join "`n")"
    }

    $status = @(& git status --short)
    if ($RequireClean -and $status) {
        throw "O worktree precisa estar limpo antes da publicacao:`n$($status -join "`n")"
    }

    Write-Host 'Auditoria de portfolio aprovada.' -ForegroundColor Green
    Write-Host "Arquivos versionados analisados: $($trackedFiles.Count)"
    Write-Host "Arquivos publicaveis analisados: $($publishableFiles.Count)"
    if ($status) {
        Write-Host 'O worktree ainda possui mudancas; use -RequireClean apos o commit final.' -ForegroundColor Yellow
    }
}
finally {
    Pop-Location
}
