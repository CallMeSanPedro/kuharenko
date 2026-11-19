<#
Collect column keys from [Display] on model files.
Key: ClassName_PropertyName. Caption is ShortName, or Name when ShortName is empty.
AutoGenerateField = false is skipped.

The file is ASCII on purpose. Windows PowerShell 5.1 on a Russian system
reads a UTF-8 script without a BOM as Windows-1251 and then fails to parse it.
Russian text is built at runtime from character codes.

CSV is UTF-8 with BOM, separator semicolon, so Excel opens the columns.

    powershell -ExecutionPolicy Bypass -File collect_display_keys.ps1 -Path C:\src\Store -English C:\src\Store\Strings.en.resx -Output display-keys.csv

Without -Path the script scans its own folder and writes display-keys.csv next to itself.
The console prints Scan progress immediately. A double-clicked window stays open until Enter.

Empty English cells are the translations still to add to both resx files.
After the CSV is filled, write it back with fill_strings_resx.ps1.
#>
param(
    [string[]]$Path,
    [string]$Output = "display-keys.csv",
    [string]$English,
    [switch]$SelfTest
)

$ErrorActionPreference = "Stop"

function T {
    param([int[]]$Code)
    $sb = New-Object System.Text.StringBuilder
    foreach ($c in $Code) { [void]$sb.Append([char]$c) }
    return $sb.ToString()
}

$script:TypeWords = @("class", "struct", "interface", "enum", "record")
$script:Modifiers = @(
    "public", "private", "protected", "internal", "abstract", "sealed", "static",
    "partial", "new", "unsafe", "file", "readonly", "ref", "virtual", "override",
    "async", "extern", "volatile", "required"
)
$script:SkipDirs = @("bin", "obj", ".git", ".vs", "packages", "node_modules", "TestResults")
$script:DisplayNames = @("Display", "DisplayAttribute")
$script:TypeFrames = @("class", "struct", "interface", "record")
$script:AcceptFrames = @("file", "namespace", "class", "struct", "interface", "record")

function Starts-At([string]$src, [int]$i, [string]$text) {
    if ($i -lt 0 -or $i + $text.Length -gt $src.Length) { return $false }
    return $src.Substring($i, $text.Length) -eq $text
}

function Skip-Ws([string]$src, [int]$i) {
    while ($i -lt $src.Length -and (" `t`r`n".IndexOf($src[$i]) -ge 0)) { $i++ }
    return $i
}

function Read-Ident([string]$src, [int]$i) {
    if ($i -ge $src.Length) { return @{ Ident = $null; Index = $i } }
    $ch = $src[$i]
    if (-not ([char]::IsLetter($ch) -or $ch -eq "_")) { return @{ Ident = $null; Index = $i } }
    $j = $i + 1
    while ($j -lt $src.Length) {
        $next = $src[$j]
        if (-not ([char]::IsLetterOrDigit($next) -or $next -eq "_")) { break }
        $j++
    }
    return @{ Ident = $src.Substring($i, $j - $i); Index = $j }
}

function Read-Dotted([string]$src, [int]$i) {
    $part = Read-Ident $src $i
    if (-not $part.Ident) { return @{ Name = ""; Index = $i } }
    $parts = New-Object System.Collections.Generic.List[string]
    [void]$parts.Add([string]$part.Ident)
    $i = [int]$part.Index
    while ($true) {
        $j = Skip-Ws $src $i
        if ($j -ge $src.Length -or $src[$j] -ne ".") { return @{ Name = ($parts -join "."); Index = $i } }
        $ident = Read-Ident $src ($j + 1)
        if (-not $ident.Ident) { return @{ Name = ($parts -join "."); Index = $i } }
        [void]$parts.Add([string]$ident.Ident)
        $i = [int]$ident.Index
    }
}

function Skip-Verbatim([string]$src, [int]$i) {
    while ($i -lt $src.Length) {
        if (Starts-At $src $i '""') { $i += 2; continue }
        if ($src[$i] -eq '"') { return $i + 1 }
        $i++
    }
    return $i
}

