@echo off
setlocal

set EXE_PATH=%~dp0KeyASIO.exe
set SHORTCUT_PATH=%USERPROFILE%\Desktop\KeyASIO.lnk

powershell -Command "$WshShell = New-Object -ComObject WScript.Shell; $Shortcut = $WshShell.CreateShortcut('%SHORTCUT_PATH%'); $Shortcut.TargetPath = '%EXE_PATH%'; $Shortcut.WorkingDirectory = '%~dp0'; $Shortcut.Save()"

echo Desktop shortcut created successfully!
echo Location: %SHORTCUT_PATH%
pause