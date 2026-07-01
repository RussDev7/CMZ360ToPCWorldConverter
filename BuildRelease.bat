:::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::
:: Build CMZ360ToPCWorldConverter Via MSBuild                    ::
:: GitHub https://github.com/RussDev7/CMZ360ToPCWorldConverter   ::
:: Developed and maintained by RussDev7 / Discord: dannyruss     ::
:::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::
@echo off
setlocal EnableExtensions

REM ================================================================
REM Build Settings
REM ================================================================

set "VersionPrefix=1.0.0.0"
set "ProjectName=CMZ360ToPCWorldConverter"
set "PackageName=%ProjectName%-%VersionPrefix%"

set "Configuration=Release"
set "Platform=x86"

REM Build the project directly so this works even if MSBuild does not support .slnx.
set "ProjectFile=%~dp0CMZ360ToPCWorldConverter\CMZ360ToPCWorldConverter.csproj"

REM Project output path from the .csproj:
REM bin\x86\Release\
set "OutputDir=%~dp0CMZ360ToPCWorldConverter\bin\Release"

REM Final staged release folder.
set "BuildRoot=%~dp0Build\Release"
set "StageDir=%BuildRoot%\%PackageName%"
set "ZipFile=%BuildRoot%\%PackageName%.zip"

REM Expected location of vswhere.
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"

echo.
echo ================================================================
echo Building %ProjectName%
echo Configuration: %Configuration%
echo Platform:      %Platform%
echo ================================================================
echo.

REM ================================================================
REM Find MSBuild
REM ================================================================

if not exist "%VSWHERE%" (
    echo ERROR: vswhere.exe was not found.
    echo Expected:
    echo "%VSWHERE%"
    echo.
    echo Install Visual Studio or Build Tools with MSBuild support.
    pause
    exit /b 1
)

for /f "usebackq tokens=*" %%I in (`
    "%VSWHERE%" -latest ^
                -products * ^
                -requires Microsoft.Component.MSBuild ^
                -find MSBuild\**\Bin\MSBuild.exe
`) do (
    set "MSBUILD=%%I"
)

if not defined MSBUILD (
    echo ERROR: MSBuild.exe was not found.
    echo Make sure Visual Studio or Build Tools includes MSBuild.
    pause
    exit /b 1
)

echo Found MSBuild:
echo "%MSBUILD%"
echo.

REM ================================================================
REM Build Project
REM ================================================================

if not exist "%ProjectFile%" (
    echo ERROR: Project file was not found:
    echo "%ProjectFile%"
    pause
    exit /b 1
)

"%MSBUILD%" "%ProjectFile%" ^
    /m ^
    /t:Rebuild ^
    /p:Configuration=%Configuration% ^
    /p:Platform=%Platform%

if errorlevel 1 (
    echo.
    echo ERROR: Build failed.
    pause
    exit /b 1
)

REM ================================================================
REM Stage Release Files
REM ================================================================

echo.
echo ================================================================
echo Staging release files
echo ================================================================
echo.

if exist "%StageDir%" rmdir /s /q "%StageDir%"
mkdir "%StageDir%"

REM Copy compiled output.
robocopy "%OutputDir%" "%StageDir%" /E >nul
if errorlevel 8 (
    echo ERROR: Failed to copy build output.
    pause
    exit /b 1
)

REM Copy root docs if present.
if exist "%~dp0README.md" copy /y "%~dp0README.md" "%StageDir%\README.md" >nul
if exist "%~dp0LICENSE" copy /y "%~dp0LICENSE" "%StageDir%\LICENSE" >nul

REM Optional: include tools folder for release users.
if exist "%~dp0CMZ360ToPCWorldConverter\Tools" (
    robocopy "%~dp0CMZ360ToPCWorldConverter\Tools" "%StageDir%\Tools" /E >nul
    if errorlevel 8 (
        echo ERROR: Failed to copy Tools folder.
        pause
        exit /b 1
    )
)

REM ================================================================
REM Create ZIP
REM ================================================================

echo.
echo ================================================================
echo Creating ZIP
echo ================================================================
echo.

if exist "%ZipFile%" del /f /q "%ZipFile%"

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
    "Compress-Archive -Path '%StageDir%\*' -DestinationPath '%ZipFile%' -Force"

if errorlevel 1 (
    echo.
    echo ERROR: Failed to create ZIP.
    pause
    exit /b 1
)

echo.
echo ================================================================
echo Build complete!
echo ================================================================
echo.
echo Release folder:
echo "%StageDir%"
echo.
echo Release ZIP:
echo "%ZipFile%"
echo.
pause
exit /b 0