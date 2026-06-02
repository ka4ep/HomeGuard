param(
    [Parameter(Mandatory=$true)]
    [string]$Source,

    [Parameter(Mandatory=$true)]
    [string]$Destination,

    [string[]]$Extensions = @(".cs", ".razor", ".cshtml", ".js", ".css")
)

$Source      = (Resolve-Path $Source).Path.TrimEnd('\')
$Destination = (Resolve-Path $Destination).Path.TrimEnd('\')

# Определяем порог: самая свежая дата файла в Destination
$existingFiles = Get-ChildItem -Path $Destination -File -ErrorAction SilentlyContinue
$threshold = $null

if ($existingFiles.Count -gt 0) {
    $threshold = ($existingFiles | Measure-Object -Property LastWriteTime -Maximum).Maximum
    Write-Host "Incremental mode: copying files newer than $threshold"
} else {
    Write-Host "Full copy mode: destination is empty"
}

# Собираем файлы из Source
$files = Get-ChildItem -Path $Source -Recurse -File |
         Where-Object { $Extensions -contains $_.Extension }

# Фильтруем по дате если нужно
if ($null -ne $threshold) {
    $files = $files | Where-Object { $_.LastWriteTime -gt $threshold }
}

if ($files.Count -eq 0) {
    Write-Host "Nothing to copy — everything is up to date."
    exit 0
}

foreach ($file in $files) {
    $relative = $file.FullName.Substring($Source.Length + 1)
    $flatName = $relative -replace '[/\\]', '.'
    $dest     = Join-Path $Destination $flatName

    Copy-Item -Path $file.FullName -Destination $dest -ErrorAction Stop
    Write-Host "Copied: $relative  ->  $flatName"
}

Write-Host "`nDone. $($files.Count) file(s) copied."