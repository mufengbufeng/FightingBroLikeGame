@echo off
setlocal EnableExtensions

rem Project-local palace. Generated drawers/vectors stay gitignored.
set "REPO=%~dp0..\.."
if defined CLAUDE_PROJECT_DIR if exist "%CLAUDE_PROJECT_DIR%\tools\mcp\run-mempalace.cmd" set "REPO=%CLAUDE_PROJECT_DIR%"
for %%I in ("%REPO%") do set "REPO=%%~fI"
set "PALACE=%REPO%\.mempalace"

if not exist "%PALACE%" mkdir "%PALACE%"

where uvx >nul 2>nul
if not errorlevel 1 (
  uvx --from mempalace mempalace-mcp --palace "%PALACE%"
  exit /b %ERRORLEVEL%
)

where python >nul 2>nul
if not errorlevel 1 (
  python -m mempalace.mcp_server --palace "%PALACE%"
  exit /b %ERRORLEVEL%
)

echo mempalace: install uv from https://docs.astral.sh/uv/ or run `pip install mempalace`. 1>&2
echo https://github.com/MemPalace/mempalace 1>&2
exit /b 1
