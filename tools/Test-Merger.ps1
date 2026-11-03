#requires -Version 5.1
<#
    Self test for the Sparkasse CSV merger.

    Creates sample data in the temp folder (ZIP with two overlapping CSV files,
    UTF-8, Windows-1252, footer lines, reordered/variant headers, extra column),
    runs the merger and verifies the result.
#>
param(
    [string]$WorkDir = (Join-Path $env:TEMP 'spk-merge-selftest')
)

$ErrorActionPreference = 'Stop'
$ae = [char]0xE4; $ue = [char]0xFC; $sz = [char]0xDF
$projectRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $projectRoot 'src\SparkasseCsvMerger\SparkasseCsvMerger.csproj'

if (Test-Path $WorkDir) { Remove-Item $WorkDir -Recurse -Force }
New-Item -ItemType Directory -Path $WorkDir | Out-Null

$header = '"Auftragskonto";"Buchungstag";"Valutadatum";"Buchungstext";"Verwendungszweck";"Gl' + $ae +
          'ubigerID";"Empf' + $ae + 'nger/Zahlungspflichtiger";"Empf' + $ae + 'nger IBAN";"Empf' +
          $ae + 'nger BIC";"Betrag";"W' + $ae + 'hrung";"Info"'

$rewe   = '"DE1111";"05.01.2024";"05.01.2024";"KARTENZAHLUNG";"REWE Markt";"";"REWE SAGT DANKE";"DE2222";"COBADEFFXXX";"-45,67";"EUR";""'
$gehalt = '"DE1111";"06.01.2024";"06.01.2024";"GUTSCHRIFT";"Gehalt Januar";"";"ACME GmbH";"DE3333";"COBADEFFXXX";"2.500,00";"EUR";""'
$miete  = '"DE1111";"01.02.2024";"01.02.2024";"LASTSCHRIFT";"Miete Februar";"";"Hausverwaltung";"DE4444";"COBADEFFXXX";"-850,00";"EUR";""'
$baeck  = '"DE1111";"07.01.2024";"08.01.2024";"KARTENZAHLUNG";"B' + $ae + 'ckerei M' + $ue + 'ller";"";"B' + $ae + 'ckerei M' + $ue + 'ller";"DE5555";"COBADEFFXXX";"-4,20";"EUR";""'

# --- ZIP with two overlapping exports, footers, UTF-8 and Windows-1252 ---------
$stage = Join-Path $WorkDir '_zip'
New-Item -ItemType Directory -Path $stage | Out-Null

$fileA = @($header, $rewe, $gehalt, $miete,
    '"Kontonummer:";"DE11111111111111111111";',
    '"IBAN:";"DE11111111111111111111";',
    '"Auszug:";"1";') -join "`r`n"
[IO.File]::WriteAllText((Join-Path $stage 'Umsatz_DE1111.csv'), $fileA, (New-Object Text.UTF8Encoding($false)))

$fileB = @($header, $gehalt, $baeck, $rewe) -join "`r`n"
[IO.File]::WriteAllText((Join-Path $stage 'Umsatz_DE1111 (1).csv'), $fileB, [Text.Encoding]::GetEncoding(1252))

Compress-Archive -Path (Join-Path $stage '*') -DestinationPath (Join-Path $WorkDir 'umsaetze-2024.zip')
Remove-Item $stage -Recurse -Force

# --- Loose CSV: reordered columns and alias header, UTF-8 with BOM -----------
$sub = Join-Path $WorkDir '2025'
New-Item -ItemType Directory -Path $sub | Out-Null
$reordered = '"Betrag";"Buchungstag";"Valutadatum";"Auftragskonto";"Buchungstext";"Verwendungszweck";"Gl' +
             $ae + 'ubigerID";"Empf' + $ae + 'nger";"Kontonummer";"BLZ";"W' + $ae + 'hrung";"Info"'
$fileC = @($reordered,
    '"-850,00";"01.02.2024";"01.02.2024";"DE1111";"LASTSCHRIFT";"Miete Februar";"";"Hausverwaltung";"DE4444";"COBADEFFXXX";"EUR";""',
    '"-12,99";"10.03.2024";"10.03.2024";"DE1111";"KARTENZAHLUNG";"Netflix";"";"Netflix International";"DE6666";"COBADEFFXXX";"EUR";""') -join "`r`n"
[IO.File]::WriteAllText((Join-Path $sub 'upload.csv'), $fileC, (New-Object Text.UTF8Encoding($true)))

# --- CSV with an extra column -> column union --------------------------------
$extra = @(($header + ';"Kategorie"'),
    '"DE1111";"15.03.2024";"15.03.2024";"KARTENZAHLUNG";"Aral Tankstelle";"";"Aral";"DE7777";"COBADEFFXXX";"-60,00";"EUR";"";"Auto"') -join "`r`n"
[IO.File]::WriteAllText((Join-Path $WorkDir 'kategorien.csv'), $extra, (New-Object Text.UTF8Encoding($false)))

# --- Run the merger ---------------------------------------------------------
Write-Host 'Building...'
& dotnet build $project -c Release -v q --nologo | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'build failed' }

$exe = Join-Path $projectRoot 'src\SparkasseCsvMerger\bin\Release\net10.0\SparkasseCsvMerger.exe'
$output = Join-Path $WorkDir 'out\merged.csv'

Write-Host 'Merging sample data...'
$log = & $exe $WorkDir -o $output 2>&1 | Out-String
if ($LASTEXITCODE -ne 0) { Write-Host $log; throw "merger failed with exit code $LASTEXITCODE" }
Write-Host $log

# --- Assertions -------------------------------------------------------------
$content = [IO.File]::ReadAllText($output, [Text.Encoding]::UTF8)
$lines = $content -split "`r`n" | Where-Object { $_.Length -gt 0 }

$errors = New-Object System.Collections.Generic.List[string]

if ($lines.Count -ne 7) { $errors.Add("expected 7 lines (header + 6 rows), got $($lines.Count)") }
if ($lines[0] -notmatch '^"Auftragskonto";"Buchungstag";') { $errors.Add("header does not start with Auftragskonto;Buchungstag: $($lines[0])") }
if ($lines[0] -notmatch 'Kategorie') { $errors.Add('extra column "Kategorie" missing in output') }
if (($lines[0] -split ';') -notcontains ('"Empf' + $ae + 'nger IBAN"')) { $errors.Add('alias "Kontonummer" was not mapped to "Empf' + $ae + 'nger IBAN"') }
if ($lines[1] -notmatch '^"DE1111";"05\.01\.2024";') { $errors.Add("rows are not sorted by date: $($lines[1])") }
if ($content -notmatch ('B' + $ae + 'ckerei M' + $ue + 'ller')) { $errors.Add('umlaut row missing or mangled') }
if ($content -notmatch '"Auto"') { $errors.Add('Kategorie value missing') }
if ($log -notmatch 'duplicates\s+:\s+3 removed') { $errors.Add('expected 3 removed duplicates') }
if (($content -split "`r`n" | Where-Object { $_ -match 'REWE Markt' }).Count -ne 1) { $errors.Add('REWE row is not unique') }
if (($content -split "`r`n" | Where-Object { $_ -match 'Gehalt Januar' }).Count -ne 1) { $errors.Add('salary row is not unique') }

if ($errors.Count -gt 0) {
    foreach ($errorItem in $errors) { Write-Host "FAIL: $errorItem" -ForegroundColor Red }
    exit 1
}

Write-Host 'PASS: 4 sources merged, 3 duplicates removed, 6 unique rows.' -ForegroundColor Green
Write-Host "Output: $output"
