@echo off
chcp 65001 >nul
setlocal
title Prepare Inventor Web Viewer Setup

set "ROOT=%~dp0.."
set "BUNDLE=%ROOT%\InventorWebViewer.bundle\Contents"
set "REL=%ROOT%\InventorWebViewer\bin\Release\InventorWebViewer.dll"
set "DBG=%ROOT%\InventorWebViewer\bin\Debug\InventorWebViewer.dll"

echo ============================================
echo  Prepare bundle for Inno Setup
echo ============================================
echo.

if not exist "%BUNDLE%" (
  mkdir "%BUNDLE%" 2>nul
)

if exist "%REL%" (
  copy /Y "%REL%" "%BUNDLE%\InventorWebViewer.dll" >nul
  echo [OK] Copied Release DLL → bundle\Contents\
  goto :done
)

if exist "%DBG%" (
  copy /Y "%DBG%" "%BUNDLE%\InventorWebViewer.dll" >nul
  echo [OK] Copied Debug DLL → bundle\Contents\
  goto :done
)

echo [ERROR] InventorWebViewer.dll not found.
echo.
echo Build the solution first:
echo   1. Open InventorWebViewer.sln in Visual Studio 2022
echo   2. Configuration = Release , Platform = x64  (or AnyCPU with x64 target)
echo   3. Build → Rebuild Solution
echo   4. Run this script again
echo.
pause
exit /b 1

:done
if not exist "%BUNDLE%\InventorWebViewer.addin" (
  echo [WARN] InventorWebViewer.addin missing in bundle\Contents
)
if not exist "%ROOT%\InventorWebViewer.bundle\PackageContents.xml" (
  echo [WARN] PackageContents.xml missing
)

echo.
echo Bundle ready. Next steps:
echo   1. Open Installer\InventorWebViewer.iss in Inno Setup Compiler
echo   2. Build → Compile  (or press Ctrl+F9)
echo   3. Output: Installer\Output\InventorWebViewer_Setup_1.1.0.exe
echo.
pause
