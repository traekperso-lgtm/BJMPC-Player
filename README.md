# BJMPC Player

**Lecteur multimédia moderne pour Windows 10/11** — BJMPC Informatique

Le design du Lecteur multimédia de Windows 11 (WinUI 3, effet Mica, thème clair/sombre), le moteur de
**mpv** (tous les formats audio et vidéo, comme VLC) et une sortie audio **WASAPI**, en mode exclusif
si vous le souhaitez.

## Installation

**Le plus simple :** dans les [Releases](https://github.com/traekperso-lgtm/BJMPC-Player/releases), téléchargez
`BJMPC-Player-<version>-Setup.exe` et lancez-le (aucun droit administrateur requis ; désinstallation depuis
Paramètres > Applications). L'installateur est signé « BJMPC Informatique » : pour que Windows reconnaisse
l'éditeur, lancez d'abord une fois `Installer-certificat-BJMPC.cmd`, fourni à côté (voir `certificat/LISEZMOI-certificat.txt`).

**Autre méthode :** double-clic sur **`installer.bat`** : il télécharge la dernière version publiée, l'installe dans
`%LOCALAPPDATA%\Programs\BJMPC Player`, crée les raccourcis (Bureau, menu Démarrer, *Envoyer vers*)
et ajoute BJMPC Player au menu **Ouvrir avec** pour les formats audio et vidéo.
Pour en faire le lecteur par défaut : clic droit sur un film > Ouvrir avec > BJMPC Player > « Toujours ».

`installer.bat --compiler` compile depuis les sources (installe le SDK .NET 8 si besoin).

## Fonctions

- **Tous les formats** grâce à libmpv/FFmpeg : MKV, MP4, AVI, MOV, WebM, TS/M2TS, FLAC, MP3, AAC,
  Opus, WAV, ALAC, DSD, AC-3, E-AC-3, DTS, TrueHD, listes M3U/PLS, radios et flux réseau
  (et vidéos en ligne si `yt-dlp` est installé).
- **Vidéo** : décodage par la carte graphique (H.264, HEVC, AV1, VP9…), rendu Direct3D 11,
  plein écran (F11 ou double-clic), **mini-lecteur** toujours au premier plan.
- **Audio WASAPI** : choix de la sortie, **mode exclusif** (bit à bit, sans le mélangeur de Windows),
  égalisation du volume.
- **Pistes** : choix de la piste audio et des sous-titres, sous-titres `.srt` voisins chargés
  automatiquement, ajout d'un fichier de sous-titres, langues préférées (français par défaut).
- **Bibliothèque** : Musique (titres, albums avec pochettes, recherche) et Vidéos (vignettes),
  dossiers au choix ; étiquettes mises en cache.
- **File d'attente** : lecture aléatoire, répétition (tout / un), « lire ensuite », réorganisation
  par glisser-déposer.
- **Informations techniques** (touche I) : sortie audio réellement utilisée (WASAPI), mode exclusif
  ou partagé, **format envoyé à la carte son**, décodage vidéo matériel ou logiciel, images perdues.
  Pour vérifier le mode exclusif : un FLAC 96 kHz / 24 bits doit arriver à la carte en
  « 96 kHz · 24 bits » ; en partagé, Windows le convertit à son format de mixage (souvent 48 kHz flottant).
- **Reprise** des vidéos longues là où vous vous êtes arrêté.
- Glisser-déposer de fichiers, dossiers et sous-titres sur la fenêtre ; une seule instance
  (un fichier ouvert depuis l'Explorateur arrive dans la fenêtre déjà ouverte).

### Raccourcis

| Touche | Action |
|---|---|
| Espace | Lecture / pause |
| ← / → | −10 s / +10 s (Ctrl : ±60 s) |
| ↑ / ↓ | Volume (sur la page Lecture en cours ou la vidéo) |
| M | Muet |
| N / P | Suivant / précédent |
| F, F11, double-clic | Plein écran |
| Échap | Quitter le plein écran / le mini-lecteur |
| Ctrl+O | Ouvrir des fichiers |
| I | Informations techniques |
| Touches multimédia du clavier | Lecture, suivant, précédent, arrêt |

## Son 3D avec Spatial3D

> Le mode exclusif réserve la carte son à un seul programme. Quand Spatial3D est ouvert, BJMPC Player
> reste donc en mode partagé (sinon il couperait le son 3D, ou VB-Cable pour les autres applications).
> Si l'exclusif est refusé parce qu'une autre application tient déjà la carte, le lecteur repasse
> tout seul en partagé au lieu de rester muet, et le signale.


Si [Spatial3D](https://github.com/traekperso-lgtm/Spatial3D) est installé et son mode temps réel
actif, tout ce que lit BJMPC Player est spatialisé (périphérique sur « Automatique », mode exclusif
désactivé). Paramètres > Audio propose un bouton pour ouvrir Spatial3D.

## Architecture

| Dossier | Rôle |
|---|---|
| `src/BJMPCPlayer.Core` | Portable, sans dépendance : liaison libmpv (`Mpv/`), file d'attente, listes M3U/PLS, réglages JSON |
| `src/BJMPCPlayer` | Application WinUI 3 : fenêtre, pages, service de lecture, bibliothèque, fenêtre vidéo |
| `tests/BJMPCPlayer.Core.Tests` | Tests du cœur, dont une **vraie lecture** avec libmpv (pause, déplacement, pistes, sous-titres) |
| `tools/install.ps1` | Installation (version publiée ou compilation) |

La vidéo est dessinée par libmpv dans une fenêtre Win32 enfant posée sur la zone vidéo ; les menus
de la barre de lecture sont des fenêtres indépendantes pour rester visibles par-dessus.

## Compilation

GitHub Actions compile, teste et publie à chaque envoi. Pour une nouvelle version, augmentez
`<Version>` dans `src/BJMPCPlayer/BJMPCPlayer.csproj` : la Release `vX.Y.Z` est créée automatiquement
avec le zip prêt à l'emploi.

En local : `installer.bat --compiler`, ou
`dotnet publish src/BJMPCPlayer/BJMPCPlayer.csproj -c Release -r win-x64 -p:Platform=x64 -o publish`
après avoir placé `libmpv-2.dll` dans `deps/mpv/`.

## Licences

Moteur : [libmpv](https://mpv.io) et [FFmpeg](https://ffmpeg.org) (versions Windows de
shinchiro/mpv-winbuild-cmake, sous licence GPLv3 car elles incluent des composants GPL).
Interface : Windows App SDK / WinUI 3 (MIT).
