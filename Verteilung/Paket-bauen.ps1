# Baut das Weitergabe-Paket dist\AmongUsSpieleabend-<Version>.zip
# Inhalt: Installer + BepInEx (aus der eigenen Mod-Kopie) + Mod-DLL. KEINE Spieldateien (Among Us Mod Policy).

param([string]$GameDir = 'D:\Installiertes\Steam\steamapps\common\Among Us Spieleabend')

$ErrorActionPreference = 'Stop'
$projekt = Split-Path $PSScriptRoot
$version = ([xml](Get-Content (Join-Path $projekt 'AmongUsSpieleabend.csproj'))).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1

dotnet build (Join-Path $projekt 'AmongUsSpieleabend.csproj') -c Release -p:GameDir="$GameDir" -p:SkipInstall=true | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Build fehlgeschlagen' }
$dll = Join-Path $projekt 'bin\Release\net6.0\AmongUsSpieleabend.dll'

$bau = Join-Path $projekt 'dist\paket'
if (Test-Path $bau) { Remove-Item $bau -Recurse -Force }
$mod = Join-Path $bau 'Mod'
New-Item -ItemType Directory -Force "$mod\BepInEx\plugins", "$mod\BepInEx\patchers" | Out-Null

# Nur BepInEx selbst - interop, cache, unity-libs und Logs erzeugt BepInEx beim Freund selbst
Copy-Item "$GameDir\BepInEx\core" "$mod\BepInEx\core" -Recurse
Copy-Item "$GameDir\dotnet" "$mod\dotnet" -Recurse
Copy-Item "$GameDir\winhttp.dll", "$GameDir\doorstop_config.ini" $mod
Set-Content "$mod\steam_appid.txt" '945360' -NoNewline -Encoding ascii
Copy-Item $dll "$mod\BepInEx\plugins"

Copy-Item "$PSScriptRoot\Installieren.ps1", "$PSScriptRoot\Installieren.bat", "$PSScriptRoot\LIESMICH.txt" $bau

$zip = Join-Path $projekt "dist\AmongUsSpieleabend-$version.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path "$bau\*" -DestinationPath $zip
Write-Host "Paket: $zip ($([math]::Round((Get-Item $zip).Length / 1MB, 1)) MB)"

# ---------- Launcher: eine EXE mit eingebettetem Mod-Paket ----------
Add-Type -AssemblyName System.IO.Compression.FileSystem
$modZip = Join-Path $projekt 'dist\ModPackage.zip'
if (Test-Path $modZip) { Remove-Item $modZip -Force }
[IO.Compression.ZipFile]::CreateFromDirectory($mod, $modZip)

dotnet build (Join-Path $projekt 'Launcher\Launcher.csproj') -c Release | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Launcher-Build fehlgeschlagen' }
$launcher = Join-Path $projekt 'dist\Among Us Spieleabend.exe'
Copy-Item (Join-Path $projekt 'Launcher\bin\Release\net48\AmongUsSpieleabend.Launcher.exe') $launcher -Force
Write-Host "Launcher: $launcher ($([math]::Round((Get-Item $launcher).Length / 1MB, 1)) MB) - diese Datei an Freunde schicken"

# Fuer GitHub-Releases: die Mod-DLL einzeln (der Launcher laedt nur diese Datei bei Updates)
Copy-Item $dll (Join-Path $projekt 'dist\AmongUsSpieleabend.dll') -Force
Write-Host "Release-Datei: dist\AmongUsSpieleabend.dll (Version $version)"
