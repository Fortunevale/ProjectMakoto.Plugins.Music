@echo off

REM User-definable variables

set PluginDir=C:\path\to\plugin\directory
set PluginName=ProjectMakoto.Plugins.Music


REM Build procedure, leave alone

for %%I in (.) do set CurrDirName=%%~nxI

echo Deleting conflicting files..
del /S build.zip >NUL
del /S %CurrDirName%.pmpl >NUL
rmdir /S /Q bin >NUL
rmdir /S /Q build >NUL
echo Building project..
dotnet clean
dotnet restore
dotnet publish %PluginName%.sln --property:PublishDir="build" --framework net9.0
if %errorlevel% neq 0 goto error
echo Zipping project to build.zip..
dotnet run --project "Tools\CreateZipFolder\CreateZipFolder.csproj" -- "build" "build.zip"
if %errorlevel% neq 0 goto error

rename build.zip %CurrDirName%.pmpl

echo Creating manifest..
set current_dir=%cd%
cd ..\deps
dotnet ProjectMakoto.dll --build-manifests %current_dir%
cd %current_dir%

echo.
echo.
echo Created pmpl-File at %cd%\%CurrDirName%.pmpl!

echo Cleaning up..
rmdir /S /Q bin >NUL
rmdir /S /Q build >NUL

if "%PluginDir%"=="C:\path\to\plugin\directory" (
	echo.
	echo Tip: You can define an output directory in this file by replacing PluginDir with the appropriate path.
	goto skipcopy
	)

echo Copying to %PluginDir%..
del /S %PluginDir%\%CurrDirName%.pmpl >NUL
timeout /t 1 >NUL
move %CurrDirName%.pmpl %PluginDir%\%CurrDirName%.pmpl
if %errorlevel% neq 0 goto error

:skipcopy
exit /b 0

:error
echo Something went wrong!
pause >NUL
exit /b %errorlevel%