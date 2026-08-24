@echo off
setlocal

for /f "usebackq delims=" %%I in (`powershell.exe -NoProfile -Command "$git = (Get-Command git.exe -ErrorAction Stop).Source; Join-Path (Split-Path (Split-Path $git -Parent) -Parent) 'bin\bash.exe'"`) do set "VECXY_GIT_BASH=%%I"

if not defined VECXY_GIT_BASH (
    echo Error: Git for Windows was not found in PATH. 1>&2
    exit /b 1
)

if not exist "%VECXY_GIT_BASH%" (
    echo Error: Git Bash was not found at "%VECXY_GIT_BASH%". 1>&2
    exit /b 1
)

"%VECXY_GIT_BASH%" "%~dp0build.sh" %*
exit /b %ERRORLEVEL%
