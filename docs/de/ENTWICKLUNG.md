# Entwicklungshandbuch

[Zurück zur README](../../README.de.md) · [English](../en/DEVELOPMENT.md)

## 1. Einrichtung

1. Das [.NET 10 SDK](https://dotnet.microsoft.com/download) installieren.
2. Die MAUI-Workload installieren: `dotnet workload install maui`.
3. Das Repository klonen und wiederherstellen: `dotnet restore TaskManager.sln`.

## 2. Bauen, starten, testen

```powershell
# alles bauen (was die CI tut)
dotnet build TaskManager.sln -c Release

# die App starten
dotnet run --project TaskManager.App -f net10.0-windows10.0.19041.0

# die Tests ausführen
dotnet test TaskManager.Tests/TaskManager.Tests.csproj
```

In Rider oder Visual Studio: die Projektmappe öffnen, `TaskManager.App` wählen, Start.

Eine laufende App sperrt `TaskManager.App.exe`; beenden Sie sie vor dem nächsten Build, sonst endet er mit der
Meldung „Datei wird von einem anderen Prozess verwendet“.

Die Tests decken Core und Infrastructure ab: Domänenregeln, Dienste, Repositories gegen SQLite (im Speicher und in
temporären Dateien), Sicherung und Wiederherstellung, Migrationen (ein Test schlägt fehl, wenn sich das Modell ohne
Migration geändert hat) und die Übersetzungstabellen, die als Quelltext in das Testprojekt eingebunden sind
(`TaskManager.Tests.csproj`, `Linked`).

## 3. Wo die App ihre Daten während der Entwicklung ablegt

Am selben Ort wie im Alltag: `%LOCALAPPDATA%\TaskManager` (Datenbank, `settings.json`, Protokolle). Für einen
sauberen Start die App schließen und dort `taskmanager.db*` löschen; sie wird beim nächsten Start neu angelegt.
*Daten → Mit Demodaten füllen* liefert einen vollständigen Satz Beispielaufgaben.

## 4. Datenbankmigrationen

Das EF-Core-Werkzeug einmal installieren: `dotnet tool install --global dotnet-ef`.

```powershell
# eine Migration hinzufügen, nachdem ein Modell oder eine Konfiguration geändert wurde
dotnet ef migrations add <Name> --project TaskManager.Infrastructure --output-dir Persistence/Migrations
```

`TaskManagerDbContextFactory` liefert den Design-Zeit-Kontext. Migrationen werden beim Start der App angewendet.
Die Wiederherstellungsprüfung erkennt Migrationen selbst: Eine Sicherung einer neueren Version (mit unbekannter
Migration) wird abgelehnt.

## 5. Konventionen

- **Schichtung**: Core darf weder EF Core noch MAUI noch Oberflächentypen referenzieren. Infrastructure setzt
  Core-Schnittstellen um. Die App spricht über `ServiceRunner` mit den Core-Diensten, nie mit dem `DbContext`.
- **Zeit**: `TimeProvider` einfügen; in der Logik kein `DateTime.Now`. Tests nutzen `FakeTimeProvider` (er läuft
  nur vorwärts).
- **Regeln in der Domäne**: Validierung und Zustandsänderungen gehören zu `TaskItem` und den Diensten, mit einer
  Meldung auf Russisch (der Übersetzungsschlüssel); die Oberfläche zeigt sie übersetzt.
- **MVVM**: Zustand und Befehle im ViewModel; Code-Behind nur für Ansichtsfragen. Datenzugriff über
  `ViewModelBase.RunAsync`.
- **Fehler**: auf Vorgangsebene abfangen, mit `ILogger` protokollieren, eine übersetzte Meldung zeigen. Nie eine
  Ausnahme die App beenden lassen.
- **Farben** nur über Design-Token (`DynamicResource`), nie als Literale in einer Seite. Elemente, die sich im Code
  selbst färben, abonnieren `ThemeManager.Changed` mit einer Instanzmethode (das Ereignis ist schwach).
- **Kommentare und XML-Dokumentation** sind auf Englisch geschrieben. Russischer Text steht im Code nur als Übersetzungsschlüssel
  und in den daraus gebildeten Meldungen für die Oberfläche.
- **Commits** sind klein und auf Englisch beschrieben (`feat:`, `fix:`, `docs:`, `test:`).

## 6. Erweiterung

### Einen Bildschirm hinzufügen

Die Seite in `Pages` und ihr ViewModel in `ViewModels` anlegen (von `ViewModelBase` erben), beide in `MauiProgram`
registrieren (`AddTransient`), in `AppShell.xaml` ein `ShellContent` mit Route und in `SidebarViewModel` einen
Eintrag hinzufügen. Daten in `OnAppearing` über das ViewModel laden.

### Einen Text hinzufügen oder ändern

Den russischen Text im Markup mit `{loc:Extension 'Text'}` oder im Code mit `Loc.T("Text")` verwenden und eine
Zeile `["Text", "English", "Deutsch", "Українська"]` in die passende Tabelle in `Services/Translations.*.cs`
eintragen. Der Übersetzungstest schlägt fehl, wenn eine Zeile unvollständig ist oder Platzhalter abweichen.

### Eine Sprache hinzufügen

Ein `Language` zu `Loc.Languages` hinzufügen, jeder Übersetzungszeile eine Spalte (der Test nennt, was fehlt) und die
Kultur für Datum und Zahlen.

### Ein Design hinzufügen

Einen Wert an `ThemeChoice` (`UserSettings.cs`; die Zahlen stehen in `settings.json`, es wird also nur angehängt)
und an `ThemeMode` anhängen, in `ThemeCatalog.All` eine Zeile (Name als russischer Schlüssel plus die drei
Vorschaufarben), in `ThemeCatalog.Palette` die zehn Palettenfarben und den zweiten Akzent `A2` sowie eine
Übersetzung des Namens ergänzen. Helle Designs stehen in `ThemeManager.IsLight`.

### Eine Spalte oder Tabelle hinzufügen

Entität und Konfiguration ändern, eine Migration anlegen (Abschnitt 4), das Repository anpassen und, falls der
Wert exportiert werden soll, `ExportService` / `ImportService` und die Exportversion.

## 7. Kontinuierliche Integration

`.github/workflows/build.yml` baut die Projektmappe auf `windows-latest` mit .NET 10 und der MAUI-Workload und
führt die Tests bei Pushes auf den Hauptzweig und bei Pull Requests aus.

## 8. Stand und Ausblick

Fertig: der vollständige Aufgabenablauf (Status, Unteraufgaben, Abhängigkeiten, Wiederholung, Verlauf),
Erinnerungen, Papierkorb, Tags, Statistik, Export und Import, Sicherung und Wiederherstellung, vier Sprachen, neun
Designs, 192 automatisierte Tests.

Ideen für später: ein Windows-Installer oder MSIX-Paket, UI-Tests für die MAUI-Schicht, weitere Exportformate,
gespeicherte Filter, Anhänge.
