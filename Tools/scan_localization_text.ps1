$root = "d:\Unity Project\SRPG-SRPG\Assets"

function Unescape-YamlText([string]$text) {
    $text = $text.Trim()
    if ($text.StartsWith("'") -and $text.EndsWith("'")) { return $text.Substring(1, $text.Length - 2) }
    if ($text.StartsWith('"') -and $text.EndsWith('"')) { return $text.Substring(1, $text.Length - 2) }
    return $text
}

function Make-Key([string]$goName) {
    $base = ($goName -replace '[^a-zA-Z0-9]+', '_').Trim('_')
    if ([string]::IsNullOrWhiteSpace($base)) { $base = "Text" }
    return "${base}_key"
}

function Parse-UnityFile([string]$path) {
    $content = Get-Content -Path $path -Raw -Encoding UTF8
    $goNames = @{}
    $goParents = @{}

    $blocks = $content -split '--- !u!1 &'
    foreach ($block in $blocks) {
        if ($block -notmatch '^(\d+)') { continue }
        $fid = $Matches[1]
        if ($block -match "`n  m_Name: (.+)") { $goNames[$fid] = $Matches[1].Trim() }
        if ($block -match "`n  m_Father: \{fileID: (\d+)\}") { $goParents[$fid] = $Matches[1] }
    }

    function Get-ParentName($goId) {
        $parentId = $goParents[$goId]
        if ($parentId -and $parentId -ne "0" -and $goNames.ContainsKey($parentId)) {
            return $goNames[$parentId]
        }
        return "Root"
    }

    $results = @()
    $seen = @{}

    $compBlocks = $content -split '--- !u!'
    foreach ($block in $compBlocks) {
        if ($block -notmatch '  m_GameObject: \{fileID: (\d+)\}') { continue }
        $goId = $Matches[1]
        $goName = if ($goNames.ContainsKey($goId)) { $goNames[$goId] } else { "Unknown" }

        $text = $null
        $compType = $null
        if ($block -match '  m_text: (.+)') {
            $text = Unescape-YamlText $Matches[1]
            $compType = "TextMeshProUGUI"
        }
        elseif ($block -match '  m_Text: (.+)') {
            $text = Unescape-YamlText $Matches[1]
            $compType = "TextLegacy"
        }

        if ($null -eq $text) { continue }
        $dedup = "$goName|$text|$compType"
        if ($seen.ContainsKey($dedup)) { continue }
        $seen[$dedup] = $true

        $results += [PSCustomObject]@{
            gameObject   = $goName
            parent       = (Get-ParentName $goId)
            type         = $compType
            text         = $text
            suggestedKey = (Make-Key $goName)
        }
    }

    foreach ($m in [regex]::Matches($content, '    - m_Text: (.+)')) {
        $text = Unescape-YamlText $m.Groups[1].Value
        $dedup = "DropdownOption|$text|Dropdown"
        if ($seen.ContainsKey($dedup)) { continue }
        $seen[$dedup] = $true
        $optKey = (($text -replace '[^a-zA-Z0-9]+', '_').Trim('_')) + "_key"
        $results += [PSCustomObject]@{
            gameObject   = "DropdownOption"
            parent       = "Dropdown"
            type         = "Dropdown"
            text         = $text
            suggestedKey = $optKey
        }
    }

    return $results
}

function Get-TableName([string]$path) {
    $name = [System.IO.Path]::GetFileNameWithoutExtension($path)
    $mapping = @{
        "0_Bootstrap" = $null
        "1_Login" = "Login"
        "2_DevConnect" = "DevConnect"
        "3_MainMenu" = "MainMenu"
        "4_Lobby" = "Lobby"
        "4_SingleStoryLobby" = "StoryLobby"
        "5_ClientMatch" = "ClientMatch"
        "5_ServerMatch" = "ServerMatch"
        "6_MatchResult" = "MatchResult"
        "7_UnitList" = "UnitList"
        "8_UnitConfig" = "UnitConfig"
        "9_Gacha" = "Gacha"
        "C0_S0" = "Story_C0_S0"
        "C0_S1" = "Story_C0_S1"
        "C0_S1_Server" = "Story_C0_S1_Server"
        "UnitContainer" = "Shared_UnitContainer"
        "OptionContainer" = "Shared_OptionContainer"
        "LoadingScreen" = "Shared_LoadingScreen"
        "MatchSessionCanvas" = "Shared_MatchSession"
        "UnitCanvas" = "Shared_UnitCanvas"
    }
    if ($mapping.ContainsKey($name)) { return $mapping[$name] }
    if ($name.StartsWith("C")) { return "Story_$name" }
    if ($path -like "*Prefabs*") { return "Shared_$name" }
    return $name
}

$tables = @{}

Get-ChildItem -Path "$root\Scenes" -Filter "*.unity" -Recurse | ForEach-Object {
    $table = Get-TableName $_.FullName
    if ($null -eq $table) { return }
    $entries = Parse-UnityFile $_.FullName
    if (-not $tables.ContainsKey($table)) {
        $tables[$table] = @{ source = $_.FullName.Replace("$root\", "Assets/"); entries = @() }
    }
    $tables[$table].entries += $entries
}

Get-ChildItem -Path "$root\Prefabs" -Filter "*.prefab" -Recurse | ForEach-Object {
    $entries = Parse-UnityFile $_.FullName
    if ($entries.Count -eq 0) { return }
    $table = Get-TableName $_.FullName
    if (-not $tables.ContainsKey($table)) {
        $tables[$table] = @{ source = $_.FullName.Replace("$root\", "Assets/"); entries = @() }
    }
    $tables[$table].entries += $entries
}

$outDir = "d:\Unity Project\SRPG-SRPG\Tools"
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir | Out-Null }

$report = @()
foreach ($table in ($tables.Keys | Sort-Object)) {
    $count = $tables[$table].entries.Count
    Write-Output "$table : $count entries"
    $report += "=== TABLE: $table (source: $($tables[$table].source)) ==="
    foreach ($e in $tables[$table].entries) {
        $report += "  KEY: $($e.suggestedKey)"
        $report += "    GO: $($e.gameObject) | Parent: $($e.parent) | Type: $($e.type)"
        $report += "    EN: $($e.text)"
        $report += ""
    }
    $report += ""
}

$report | Out-File -FilePath "$outDir\localization_scan_report.txt" -Encoding UTF8
Write-Output "Report written to Tools/localization_scan_report.txt"
