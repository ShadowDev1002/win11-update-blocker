# Changelog

Alle wesentlichen Änderungen an diesem Projekt werden in dieser Datei dokumentiert.

## [1.0.3] - 2026-09-01

### Geändert

- UI-Theme an Windows-11-Einstellungen angelehnt (Graphit statt Navy, Fluent-Cyan-Akzent)
- Flachere Flächen und kleinere Eckenradien
- Info-/Warnhinweise ohne blau getönte Banner-Hintergründe

## [1.0.2] - 2026-08-31

### Behoben

- Toggles setzen sich nicht mehr beim Ändern zurück (Auto-Refresh überschreibt ungespeicherte Änderungen nicht mehr)
- Falsche Drift-Erkennung bei Teilblockaden behoben (z. B. nur Treiber erlaubt)
- Drift-Hinweis verschwindet während du Einstellungen anpasst und erscheint erst wieder nach dem Anwenden, falls nötig

## [1.0.1] - 2026-08-31

### Hinzugefügt

- Startup-Update-Hinweis und Tray-Benachrichtigung bei neuer Version
- Automatische Update-Prüfung alle 6 Stunden

### Behoben

- Installer: kein erzwungener Neustart bei Upgrade
- Pending-File-Rename-Bereinigung vor Installation

## [1.0.0] - 2026-08-31

### Hinzugefügt

- Erste öffentliche Version
- Granulare Steuerung von Feature-, Sicherheits-, Qualitäts-, Treiber- und optionalen Updates
- Hintergrund-Dienst mit Watchdog
- GitHub-Release-Update-Checker in der App
- Inno-Setup-Installer
