@echo off
:: Verifica se o script está sendo executado como administrador
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo Solicitar permissões de administrador...
    powershell -Command "Start-Process '%~f0' -Verb RunAs"
    exit /b
)

:: Definir as variáveis de caminho
set caminho1=D:\SERVER\03 - ALPHA SERVER\ADMO_SERVER_V9_TEST_04
set caminho2=src\Source\Distribution\DigitalWorldOnline.Admin\bin\Debug\net7.0\DigitalWorldOnline.Admin.exe

:: Espera 1 segundo
timeout /t 1 /nobreak > nul

:: Executa o programa
start "" "%caminho1%\%caminho2%"
