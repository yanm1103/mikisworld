@echo off
rem Builds the distributable game: dist\MikisWorldCollection.exe (one self-contained file, no .NET install needed).
rem The game files are NOT included: put MikisWorld.jar and MikisWorld2.jar in a JAR folder next to the exe.
setlocal
set "ROOT=%~dp0.."
set "DOTNET_CLI_HOME=%ROOT%\tools\.dotnet-home"
set "DOTNET_NOLOGO=1"
set "DOTNET_CLI_TELEMETRY_OPTOUT=1"
"%ROOT%\tools\dotnet\dotnet.exe" publish "%ROOT%\port\MikisWorld.Game" -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none ^
  -o "%ROOT%\dist" || exit /b 1
echo.
echo Done: %ROOT%\dist\MikisWorldCollection.exe
