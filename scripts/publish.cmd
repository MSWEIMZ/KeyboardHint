@echo off
taskkill /F /IM KeyboardHint.exe >nul 2>&1
"C:\Program Files\dotnet\dotnet.exe" publish src\KeyboardHint\KeyboardHint.csproj -c Release -o publish
exit /b %ERRORLEVEL%
