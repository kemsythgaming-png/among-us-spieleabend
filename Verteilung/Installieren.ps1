# Installiert "Among Us Spieleabend":
# 1. Sucht die eigene Steam-Installation von Among Us
# 2. Kopiert sie nach "...\steamapps\common\Among Us Spieleabend" (das Original bleibt unveraendert)
# 3. Legt BepInEx und den Mod hinein und erstellt eine Desktop-Verknuepfung
# Erneut ausfuehren nach jedem Among-Us-Update, damit die Mod-Kopie wieder aktuell ist.

param(
    [string]$Spielordner,      # optional: Pfad zum Steam-Ordner "Among Us"
    [string]$Ziel,             # optional: Zielordner der Mod-Kopie
    [switch]$KeineVerknuepfung
)

$ErrorActionPreference = 'Stop'
$paket = $PSScriptRoot

function Fertig($text, $code) {
    Write-Host ""
    Write-Host $text
    if (-not $env:SPIELEABEND_NOPAUSE) { Read-Host "Enter druecken zum Schliessen" | Out-Null }
    exit $code
}

Write-Host "=== Among Us Spieleabend - Installation ===" -ForegroundColor Magenta

# --- Steam-Spiel finden ---
if (-not $Spielordner) {
    $steam = $null
    try { $steam = (Get-ItemProperty 'HKCU:\Software\Valve\Steam').SteamPath -replace '/', '\' } catch { }
    $bibliotheken = @()
    if ($steam) {
        $bibliotheken += $steam
        $vdf = Join-Path $steam 'steamapps\libraryfolders.vdf'
        if (Test-Path $vdf) {
            foreach ($m in Select-String -Path $vdf -Pattern '"path"\s+"(.+?)"') {
                $bibliotheken += ($m.Matches[0].Groups[1].Value -replace '\\\\', '\')
            }
        }
    }
    $Spielordner = $bibliotheken | ForEach-Object { Join-Path $_ 'steamapps\common\Among Us' } |
        Where-Object { Test-Path (Join-Path $_ 'Among Us.exe') } | Select-Object -First 1
}

if (-not $Spielordner -or -not (Test-Path (Join-Path $Spielordner 'Among Us.exe'))) {
    Fertig "Among Us (Steam) wurde nicht gefunden. Bitte mit -Spielordner 'Pfad\zu\Among Us' starten." 1
}
Write-Host "Steam-Spiel: $Spielordner"

# --- 64-Bit pruefen (die BepInEx-Version im Paket ist fuer 64-Bit) ---
$bytes = [IO.File]::ReadAllBytes((Join-Path $Spielordner 'GameAssembly.dll'))
$pe = [BitConverter]::ToInt32($bytes, 60)
if ([BitConverter]::ToUInt16($bytes, $pe + 4) -ne 0x8664) {
    Fertig "Diese Among-Us-Version ist nicht 64-Bit - das Paket passt nicht dazu." 1
}

if (-not $Ziel) { $Ziel = Join-Path (Split-Path $Spielordner) 'Among Us Spieleabend' }
if ((Resolve-Path $Spielordner).Path.TrimEnd('\') -eq [IO.Path]::GetFullPath($Ziel).TrimEnd('\')) {
    Fertig "Ziel darf nicht der Steam-Ordner selbst sein." 1
}

if (Get-Process -Name 'Among Us' -ErrorAction SilentlyContinue) {
    Fertig "Bitte zuerst Among Us schliessen und dann erneut installieren." 1
}

# --- Spiel kopieren (vorhandenes BepInEx im Ziel bleibt erhalten) ---
Write-Host "Kopiere Spiel nach: $Ziel  (ca. 1 GB, kann etwas dauern)"
robocopy $Spielordner $Ziel /E /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -ge 8) { Fertig "Kopieren fehlgeschlagen (robocopy Code $LASTEXITCODE)." 1 }

# --- BepInEx + Mod hinein ---
Write-Host "Installiere BepInEx und Mod ..."
robocopy (Join-Path $paket 'Mod') $Ziel /E /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -ge 8) { Fertig "Mod-Dateien konnten nicht kopiert werden (robocopy Code $LASTEXITCODE)." 1 }

# --- Desktop-Verknuepfung ---
if (-not $KeineVerknuepfung) {
    $desktop = [Environment]::GetFolderPath('Desktop')
    $ws = New-Object -ComObject WScript.Shell
    $s = $ws.CreateShortcut((Join-Path $desktop 'Among Us Spieleabend.lnk'))
    $s.TargetPath = Join-Path $Ziel 'Among Us.exe'
    $s.WorkingDirectory = $Ziel
    $s.IconLocation = (Join-Path $Ziel 'Among Us.exe') + ',0'
    $s.Description = 'Among Us mit Spieleabend-Mod'
    $s.Save()
    Write-Host "Desktop-Verknuepfung 'Among Us Spieleabend' erstellt."
}

Fertig ("Fertig! Starte das Spiel ueber die Verknuepfung (Steam muss laufen).`n" +
        "Der erste Start dauert 1-3 Minuten (BepInEx richtet sich ein).`n" +
        "Das normale Among Us ueber Steam bleibt unveraendert.") 0
