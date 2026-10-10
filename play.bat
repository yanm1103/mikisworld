@echo off
rem Builds (if needed) and runs the game using the portable .NET SDK in tools\dotnet.
rem Needs JAR\MikisWorld.jar. Extra arguments are passed to the game, e.g.:  play.bat --scale 5
rem Mods in the "mods" folder of the repository are found too (see docs\MODDING.md).
setlocal
set "ROOT=%~dp0"
set "DOTNET_CLI_HOME=%ROOT%tools\.dotnet-home"
set "DOTNET_NOLOGO=1"
set "DOTNET_CLI_TELEMETRY_OPTOUT=1"
if not exist "%ROOT%tools\dotnet\dotnet.exe" (
  echo Portable .NET SDK not found in tools\dotnet. See README ^(Reproduzindo o ambiente^).
  exit /b 1
)
"%ROOT%tools\dotnet\dotnet.exe" run --project "%ROOT%port\MikisWorld.Game" -- --mods-dir "%ROOT%mods" %*
