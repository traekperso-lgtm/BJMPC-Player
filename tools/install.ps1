# BJMPC Player - installation (appele par installer.bat)
# 1. Telecharge la derniere version publiee sur GitHub (rien a compiler)
#    ou, avec -Compiler (ou si aucune version n'est publiee), compile depuis les sources
# 2. Installe dans %LOCALAPPDATA%\Programs\BJMPC Player
# 3. Raccourcis Bureau / menu Demarrer / Envoyer vers + entree "Ouvrir avec" pour les formats audio et video
param([switch]$Compiler)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$repo = 'traekperso-lgtm/BJMPC-Player'
$dest = Join-Path $env:LOCALAPPDATA 'Programs\BJMPC Player'
$src = Split-Path $PSScriptRoot -Parent
$tmp = Join-Path $env:TEMP 'bjmpc-player-install'

function Step($m) { Write-Host "`n== $m" -ForegroundColor Cyan }
function Fail($m) { Write-Host "`nECHEC : $m" -ForegroundColor Red; exit 1 }

Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory $tmp | Out-Null
$publish = Join-Path $tmp 'publish'

# ---------------------------------------------------------------- 1. version publiee
$zip = $null
if (-not $Compiler) {
    try {
        $rel = Invoke-RestMethod "https://api.github.com/repos/$repo/releases/latest"
        $asset = $rel.assets | Where-Object { $_.name -like 'BJMPC-Player-*-win64.zip' } | Select-Object -First 1
        if ($asset) {
            Step "Telechargement de $($asset.name)"
            $zip = Join-Path $tmp $asset.name
            Invoke-WebRequest $asset.browser_download_url -OutFile $zip
            Expand-Archive $zip -DestinationPath $publish
        }
    } catch { Write-Host "Aucune version publiee trouvee : compilation depuis les sources." }
}

# ---------------------------------------------------------------- 1 bis. compilation
if (-not $zip) {
    Step 'SDK .NET 8'
    $hasSdk = $false
    try { $hasSdk = (& dotnet --list-sdks 2>$null) -match '^8\.' } catch {}
    if (-not $hasSdk) {
        if (-not (Get-Command winget -ErrorAction SilentlyContinue)) { Fail 'winget introuvable : installez "Programme d''installation d''application" depuis le Microsoft Store.' }
        winget install -e --id Microsoft.DotNet.SDK.8 --source winget --silent --accept-package-agreements --accept-source-agreements
        $env:PATH = [Environment]::GetEnvironmentVariable('PATH', 'Machine') + ';' + [Environment]::GetEnvironmentVariable('PATH', 'User')
    }
    Step 'libmpv (moteur de lecture)'
    $mpvDir = Join-Path $src 'deps\mpv'
    if (-not (Test-Path (Join-Path $mpvDir 'libmpv-2.dll'))) {
        $rel = Invoke-RestMethod 'https://api.github.com/repos/shinchiro/mpv-winbuild-cmake/releases/latest'
        $asset = $rel.assets | Where-Object { $_.name -match '^mpv-dev-x86_64-\d{8}-git-[0-9a-f]+\.7z$' } | Select-Object -First 1
        if (-not $asset) { Fail 'libmpv introuvable sur GitHub (shinchiro/mpv-winbuild-cmake)' }
        $archive = Join-Path $tmp $asset.name
        Invoke-WebRequest $asset.browser_download_url -OutFile $archive
        New-Item -ItemType Directory $mpvDir -Force | Out-Null
        tar -xf $archive -C $mpvDir
        if (-not (Test-Path (Join-Path $mpvDir 'libmpv-2.dll'))) { Fail 'extraction de libmpv impossible (Windows 11 requis pour lire les .7z)' }
    }
    Step 'Compilation'
    dotnet publish (Join-Path $src 'src\BJMPCPlayer\BJMPCPlayer.csproj') -c Release -r win-x64 -p:Platform=x64 -o $publish
    if ($LASTEXITCODE -ne 0) { Fail 'compilation' }
}
if (-not (Test-Path (Join-Path $publish 'BJMPCPlayer.exe'))) { Fail 'BJMPCPlayer.exe absent du paquet' }

# ---------------------------------------------------------------- 2. installation
Step "Installation dans $dest"
Get-Process BJMPCPlayer -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500
New-Item -ItemType Directory $dest -Force | Out-Null
robocopy $publish $dest /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -ge 8) { Fail 'copie des fichiers' }
$exe = Join-Path $dest 'BJMPCPlayer.exe'

# ---------------------------------------------------------------- 3. raccourcis et "Ouvrir avec"
Step 'Raccourcis'
$shell = New-Object -ComObject WScript.Shell
function Lnk($dir, $name) {
    $l = $shell.CreateShortcut((Join-Path $dir "$name.lnk"))
    $l.TargetPath = $exe; $l.WorkingDirectory = $dest; $l.IconLocation = "$exe,0"; $l.Description = 'Lecteur multimedia BJMPC'
    $l.Save()
}
Lnk ([Environment]::GetFolderPath('Desktop')) 'BJMPC Player'
Lnk ([Environment]::GetFolderPath('Programs')) 'BJMPC Player'
Lnk ([Environment]::GetFolderPath('SendTo')) 'BJMPC Player'

$k = 'HKCU:\Software\Classes\Applications\BJMPCPlayer.exe'
New-Item "$k\shell\open\command" -Force | Out-Null
Set-Item "$k\shell\open\command" -Value "`"$exe`" `"%1`""
New-ItemProperty $k -Name FriendlyAppName -Value 'BJMPC Player' -Force | Out-Null
New-Item "$k\DefaultIcon" -Force | Out-Null
Set-Item "$k\DefaultIcon" -Value "$exe,0"
New-Item "$k\SupportedTypes" -Force | Out-Null
$exts = '.mp3 .flac .wav .m4a .aac .ogg .opus .wma .aiff .ape .wv .mka .ac3 .eac3 .dts .thd .dsf .m4b .mid ' +
        '.mkv .mp4 .m4v .avi .mov .wmv .webm .flv .ts .m2ts .mts .mpg .mpeg .vob .ogv .3gp .divx .m3u .m3u8 .pls'
foreach ($e in $exts.Split(' ', [StringSplitOptions]::RemoveEmptyEntries)) {
    New-ItemProperty "$k\SupportedTypes" -Name $e -Value '' -Force | Out-Null
    New-Item "HKCU:\Software\Classes\$e\OpenWithList\BJMPCPlayer.exe" -Force | Out-Null
}

Write-Host "`nBJMPC Player est installe." -ForegroundColor Green
Write-Host 'Clic droit sur un film ou une musique > Ouvrir avec > BJMPC Player (cochez "Toujours" pour en faire le lecteur par defaut).'
$r = Read-Host 'Lancer BJMPC Player maintenant ? (O/N)'
if ($r -match '^[oOyY]') { Start-Process $exe }
