# Architektur

[Zurück zur README](../../README.de.md) · [English](../en/ARCHITECTURE.md)

## 1. Überblick

Drei Quellprojekte und ein Testprojekt. Die Abhängigkeiten zeigen nur nach innen:

```
TaskManager.App  ──►  TaskManager.Infrastructure  ──►  TaskManager.Core
 (MAUI, MVVM)          (EF Core, SQLite, Sicherung)      (Domäne, Dienste)
                                  ▲
TaskManager.Tests ────────────────┘   (verweist auch auf Core)
```

- **Core** hat keinen Verweis auf EF Core, MAUI oder einen Oberflächentyp. Es beschreibt, was die Anwendung tut,
  und die Schnittstellen, die es braucht (`ITaskRepository`, `IUnitOfWork`, `IReminderNotifier`,
  `IDatabaseBackup`, …).
- **Infrastructure** setzt diese Schnittstellen mit EF Core und SQLite um.
- **App** ist die Windows-MAUI-Anwendung. Sie spricht über einen Scope je Vorgang mit den Core-Diensten und nie
  direkt mit dem `DbContext`.

## 2. Core

| Ordner | Inhalt |
|--------|--------|
| `Domain` | `TaskItem` (das Aggregat), `Tag`, `TaskDependency`, `TaskHistoryEntry`, `SentReminder`, die Enums (`Priority`, `TaskStatus`, `RecurrenceFrequency`, `RecurrenceMode`, `ReminderKind`), `RecurrenceRule`, der Statusautomat und die Domänenausnahmen |
| `Abstractions` | Schnittstellen für Repositories, Unit of Work, Benachrichtigung und Sicherung; `ConcurrencyConflictException` |
| `Services` | `TaskService`, `TrashService`, `TagService`, `StatisticsService`, `ReminderChecker`, `ReminderPolicy`, `DemoDataService`, `RecurrenceCalculator`, `DependencyGraph` |
| `Specifications` | zusammensetzbare Abfragebedingungen (`TaskFilter` → Spezifikation), von den Repositories genutzt |
| `Querying` | `TaskFilter`, `TaskQuery`, `TaskSort`, `PagedResult<T>` |
| `Export` | `ExportService`, `ImportService`, `MarkdownRenderer`, das versionierte JSON-Dokument und der Importbericht |

Zentrale Regeln, die hier und nur hier leben:

- **Statusautomat** (`TaskStateMachine`): die einzige Stelle, die festlegt, welche Übergänge erlaubt sind.
  Erledigte und abgebrochene Aufgaben verlassen ihren Status nur durch *Wieder öffnen* (erledigt → in Arbeit,
  abgebrochen → offen).
- **Unteraufgaben**: Die Tiefe ist auf drei Ebenen begrenzt (`TaskItem.MaxDepth`); eine Aufgabe kann nicht
  erledigt werden, solange eine Unteraufgabe offen ist; beim Verschieben wird die Höhe des verschobenen Teilbaums
  geprüft.
- **Abhängigkeiten**: Eine Aufgabe kann nicht nach *offen*, *in Arbeit* oder *erledigt* wechseln, solange eine
  Abhängigkeit unerledigt ist; eine *offene* Aufgabe mit unerledigter Abhängigkeit wird *blockiert* und
  automatisch wieder freigegeben. Beim Hinzufügen läuft eine Zyklusprüfung (`DependencyGraph`).
- **Wiederholung**: `RecurrenceCalculator` berechnet das nächste Vorkommen (täglich, wöchentlich mit Wochentagen,
  monatlich, jährlich, alle *N*). `GenerateDueOccurrencesAsync` holt verpasste Vorkommen mit einem Ankerdatum nach.
- **Erinnerungen**: `ReminderPolicy` ordnet die verbleibende Zeit *fällig in 24 Stunden*, *fällig in 1 Stunde*
  oder *überfällig* zu; `ReminderChecker` zeigt die noch nicht gezeigten und merkt sie sich.
- **Verlauf**: Jede Änderung schreibt der Dienst als `TaskHistoryEntry`, nicht die Oberfläche.
- **Zeit** kommt immer von einem eingefügten `TimeProvider`.

## 3. Infrastructure

- **`TaskManagerDbContext`** mit je einer `IEntityTypeConfiguration` pro Entität und fünf Code-First-Migrationen
  (Anfangsschema, Suchtext, Wiederholungsanker, Nachfolger-Kennzeichen, gesendete Erinnerungen).
- **Weiches Löschen**: `TaskItem.IsDeleted` plus ein globaler Abfragefilter. Normale Abfragen sehen gelöschte
  Aufgaben nie; das Papierkorb-Repository nutzt `IgnoreQueryFilters()`. Zeilen, die von einer Aufgabe abhängen
  (`TaskDependency`), erben den Filter über ihre Navigation. Das zählt beim endgültigen Löschen: Auch das Löschen
  der Abhängigkeitszeilen muss die Filter ignorieren.
