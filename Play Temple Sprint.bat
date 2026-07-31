@echo off
title Temple Sprint - Local Play
set "PROJECT=C:\Users\dagar\OneDrive\Desktop\Temple Run"
set "UNITY=C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Unity.exe"

if not exist "%UNITY%" (
  echo ERROR: Unity 6000.5.6f1 not found
  pause
  exit /b 1
)

echo Opening Temple Sprint and entering Play Mode...
start "" "%UNITY%" -projectPath "%PROJECT%" -executeMethod TempleSprint.EditorTools.ProjectSetup.OpenAndPlay
exit /b 0
