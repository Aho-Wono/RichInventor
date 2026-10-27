<#
.SYNOPSIS
    RichInventor アドインの登録を解除する。

.DESCRIPTION
    アドインフォルダから RichInventor.addin を削除するだけ。
    ビルドした DLL やリポジトリのファイルには触らない。
    次に Inventor を起動したときにアドインが読み込まれなくなる。

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools\Uninstall-AddIn.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

$found = $false

$addinDirs = Get-ChildItem "$env:APPDATA\Autodesk" -Directory -Filter "Inventor 20*" -ErrorAction SilentlyContinue |
             ForEach-Object { Join-Path $_.FullName "Addins" }

foreach ($dir in @($addinDirs)) {
    $target = Join-Path $dir "RichInventor.addin"
    if (Test-Path $target) {
        Remove-Item $target -Force
        Write-Host "削除しました: $target"
        $found = $true
    }
}

if (-not $found) {
    Write-Host "登録されている RichInventor.addin は見つかりませんでした。"
} else {
    Write-Host ""
    Write-Host "Inventor を再起動すると [RichInventor] タブが消えます。"
}
