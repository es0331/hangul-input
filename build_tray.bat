@echo off
set CSC=%windir%\Microsoft.NET\Framework\v3.5\csc.exe
if not exist "%CSC%" set CSC=%windir%\Microsoft.NET\Framework\v4.0.30319\csc.exe
"%CSC%" /nologo /codepage:65001 /target:winexe /out:HangulInputTray.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll HangulInputTray.cs
if exist HangulInputTray.exe (echo Build OK: HangulInputTray.exe) else (echo Build failed)
pause
