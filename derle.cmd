@echo off
rem K55 Kontrol derleme betigi.
rem Uygulama: Visual Studio derleme araclarindaki MSBuild ile (WPF arayuzu).
rem Protokol testi: ayni araclardaki Roslyn C# derleyicisiyle.
setlocal
cd /d "%~dp0"

set VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe
if not exist "%VSWHERE%" (
  echo Visual Studio derleme araclari bulunamadi.
  exit /b 1
)
for /f "usebackq delims=" %%i in (`"%VSWHERE%" -latest -products * -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`) do set MSBUILD=%%i
if not defined MSBUILD (
  echo MSBuild bulunamadi.
  exit /b 1
)

"%MSBUILD%" K55Kontrol.csproj -nologo -v:minimal -p:Configuration=Release || exit /b 1

for %%m in ("%MSBUILD%") do set CSC=%%~dpmRoslyn\csc.exe
"%CSC%" /nologo /langversion:latest /platform:x64 /target:exe /codepage:65001 /out:bin\ProtokolTesti.exe ^
  src\Hid.cs src\K55.cs src\Renk.cs test\ProtokolTesti.cs || exit /b 1

"%CSC%" /nologo /langversion:latest /platform:x64 /target:exe /codepage:65001 /out:bin\KaynakTesti.exe /r:System.Core.dll ^
  src\Renk.cs src\K55.cs src\Hid.cs src\SesAnalizcisi.cs src\EkranOrnekleyici.cs src\SistemGostergesi.cs src\TusDinleyici.cs ^
  test\KaynakTesti.cs || exit /b 1

"%CSC%" /nologo /langversion:latest /platform:x64 /target:exe /codepage:65001 /out:bin\TusGozlemi.exe ^
  /r:System.Windows.Forms.dll /r:System.Core.dll src\Hid.cs src\K55.cs src\Renk.cs test\TusGozlemi.cs || exit /b 1

"%CSC%" /nologo /langversion:latest /platform:x64 /target:exe /codepage:65001 /out:bin\HafizaTesti.exe ^
  /r:System.Runtime.Serialization.dll /r:System.Core.dll src\Hid.cs src\K55.cs src\Renk.cs src\DonanimAydinlatmasi.cs ^
  src\Ayarlar.cs src\Makro.cs src\GTuslari.cs src\SistemGostergesi.cs test\HafizaTesti.cs || exit /b 1

"%CSC%" /nologo /langversion:latest /platform:x64 /target:exe /codepage:65001 /out:bin\IsikTesti.exe ^
  src\Hid.cs src\K55.cs src\Renk.cs test\IsikTesti.cs || exit /b 1

"%CSC%" /nologo /langversion:latest /platform:x64 /target:exe /codepage:65001 /out:bin\BirimTesti.exe ^
  /r:System.Runtime.Serialization.dll /r:System.Core.dll src\Hid.cs src\K55.cs src\Renk.cs src\DonanimAydinlatmasi.cs ^
  src\Ayarlar.cs src\Makro.cs src\GTuslari.cs src\SistemGostergesi.cs test\BirimTesti.cs || exit /b 1
bin\BirimTesti.exe || exit /b 1

echo Derleme tamam: bin\K55Kontrol.exe
