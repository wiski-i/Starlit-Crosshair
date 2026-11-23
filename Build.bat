@echo off
setlocal
pushd "%~dp0" || exit /b 1
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    >&2 echo C# compiler missing. Enable .NET Framework 4.x and try again.
    popd
    exit /b 1
)
"%CSC%" /nologo /codepage:65001 /optimize+ /target:winexe /win32manifest:app.manifest /win32icon:assets\Crosshair.ico /resource:assets\Crosshair-title.png,Crosshair.Title.png /out:OzelCrosshair.exe Program.cs Overlay.cs Glass.cs Optics.cs ProfilePack.cs GameProfiles.cs GameRulesDialog.cs VisibilityScenes.cs ProfileLibrary.cs UiText.cs Workflow.cs WorkflowDialogs.cs
set "RESULT=%ERRORLEVEL%"
popd
exit /b %RESULT%
