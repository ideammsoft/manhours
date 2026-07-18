@echo off
echo =============================================
echo  manHours Build + Sign + Installer
echo =============================================

set "ST="
if exist "C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\signtool.exe" set "ST=C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\signtool.exe"
if not defined ST if exist "C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64\signtool.exe" set "ST=C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64\signtool.exe"
if not defined ST if exist "C:\Program Files (x86)\Windows Kits\10\bin\10.0.22000.0\x64\signtool.exe" set "ST=C:\Program Files (x86)\Windows Kits\10\bin\10.0.22000.0\x64\signtool.exe"
if not defined ST if exist "C:\Program Files (x86)\Windows Kits\10\bin\10.0.19041.0\x64\signtool.exe" set "ST=C:\Program Files (x86)\Windows Kits\10\bin\10.0.19041.0\x64\signtool.exe"
if not defined ST (
    echo [ERROR] signtool.exe not found. Install Windows SDK.
    pause
    exit /b 1
)
echo [INFO] signtool: %ST%

echo.
echo [0/5] Auto-increment version...
python bump_version.py
if errorlevel 1 (
    echo [ERROR] version update failed.
    pause
    exit /b 1
)

echo.
echo [1/5] dotnet publish (self-contained x86)...
if exist dist\manHours.exe del /f /q dist\manHours.exe

for /f "tokens=*" %%v in ('python -c "import re; t=open('manHours/manHours.csproj',encoding='utf-8').read(); print(re.search(r'<InformationalVersion>([^<]+)', t).group(1))"') do set VER=%%v
echo Version to embed: %VER%

dotnet publish manHours\manHours.csproj -c Release -r win-x86 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:InformationalVersion=%VER% --output dist --nologo -v quiet
if errorlevel 1 (
    echo [ERROR] dotnet publish failed.
    pause
    exit /b 1
)
echo [OK] dist\manHours.exe done  (v%VER%)

echo.
echo [2/5] Signing dist\manHours.exe...
"%ST%" sign /a /s my /n "mmsoft" /fd sha256 /tr http://timestamp.digicert.com /td sha256 /v "%~dp0dist\manHours.exe"
if errorlevel 1 (
    echo [ERROR] Signing dist\manHours.exe failed.
    pause
    exit /b 1
)
echo [OK] dist\manHours.exe signed

echo.
echo [3/5] Creating installer...
set "ISCC=C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" set "ISCC=C:\Program Files\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" (
    echo [ERROR] Inno Setup 6 not found.
    pause
    exit /b 1
)
"%ISCC%" setup_manhours.iss
if errorlevel 1 (
    echo [ERROR] Installer creation failed.
    pause
    exit /b 1
)
echo [OK] dist\setup_manhours.exe done

echo.
echo [4/5] Signing dist\setup_manhours.exe...
"%ST%" sign /a /s my /n "mmsoft" /fd sha256 /tr http://timestamp.digicert.com /td sha256 /v "%~dp0dist\setup_manhours.exe"
if errorlevel 1 (
    echo [ERROR] Signing dist\setup_manhours.exe failed.
    pause
    exit /b 1
)
echo [OK] dist\setup_manhours.exe signed

echo.
echo [5/5] Done
echo =============================================
echo  dist\manHours.exe        signed  (v%VER%)
echo  dist\setup_manhours.exe  signed
echo =============================================

:ask_ftp
set FTP_CHOICE=
set /p FTP_CHOICE=Upload to FTP? [Y/N]:
if /i "%FTP_CHOICE%"=="Y" goto :do_ftp
if /i "%FTP_CHOICE%"=="N" goto :end
goto :ask_ftp

:do_ftp
if not exist "C:\_mm_project\ftp_config.bat" (
    echo [ERROR] ftp_config.bat not found
    goto :end
)
call "C:\_mm_project\ftp_config.bat"
echo [FTP] Uploading...
python "C:\_mm_project\ftp_upload.py" %FTP_HOST% %FTP_USER% %FTP_PASS% "%~dp0dist\manHours.exe" "/autoupdate_new/manHours/manHours.exe" "%~dp0dist\setup_manhours.exe" "/program/setup_manhours.exe" "C:\_mm_project\autoupdate_new\config.json" "/autoupdate_new/config.json"
if errorlevel 1 (
    echo [ERROR] FTP upload failed.
) else (
    echo [OK] FTP upload done.
)

:end
