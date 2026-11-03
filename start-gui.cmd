@echo off
setlocal
set "ROOT=%~dp0"
set "EXE=%ROOT%dist\gui\SparkasseCsvMerger.Gui.exe"

if not exist "%EXE%" (
    echo Die GUI-EXE wird einmalig veroeffentlicht, das dauert ein bis zwei Minuten...
    call "%ROOT%publish-gui.cmd"
    if errorlevel 1 exit /b %ERRORLEVEL%
)

start "" "%EXE%"
exit /b 0
