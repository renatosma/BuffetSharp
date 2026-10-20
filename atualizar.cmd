@echo off
rem Compila, gera (opcionalmente) uma migration nos 4 bancos e roda os testes.
rem   atualizar.cmd                 compila e testa
rem   atualizar.cmd NomeDaMigration compila, gera a migration "NomeDaMigration" nos 4 bancos (se ainda nao existir) e testa
rem Toda a saida vai para atualizacao-log.txt (na pasta do projeto, fora do git).
setlocal
cd /d "%~dp0"
set "LOG=%~dp0atualizacao-log.txt"
set "MIGRATION=%~1"
echo Inicio %date% %time% > "%LOG%"

echo Compilando...
echo === BUILD === >> "%LOG%"
dotnet build -nologo >> "%LOG%" 2>&1 || goto erro

if "%MIGRATION%"=="" goto testes
echo Gerando a migration %MIGRATION%...
dotnet tool restore >> "%LOG%" 2>&1
for %%B in (SqlServer PostgreSql MySql Oracle) do (
  if not exist "src\BuffetSharp.Infrastructure.%%B\Migrations\*_%MIGRATION%.cs" (
    echo === MIGRATION %%B === >> "%LOG%"
    dotnet ef migrations add %MIGRATION% --project src\BuffetSharp.Infrastructure.%%B --startup-project src\BuffetSharp.Infrastructure.%%B -o Migrations >> "%LOG%" 2>&1 || goto erro
  )
)

:testes
echo Rodando os testes (pode levar alguns minutos)...
echo === TESTES === >> "%LOG%"
dotnet test -nologo >> "%LOG%" 2>&1 || goto erro
echo === FIM === >> "%LOG%"
echo.
echo Concluido. Pode fechar esta janela.
pause
exit /b 0

:erro
echo === ERRO === >> "%LOG%"
echo.
echo Houve um erro. Veja o atualizacao-log.txt. Pode fechar esta janela.
pause
exit /b 1
