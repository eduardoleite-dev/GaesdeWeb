param(
    [string]$ResourceGroup = "GaesdeWeb_group",
    [string]$AppName = "gaesdeweb"
)

$projectFile = Get-ChildItem -Path . -Filter *.csproj -File | Select-Object -First 1

if ($null -eq $projectFile) {
    Write-Host "ERRO: Nenhum arquivo .csproj foi encontrado na raiz do projeto." -ForegroundColor Red
    exit 1
}

$projectName = [System.IO.Path]::GetFileNameWithoutExtension($projectFile.Name)

Write-Host "[1/4] Limpando pasta de publicação e zip antigo..." -ForegroundColor Yellow
Remove-Item -Recurse -Force .\bin, .\obj, .\publish, .\deploy.zip -ErrorAction SilentlyContinue

Write-Host "[2/4] Executando dotnet publish de $projectName..." -ForegroundColor Yellow
dotnet publish $projectFile.FullName -c Release -o .\publish /p:UseAppHost=false

if ($LASTEXITCODE -ne 0 -or -not (Test-Path ".\publish\$projectName.dll")) {
    Write-Host "ERRO: Falha na compilação. $projectName.dll não encontrado." -ForegroundColor Red
    exit 1
}

Write-Host "[3/4] Compactando arquivos no formato compatível com Linux..." -ForegroundColor Yellow
# O tar nativo do Windows gera um pacote que pode ser extraído pelo Azure App Service Linux.
tar -a -cf deploy.zip -C publish *

if ($LASTEXITCODE -ne 0 -or -not (Test-Path .\deploy.zip)) {
    Write-Host "ERRO: Não foi possível gerar o arquivo deploy.zip." -ForegroundColor Red
    exit 1
}

Write-Host "[4/4] Enviando pacote ao Azure App Service..." -ForegroundColor Yellow
az webapp deploy `
    --resource-group $ResourceGroup `
    --name $AppName `
    --src-path .\deploy.zip `
    --type zip `
    --clean true

if ($LASTEXITCODE -eq 0) {
    Write-Host "Deploy concluído com sucesso!" -ForegroundColor Green
} else {
    Write-Host "ERRO: O envio para o Azure falhou." -ForegroundColor Red
    exit 1
}