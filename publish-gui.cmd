@echo off
setlocal
set "ROOT=%~dp0"

dotnet publish "%ROOT%src\SparkasseCsvMerger.Gui\SparkasseCsvMerger.Gui.csproj" ^
    -c Release -r win-x64 --self-contained true ^
    -p:PublishSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -o "%ROOT%dist\gui"

if errorlevel 1 exit /b %ERRORLEVEL%

echo.
echo GUI veroeffentlicht: %ROOT%dist\gui\SparkasseCsvMerger.Gui.exe
