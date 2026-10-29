@echo off
setlocal
chcp 65001 >nul
cd /d "%~dp0"
title Guarana - compilando

echo.
echo  Guarana - compilacao
echo  ====================
echo.

where dotnet >nul 2>nul
if errorlevel 1 goto :nodotnet
dotnet --list-sdks | findstr /b "10." >nul
if errorlevel 1 goto :nodotnet

echo  [1/2] Rodando os testes...
dotnet run --project tests\Guarana.Tests.csproj -c Release
if errorlevel 1 goto :fail

echo.
echo  [2/2] Gerando o executavel...
dotnet publish Guarana.csproj -c Release -r win-x64 -o dist
if errorlevel 1 goto :fail

echo.
echo  Pronto: %~dp0dist\Guarana.exe
echo  Abrindo o Guarana...
start "" "%~dp0dist\Guarana.exe"
echo.
pause
exit /b 0

:nodotnet
echo  O SDK do .NET 10 nao foi encontrado.
echo  Instale com o comando abaixo (ou em https://dotnet.microsoft.com/download):
echo.
echo     winget install Microsoft.DotNet.SDK.10
echo.
echo  Depois, rode este arquivo de novo.
echo.
pause
exit /b 1

:fail
echo.
echo  A compilacao falhou. Copie as mensagens acima e cole na conversa com o Claude.
echo.
pause
exit /b 1