- **Optimistische Nebenläufigkeit**: `TaskItem.RowVersion` ist ein Konkurrenz-Token; eine veraltete Änderung
  löst `ConcurrencyConflictException` aus, die die Oberfläche erklärt und mit Neuladen beantwortet.
- **Datumswerte** sind `DateTimeOffset`, als UTC-Ticks gespeichert (`DateTimeOffsetToUtcTicksConverter`), damit
  Sortierung und Bereichsabfragen in SQLite funktionieren.
- **Suche** nutzt die denormalisierte Spalte `SearchText` (Titel und Beschreibung als ein normalisierter Text,
  die Suche ignoriert also die Groß- und Kleinschreibung).
- **`SqliteTuningInterceptor`** setzt bei jeder Verbindung `synchronous = NORMAL` und `temp_store = MEMORY`;
  Fremdschlüssel erzwingt SQLite (`ON DELETE RESTRICT` für die Verknüpfung zur übergeordneten Aufgabe und für die
  Seite „wovon abhängt“ einer Abhängigkeit). Beim endgültigen Löschen werden die zu entfernenden Aufgaben deshalb
  zuerst voneinander gelöst.
- **`SqliteDatabaseBackup`**: eine konsistente Kopie einer laufenden Datenbank; `InspectAsync` prüft eine Datei
  (Integritätsprüfung, Schema, Migrationen einer neueren Version werden abgelehnt); eine Wiederherstellung wird
  neben der Datenbank *vorbereitet* und beim nächsten Start angewendet, bevor irgendetwas die Datei öffnet, mit
  einer Sicherheitskopie der alten.
- **`ReminderService`** ist ein `BackgroundService`: Pro Intervall erzeugt er fällige wiederkehrende Aufgaben und
  prüft Fristen, jeweils in einem eigenen Scope. `ReminderOptions.Enabled` schaltet nur die Fristprüfung ab
  (Einstellung *Nie*); wiederkehrende Aufgaben werden weiter erzeugt.
- Alles wird von `AddInfrastructure(databasePath)` registriert.

## 4. Anwendung (MAUI)

### 4.1 Aufbau

| Ordner | Inhalt |
|--------|--------|
| `Pages` | eine XAML-Seite je Bereich sowie Aufgabenkarte und Aufgabenformular |
| `ViewModels` | ein ViewModel je Seite, Zeilen-ViewModels, `SidebarViewModel`, `ReminderBannerViewModel` |
| `Controls` | wiederverwendbare Steuerelemente (siehe 4.4) |
| `Services` | Anwendungsdienste (siehe 4.2) |
| `Platforms/Windows` | WinUI-Code: Aufklapplisten, Dialoge, Kalender, Fensteraktivierung, Systembenachrichtigungen |
| `Resources/Styles` | globale Stile und die beiden XAML-Design-Wörterbücher |
| `Configuration` | `AppPaths`, `AppOptions` |

Seiten und ViewModels entstehen über Dependency Injection. ViewModels erben von `ViewModelBase`, das ihnen
`RunAsync` (Beschäftigt-Kennzeichen, immer nur ein Lauf, erwartete Fehler als Meldung, unerwartete ins Protokoll)
und den Dialogdienst gibt.

### 4.2 Anwendungsdienste

| Dienst | Aufgabe |
|--------|---------|
| `ServiceRunner` | öffnet je Vorgang einen DI-Scope und führt darin einen Core-Dienst aus |
| `AppInitializer` | beim Start: vorbereitete Wiederherstellung anwenden, Datenbank migrieren, Erinnerungsdienst starten; beim Beenden stoppen |
| `UserSettings` | `settings.json`: Seitengröße, Design, Sprache, Ansicht, Erinnerungsoptionen; löst `Changed` aus |
| `Loc` | Übersetzungssuche und aktuelle Sprache (siehe 4.5) |
| `ThemeManager`, `ThemeCatalog`, `GlowBrushes` | Designs (siehe 4.3) |
| `DialogService` | Hinweis- und Bestätigungsfenster; unter Windows öffnet er `SignalDialog` im Design der App |
| `FileService` | Speichern- und Öffnen-Dialoge, der Ordner *Dokumente\TaskManager*, Ordnerauswahl |
| `HotKeyService` | Fensterkürzel (`Strg+1…6`, `Strg+N`, `Alt+←`) und Weitergabe der seitenbezogenen |
| `ShellNavigationService`, `AppRestarter` | Navigation über Routen; Neustart nach einer Wiederherstellung oder beim Verschieben der Datenbank |

### 4.3 Designs

Farben sind Token (`Bg`, `Surface`, `Line`, `Ink`, `InkMuted`, `Accent`, `OnAccent`, `A2`, die Prioritäts- und
Statusfarben, …). Zwei Designs sind XAML-Wörterbücher (`DarkTheme`, `LightTheme`); die übrigen werden aus der
Tabelle in `ThemeCatalog` gebaut. Stile verweisen mit `DynamicResource` auf Token, sodass der Austausch des
Wörterbuchs die Anwendung umfärbt. Drei Besonderheiten:

- **Verläufe und Leuchten** (Seitenhintergrund, Panelverlauf, die Hauptschaltfläche, der gewählte Menüpunkt)
  sind `GlowBrushes`: Jeder Pinsel existiert nur einmal und wird bei einem Designwechsel an Ort und Stelle
  umgefärbt, was zuverlässiger ist als den Pinsel als Ressource auszutauschen.
- **Trigger** (gewählter Menüpunkt, Zeitraumschalter, Kontrollkästchen) behalten die Farbe, die sie beim
  letzten Auslösen hatten; deshalb **baut ein Designwechsel das Fenster neu auf** (derselbe Mechanismus wie beim
  Sprachwechsel), und der geöffnete Bereich bleibt.
- **Windows-Aufklappfenster** (Listen, Kalender, Dialoge) sind WinUI-Elemente; `ThemeManager` gibt ihnen das
  passende `ElementTheme`, und Listen, Kalender und Dialoge zeichnet die App selbst (`SignalMenu`,
  `SignalCalendar`, `SignalDialog`), damit sie dieselben Token verwenden.

Für ein neues Design genügt eine neue Zeile in `ThemeCatalog` (siehe Entwicklungshandbuch).

### 4.4 Steuerelemente

`SignalButton` (primär / sekundär / Geist / gefährlich, Pillenform, Verlauf und Leuchten), `DropField` und
`DropMenu` (Aufklappliste im Design), `DateField` mit dem Signal-Kalender, `SegmentedChoice`, `SwitchToggle`,
`Icon` (Lucide-Pfade als Geometrie), `Chips` (Status und Priorität), `PriorityBars`, `BarMeter`, `Segments`,
`DayChart` (das Diagramm *Erledigt pro Tag*, eine einzige `GraphicsView`-Fläche), `BoardColumn`, `SidebarView`
und `ReminderBanner`.

### 4.5 Lokalisierung

Vier Sprachen: Russisch, Englisch, Deutsch, Ukrainisch. Die Tabellen sind einfache Arrays in
`Services/Translations.*.cs`; eine Zeile ist `[russischer Schlüssel, Englisch, Deutsch, Ukrainisch]`, und der
russische Text ist der Schlüssel. `Loc.T(key)` liefert den Text in der aktuellen Sprache oder den Schlüssel selbst,
wenn nichts gefunden wird; eine fehlende Übersetzung erscheint also als russische Zeile statt als Lücke. Texte mit
Zahlen verwenden `{0}`; Pluralformen stehen als `eins|wenige|viele` und werden von `Loc.Count` gewählt.
Meldungen aus Core werden per Muster übersetzt (`Loc.Message`). Ein Sprachwechsel baut das Fenster neu auf. Datum
und Zahlen folgen der Kultur der Sprache.

Ein Test bindet die Übersetzungsdateien in das Testprojekt ein und prüft, dass jede Zeile vier gefüllte Spalten
hat, Platzhalter und Pluralformen zwischen den Sprachen übereinstimmen und kein Schlüssel auf zwei Arten
übersetzt wird.

## 5. Qualitätsentscheidungen

- **Die Schichtung erzwingen die Projektverweise**, nicht eine Vereinbarung: Core kann die Verwendung von EF Core
  nicht einmal kompilieren.
- **Domänenregeln leben in der Domäne** und werden ohne Datenbank getestet; die Persistenz wird gegen echtes
  SQLite getestet (im Speicher und in temporären Dateien).
- **Kein Datenbankzugriff im UI-Thread**: ViewModels rufen Core über `ServiceRunner` in Hintergrundaufgaben auf
  und aktualisieren gebundene Eigenschaften im UI-Thread.
- **Zerstörerische Aktionen sind umkehrbar oder bestätigt**: Löschen geht in den Papierkorb, endgültiges Löschen
  und Leeren fragen vorher, eine Wiederherstellung behält eine Sicherheitskopie, der Import überspringt Duplikate
  und berichtet alles, was er getan hat.
- **Ein Absturz im Hintergrund beendet die App nicht**: Ein fehlgeschlagener Erinnerungslauf wird protokolliert und
  beim nächsten Intervall wiederholt.
- **Analyzer an** (`AnalysisLevel latest-recommended`, Codestil beim Build erzwungen); 191 automatisierte Tests.

## 6. Bekannte Grenzen

- Nur Windows (MAUI mit WinUI-Kopf); noch kein Installer und keine automatische Aktualisierung.
- Ein Benutzer und eine Datenbank; keine Synchronisierung.
- Die Systemdialoge zum Speichern und Öffnen (Speichern unter, Datei oder Ordner wählen) haben das Windows-Aussehen;
  jedes andere Aufklappfenster ist im Design der App gezeichnet.
- Die MAUI-Schicht selbst (Seiten, ViewModels, Steuerelemente) hat keine automatisierten UI-Tests; die wichtige Logik
  liegt in Core und Infrastructure und ist abgedeckt.
- Das Fenster wird bei einem Design- oder Sprachwechsel neu aufgebaut, ungespeicherter Text in einem offenen
  Formular bleibt also nicht erhalten; beides wird auf der Einstellungsseite geändert.