function Skip-Regular([string]$src, [int]$i) {
    while ($i -lt $src.Length) {
        if ($src[$i] -eq "\") { $i += 2; continue }
        if ($src[$i] -eq '"') { return $i + 1 }
        $i++
    }
    return $i
}

function Skip-CharLiteral([string]$src, [int]$i) {
    while ($i -lt $src.Length) {
        if ($src[$i] -eq "\") { $i += 2; continue }
        if ($src[$i] -eq "'") { return $i + 1 }
        $i++
    }
    return $i
}

function Skip-Interpolated([string]$src, [int]$i, [bool]$verbatim) {
    $depth = 1
    while ($i -lt $src.Length -and $depth -gt 0) {
        if ($verbatim -and (Starts-At $src $i '""')) { $i += 2; continue }
        if (-not $verbatim -and $src[$i] -eq "\") { $i += 2; continue }
        if ($src[$i] -eq "{") {
            if (($i + 1) -lt $src.Length -and $src[$i + 1] -eq "{") { $i += 2; continue }
            $depth++
        }
        elseif ($src[$i] -eq "}") {
            if (($i + 1) -lt $src.Length -and $src[$i + 1] -eq "}") { $i += 2; continue }
            $depth--
            $i++
            continue
        }
        elseif ($src[$i] -eq '"') {
            $depth--
            $i++
            continue
        }
        $i++
    }
    return $i
}

function Skip-String([string]$src, [int]$i) {
    if ((Starts-At $src $i '$@"') -or (Starts-At $src $i '@$"')) { return Skip-Interpolated $src ($i + 3) $true }
    if (Starts-At $src $i '@"') { return Skip-Verbatim $src ($i + 2) }
    if (Starts-At $src $i '$"') { return Skip-Interpolated $src ($i + 2) $false }
    if ($src[$i] -eq "'") { return Skip-CharLiteral $src ($i + 1) }
    if ($src[$i] -eq '"') { return Skip-Regular $src ($i + 1) }
    return $i + 1
}

function Skip-StringOrNone([string]$src, [int]$i) {
    if ($i -ge $src.Length) { return -1 }
    if ((Starts-At $src $i '$@"') -or (Starts-At $src $i '@"') -or (Starts-At $src $i '@$"') -or (Starts-At $src $i '$"') -or $src[$i] -eq "'" -or $src[$i] -eq '"') {
        return Skip-String $src $i
    }
    return -1
}

function Remove-Preprocessor([string]$src) {
    $out = New-Object System.Text.StringBuilder
    $lines = $src -split "`n", -1
    for ($n = 0; $n -lt $lines.Length; $n++) {
        $line = $lines[$n]
        $trim = $line.TrimStart()
        if ($trim.StartsWith("#")) {
            if ($n -lt $lines.Length - 1) { [void]$out.Append("`n") }
        }
        else {
            [void]$out.Append($line)
            if ($n -lt $lines.Length - 1) { [void]$out.Append("`n") }
        }
    }
    return $out.ToString()
}

function Remove-Comments([string]$src) {
    $out = New-Object System.Text.StringBuilder
    $i = 0
    $n = $src.Length
    while ($i -lt $n) {
        if (Starts-At $src $i "//") {
            while ($i -lt $n -and $src[$i] -ne "`n") { $i++ }
            continue
        }
        if (Starts-At $src $i "/*") {
            $i += 2
            while (($i + 1) -lt $n -and -not (Starts-At $src $i "*/")) {
                if ($src[$i] -eq "`n") { [void]$out.Append("`n") } else { [void]$out.Append(" ") }
                $i++
            }
            $i += 2
            continue
        }
        $step = Skip-StringOrNone $src $i
        if ($step -ge 0) {
            [void]$out.Append($src.Substring($i, $step - $i))
            $i = $step
            continue
        }
        [void]$out.Append($src[$i])
        $i++
    }
    return $out.ToString()
}

function Test-FrameAcceptsType($frames) {
    if ($frames.Count -eq 0) { return $false }
    return $script:AcceptFrames -contains $frames[$frames.Count - 1].Kind
}

function Test-InType($frames) {
    if ($frames.Count -eq 0) { return $false }
    return $script:TypeFrames -contains $frames[$frames.Count - 1].Kind
}

function Test-MemberBoundary([string]$src, [int]$index) {
    $j = $index - 1
    while ($j -ge 0 -and (" `t`r`n".IndexOf($src[$j]) -ge 0)) { $j-- }
    if ($j -lt 0) { return $true }
    $prev = $src[$j]
    return $prev -eq "{" -or $prev -eq "}" -or $prev -eq ";"
}

function Find-TypeBody([string]$src, [int]$i) {
    $depthAngle = 0
    $depthParen = 0
    $depthBrack = 0
    while ($i -lt $src.Length) {
        $step = Skip-StringOrNone $src $i
        if ($step -ge 0) { $i = $step; continue }
        $c = $src[$i]
        if ($c -eq "<") { $depthAngle++ }
        elseif ($c -eq ">" -and $depthAngle -gt 0) { $depthAngle-- }
        elseif ($c -eq "(") { $depthParen++ }
        elseif ($c -eq ")" -and $depthParen -gt 0) { $depthParen-- }
        elseif ($c -eq "[") { $depthBrack++ }
        elseif ($c -eq "]" -and $depthBrack -gt 0) { $depthBrack-- }
        elseif (($c -eq "{" -or $c -eq ";") -and $depthAngle -eq 0 -and $depthParen -eq 0 -and $depthBrack -eq 0) { return $i }
        $i++
    }
    return -1
}

function Try-ParseNamespace([string]$src, [int]$i) {
    $ident = Read-Ident $src $i
    if ($ident.Ident -ne "namespace") { return $null }
    $j = Skip-Ws $src ([int]$ident.Index)
    $dotted = Read-Dotted $src $j
    $j = Skip-Ws $src ([int]$dotted.Index)
    if ($j -lt $src.Length -and $src[$j] -eq ";") { return @{ Name = [string]$dotted.Name; Index = ($j + 1); Brace = $false } }
    if ($j -lt $src.Length -and $src[$j] -eq "{") { return @{ Name = [string]$dotted.Name; Index = $j; Brace = $true } }
    return $null
}

function Try-ParseType([string]$src, [int]$i) {
    $j = $i
    $ident = $null
    $k = $j
    while ($true) {
        $j = Skip-Ws $src $j
        $ident = Read-Ident $src $j
        if ($script:Modifiers -contains $ident.Ident) { $j = [int]$ident.Index; continue }
        $k = [int]$ident.Index
        break
    }
    $kind = $null
    if ($ident.Ident -eq "record") {
        $kind = "record"
        $j = Skip-Ws $src $k
        $nxt = Read-Ident $src $j
        if ($nxt.Ident -eq "class" -or $nxt.Ident -eq "struct") { $j = [int]$nxt.Index }
        else { $j = $k }
    }
    elseif ($script:TypeWords -contains $ident.Ident) {
        $kind = [string]$ident.Ident
        $j = $k
    }
    else { return $null }

    $j = Skip-Ws $src $j
    $name = Read-Ident $src $j
    if (-not $name.Ident) { return $null }
    $end = Find-TypeBody $src ([int]$name.Index)
    if ($end -lt 0 -or $src[$end] -ne "{") { return $null }
    return @{ Kind = $kind; Name = [string]$name.Ident; Index = $end }
}

function Read-Bracket([string]$src, [int]$i) {
    $i++
    $start = $i
    $depth = 1
    while ($i -lt $src.Length -and $depth -gt 0) {
        $step = Skip-StringOrNone $src $i
        if ($step -ge 0) { $i = $step; continue }
        if ($src[$i] -eq "[") { $depth++ }
        elseif ($src[$i] -eq "]") {
            $depth--
            if ($depth -eq 0) { return @{ Body = $src.Substring($start, $i - $start); Index = ($i + 1) } }
        }
        $i++
    }
    $len = $i - $start
    if ($len -lt 0) { $len = 0 }
    return @{ Body = $src.Substring($start, $len); Index = $i }
}

function Split-TopCommas([string]$text) {
    $parts = New-Object System.Collections.Generic.List[string]
    $start = 0
    $depthParen = 0
    $depthAngle = 0
    $depthBrack = 0
    $i = 0
    while ($i -lt $text.Length) {
        $step = Skip-StringOrNone $text $i
        if ($step -ge 0) { $i = $step; continue }
        $c = $text[$i]
        if ($c -eq "(") { $depthParen++ }
        elseif ($c -eq ")" -and $depthParen -gt 0) { $depthParen-- }
        elseif ($c -eq "<") { $depthAngle++ }
        elseif ($c -eq ">" -and $depthAngle -gt 0) { $depthAngle-- }
        elseif ($c -eq "[") { $depthBrack++ }
        elseif ($c -eq "]" -and $depthBrack -gt 0) { $depthBrack-- }
        elseif ($c -eq "," -and $depthParen -eq 0 -and $depthAngle -eq 0 -and $depthBrack -eq 0) {
            [void]$parts.Add($text.Substring($start, $i - $start))
            $start = $i + 1
        }
        $i++
    }
    $tail = $text.Substring($start)
    if ($tail.Trim().Length -gt 0) { [void]$parts.Add($tail) }
    return ,$parts
}

function Read-ParenInner([string]$src, [int]$i) {
    $i++
    $start = $i
    $depth = 1
    while ($i -lt $src.Length -and $depth -gt 0) {
        $step = Skip-StringOrNone $src $i
        if ($step -ge 0) { $i = $step; continue }
        if ($src[$i] -eq "(") { $depth++ }
        elseif ($src[$i] -eq ")") {
            $depth--
            if ($depth -eq 0) { return @{ Body = $src.Substring($start, $i - $start); Index = ($i + 1) } }
        }
        $i++
    }
    return @{ Body = $src.Substring($start); Index = $i }
}

function Split-NameArgs([string]$body) {
    $depth = 0
    $i = 0
    while ($i -lt $body.Length) {
        $step = Skip-StringOrNone $body $i
        if ($step -ge 0) { $i = $step; continue }
        if ($body[$i] -eq "(" -and $depth -eq 0) {
            $inner = Read-ParenInner $body $i
            return @{ Name = $body.Substring(0, $i); Args = [string]$inner.Body }
        }
        if ($body[$i] -eq "(") { $depth++ }
        elseif ($body[$i] -eq ")" -and $depth -gt 0) { $depth-- }
        $i++
    }
    return @{ Name = $body; Args = "" }
}

function Get-SimpleAttributeName([string]$name) {
    $text = $name.Trim()
    if ($text.StartsWith("global::")) { $text = $text.Substring(8) }
    $cut = -1
    $i = 0
    while ($i -lt $text.Length) {
        if (Starts-At $text $i "::") { $i += 2; continue }
        if ($text[$i] -eq ":") { $cut = $i + 1 }
        $i++
    }
    if ($cut -ge 0) { $text = $text.Substring($cut) }
    $text = $text.Trim()
    $dot = $text.LastIndexOf(".")
    if ($dot -ge 0) { $text = $text.Substring($dot + 1) }
    return $text
}

function Convert-CsharpString([string]$raw) {
    $verbatim = $raw.StartsWith('@"')
    if ($verbatim) { $body = $raw.Substring(2) } else { $body = $raw.Substring(1) }
    if ($body.EndsWith('"')) { $body = $body.Substring(0, $body.Length - 1) }
    if ($verbatim) { return $body.Replace('""', '"') }
    $out = New-Object System.Text.StringBuilder
    $i = 0
    while ($i -lt $body.Length) {
        if ($body[$i] -ne "\") { [void]$out.Append($body[$i]); $i++; continue }
        $i++
        if ($i -ge $body.Length) { break }
        $esc = $body[$i]
        if ($esc -eq "n") { [void]$out.Append("`n"); $i++ }
        elseif ($esc -eq "r") { [void]$out.Append("`r"); $i++ }
        elseif ($esc -eq "t") { [void]$out.Append("`t"); $i++ }
        elseif ($esc -eq "\") { [void]$out.Append("\"); $i++ }
        elseif ($esc -eq '"') { [void]$out.Append('"'); $i++ }
        elseif ($esc -eq "'") { [void]$out.Append("'"); $i++ }
        elseif ($esc -eq "0") { [void]$out.Append([char]0); $i++ }
        elseif ($esc -eq "u" -and ($i + 4) -lt $body.Length) {
            $hex = $body.Substring($i + 1, 4)
            [void]$out.Append([char][Convert]::ToInt32($hex, 16))
            $i += 5
        }
        else { [void]$out.Append($esc); $i++ }
    }
    return $out.ToString()
}

function Convert-Value([string]$raw) {
    if ($raw.StartsWith('@"') -or $raw.StartsWith('"')) { return Convert-CsharpString $raw }
    if ($raw.ToLowerInvariant() -eq "true") { return $true }
    if ($raw.ToLowerInvariant() -eq "false") { return $false }
    return $raw
}

function Convert-NamedArgs([string]$argsText) {
    $values = @{}
    $parts = Split-TopCommas $argsText
    foreach ($part in $parts) {
        $eq = $part.IndexOf("=")
        if ($eq -lt 0) { continue }
        $key = $part.Substring(0, $eq).Trim()
        $raw = $part.Substring($eq + 1).Trim()
        $values[$key] = Convert-Value $raw
    }
    return $values
}

function Split-Attributes([string]$body) {
    $found = New-Object System.Collections.Generic.List[object]
    $parts = Split-TopCommas $body
    foreach ($part in $parts) {
        $split = Split-NameArgs $part
        $simple = Get-SimpleAttributeName ([string]$split.Name)
        if ($script:DisplayNames -contains $simple) {
            [void]$found.Add((Convert-NamedArgs ([string]$split.Args)))
        }
    }
    return ,$found
}

function Read-AttributeGroup([string]$src, [int]$i) {
    $attrs = New-Object System.Collections.Generic.List[object]
    while ($i -lt $src.Length -and $src[$i] -eq "[") {
        $bracket = Read-Bracket $src $i
        $i = [int]$bracket.Index
        $parsed = Split-Attributes ([string]$bracket.Body)
        foreach ($item in $parsed) { [void]$attrs.Add($item) }
        $i = Skip-Ws $src $i
    }
    return @{ Attrs = $attrs; Index = $i }
}

function Read-Member([string]$src, [int]$i) {
    $start = $i
    while ($i -lt $src.Length) {
        $step = Skip-StringOrNone $src $i
        if ($step -ge 0) { $i = $step; continue }
        if (Starts-At $src $i "=>") { return @{ Member = $src.Substring($start, $i - $start); Index = ($i + 2); Term = "=>" } }
        if ($src[$i] -eq "{" -or $src[$i] -eq ";") { return @{ Member = $src.Substring($start, $i - $start); Index = $i; Term = [string]$src[$i] } }
        $i++
    }
    return @{ Member = $src.Substring($start); Index = $i; Term = "" }
}

function Skip-Balanced([string]$text, [int]$i, [char]$openCh, [char]$closeCh) {
    $depth = 0
    while ($i -lt $text.Length) {
        $step = Skip-StringOrNone $text $i
        if ($step -ge 0) { $i = $step; continue }
        if ($text[$i] -eq $openCh) { $depth++ }
        elseif ($text[$i] -eq $closeCh) {
            $depth--
            if ($depth -eq 0) { return $i + 1 }
        }
        $i++
    }
    return $i
}

function Remove-Generics([string]$text) {
    $out = New-Object System.Text.StringBuilder
    $i = 0
    while ($i -lt $text.Length) {
        $step = Skip-StringOrNone $text $i
        if ($step -ge 0) {
            [void]$out.Append($text.Substring($i, $step - $i))
            $i = $step
            continue
        }
        if ($text[$i] -eq "<") {
            $i = Skip-Balanced $text $i "<" ">"
            [void]$out.Append(" ")
            continue
        }
        if ($text[$i] -eq "[") {
            $i = Skip-Balanced $text $i "[" "]"
            [void]$out.Append(" ")
            continue
        }
        [void]$out.Append($text[$i])
        $i++
    }
    return $out.ToString()
}

function Test-HasWord([string]$text, [string]$word) {
    $i = 0
    while ($i -lt $text.Length) {
        $ident = Read-Ident $text $i
        if ($ident.Ident -eq $word) { return $true }
        if ($ident.Ident) { $i = [int]$ident.Index } else { $i++ }
    }
    return $false
}

function Test-HasParen([string]$text) {
    $depthAngle = 0
    $depthBrack = 0
    $i = 0
    while ($i -lt $text.Length) {
        $step = Skip-StringOrNone $text $i
        if ($step -ge 0) { $i = $step; continue }
        $c = $text[$i]
        if ($c -eq "<") { $depthAngle++ }
        elseif ($c -eq ">" -and $depthAngle -gt 0) { $depthAngle-- }
        elseif ($c -eq "[") { $depthBrack++ }
        elseif ($c -eq "]" -and $depthBrack -gt 0) { $depthBrack-- }
        elseif ($c -eq "(" -and $depthAngle -eq 0 -and $depthBrack -eq 0) { return $true }
        $i++
    }
    return $false
}

function Get-PropertyName([string]$member) {
    if ((Test-HasWord $member "event") -or (Test-HasWord $member "delegate") -or (Test-HasWord $member "operator")) { return $null }
    if (Test-HasWord $member "this") { return $null }
    if (Test-HasParen $member) { return $null }
    $cleaned = Remove-Generics $member
    $idents = New-Object System.Collections.Generic.List[string]
    $i = 0
    while ($i -lt $cleaned.Length) {
        $ident = Read-Ident $cleaned $i
        if ($ident.Ident) { [void]$idents.Add([string]$ident.Ident); $i = [int]$ident.Index }
        else { $i++ }
    }
    if ($idents.Count -eq 0) { return $null }
    return $idents[$idents.Count - 1]
}

function Get-NamespaceOf($frames, [string]$fileNamespace) {
    $spaces = New-Object System.Collections.Generic.List[string]
    $classes = New-Object System.Collections.Generic.List[string]
    foreach ($frame in $frames) {
        if ($frame.Kind -eq "namespace" -and $frame.Name) { [void]$spaces.Add([string]$frame.Name) }
        if ($script:TypeFrames -contains $frame.Kind -and $frame.Name) { [void]$classes.Add([string]$frame.Name) }
    }
    if ($spaces.Count -gt 0) { $ns = ($spaces -join ".") } else { $ns = $fileNamespace }
    $className = $null
    if ($classes.Count -gt 0) { $className = $classes[$classes.Count - 1] }
    return @{ Namespace = $ns; Class = $className }
}

function Convert-MemberRow($attrs, [string]$member, [string]$className, [string]$namespace) {
    if ($null -eq $attrs -or $attrs.Count -eq 0 -or [string]::IsNullOrEmpty($className)) { return $null }
    $chosen = $null
    foreach ($args in $attrs) {
        if ($args.ContainsKey("AutoGenerateField") -and $args["AutoGenerateField"] -eq $false) { return $null }
        $chosen = $args
    }
    if ($null -eq $chosen) { return $null }
    $name = Get-PropertyName $member
    if (-not $name) { return $null }
    $short = $null
    $displayName = $null
    if ($chosen.ContainsKey("ShortName")) { $short = [string]$chosen["ShortName"] }
    if ($chosen.ContainsKey("Name")) { $displayName = [string]$chosen["Name"] }
    $caption = $null
    $source = $null
    if (-not [string]::IsNullOrWhiteSpace($short)) { $caption = $short; $source = "ShortName" }
    elseif (-not [string]::IsNullOrWhiteSpace($displayName)) { $caption = $displayName; $source = "Name" }
    else { return $null }
    return @{
        Key = ($className + "_" + $name)
        ShortName = $caption
        Source = $source
        English = ""
        Class = $className
        Property = $name
        Namespace = $namespace
        File = ""
    }
}

function Read-SourceRows([string]$src) {
    $src = Remove-Comments $src
    $rows = New-Object System.Collections.Generic.List[object]
    $frames = New-Object System.Collections.Generic.List[object]
    [void]$frames.Add(@{ Kind = "file"; Name = $null })
    $fileNamespace = ""
    $i = 0
    $n = $src.Length
    while ($i -lt $n) {
        $i = Skip-Ws $src $i
        if ($i -ge $n) { break }
        if ($src[$i] -eq "{") { [void]$frames.Add(@{ Kind = "block"; Name = $null }); $i++; continue }
        if ($src[$i] -eq "}") {
            if ($frames.Count -gt 1) { $frames.RemoveAt($frames.Count - 1) }
            $i++
            continue
        }
        $step = Skip-StringOrNone $src $i
        if ($step -ge 0) { $i = $step; continue }
        $ident = Read-Ident $src $i
        if ($ident.Ident -and (Test-FrameAcceptsType $frames)) {
            $ns = Try-ParseNamespace $src $i
            if ($null -ne $ns) {
                if ($ns.Brace) {
                    [void]$frames.Add(@{ Kind = "namespace"; Name = [string]$ns.Name })
                    $i = [int]$ns.Index + 1
                }
                else {
                    $fileNamespace = [string]$ns.Name
                    $i = [int]$ns.Index
                }
                continue
            }
            $type = Try-ParseType $src $i
            if ($null -ne $type) {
                [void]$frames.Add(@{ Kind = [string]$type.Kind; Name = [string]$type.Name })
                $i = [int]$type.Index + 1
                continue
            }
        }
        if ($src[$i] -eq "[" -and (Test-InType $frames) -and (Test-MemberBoundary $src $i)) {
            $group = Read-AttributeGroup $src $i
            $j = Skip-Ws $src ([int]$group.Index)
            if ($null -ne (Try-ParseType $src $j)) { $i = $j; continue }
            $member = Read-Member $src $j
            $i = [int]$member.Index
            if ($member.Term -eq "{" -or $member.Term -eq "=>" -or $member.Term -eq ";") {
                $where = Get-NamespaceOf $frames $fileNamespace
                $row = Convert-MemberRow $group.Attrs ([string]$member.Member) ([string]$where.Class) ([string]$where.Namespace)
                if ($null -ne $row) { [void]$rows.Add($row) }
            }
            continue
        }
        if ($ident.Ident) { $i = [int]$ident.Index } else { $i++ }
    }
    return ,$rows
}

function Get-RelativePath([string]$file, [string]$root) {
    $full = [System.IO.Path]::GetFullPath($file)
    $rootFull = [System.IO.Path]::GetFullPath($root)
    if (-not $rootFull.EndsWith([char]92) -and -not $rootFull.EndsWith([char]47)) {
        $rootFull = $rootFull + [System.IO.Path]::DirectorySeparatorChar
    }
    if ($full.StartsWith($rootFull, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $full.Substring($rootFull.Length)
    }
    return $full
}

function Test-SkippedPath([string]$fullName) {
    $parts = $fullName.Split([char[]]@("\", "/"))
    foreach ($part in $parts) {
        if ($script:SkipDirs -contains $part) { return $true }
    }
    return $false
}

function Add-CsFiles([string]$dir, $list) {
    foreach ($entry in [System.IO.Directory]::EnumerateFileSystemEntries($dir)) {
        $name = [System.IO.Path]::GetFileName($entry)
        if ($script:SkipDirs -contains $name) { continue }
        if ([System.IO.Directory]::Exists($entry)) {
            Add-CsFiles $entry $list
            continue
        }
        if (-not $name.EndsWith(".cs")) { continue }
        if ($name.EndsWith(".Designer.cs") -or $name.EndsWith(".g.cs")) { continue }
        [void]$list.Add($entry)
    }
}

function Read-ModelFiles([string]$root) {
    $rows = New-Object System.Collections.Generic.List[object]
    $item = Get-Item -LiteralPath $root
    $files = New-Object System.Collections.Generic.List[string]
    if ($item.PSIsContainer) {
        $base = $item.FullName
        Write-Host ("Listing cs: " + $base)
        Add-CsFiles $item.FullName $files
    }
    else {
        $base = Split-Path -Parent $item.FullName
        [void]$files.Add($item.FullName)
    }
    Write-Host ("cs files: " + $files.Count)
    $seen = 0
    foreach ($full in $files) {
        $seen++
        if (($seen % 25) -eq 0) { Write-Host ("read " + $seen + "/" + $files.Count) }
        $text = [System.IO.File]::ReadAllText($full)
        if ($text.IndexOf("Display", [System.StringComparison]::Ordinal) -lt 0) { continue }
        $name = [System.IO.Path]::GetFileName($full)
        Write-Host ("Display: " + $name)
        $text = Remove-Preprocessor $text
        $found = Read-SourceRows $text
        foreach ($row in $found) {
            $row.File = Get-RelativePath $full $base
            [void]$rows.Add($row)
        }
    }
    return ,$rows
}

function Read-Resx([string]$path) {
    $values = @{}
    if ([string]::IsNullOrWhiteSpace($path) -or -not (Test-Path -LiteralPath $path)) {
        if (-not [string]::IsNullOrWhiteSpace($path)) { Write-Host ((T 0x43d,0x435,0x442,0x20,0x72,0x65,0x73,0x78,0x3a,0x20) + $path) }
        return $values
    }
    $xml = New-Object System.Xml.XmlDocument
    $xml.Load((Resolve-Path -LiteralPath $path))
    foreach ($node in $xml.GetElementsByTagName("data")) {
        $name = $node.GetAttribute("name")
        $valueNode = $node.SelectSingleNode("value")
        if ($name -and $null -ne $valueNode -and $valueNode.InnerText) { $values[$name] = $valueNode.InnerText }
    }
    return $values
}

function Format-CsvField([string]$value) {
    if ($null -eq $value) { return "" }
    $chars = @([char]";", [char]'"', [char]10, [char]13)
    if ($value.IndexOfAny($chars) -ge 0) { return '"' + $value.Replace('"', '""') + '"' }
    return $value
}

function Write-CsvFile([string]$path, $rows) {
    $dir = Split-Path -Parent $path
    if ($dir -and -not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }
    $lines = New-Object System.Collections.Generic.List[string]
    [void]$lines.Add("Key;ShortName;English;Source;Class;Property;Namespace;File")
    foreach ($row in $rows) {
        $fields = @(
            (Format-CsvField $row.Key),
            (Format-CsvField $row.ShortName),
            (Format-CsvField $row.English),
            (Format-CsvField $row.Source),
            (Format-CsvField $row.Class),
            (Format-CsvField $row.Property),
            (Format-CsvField $row.Namespace),
            (Format-CsvField $row.File)
        )
        [void]$lines.Add($fields -join ";")
    }
    $utf8 = New-Object System.Text.UTF8Encoding $true
    [System.IO.File]::WriteAllLines($path, $lines.ToArray(), $utf8)
}

function Write-DuplicateWarnings($rows) {
    $seen = @{}
    foreach ($row in $rows) {
        if (-not $seen.ContainsKey($row.Key)) { $seen[$row.Key] = New-Object System.Collections.Generic.List[string] }
        [void]$seen[$row.Key].Add([string]$row.File)
    }
    foreach ($key in ($seen.Keys | Sort-Object)) {
        if ($seen[$key].Count -gt 1) {
            $dup = T 0x43e,0x434,0x438,0x43d,0x20,0x43a,0x43b,0x44e,0x447,0x20,0x432,0x20,0x43d,0x435,0x441,0x43a,0x43e,0x43b,0x44c,0x43a,0x438,0x445,0x20,0x444,0x430,0x439,0x43b,0x430,0x445
            Write-Host ("{0}: {1} ({2})" -f $dup, $key, ($seen[$key] -join ", "))
        }
    }
}

function Invoke-SelfTest {
    $stroka = T 0x421,0x442,0x440,0x43e,0x43a,0x430
    $serial = T 0x421,0x435,0x440,0x438,0x439,0x43d,0x44b,0x439,0x20,0x43d,0x43e,0x43c,0x435,0x440
    $imya = T 0x418,0x43c,0x44f
    $imyaFull = T 0x418,0x43c,0x44f,0x2c,0x20,0x43f,0x43e,0x43b,0x43d,0x43e,0x435
    $metod = T 0x41c,0x435,0x442,0x43e,0x434
    $nested = T 0x412,0x43b,0x43e,0x436,0x435,0x43d,0x43d,0x44b,0x439,0x20,0x442,0x438,0x43f
    $vnutri = T 0x412,0x43d,0x443,0x442,0x440,0x438
    $obob = T 0x41e,0x431,0x43e,0x431,0x449,0x435,0x43d,0x438,0x435
    $seria = T 0x421,0x435,0x440,0x438,0x44f
    $onlyName = T 0x422,0x43e,0x43b,0x44c,0x43a,0x43e,0x20,0x438,0x43c,0x44f
    $fromIface = T 0x418,0x437,0x20,0x438,0x43d,0x442,0x435,0x440,0x444,0x435,0x439,0x441,0x430
    $sample = @"
namespace Store.Device.Models
{
    public class VerifyIdentifierChange
    {
        [Display(AutoGenerateField = false)]
        public bool ChBoxS { get; set; }

        // [Display(ShortName = "ignored")]
        public string Ignored { get; set; }

        [Display(Name = "$stroka", Description = "$stroka")]
        public override decimal? LineNo { get; set; }

        [Display(Name = "$serial", ShortName = "$serial", Description = "$serial")]
        public override string SerialNumber { get; set; }

        [Display(
            Name = "$imya",
            ShortName = "$imyaFull")]
        public string Title { get; set; }

        [Display(ShortName = "$metod")]
        public void Run() { }

        [Display(ShortName = "$nested")]
        public class Inner
        {
            [Display(ShortName = "$vnutri")]
            public string Name { get; set; }
        }
    }

    public class Box<T>
    {
        [Display(ShortName = "$obob")]
        public string Value { get; set; }
    }
}
"@
    $rows = Read-SourceRows $sample
    $got = @{}
    foreach ($row in $rows) { $got[$row.Key] = $row.ShortName }
    $expected = @{
        "VerifyIdentifierChange_LineNo" = $stroka
        "VerifyIdentifierChange_SerialNumber" = $serial
        "VerifyIdentifierChange_Title" = $imyaFull
        "Inner_Name" = $vnutri
        "Box_Value" = $obob
    }
    foreach ($key in $expected.Keys) {
        if ($got[$key] -ne $expected[$key]) {
            Write-Host ("self-test: {0} = {1}" -f $key, $got[$key])
            exit 1
        }
    }
    if ($got.Count -ne $expected.Count) {
        Write-Host ("self-test count " + $got.Count)
        exit 1
    }

    $extra = @"
namespace Store.Models;

public class AfterMethod
{
    public void Run()
    {
        var values = new int[2];
        values[0] = 1;
    }

    [Display(ShortName = @"$seria ""A""")]
    public string Serial { get; set; }

    [Display(
        // comment
        Name = "$onlyName")]
    public int Count { get; set; }
}

public interface IRow
{
    [System.ComponentModel.DataAnnotations.DisplayAttribute(ShortName = "$fromIface")]
    string Title { get; set; }
}
"@
    $rows = Read-SourceRows $extra
    $got = @{}
    foreach ($row in $rows) { $got[$row.Key] = $row.ShortName }
    $seriaA = $seria + ' "A"'
    if ($got["AfterMethod_Serial"] -ne $seriaA -or $got["AfterMethod_Count"] -ne $onlyName -or $got["IRow_Title"] -ne $fromIface -or $got.Count -ne 3) {
        Write-Host "self-test extra failed"
        foreach ($key in $got.Keys) { Write-Host ($key + " = " + $got[$key]) }
        exit 1
    }
    Write-Host "self-test ok"
    exit 0
}

if ($SelfTest) { Invoke-SelfTest }

try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch {}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if ([string]::IsNullOrWhiteSpace($scriptDir)) { $scriptDir = (Get-Location).Path }
if (-not $PSBoundParameters.ContainsKey("Output")) {
    $Output = Join-Path $scriptDir "display-keys.csv"
}
if ($null -eq $Path -or $Path.Count -eq 0) {
    $Path = @($scriptDir)
    Write-Host ("No -Path, scan the script folder: " + $scriptDir)
}

$Output = [System.IO.Path]::GetFullPath($Output)
Write-Host ("CSV: " + $Output)

$englishMap = Read-Resx $English
$all = New-Object System.Collections.Generic.List[object]
foreach ($root in $Path) {
    if (-not (Test-Path -LiteralPath $root)) {
        Write-Host ((T 0x43d,0x435,0x442,0x20,0x43f,0x443,0x442,0x438,0x3a,0x20) + $root)
        exit 2
    }
    $found = Read-ModelFiles $root
    foreach ($row in $found) { [void]$all.Add($row) }
}

$sorted = @($all | Sort-Object Key, File)
Write-DuplicateWarnings $sorted
$missing = 0
foreach ($row in $sorted) {
    if ($englishMap.ContainsKey($row.Key)) { $row.English = [string]$englishMap[$row.Key] }
    if ([string]::IsNullOrEmpty($row.English)) { $missing++ }
}
Write-CsvFile $Output $sorted
Write-Host ("Collected {0}. Empty English: {1}." -f $sorted.Count, $missing)
Write-Host ("CSV: " + $Output)
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
exit 0
