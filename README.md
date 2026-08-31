# Win11 Update Blocker

Windows-11-Desktop-App zum gezielten Steuern von Windows Update: Feature-, Sicherheits-, Qualitäts-, Treiber- und optionale Updates lassen sich einzeln erlauben oder blockieren. Ein optionaler Hintergrund-Dienst hält die Einstellungen dauerhaft aktiv.

## Download

**Installer (empfohlen):** [Neuestes Release](https://github.com/ShadowDev1002/win11-update-blocker/releases/latest)

1. `Win11 Update Blocker Setup.exe` herunterladen
2. Als Administrator ausführen
3. App starten, gewünschte Update-Typen wählen
4. **Einstellungen anwenden** klicken

Kein Build nötig — einfach installieren und nutzen.

## Voraussetzungen

- Windows 11 (22H2+), x64
- Administratorrechte für Installation und zum Anwenden der Update-Einstellungen

## Funktionen

- WPF-Oberfläche mit dunklem Theme
- Fünf Update-Kategorien einzeln schaltbar
- Hintergrund-Dienst (Watchdog alle 5 Minuten)
- Autostart und System-Tray
- Saubere Deinstallation mit Wiederherstellung der Windows-Einstellungen

## Konfiguration

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

## Entwicklung

Quellcode bauen und Installer erzeugen ist nur für Entwickler relevant — siehe `installer/build-installer.ps1`.

## Lizenz

MIT — siehe [LICENSE](LICENSE).
