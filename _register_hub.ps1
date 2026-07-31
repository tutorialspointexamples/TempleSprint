$hub = Join-Path $env:APPDATA "UnityHub"
$project = "C:\Users\dagar\OneDrive\Desktop\Temple Run"
$now = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
New-Item -ItemType Directory -Force $hub | Out-Null
$pEsc = $project.Replace('\','\\')
$manual = @"
{
  "schema_version": "v1",
  "data": {
    "$pEsc": {
      "title": "Temple Sprint",
      "lastModified": $now,
      "lastOpened": $now,
      "isCustomEditor": false,
      "path": "$pEsc",
      "containingFolderPath": "C:\\Users\\dagar\\OneDrive\\Desktop",
      "version": "6000.5.6f1",
      "architecture": "x86_64",
      "changeset": "0e0577a1a2ac",
      "isFavorite": true,
      "cloudEnabled": false
    }
  }
}
"@
Set-Content -LiteralPath (Join-Path $hub "projects-v1.json") -Value $manual -Encoding utf8
Set-Content -LiteralPath (Join-Path $hub "projectDir.json") -Value '{"directoryPath":"C:\\Users\\dagar\\OneDrive\\Desktop\\Temple Run"}' -Encoding utf8
Write-Host "OK registered Temple Sprint"
