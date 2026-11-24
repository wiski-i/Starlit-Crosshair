@echo off
setlocal
pushd "%~dp0.." || exit /b 1
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    >&2 echo C# compiler missing. Enable .NET Framework 4.x and try again.
    popd
    exit /b 1
)
set "TEST_EXE=%TEMP%\Crosshair-regression-%RANDOM%-%RANDOM%.exe"
"%CSC%" /nologo /codepage:65001 /langversion:5 /warnaserror+ /main:Regression /win32manifest:app.manifest /win32icon:assets\Crosshair.ico /resource:assets\Crosshair-title.png,Crosshair.Title.png "/out:%TEST_EXE%" AssemblyInfo.cs Program.cs Overlay.cs Glass.cs Optics.cs ProfilePack.cs GameProfiles.cs GameRulesDialog.cs VisibilityScenes.cs ProfileLibrary.cs UiText.cs Workflow.cs WorkflowDialogs.cs tests\Regression.cs
if errorlevel 1 (
    popd
    exit /b 1
)
"%TEST_EXE%" %*
set "RESULT=%ERRORLEVEL%"
del "%TEST_EXE%"
popd
exit /b %RESULT%
