@echo off
title Sistema de Control de Inventario y Stock - Rosario
cd /d "%~dp0"
taskkill /F /IM ControlInventario.exe >nul 2>&1
echo =========================================================
echo   Iniciando Sistema de Control de Inventario y Stock...
echo   Abriendo http://localhost:5267 en tu navegador...
echo   (Deja esta ventana abierta mientras uses la pagina)
echo =========================================================
start /b powershell -NoProfile -WindowStyle Hidden -Command "Start-Sleep -Seconds 2; Start-Process 'http://localhost:5267'"
"bin\Debug\net10.0\ControlInventario.exe" --urls "http://localhost:5267"
pause
