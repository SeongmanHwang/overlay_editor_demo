param(
  # Project root to analyze. Typically repository root.
  [string]$Root = ".",

  # File extensions counted toward "code-like" line totals.
  [string[]]$CodeExt = @(".cs",".xaml",".csproj",".json",".md",".xml"),

  # Directories to exclude from ALL file enumeration (regex against full path).
  # Default excludes build artifacts and the metrics tooling/output itself.
  [string]$ExcludeDirRegex = '\\(bin|obj|\.git|tools\\metrics|docs\\metrics)\\',

  # How many top folders to show in the summary.
  [int]$TopFolderCount = 20,

  # Optional: write the generated markdown summary to this path (relative to Root if relative).
  [string]$WriteMarkdownReport = "",

  # Optional: update a README file by replacing content between markers.
  # Markers must exist:
  #   <!-- METRICS:START -->
  #   <!-- METRICS:END -->
  [string]$UpdateReadme = ""
)

$ErrorActionPreference = "Stop"

function Resolve-FullPath([string]$Path, [string]$BaseDir) {
  if ([string]::IsNullOrWhiteSpace($Path)) { return "" }
  if ([System.IO.Path]::IsPathRooted($Path)) {
    return [System.IO.Path]::GetFullPath($Path)
  }
  return [System.IO.Path]::GetFullPath((Join-Path $BaseDir $Path))
}

function Get-LineCount([string]$Path) {
  try {
    return [int64][System.Linq.Enumerable]::Count([System.IO.File]::ReadLines($Path))
  } catch {
    return 0
  }
}

$rootFull = (Resolve-Path $Root).Path.TrimEnd('\')

# Compute paths to exclude as files (script itself + report file if requested)
$excludeFiles = New-Object System.Collections.Generic.HashSet[string]
if ($MyInvocation.MyCommand.Path) {
  $excludeFiles.Add((Resolve-Path $MyInvocation.MyCommand.Path).Path) | Out-Null
}

$reportFull = ""
if ($WriteMarkdownReport) {
  $reportFull = Resolve-FullPath $WriteMarkdownReport $rootFull
  if ($reportFull) { $excludeFiles.Add($reportFull) | Out-Null }
}

# Enumerate files
$allFiles = Get-ChildItem -Path $rootFull -Recurse -File | Where-Object {
  $_.FullName -notmatch $ExcludeDirRegex -and -not $excludeFiles.Contains($_.FullName)
}

$codeFiles = $allFiles | Where-Object { $CodeExt -contains $_.Extension.ToLowerInvariant() }

# Totals
$totalFiles = $allFiles.Count
$totalCodeFiles = $codeFiles.Count
$totalLines = 0
foreach ($f in $codeFiles) { $totalLines += (Get-LineCount $f.FullName) }

# By extension (line count)
$byExt = $CodeExt | ForEach-Object {
  $ext = $_
  $fs = $allFiles | Where-Object { $_.Extension.ToLowerInvariant() -eq $ext }
  $lines = 0
  foreach ($f in $fs) { $lines += (Get-LineCount $f.FullName) }
  [pscustomobject]@{ Extension=$ext; Files=$fs.Count; Lines=[int64]$lines }
} | Sort-Object Lines -Descending

# Top folders (line count)
$folderAgg = @{}
foreach ($f in $codeFiles) {
  $rel = $f.FullName.Substring($rootFull.Length).TrimStart('\')
  $dir = Split-Path $rel -Parent
  if ([string]::IsNullOrWhiteSpace($dir)) { $dir = "." }
  if (-not $folderAgg.ContainsKey($dir)) { $folderAgg[$dir] = [int64]0 }
  $folderAgg[$dir] += (Get-LineCount $f.FullName)
}
$topFoldersByLines = $folderAgg.GetEnumerator() |
  ForEach-Object { [pscustomobject]@{ Dir=$_.Key; Lines=$_.Value } } |
  Sort-Object Lines -Descending |
  Select-Object -First $TopFolderCount

# Markdown summary
$date = (Get-Date).ToString("yyyy-MM-dd")
$rootDisplay = Split-Path $rootFull -Leaf
if ([string]::IsNullOrWhiteSpace($rootDisplay)) { $rootDisplay = "." }
$md = @()
$md += "#### Project metrics ($date)"
$md += ""
$md += "- Root: `"$rootDisplay`""
$md += "- Files (excluding bin/obj/.git/tools/metrics/docs/metrics): **$totalFiles**"
$md += "- Code-like files ($($CodeExt -join ', ')): **$totalCodeFiles**"
$md += "- Total lines (code-like): **$totalLines**"
$md += ""
$md += "**By extension (lines)**"
$md += ""
$md += "| Ext | Files | Lines |"
$md += "|---:|---:|---:|"
foreach ($row in $byExt) { $md += "| $($row.Extension) | $($row.Files) | $($row.Lines) |" }
$md += ""
$md += "**Top folders by lines**"
$md += ""
$md += "| Folder | Lines |"
$md += "|---|---:|"
foreach ($row in $topFoldersByLines) { $md += "| $($row.Dir) | $($row.Lines) |" }
$mdText = ($md -join "`r`n")

# Output to console
$mdText

# Write report (optional)
if ($reportFull) {
  $outDir = Split-Path $reportFull -Parent
  if ($outDir) { New-Item -ItemType Directory -Force -Path $outDir | Out-Null }
  Set-Content -Path $reportFull -Value $mdText -Encoding UTF8
}

# Update README between markers (optional)
if ($UpdateReadme) {
  $readmeFull = Resolve-FullPath $UpdateReadme $rootFull
  $start = "<!-- METRICS:START -->"
  $end = "<!-- METRICS:END -->"

  $readme = Get-Content -Path $readmeFull -Raw -Encoding UTF8
  if ($readme -notmatch [regex]::Escape($start) -or $readme -notmatch [regex]::Escape($end)) {
    throw "README markers not found. Add markers: $start and $end"
  }

  $pattern = "(?s)$([regex]::Escape($start)).*?$([regex]::Escape($end))"
  $replacement = "$start`r`n$mdText`r`n$end"
  $newReadme = [regex]::Replace($readme, $pattern, $replacement)
  Set-Content -Path $readmeFull -Value $newReadme -Encoding UTF8
}

