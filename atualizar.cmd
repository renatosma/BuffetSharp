@echo off
rem Compila, gera as migrations das novidades nos 4 bancos e roda os testes.
rem Toda a saida vai para atualizacao-log.txt (na pasta do projeto).
cd /d "%~dp0"
set "LOG=%~dp0atualizacao-log.txt"
echo Inicio %date% %time% > "%LOG%"

if exist "src\BuffetSharp.Application\Servicos\ModeloContrato.cs" del "src\BuffetSharp.Application\Servicos\ModeloContrato.cs"

echo Compilando...
echo === BUILD === >> "%LOG%"
dotnet build -nologo >> "%LOG%" 2>&1 || goto erro

echo Gerando migrations...
dotnet tool restore >> "%LOG%" 2>&1
for %%B in (SqlServer PostgreSql MySql Oracle) do (
  if not exist "src\BuffetSharp.Infrastructure.%%B\Migrations\*_CondicoesModelosContatos.cs" (
    echo === MIGRATION %%B === >> "%LOG%"
    dotnet ef migrations add CondicoesModelosContatos --project src\BuffetSharp.Infrastructure.%%B --startup-project src\BuffetSharp.Infrastructure.%%B -o Migrations >> "%LOG%" 2>&1 || goto erro
  )
)

echo Rodando os testes (pode levar alguns minutos)...
echo === TESTES === >> "%LOG%"
dotnet test -nologo >> "%LOG%" 2>&1
echo === FIM === >> "%LOG%"
echo.
echo Concluido. Pode fechar esta janela.
pause
exit /b

:erro
echo === ERRO === >> "%LOG%"
echo.
echo Houve um erro. Pode fechar esta janela.
pause
