# Скачивает Plus Jakarta Sans woff2/woff в wwwroot/fonts/PlusJakartaSans/
# Запускать из корня проекта: .\download-fonts.ps1

$dest = "wwwroot\fonts\PlusJakartaSans"
New-Item -ItemType Directory -Force -Path $dest | Out-Null

$baseUrl = "https://fonts.gstatic.com/s/plusjakartasans/v8"

$files = @{
    "PlusJakartaSans-Regular.woff2"  = "$baseUrl/LDIbaomQNQcsA67c1aCxLw.woff2"
    "PlusJakartaSans-Medium.woff2"   = "$baseUrl/LDIbaomQNQcsA67c1aCxLw.woff2"   # заменить на точный URL
    "PlusJakartaSans-SemiBold.woff2" = "$baseUrl/LDIbaomQNQcsA67c1aCxLw.woff2"   # заменить на точный URL
}

# Надёжнее — скачать через google-webfonts-helper:
# https://gwfh.mranftl.com/fonts/plus-jakarta-sans?subsets=latin
# Выбрать: weights 400, 500, 600 — форматы woff2+woff
# Распаковать в wwwroot/fonts/PlusJakartaSans/

Write-Host ""
Write-Host "  Рекомендуемый способ:" -ForegroundColor Cyan
Write-Host "  1. Открой https://gwfh.mranftl.com/fonts/plus-jakarta-sans?subsets=latin"
Write-Host "  2. Выбери weights: 400, 500, 600"
Write-Host "  3. Formats: woff2 + woff"
Write-Host "  4. Скачай ZIP и распакуй в: $dest"
Write-Host ""
Write-Host "  Или через npm (если есть node в проекте):"
Write-Host "  npm install @fontsource/plus-jakarta-sans"
Write-Host "  Файлы будут в node_modules/@fontsource/plus-jakarta-sans/files/"
Write-Host ""
