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
set caminho2=src\Source\Distribution\DigitalWorldOnline.Account.Host\bin\Debug\net7.0\DigitalWorldOnline.Account.exe
set caminho3=src\Source\Distribution\DigitalWorldOnline.Character.Host\bin\Debug\net7.0\DigitalWorldOnline.Character.exe
set caminho4=src\Source\Distribution\DigitalWorldOnline.Game.Host\bin\Debug\net7.0\DigitalWorldOnline.Game.exe
set caminho5=src\Source\Distribution\DigitalWorldOnline.Routine.Host\DigitalWorldOnline.Routine\bin\Debug\net7.0\DigitalWorldOnline.Routine.exe

:: Espera 1 segundo
timeout /t 1 /nobreak > nul

:: Executa o Account.Host
start "" "%caminho1%\%caminho2%"

:: Espera 2 segundo
timeout /t 2 /nobreak > nul

:: Executa o Character.Host
start "" "%caminho1%\%caminho3%"

:: Espera 2 segundo
timeout /t 2 /nobreak > nul

:: Executa o Game.Host
start "" "%caminho1%\%caminho4%"

:: Espera 2 segundo
timeout /t 2 /nobreak > nul

:: Executa o Routine.Host
start "" "%caminho1%\%caminho5%"
