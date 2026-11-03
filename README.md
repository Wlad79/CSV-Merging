# Sparkasse Auswertung – CSV-Merger

.NET-10-Tool (GUI + Kommandozeile), das alle Sparkasse-CSV-Exporte – auch solche, die
in ZIP-Archiven oder Unterordnern liegen – zu **einer** CSV-Datei zusammenführt und
**doppelte Datensätze entfernt**.

## Voraussetzungen

- .NET 10 SDK (`dotnet --version` sollte `10.x` liefern)
- Windows für die GUI (WinForms): in Win-Settings "Smart App Control" is deactivated

## GUI (empfohlen)

```powershell
# beim ersten Mal wird die EXE veröffentlicht, danach direkt gestartet
start-gui.cmd
```

`start-gui.cmd` legt beim ersten Aufruf eine eigenständige Single-File-EXE unter
`dist\gui\SparkasseCsvMerger.Gui.exe` an (dauert ein bis zwei Minuten) und startet
sie danach direkt. Manuell geht das so:

```powershell
publish-gui.cmd
.\dist\gui\SparkasseCsvMerger.Gui.exe
```

Zum Entwickeln geht auch `dotnet run --project src/SparkasseCsvMerger.Gui`.

### Hinweis zu Smart App Control

Auf Windows 11 mit aktiver **Smart App Control** (Windows-Sicherheit → App- und
Browsersteuerung) blockiert das System frisch gebaute, nicht signierte DLLs
(`FileLoadException 0x800711C7`). Die normale Build-Ausgabe der GUI startet dann
nicht. Die mit `publish-gui.cmd` erzeugte **Single-File-EXE** läuft auch mit
aktiver Smart App Control – deshalb ist sie der empfohlene Weg.

Bedienung:

1. **Quellen** – Ordner, ZIP-Archive oder einzelne CSV-Dateien hinzufügen
   (Buttons oder Drag & Drop). ZIP-Inhalte und Unterordner werden automatisch
   mitgelesen.
2. **Zieldatei** – Zielordner wählen und den Dateinamen frei eingeben
   (`merged.csv` wird automatisch ergänzt). Der Ordner wird bei Bedarf angelegt.
3. **Optionen** – Duplikate entfernen, nach Datum sortieren, Unterordner einbeziehen.
4. **Zusammenführen** – Fortschritt und Ergebnis stehen im Protokoll, danach lassen
   sich Ergebnis und Zielordner direkt öffnen.
5. **Suche** – Suchbegriff eingeben und „Treffer anzeigen“ (oder Enter drücken). Das
   Protokoll listet alle Datensätze auf, die den Begriff in irgendeiner Spalte
   enthalten – ohne Beachtung der Groß-/Kleinschreibung. Ist beim Start der
   Zusammenführung bereits ein Begriff eingetragen, wird die Suche automatisch
   ausgeführt.

Die zuletzt verwendeten Einstellungen werden gespeichert unter
`%APPDATA%\SparkasseCsvMerger\gui-settings.json`. Unerwartete Fehler landen in
`%LOCALAPPDATA%\SparkasseCsvMerger\error.log`.

## Kommandozeile

```powershell
# bauen
dotnet build

# Ordner mit ZIP- und/oder CSV-Dateien zusammenführen
dotnet run --project src/SparkasseCsvMerger -- "C:\Users\ich\Desktop\Umsaetze" -o merged.csv
```

Alternativ das Hilfsskript im Stammverzeichnis:

```cmd
merge-csv.cmd "C:\Users\ich\Desktop\Umsaetze" -o merged.csv
```

Ergebnis: `merged.csv` (UTF-8 mit BOM, Semikolon, sortiert nach Buchungstag).

### Aufruf

```
SparkasseCsvMerger <input>... [Optionen]
```

`<input>` darf sein:

| Eingabe | Verhalten |
|---|---|
| Ordner | alle `*.csv` und `*.zip` darin werden gelesen (standardmäßig rekursiv) |
| `*.zip` | jedes `*.csv` im Archiv wird gelesen (auch in Unterordnern) |
| `*.csv` | wird direkt gelesen |
| sonstige Datei | wird explizit als CSV gelesen |

### Optionen

| Option | Bedeutung |
|---|---|
| `-o`, `--output <pfad>` | Zieldatei (Standard: `merged.csv` im aktuellen Verzeichnis) |
| `--no-recursive` | Unterordner bei Ordner-Eingaben nicht durchsuchen |
| `--no-sort` | Reihenfolge der Quelldateien beibehalten statt nach Datum zu sortieren |
| `--keep-duplicates` | Duplikate **nicht** entfernen (nur zusammenführen) |
| `--plain-quotes` | nur Felder quoten, die es brauchen (Standard: alle Felder) |
| `--dry-run` | nur analysieren und Bericht ausgeben, nichts schreiben |
| `--encoding <name>` | Eingabe-Encoding erzwingen, z. B. `windows-1252` |
| `--delimiter <zeichen>` | Trennzeichen erzwingen: `;` `,` `|` oder `tab` |
| `-v`, `--verbose` | Details je Datei (Encoding, Trennzeichen, übersprungene Zeilen) |
| `-h`, `--help` | Hilfe |

### Beispiele

```powershell
# Ein Archiv
dotnet run --project src/SparkasseCsvMerger -- umsaetze-2024.zip -o alle.csv

# Mehrere Quellen mischen
dotnet run --project src/SparkasseCsvMerger -- export-2023.zip export-2024.zip konto.csv -o alles.csv -v

# Nur prüfen, was passieren würde
dotnet run --project src/SparkasseCsvMerger -- "C:\Umsaetze" --dry-run -v
```

