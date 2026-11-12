@echo off
taskkill /F /IM KeyboardHint.exe >nul 2>&1
"C:\Program Files\dotnet\dotnet.exe" build src\KeyboardHint\KeyboardHint.csproj -c Release -r win-x64 --self-contained true -v minimal
exit /b %ERRORLEVEL%
