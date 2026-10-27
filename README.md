# RichInventor

Autodesk Inventor のアドイン。面倒な作業を見つけるたびにここへ機能を足していく。

Inventor を起動するとリボンに **[RichInventor]** タブが出る。

対象: **Inventor 2025**(2025.4 で動作確認 / .NET 8 / C#)

リポジトリ: GitHub <https://github.com/Aho-Wono/RichInventor> ／ GitLab <https://gitlab.com/Aho-Wono/RichInventor>(中身は同じ) ・ ライセンス: [MIT](LICENSE)

> Autodesk 社とは関係のない個人の非公式ツールです。使う前に大事なデータはバックアップしてください。

---

## できること

リボン **[RichInventor]** タブの **[STL 出力]**・**[寸法PDF]**・**[配置]** パネル

| ボタン | 何をするか |
|---|---|
| **3DP 部品を出力** | アセンブリ内から、ファイル名に `3DP` が入っている全パーツを探して個別の STL にする |
| **選択部品を出力** | アセンブリで選択したパーツ／サブアセンブリを STL にする |
| **開いているパーツを出力** | 現在アクティブなパーツを STL にする |
| **ASP部品の寸法PDF** | アセンブリ内のファイル名に大文字の `ASP` を含むパーツの寸法一覧をPDFにする |
| **選択部品の寸法PDF** | 選択したパーツやサブアセンブリ内のパーツの寸法一覧をPDFにする |
| **開いているパーツの寸法PDF** | 現在アクティブなパーツの寸法と投影図をPDFにする |
| **最大平面を接地** | アセンブリ直下の全パーツ／サブアセンブリから基準部品を1つ選び、ほかの部品の面積が最大の平面を基準部品の最大平面にフラッシュ拘束する |
| **選択部品を接地** | 「最大平面を接地」を、選択したパーツ／サブアセンブリだけで行う(基準部品も選択の中から選ぶ) |
| **設定** | 設定ファイル(`settings.json`)を開く |

アセンブリではSTLとPDFそれぞれの最初の2つと「最大平面を接地」「選択部品を接地」、パーツでは「開いているパーツ」のSTL・PDFが表示される。
「設定」はどちらにも表示される。

STL は既定で **ミリメートル / バイナリ / 高精度**。変更は [設定](#設定) から。

---

## 導入方法

配布用の DLL は置いていないので、手元の PC でソースからビルドして登録する。
付属のスクリプトがビルドから登録までを 1 回で行う。管理者権限は要らない(ユーザー単位で入る)。

### 必要なもの

| もの | 備考 |
|---|---|
| Windows 10 / 11 (64 bit) | |
| Autodesk Inventor 2025 | 2025.4 で動作確認。ビルドに Inventor 付属の `Autodesk.Inventor.Interop.dll` を使う |
| .NET 8 SDK | ビルドに使う。PowerShell で `winget install --id Microsoft.DotNet.SDK.8` |
| Noto Sans JP フォント | 寸法PDF を使う場合だけ。[Google Fonts](https://fonts.google.com/noto/specimen/Noto+Sans+JP) から入れる |

Inventor を **一度は起動しておく**こと。登録先の `%APPDATA%\Autodesk\Inventor 2025\` は初回起動時に作られる。

### 1. ソースを取ってくる

git がある場合(GitHub と GitLab のどちらからでもよい):

```
git clone https://github.com/Aho-Wono/RichInventor.git
```

git が無い場合は zip でダウンロードして、好きな場所に展開する。

- GitHub: [リポジトリのページ](https://github.com/Aho-Wono/RichInventor) の **[Code]** → **[Download ZIP]**
- GitLab: [リポジトリのページ](https://gitlab.com/Aho-Wono/RichInventor) の **[Code]** → **[zip]**

展開したフォルダの名前は `RichInventor-main` などになるが、そのままで使える。

### 2. インストールする

取ってきたフォルダ(`README.md` がある場所)で PowerShell を開き、次を実行する。

```
powershell -ExecutionPolicy Bypass -File tools\Install-AddIn.ps1
```

- `-ExecutionPolicy Bypass` はこのスクリプトの実行にだけ効く。PC の設定は変わらない
- ビルドした DLL は `%APPDATA%\RichInventor\AddIn\<日時>\` に、
  登録用の `RichInventor.addin` は `%APPDATA%\Autodesk\Inventor 20xx\Addins\` に置かれる
- ビルドのたびに新しいフォルダへ出力するので、Inventor を起動したままでも実行できる

### 3. Inventor を起動する

**Inventor を起動し直す**と、リボンに **[RichInventor]** タブが出る。
アドインは起動時にしか読み込まれないので、起動中の Inventor にはすぐには反映されない。

### 更新

新しいソースを取ってきて(`git pull`、または zip を展開し直す)、手順 2 と 3 をもう一度行う。
古い版のボタンは起動時に自動で整理される。

### アンインストール

```
powershell -ExecutionPolicy Bypass -File tools\Uninstall-AddIn.ps1
```

`RichInventor.addin` を消すだけなので、Inventor を起動し直すとタブが消える。
ビルドした DLL と設定も消したい場合は、`%APPDATA%\RichInventor\` フォルダを削除する。

---

## 使い方

### 最大平面を接地

アセンブリを開いた状態で **[最大平面を接地]** を押す。

1. アセンブリ直下の各パーツ／サブアセンブリについて、面積が最大の平面を探す
   (サブアセンブリは1つの塊として扱い、中のパーツの面から最大のものを選ぶ)
2. その中から**基準部品**を1つ選ぶ。固定(グラウンド)された部品があればそれを優先し、
   無ければ最大の平面がいちばん大きい部品にする。基準部品は動かさない
3. 基準・拘束する件数・固定を解除する件数・スキップする件数が出るので確認して **[はい]**
4. ほかの部品の最大平面を、基準部品の最大平面に**フラッシュ拘束**する
   (同じ平面・同じ向きにそろうので、全部品が基準部品と同じ床に並ぶ)
5. 結果を表示する

- 基準部品の最大平面が同じ大きさで複数あるとき(板の表裏など)は、いま下(-Z)を向いている面を使う
- 基準部品が固定されていなければ固定する。基準以外で固定されている部品は、固定を解除してから拘束する
- ほかの拘束やジョイントが付いている部品、抑制中の部品、パターン要素、平面の無い部品はスキップして一覧で報告する
  (基準部品は動かさないので、拘束が付いていても基準にはなれる)
- もう一度押すと、前回このボタンで付けた拘束(名前 `接地:<部品名>`)を付け直す。重複はしない。
  以前の版で付けた XY 平面への接地拘束も、このときに基準部品への拘束に置き換わる
- 処理全体が1つの操作になっているので、**Ctrl+Z 1回**でまとめて元に戻せる

### 選択部品を接地

モデルツリーまたは画面上で部品を選択して **[選択部品を接地]** を押す。複数選択できる。
選んだものだけを対象に、[最大平面を接地] と同じ処理をする(確認・スキップ・付け直し・Ctrl+Z も同じ)。

- 基準部品も選んだものの中から同じ規則で選ぶ。基準にしたい部品と、そろえたい部品を合わせて2個以上選ぶ
- 面やエッジを選んだ場合も、その所属部品まで自動で遡る
- サブアセンブリの中の部品を選んだ場合は、そのサブアセンブリ全体が対象になる
  (中の全パーツの面から最大のものを選ぶ)
- 選択していない部品の拘束・固定・位置は変えない
- 何も選択していなければ案内を出して終了する(全部品を暗黙に処理することはない)

### 寸法PDF

アセンブリでは **[ASP部品の寸法PDF]** または、モデルツリーや画面上で選択して
**[選択部品の寸法PDF]** を押す。選択したサブアセンブリはパーツへ展開される。
パーツでは **[開いているパーツの寸法PDF]** を押す。保存ダイアログでPDFの場所と名前を選ぶ。

PDFの先頭は部品一覧、続くページは部品ごとの6方向からの投影図と穴一覧。
同じ図になる方向はまとめて表示する。部品の長手方向をX軸とし、外形の最小端を
座標原点として、外形・肉厚・穴径・穴の入口中心座標・タップ・深さをmmで示す。
個数は対象範囲の抑制されていないオカレンスを数える。同一ファイルは1シートにまとめる。

穴はInventorの穴フィーチャーと、実体形状の内側円筒面から検出する。
形状だけではタップ規格や加工深さを確定できない場合は「不明」と記載する。
肉厚も一意に特定できない場合は「未判定」となる。製作前には寸法を原モデルと照合すること。

作者の環境の `ASP1.ipt` では、120×10×10 mm、肉厚1 mm、穴6件を検出した。
`ASP1.ipt` と `ASP2.ipt` を各2個含む実アセンブリから、一覧＋部品2枚の
3ページPDFを出力し、全ページを画像化して確認した。
日本語のPDF出力には `Noto Sans JP` フォントを使用する。未インストールのPCでは
先にこのフォントを導入する。

### 選択部品を出力

アセンブリを開き、モデルツリーまたは画面上で部品を選択して
**[選択部品を出力]** を押す。複数選択できる。

- パーツはそれぞれ別の STL になる
- サブアセンブリは、そのサブアセンブリを結合した1つの STL になる
- 面やエッジを選んだ場合も、その所属部品まで自動で遡る
- 同じパーツのオカレンスを複数選んでも、同じファイルは1回だけ出力する

出力先の既定は、アセンブリと同じ階層の `3DPstl\`。

### 開いているパーツを出力

パーツをアクティブにして **[開いているパーツを出力]** を押す。
現在アクティブなパーツ1件だけがSTLになる。ほかのウィンドウで開いている
パーツは対象にしない。出力先の既定は元のパーツと同じフォルダ。

### 3DP 部品を出力

アセンブリを開いた状態で **[3DP 部品を出力]** を押す。

1. 参照している全パーツを再帰的にたどり、ファイル名に `3DP` を含むものを集める
2. 見つかった一覧が出るので確認して **[はい]**
3. 出力先フォルダを選ぶ(既定はアセンブリと同じ階層の `3DPstl\`)
4. 完了

**前提**: 3D プリントする部品のファイル名に `3DP` を入れておくこと。位置はどこでもよい。

```
3DP_BenzSpacer.ipt                     ← 前につける
MeasureEncoderUnit_3DP_OmniAdapter.ipt ← 途中に入れる
```

---

## 設定

**[設定]** ボタンで開く。実体は `%APPDATA%\RichInventor\settings.json`。
**再ビルド不要**で、次にボタンを押したときから反映される。

```json
{
  "PrintMarker": "3DP",
  "MarkerIgnoreCase": false,
  "ExcludeOldVersions": false,
  "DefaultOutputFolderName": "3DPstl",
  "StlUnits": 5,
  "StlResolution": 0,
  "StlBinary": true,
  "StlExportColor": false
}
```

| キー | 意味 |
|---|---|
| `PrintMarker` | 3Dプリント対象の目印にする文字列 |
| `MarkerIgnoreCase` | `true` にすると `3dp` / `3Dp` も拾う |
| `ExcludeOldVersions` | `true` にすると `OldVersions\` / `backup\` 配下への参照を無視する |
| `DefaultOutputFolderName` | 出力先の既定フォルダ名(アセンブリと同じ階層に作られる) |
| `StlUnits` | `5`=ミリメートル / `4`=cm / `2`=インチ / `6`=m。詳細は [仕様書](docs/仕様書.md#22-stl-トランスレータのオプション値実測) |
| `StlResolution` | `0`=高精度 / `1`=中 / `2`=低 / `3`=カスタム / `4`=Brep |
| `StlBinary` | `false` にすると ASCII STL |
| `StlExportColor` | `true` にすると STL に色情報を埋める(スライサ互換性のため既定は `false`) |

壊れた JSON を書いても既定値で動き続けるので、いじって困ったらファイルを消せばよい。

---

## ファイル構成

```
RichInventor/
├── README.md
├── LICENSE                       MIT ライセンス
├── docs/
│   ├── 仕様書.md                 機能の詳細仕様・STL設定の実測値
│   └── 引継ぎ資料.md             設計の理由・落とし穴・機能の足し方
├── src/
│   ├── RichInventor.addin.template   アドインのマニフェスト(雛形)
│   ├── RichInventor/                 アドイン本体
│   │   ├── StandardAddInServer.cs      Inventor が最初に呼ぶ入口
│   │   ├── GlobalUsings.cs             Inventor と .NET の型名衝突を一括解決
│   │   ├── Core/                       Inventor に依存する処理(STL書き出し・走査・設定)
│   │   ├── Commands/                   リボンボタン1個 = クラス1個
│   │   └── UI/                         リボン組み立て・ダイアログ・アイコン
│   └── RichInventor.Probe/           検証用コンソールツール(成果物ではない)
└── tools/
    ├── Install-AddIn.ps1         ビルドして Inventor に登録
    └── Uninstall-AddIn.ps1       登録解除
```

---

## 開発

コードを直したら:

```
powershell -ExecutionPolicy Bypass -File tools\Install-AddIn.ps1
```

でビルドと再登録が済む。ビルド結果はユーザー領域の新しいフォルダへ配置されるため、
Inventorが起動中でもDLLを上書きしない。**新しいボタンの反映にはInventorの再起動が必要**。

旧版から更新した場合も同じコマンドを実行する。再起動後、保存済みの旧ボタンを整理し、
Part／Assemblyそれぞれに必要なボタンだけが表示される。

Inventor を再起動せずにコアの動作を確かめたいときは検証ツールを使う:

```
dotnet build src\RichInventor.Probe\RichInventor.Probe.csproj -c Release
src\RichInventor.Probe\bin\Release\RichInventorProbe.exe scan
```

起動中の Inventor につないで「いま実行したら何件書き出されるか」を表示する。
詳細は [引継ぎ資料](docs/引継ぎ資料.md#43-inventor-を再起動せずに確かめる)。

---

## うまく動かないとき

| 症状 | 対処 |
|---|---|
| インストールで「Autodesk.Inventor.Interop.dll が見つかりません」と出る | Inventor が `C:\Program Files\Autodesk\Inventor 2025` 以外に入っている。先に `$env:InventorBin = "<Inventorのフォルダ>\Bin"` を実行してから、同じ PowerShell でインストールする |
| インストールで「Inventor のアドインフォルダが見つかりません」と出る | Inventor を一度起動して終了してから、もう一度実行する |
| インストールで `dotnet` が見つからないと出る | .NET 8 SDK を入れる。入れた直後は PowerShell を開き直す |
| リボンに [RichInventor] タブが出ない | Inventor を再起動したか確認。まだ出ないなら **[ツール]** → **[アドイン]** で RichInventor が一覧にあるか、読み込みエラーが出ていないか見る |
| 新しいボタンが出ない | 再インストール後にInventorを再起動し、[ツール]→[アドイン]でRichInventorの読み込みを確認する |
| 3DP 部品を出力で0件と言われる | ファイル名に `3DP` が入っているか確認する。小文字も拾いたいときは設定の `MarkerIgnoreCase` を `true` に |
| STL の寸法がおかしい | 設定の `StlUnits` を確認する。`5` がミリメートル |
| 設定を変えたのに反映されない | 設定はボタンを押すたびに読み直される。JSON の書式が壊れていると既定値に戻るので、カンマや括弧を確認する |

---

## ライセンス

[MIT License](LICENSE)。自由に使用・改変・再配布してよいが、無保証。

Autodesk および Inventor は Autodesk, Inc. の登録商標です。
ビルドに使う `Autodesk.Inventor.Interop.dll` は Inventor に付属するもので、このリポジトリには含めていない。