## Was der Merger macht

1. **Quellen einsammeln** – Ordner werden (optional rekursiv) nach `*.csv` und `*.zip`
   durchsucht; ZIP-Archive werden entpackt und alle enthaltenen CSV-Dateien gelesen,
   unabhängig davon, in welchem Unterordner sie im Archiv liegen.
2. **Encoding erkennen** – BOM wird ausgewertet, sonst strikt UTF-8 versucht und bei
   ungültigen Bytes auf Windows-1252 (Sparkasse-Standard) zurückgefallen.
3. **Trennzeichen erkennen** – `;`, `,`, Tab oder `|` werden anhand der Feldanzahl
   automatisch bestimmt.
4. **Kopfzeile finden** – auch bei Vorspann-Zeilen (z. B. „Umsatzanzeige“).
5. **Fußzeilen ignorieren** – Zeilen wie `"Kontonummer:"`, `"IBAN:"`, `"Auszug:"`
   haben weniger Spalten und werden übersprungen (Anzahl steht im `-v`-Bericht).
6. **Spalten vereinheitlichen** – leicht abweichende Spaltennamen und -reihenfolgen
   werden auf gemeinsame Namen abgebildet, z. B.:
   - `Empfänger` → `Empfänger/Zahlungspflichtiger`
   - `Umsatzart` → `Buchungstext`
   - `Kontonummer` / `Kontonummer/IBAN` / `IBAN` → `Empfänger IBAN`
   - `BLZ` / `BIC (SWIFT-Code)` → `Empfänger BIC`
   - Umlaute/Schreibweisen (`GlaeubigerID` → `Gläubiger ID`)
   - Die Spaltenreihenfolge der Ausgabe ist fest (Auftragskonto, Buchungstag, …,
     Kategorie, danach Zusatzspalten wie Mandatsreferenz).
7. **Duplikate entfernen** – zwei Zeilen gelten als gleich, wenn alle Spaltenwerte
   übereinstimmen. Dabei werden Leerzeichen, Datumsformate (`6.1.2024` =
   `06.01.2024`) und Betragsformate (`2500` = `2.500,00`) normalisiert.
8. **Sortieren** – standardmäßig aufsteigend nach `Buchungstag` (Fallback
   `Valutadatum`); Zeilen ohne Datum stehen am Ende. Die Originalschreibweise der
   Werte bleibt in der Ausgabe erhalten.
9. **Schreiben** – eine Datei, UTF-8 mit BOM, `;` als Trennzeichen (bzw. `,` wenn alle
   Quellen Komma nutzten), CRLF-Zeilenenden – direkt in Excel öffenbar.

## Hinweise / Grenzen

- **Echte Doppelbuchungen:** Sind zwei Zeilen wirklich identisch (gleicher Tag,
  gleicher Betrag, gleicher Text), werden sie als Duplikat behandelt – auch wenn es
  zufällig zwei echte Zahlungen waren. Mit `--keep-duplicates` bzw. dem
  GUI-Häkchen lässt sich das abschalten.
- **Unterschiedliche Spaltensätze:** Hat eine Datei zusätzliche Spalten (z. B.
  `Kategorie`), werden diese angehängt. Zeilen aus Dateien ohne diese Spalte bleiben
  in dieser Spalte leer und gelten dadurch nicht als Duplikat einer Zeile aus einer
  Datei mit gefüllter Spalte.
- Die Ausgabedatei wird beim erneuten Lauf nicht als Eingabe gelesen, wenn sie über
  `-o` genannnt wird. Liegt eine **alte** Ausgabedatei im Suchordner, wird sie
  mitgelesen – die Duplikaterkennung verhindert aber doppelte Zeilen.
- Exit-Codes: `0` = ok, `1` = keine/keine lesbaren Daten, `2` = Aufruf-Fehler.

## Selbsttest mit Beispieldaten

```powershell
powershell -ExecutionPolicy Bypass -File tools\Test-Merger.ps1
```

Das Skript erzeugt Testdaten (ZIP, UTF-8, Windows-1252, überlappende Zeilen,
Fußzeilen, abweichende Spaltenreihenfolge), baut die Release-Version, führt den
Merger aus und prüft Zeilen-, Spalten- und Duplikatzahl.

## Veröffentlichen (eigenständige EXE)

```powershell
# Kommandozeile
dotnet publish src/SparkasseCsvMerger -c Release -o dist\cli

# GUI (Single-File, siehe Hinweis zu Smart App Control)
publish-gui.cmd
```

## Projektstruktur

```
SparkasseCsvMerger.slnx
src/SparkasseCsvMerger.Core/     gemeinsame Logik (Bibliothek)
  MergerService.cs               öffentliche API für CLI und GUI
  Csv/                           Parser, Encoding-/Trennzeichen-Erkennung, Spalten-Mapping
  Ingestion/                     Ordner-/ZIP-Scan
  Merging/                       Zusammenführen, Duplikate, Sortierung
  Output/                        CSV-Ausgabe
src/SparkasseCsvMerger/          Kommandozeile
  Program.cs, App.cs, CliOptions.cs
src/SparkasseCsvMerger.Gui/      WinForms-GUI
  Program.cs, MainForm.cs, GuiSettings.cs
tools/Test-Merger.ps1
merge-csv.cmd
start-gui.cmd
publish-gui.cmd
dist/                            veröffentlichte EXEs (nicht im Repo)
```
