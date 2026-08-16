@echo off
setlocal EnableExtensions

rem Resolve the Unity project root from this script, then allow Claude to override.
set "REPO=%~dp0.."
if defined CLAUDE_PROJECT_DIR if exist "%CLAUDE_PROJECT_DIR%\.codedb-mcp\codedb-mcp.toml" set "REPO=%CLAUDE_PROJECT_DIR%"
for %%I in ("%REPO%") do set "REPO=%%~fI"
set "CONFIG=%REPO%\.codedb-mcp\codedb-mcp.toml"

if not exist "%CONFIG%" (
  echo codedb-mcp: missing config "%CONFIG%" 1>&2
  exit /b 1
)

set "EXE="
if defined CODEDB_MCP_EXE if exist "%CODEDB_MCP_EXE%" set "EXE=%CODEDB_MCP_EXE%"

if not defined EXE if exist "%USERPROFILE%\.claude\skills\codedb-mcp\assets\codebase-mcp.exe" set "EXE=%USERPROFILE%\.claude\skills\codedb-mcp\assets\codebase-mcp.exe"
if not defined EXE if exist "%USERPROFILE%\.codex\skills\codedb-mcp\assets\codebase-mcp.exe" set "EXE=%USERPROFILE%\.codex\skills\codedb-mcp\assets\codebase-mcp.exe"
if not defined EXE if exist "%USERPROFILE%\.agents\skills\codedb-mcp\assets\codebase-mcp.exe" set "EXE=%USERPROFILE%\.agents\skills\codedb-mcp\assets\codebase-mcp.exe"

if not defined EXE (
  for /f "delims=" %%I in ('where codebase-mcp.exe 2^>nul') do (
    set "EXE=%%I"
    goto :run
  )
)

:run
if not defined EXE (
  echo codedb-mcp: codebase-mcp.exe not found. 1>&2
  echo Install the codedb-mcp skill or set CODEDB_MCP_EXE to the executable. 1>&2
  echo https://github.com/killop/codedb-mcp 1>&2
  exit /b 1
)

"%EXE%" --config "%CONFIG%" mcp "%REPO%"
exit /b %ERRORLEVEL%
