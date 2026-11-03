@echo off
setlocal
set "ROOT=%~dp0"
set "EXE=%ROOT%src\SparkasseCsvMerger\bin\Release\net10.0\SparkasseCsvMerger.exe"

if exist "%EXE%" (
    "%EXE%" %*
    exit /b %ERRORLEVEL%
)

dotnet run --project "%ROOT%src\SparkasseCsvMerger\SparkasseCsvMerger.csproj" -- %*
exit /b %ERRORLEVEL%
