<#
Fill Strings.resx and Strings.en.resx from display-keys.csv.

ShortName goes into Strings.resx. A non-empty English cell goes into Strings.en.resx.
Keys already in the resx and absent from the CSV stay as they are.
An empty English cell does not erase an existing English value.

The file is ASCII so Windows PowerShell 5.1 can parse it on a Russian system.
CSV may be UTF-8 or Windows-1251 (Excel on Russian Windows).

    powershell -NoExit -ExecutionPolicy Bypass -File fill_strings_resx.ps1 -Csv display-keys.csv -Resx Strings.resx

Strings.en.resx is the same folder and name with .en before .resx, unless -EnglishResx is set.
#>
param(
    [string]$Csv,
    [string]$Resx,
    [string]$EnglishResx,
    [switch]$SelfTest
)

$ErrorActionPreference = "Stop"

function T {
    param([int[]]$Code)
    $sb = New-Object System.Text.StringBuilder
    foreach ($c in $Code) { [void]$sb.Append([char]$c) }
    return $sb.ToString()
}

function Read-CsvText([string]$path) {
    $bytes = [System.IO.File]::ReadAllBytes($path)
    $offset = 0
    if ($bytes.Length -ge 3 -and $bytes[0] -eq 239 -and $bytes[1] -eq 187 -and $bytes[2] -eq 191) {
        $offset = 3
    }
    $slice = New-Object byte[] ($bytes.Length - $offset)
    if ($slice.Length -gt 0) { [System.Array]::Copy($bytes, $offset, $slice, 0, $slice.Length) }
    $strict = New-Object System.Text.UTF8Encoding $false, $true
    try {
        return $strict.GetString($slice)
    }
    catch {
        Write-Host "CSV is not UTF-8, reading Windows-1251."
        return [System.Text.Encoding]::GetEncoding(1251).GetString($slice)
    }
}

function ConvertFrom-SemicolonCsv([string]$text) {
    $text = $text.Replace("`r`n", "`n").Replace("`r", "`n")
    $rows = New-Object System.Collections.Generic.List[object]
    $row = New-Object System.Collections.Generic.List[string]
    $field = New-Object System.Text.StringBuilder
    $quoted = $false
    $i = 0
    while ($i -lt $text.Length) {
        $ch = $text[$i]
        if ($quoted) {
            if ($ch -eq '"') {
                if (($i + 1) -lt $text.Length -and $text[$i + 1] -eq '"') {
                    [void]$field.Append('"')
                    $i += 2
                    continue
                }
                $quoted = $false
                $i++
                continue
            }
            [void]$field.Append($ch)
            $i++
            continue
        }
        if ($ch -eq '"') { $quoted = $true; $i++; continue }
        if ($ch -eq ';') {
            [void]$row.Add($field.ToString())
            $field = New-Object System.Text.StringBuilder
            $i++
            continue
        }
        if ($ch -eq "`n") {
            [void]$row.Add($field.ToString())
            $field = New-Object System.Text.StringBuilder
            if ($row.Count -gt 1 -or $row[0] -ne "") { [void]$rows.Add($row.ToArray()) }
            $row = New-Object System.Collections.Generic.List[string]
            $i++
            continue
        }
        [void]$field.Append($ch)
        $i++
    }
    if ($field.Length -gt 0 -or $row.Count -gt 0) {
        [void]$row.Add($field.ToString())
        if ($row.Count -gt 1 -or $row[0] -ne "") { [void]$rows.Add($row.ToArray()) }
    }
    if ($rows.Count -eq 0) { return @() }
    $header = $rows[0]
    $result = New-Object System.Collections.Generic.List[object]
    for ($r = 1; $r -lt $rows.Count; $r++) {
        $item = @{}
        for ($c = 0; $c -lt $header.Length; $c++) {
            $name = [string]$header[$c]
            if ([string]::IsNullOrWhiteSpace($name)) { continue }
            $value = ""
            if ($c -lt $rows[$r].Length) { $value = [string]$rows[$r][$c] }
            $item[$name.Trim()] = $value
        }
        [void]$result.Add($item)
    }
    return ,$result
}

