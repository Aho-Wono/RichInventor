<#
.SYNOPSIS
    RichInventor アドインをビルドして Inventor に登録する。

.DESCRIPTION
    やることは 2 つだけ。

      1. dotnet build でアドインの DLL を作る
      2. .addin マニフェストを Inventor のアドインフォルダに置く
         (マニフェストの <Assembly> にビルドした DLL の絶対パスを埋め込む)

    COM への登録(regasm)は要らない。Inventor がマニフェストの ClassId を見て
    直接クラスを生成する仕組みになっている。

    登録先は既定でユーザー単位(%APPDATA%)なので管理者権限は不要。

.PARAMETER Configuration
    ビルド構成。既定は Release。

.PARAMETER SkipBuild
    ビルドを飛ばして登録だけ行う。

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools\Install-AddIn.ps1
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

$root     = Split-Path -Parent $PSScriptRoot
$project  = Join-Path $root "src\RichInventor\RichInventor.csproj"
$template = Join-Path $root "src\RichInventor.addin.template"

if (-not (Test-Path $project))  { throw "プロジェクトが見つかりません: $project" }
if (-not (Test-Path $template)) { throw "マニフェストのテンプレートが見つかりません: $template" }

# --- 1. ビルド -----------------------------------------------------------
$dotnet = "C:\Program Files\dotnet\dotnet.exe"
if (-not (Test-Path $dotnet)) {
    $cmd = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($null -eq $cmd) {
        throw ".NET SDK が見つかりません。`n" +
              "  winget install --id Microsoft.DotNet.SDK.8`n" +
              "でインストールしてください。"
    }
    $dotnet = $cmd.Source
}

# Inventorが起動中でもDLLを上書きしないよう、毎回新しい配置先へビルドする。
$buildId = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
$deployDir = Join-Path $env:APPDATA "RichInventor\AddIn\$buildId"
$null = New-Item -ItemType Directory -Path $deployDir -Force
if (-not $SkipBuild) {
    Write-Host "--- ビルド中 ($Configuration) ---"
    & $dotnet build $project -c $Configuration "-p:OutputPath=$deployDir\"
    if ($LASTEXITCODE -ne 0) { throw "ビルドに失敗しました。" }
    Write-Host ""
}

$dll = Join-Path $deployDir 'RichInventor.dll'
if ($SkipBuild) {
    $sourceDll = Join-Path $root "src\RichInventor\bin\$Configuration\RichInventor.dll"
    if (-not (Test-Path $sourceDll)) { throw "ビルド結果が見つかりません: $sourceDll" }
    Copy-Item -LiteralPath $sourceDll -Destination $dll
}
if (-not (Test-Path $dll)) { throw "ビルド結果が見つかりません: $dll" }

# --- 2. マニフェストを配置 ----------------------------------------------
# Inventor のバージョンごとにアドインフォルダが分かれている。
# インストール済みのものすべてに登録する。
$addinDirs = Get-ChildItem "$env:APPDATA\Autodesk" -Directory -Filter "Inventor 20*" -ErrorAction SilentlyContinue |
             ForEach-Object { Join-Path $_.FullName "Addins" }

if ($null -eq $addinDirs -or @($addinDirs).Count -eq 0) {
    throw "Inventor のアドインフォルダが見つかりません: $env:APPDATA\Autodesk\Inventor 20*\Addins"
}

$manifest = (Get-Content $template -Raw -Encoding UTF8).Replace("{{ASSEMBLY_PATH}}", $dll)
$utf8 = New-Object System.Text.UTF8Encoding($false)

foreach ($dir in @($addinDirs)) {
    if (-not (Test-Path $dir)) { $null = New-Item -ItemType Directory -Path $dir -Force }
    $target = Join-Path $dir "RichInventor.addin"
    [System.IO.File]::WriteAllText($target, $manifest, $utf8)
    Write-Host "登録しました: $target"
}

Write-Host ""
Write-Host "DLL: $dll"
Write-Host ""
Write-Host "Inventor を再起動すると、リボンに [RichInventor] タブが現れます。" -ForegroundColor Green
Write-Host "(アドインは起動時に読み込まれるため、起動中の Inventor には反映されません)"
