@echo off
setlocal
cd /d "%~dp0"

echo [QuickVolume] Compiling Native C# Build from src/...

set CSC="C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist %CSC% (
    set CSC="C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
)

if not exist %CSC% (
    echo [QuickVolume] ERROR: .NET Framework 4.0 csc.exe compiler not found!
    exit /b 1
)

set ICON_PARAM=
if exist "..\assets\quickvolume.ico" (
    set ICON_PARAM=/win32icon:"..\assets\quickvolume.ico"
) else if exist "..\assets\QuickVolume.ico" (
    set ICON_PARAM=/win32icon:"..\assets\QuickVolume.ico"
) else if exist "quickvolume.ico" (
    set ICON_PARAM=/win32icon:"quickvolume.ico"
)

%CSC% /nologo /target:winexe /optimize+ /platform:anycpu %ICON_PARAM% /r:System.dll,System.Drawing.dll,System.Windows.Forms.dll /out:"..\QuickVolume.exe" "QuickVolume.cs"

if %errorlevel% neq 0 (
    echo [QuickVolume] Compilation FAILED.
    exit /b %errorlevel%
)

echo [QuickVolume] Compilation SUCCESSFUL: ..\QuickVolume.exe