function New-ResxDocument {
    $xml = New-Object System.Xml.XmlDocument
    $decl = $xml.CreateXmlDeclaration("1.0", "utf-8", $null)
    [void]$xml.AppendChild($decl)
    $root = $xml.CreateElement("root")
    [void]$xml.AppendChild($root)
    $headers = @(
        @("resmimetype", "text/microsoft-resx"),
        @("version", "2.0"),
        @("reader", "System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"),
        @("writer", "System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
    )
    foreach ($pair in $headers) {
        $header = $xml.CreateElement("resheader")
        [void]$header.SetAttribute("name", [string]$pair[0])
        $value = $xml.CreateElement("value")
        $value.InnerText = [string]$pair[1]
        [void]$header.AppendChild($value)
        [void]$root.AppendChild($header)
    }
    return $xml
}

function Open-Resx([string]$path) {
    $xml = New-Object System.Xml.XmlDocument
    $xml.PreserveWhitespace = $true
    if (Test-Path -LiteralPath $path) {
        $xml.Load($path)
    }
    else {
        $xml = New-ResxDocument
        $xml.PreserveWhitespace = $true
    }
    return $xml
}

function Save-Resx($xml, [string]$path) {
    $dir = Split-Path -Parent $path
    if ($dir -and -not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }
    $xml.Save($path)
}

function Get-ResxData($xml) {
    $map = @{}
    foreach ($node in $xml.GetElementsByTagName("data")) {
        $name = $node.GetAttribute("name")
        if (-not [string]::IsNullOrWhiteSpace($name)) { $map[$name] = $node }
    }
    return $map
}

function Set-ResxValue($xml, $map, [string]$key, [string]$value) {
    $state = "same"
    if ($map.ContainsKey($key)) {
        $node = $map[$key]
        $valueNode = $node.SelectSingleNode("value")
        if ($null -eq $valueNode) {
            $valueNode = $xml.CreateElement("value")
            [void]$node.AppendChild($valueNode)
        }
        if ($valueNode.InnerText -ne $value) { $state = "updated" }
    }
    else {
        $node = $xml.CreateElement("data")
        [void]$node.SetAttribute("name", $key)
        [void]$node.SetAttribute("space", "http://www.w3.org/XML/1998/namespace", "preserve")
        $valueNode = $xml.CreateElement("value")
        [void]$node.AppendChild($valueNode)
        [void]$xml.DocumentElement.AppendChild($xml.CreateWhitespace("`r`n  "))
        [void]$xml.DocumentElement.AppendChild($node)
        $map[$key] = $node
        $state = "added"
    }
    $valueNode.InnerText = $value
    return $state
}

function Get-Cell($row, [string]$name) {
    if (-not $row.ContainsKey($name) -or $null -eq $row[$name]) { return "" }
    return [string]$row[$name]
}

function Update-ResxFromCsv([string]$csvPath, [string]$resxPath, [string]$englishPath) {
    if (-not (Test-Path -LiteralPath $csvPath)) {
        Write-Host ("No CSV: " + $csvPath)
        return 2
    }
    Write-Host ("CSV: " + $csvPath)
    Write-Host ("Russian: " + $resxPath)
    Write-Host ("English: " + $englishPath)
    $rows = ConvertFrom-SemicolonCsv (Read-CsvText $csvPath)
    $russian = Open-Resx $resxPath
    $english = Open-Resx $englishPath
    $russianMap = Get-ResxData $russian
    $englishMap = Get-ResxData $english
    $counts = @{
        ruAdded = 0; ruUpdated = 0; ruSame = 0
        enAdded = 0; enUpdated = 0; enSame = 0; enSkipped = 0
    }
    $seen = @{}
    foreach ($row in $rows) {
        $key = (Get-Cell $row "Key").Trim()
        $short = Get-Cell $row "ShortName"
        $translated = (Get-Cell $row "English").Trim()
        if ([string]::IsNullOrWhiteSpace($key) -or [string]::IsNullOrWhiteSpace($short)) { continue }
        if ($seen.ContainsKey($key)) { Write-Host ("Duplicate key, last row wins: " + $key) }
        $seen[$key] = $true
        $ruState = Set-ResxValue $russian $russianMap $key $short
        if ($ruState -eq "added") { $counts.ruAdded++ }
        elseif ($ruState -eq "updated") { $counts.ruUpdated++ }
        else { $counts.ruSame++ }
        if ([string]::IsNullOrWhiteSpace($translated)) {
            $counts.enSkipped++
            continue
        }
        $enState = Set-ResxValue $english $englishMap $key $translated
        if ($enState -eq "added") { $counts.enAdded++ }
        elseif ($enState -eq "updated") { $counts.enUpdated++ }
        else { $counts.enSame++ }
    }
    Save-Resx $russian $resxPath
    Save-Resx $english $englishPath
    Write-Host ("Russian added {0}, updated {1}, unchanged {2}." -f $counts.ruAdded, $counts.ruUpdated, $counts.ruSame)
    Write-Host ("English added {0}, updated {1}, unchanged {2}, empty skipped {3}." -f $counts.enAdded, $counts.enUpdated, $counts.enSame, $counts.enSkipped)
    Write-Host ("Wrote: " + $resxPath)
    Write-Host ("Wrote: " + $englishPath)
    return 0
}

function Get-ResxText($xml, [string]$key) {
    $map = Get-ResxData $xml
    if (-not $map.ContainsKey($key)) { return $null }
    $value = $map[$key].SelectSingleNode("value")
    if ($null -eq $value) { return $null }
    return $value.InnerText
}

function Invoke-SelfTest {
    $dir = Join-Path ([System.IO.Path]::GetTempPath()) ("fill-resx-" + [Guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path $dir | Out-Null
    $serial = T 0x421,0x435,0x440,0x438,0x439,0x43d,0x44b,0x439,0x20,0x43d,0x43e,0x43c,0x435,0x440
    $line = T 0x421,0x442,0x440,0x43e,0x43a,0x430
    $amp = "A & B"
    $withSemi = "Name; full"
    $csvPath = Join-Path $dir "display-keys.csv"
    $resxPath = Join-Path $dir "Strings.resx"
    $enPath = Join-Path $dir "Strings.en.resx"
    $seed = New-ResxDocument
    $seedMap = Get-ResxData $seed
    [void](Set-ResxValue $seed $seedMap "Menu_Warehouse" "Warehouse-ru")
    [void](Set-ResxValue $seed $seedMap "VerifyIdentifierChange_SerialNumber" "old")
    Save-Resx $seed $resxPath
    $seedEn = New-ResxDocument
    $seedEnMap = Get-ResxData $seedEn
    [void](Set-ResxValue $seedEn $seedEnMap "Menu_Warehouse" "Warehouse")
    [void](Set-ResxValue $seedEn $seedEnMap "VerifyIdentifierChange_LineNo" "Line")
    Save-Resx $seedEn $enPath
    $lines = @(
        "Key;ShortName;English;Source;Class;Property;Namespace;File",
        ("VerifyIdentifierChange_SerialNumber;" + $serial + ";Serial number;ShortName;VerifyIdentifierChange;SerialNumber;Store;File.cs"),
        ("VerifyIdentifierChange_LineNo;" + $line + ";;Name;VerifyIdentifierChange;LineNo;Store;File.cs"),
        ("Box_Title;""$withSemi"";Title;ShortName;Box;Title;Store;File.cs"),
        ("Box_Amp;$amp;A and B;ShortName;Box;Amp;Store;File.cs")
    )
    $utf8 = New-Object System.Text.UTF8Encoding $true
    [System.IO.File]::WriteAllLines($csvPath, $lines, $utf8)
    $code = Update-ResxFromCsv $csvPath $resxPath $enPath
    $ru = Open-Resx $resxPath
    $en = Open-Resx $enPath
    $ok = $true
    if ((Get-ResxText $ru "Menu_Warehouse") -ne "Warehouse-ru") { Write-Host "kept menu failed"; $ok = $false }
    if ((Get-ResxText $ru "VerifyIdentifierChange_SerialNumber") -ne $serial) { Write-Host "russian update failed"; $ok = $false }
    if ((Get-ResxText $ru "VerifyIdentifierChange_LineNo") -ne $line) { Write-Host "russian add failed"; $ok = $false }
    if ((Get-ResxText $ru "Box_Title") -ne $withSemi) { Write-Host "semicolon field failed"; $ok = $false }
    if ((Get-ResxText $ru "Box_Amp") -ne $amp) { Write-Host "amp failed"; $ok = $false }
    if ((Get-ResxText $en "Menu_Warehouse") -ne "Warehouse") { Write-Host "english menu failed"; $ok = $false }
    if ((Get-ResxText $en "VerifyIdentifierChange_LineNo") -ne "Line") { Write-Host "empty english erased a value"; $ok = $false }
    if ((Get-ResxText $en "VerifyIdentifierChange_SerialNumber") -ne "Serial number") { Write-Host "english add failed"; $ok = $false }
    if ((Get-ResxText $en "Box_Amp") -ne "A and B") { Write-Host "english amp failed"; $ok = $false }
    Remove-Item -LiteralPath $dir -Recurse -Force
    if (-not $ok -or $code -ne 0) { exit 1 }
    Write-Host "self-test ok"
    exit 0
}

if ($SelfTest) { Invoke-SelfTest }

try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch {}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if ([string]::IsNullOrWhiteSpace($scriptDir)) { $scriptDir = (Get-Location).Path }
if ([string]::IsNullOrWhiteSpace($Csv)) { $Csv = Join-Path $scriptDir "display-keys.csv" }
if ([string]::IsNullOrWhiteSpace($Resx)) { $Resx = Join-Path $scriptDir "Strings.resx" }
if ([string]::IsNullOrWhiteSpace($EnglishResx)) {
    $folder = Split-Path -Parent $Resx
    $name = [System.IO.Path]::GetFileNameWithoutExtension($Resx)
    if ($name.EndsWith(".en")) { $EnglishResx = $Resx }
    else { $EnglishResx = Join-Path $folder ($name + ".en.resx") }
}
$code = Update-ResxFromCsv $Csv $Resx $EnglishResx
if ($env:OS -eq "Windows_NT") {
    try {
        $parentId = (Get-CimInstance Win32_Process -Filter "ProcessId=$PID" -ErrorAction Stop).ParentProcessId
        $parentName = (Get-Process -Id $parentId -ErrorAction Stop).ProcessName
        if ($parentName -eq "explorer") {
            Write-Host "Press Enter to close."
            [void](Read-Host)
        }
    } catch {}
}
exit $code
