@echo off
chcp 65001 >nul
cd /d "%~dp0.."
if exist "publish" rd /s /q "publish"
dotnet publish CustomizedBrowser.csproj -c Release -r win-x64 --self-contained true -p:PublishTrimmed=false -o "publish"
echo.
echo 便携版已输出到: %CD%\publish
pause
