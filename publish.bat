@echo off
echo Publishing FusionClientUpdater as single self-contained EXE...

dotnet publish src\FusionClientUpdater.csproj ^
    -c Release ^
    -r win-x64 ^
    --self-contained true ^
    -p:PublishSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -p:EnableCompressionInSingleFile=true ^
    -p:DebugType=none ^
    -o publish

echo.
if %ERRORLEVEL% == 0 (
    echo Done! Output: publish\FusionClientUpdater.exe
) else (
    echo Build failed with error code %ERRORLEVEL%
)
pause
