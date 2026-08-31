# Win11 Update Blocker

Windows-11-Desktop-App zum gezielten Steuern von Windows Update: Feature-, Sicherheits-, Qualitäts-, Treiber- und optionale Updates lassen sich einzeln erlauben oder blockieren. Ein optionaler Hintergrund-Dienst hält die Einstellungen dauerhaft aktiv.

## Funktionen

- WPF-Oberfläche mit dunklem Theme
- Fünf Update-Kategorien einzeln schaltbar
- Hintergrund-Dienst (Watchdog alle 5 Minuten)
- Autostart und System-Tray
- Inno-Setup-Installer
- Saubere Deinstallation mit Wiederherstellung der Windows-Einstellungen

## Voraussetzungen

- Windows 11 (22H2+), x64
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) zum Bauen
- Optional: [Inno Setup 6](https://jrsoftware.org/isinfo.php) für den Installer

## Build

```powershell
dotnet build
```

Installer erstellen:

```powershell
.\installer\build-installer.ps1
```

Ausgabe: `installer\output\Win11 Update Blocker Setup.exe`

## Installation

1. Setup als Administrator ausführen
2. App starten, gewünschte Update-Typen wählen
3. **Einstellungen anwenden** klicken

Konfiguration und Log:

- `%ProgramData%\Win11UpdateBlocker\config.json`
- `%ProgramData%\Win11UpdateBlocker\blocker.log`

## Deinstallation

Über „Programme hinzufügen oder entfernen“. Der Deinstaller stellt Registry, Dienste und Autostart wieder her.

Manuelles Zurücksetzen:

```text
Win11UpdateBlocker.exe --restore
```

## Hinweis

Das Blockieren von Sicherheitsupdates erhöht das Risiko für Sicherheitslücken. Nur bewusst und zeitlich begrenzt einsetzen.

## Lizenz

MIT — siehe [LICENSE](LICENSE).
