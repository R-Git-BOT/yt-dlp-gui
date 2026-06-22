# オプションカタログとマージ設計

## 目的

起動直後からオプション一覧を安定して表示し、説明文を日本語で維持しながら、インストール済みの `yt-dlp --help` による最新オプション検出も行う。

この設計では、同梱する日本語カタログを表示の主データ、`yt-dlp --help` の解析結果を互換性確認と差分検出の補助データとして扱う。

## ファイル構成

- `Resources/yt-dlp-options.ja.json`
  - アプリに同梱する日本語オプションカタログ。
  - 起動時に最初に読み込む。
- `Resources/yt-dlp-options.ja.schema.json`
  - カタログ編集時の検証用JSON Schema。
- `%AppData%/YtDlpGui/yt-dlp-help-cache.json`
  - 前回取得した `yt-dlp --help` の解析結果キャッシュ。
  - 同梱カタログより優先しない。初回表示の補助と差分確認に使う。

## カタログJSON形式

トップレベルは次の構造にする。

```json
{
  "schemaVersion": 1,
  "catalogLanguage": "ja-JP",
  "catalogRevision": "2026-06-22",
  "source": {
    "ytDlpVersion": "2026.06.22",
    "helpCapturedAt": "2026-06-22",
    "notes": "翻訳済み同梱カタログ"
  },
  "categories": [],
  "options": []
}
```

### categories

カテゴリの表示順と日本語名を管理する。

```json
{
  "id": "format-video",
  "name": "形式・動画",
  "order": 100,
  "description": "動画形式、画質、コンテナ形式に関する設定"
}
```

### options

1オプションにつき1要素。`primarySwitch` を同一性判定の主キーにする。

```json
{
  "primarySwitch": "--format",
  "aliases": ["-f", "--format"],
  "categoryId": "format-video",
  "displayName": "フォーマット指定",
  "description": "ダウンロードする動画・音声形式を指定します。",
  "argument": {
    "name": "FORMAT",
    "kind": "text",
    "required": true,
    "placeholder": "bestvideo+bestaudio/best"
  },
  "choices": [],
  "tags": ["basic"],
  "ui": {
    "control": "text",
    "isAdvanced": false
  },
  "merge": {
    "keepWhenMissingFromHelp": true,
    "preferCatalogArgument": false
  }
}
```

### argument.kind

- `none`
  - 引数なし。チェックボックスのみ。
- `text`
  - 自由入力。
- `choice`
  - `choices` から選択。
- `multiChoice`
  - 複数選択。将来的にカンマ結合や複数引数生成へ拡張する。
- `path`
  - ファイルまたはフォルダパス。
- `number`
  - 数値。

### ui.control

WPF側で使う表示コントロールの希望値。

- `checkbox`
- `text`
- `combo`
- `multiSelect`
- `pathPicker`
- `number`

`argument.kind` と矛盾する場合は `ui.control` を優先せず、実装側で安全な表示にフォールバックする。

## 既存パーサーとのマージ設計

### 入力

- `CatalogOption`
  - 同梱カタログ由来。日本語表示名、日本語説明、UI制御情報を持つ。
- `ParsedHelpOption`
  - `YtDlpHelpParser.Parse()` 由来。実際の `yt-dlp --help` から取得したスイッチ、引数名、英語説明を持つ。
- `SavedOptionState`
  - ユーザー設定ファイル由来。選択状態と入力値を持つ。

### 出力

既存UIが使う `YtDlpOptionDefinition` を生成する。将来的には次のメタ情報を追加した `MergedYtDlpOptionDefinition` に拡張してもよい。

- `Availability`
  - `CatalogOnly`
  - `HelpOnly`
  - `CatalogAndHelp`
- `DescriptionSource`
  - `Catalog`
  - `Help`
- `IsNewInHelp`
- `IsMissingFromHelp`

当面は既存 `YtDlpOptionDefinition` に変換し、ステータスやログ用にマージ結果サマリーを別途返す。

## マージキー

基本キーは `primarySwitch`。

補助的に `aliases` と `ParsedHelpOption.Switches` も照合する。

照合順:

1. `catalog.primarySwitch == parsed.PrimarySwitch`
2. `catalog.aliases` のいずれかが `parsed.Switches` に含まれる
3. `catalog.primarySwitch` が `parsed.Switches` に含まれる

同一キーに複数のhelp結果がぶつかった場合は、最初の1件を採用し、残りはログへ警告を出す。

## 優先順位

| 項目 | 優先データ | 理由 |
| --- | --- | --- |
| カテゴリ | カタログ | UI向けに整理した日本語カテゴリを維持するため |
| 表示名 | カタログ | 日本語表示を維持するため |
| 説明文 | カタログ | 翻訳品質を固定するため |
| primarySwitch | カタログ | 保存設定のキーを安定させるため |
| aliases/switches | カタログとhelpの和集合 | 短縮形や追加エイリアスを落とさないため |
| 引数名 | help優先、ただし `preferCatalogArgument=true` ならカタログ | yt-dlp更新による引数名変更に追従するため |
| 引数必須 | helpで引数が取れたら必須扱い、カタログの `required` で補完 | 実行コマンド生成の安全性を上げるため |
| choices | カタログ優先 | GUIの選択肢は手作業で整える必要があるため |
| UI制御 | カタログ | helpだけではUI種別を判断できないため |

## マージアルゴリズム

```text
1. 同梱カタログを読み込む
2. カタログだけで OptionCategoryViewModel を作り、即表示する
3. バックグラウンドで次を実行する
   a. yt-dlp --version を取得
   b. yt-dlp --help を取得
   c. 既存 YtDlpHelpParser.Parse() で ParsedHelpOption を作る
4. カタログとhelp結果を primarySwitch/aliases で照合する
5. カタログ順に merged option を生成する
6. helpにしかない新規オプションを末尾に追加する
7. カタログにしかないオプションは既定で表示維持する
8. ユーザーの選択状態、入力値、展開状態、検索条件を再適用する
9. UIを差し替える
10. 差分件数をステータスとログに出す
```

## 起動時の表示ルール

起動時に `Categories.Clear()` して空にする処理は避ける。

推奨フロー:

1. `LoadCatalogOptions()` を同期または高速非同期で実行
2. カタログ由来の一覧を即表示
3. `RefreshOptionsFromHelpAsync()` を裏で実行
4. help取得に成功したらマージ済み一覧へ差し替え
5. help取得に失敗してもカタログ表示を維持

ステータス例:

- `同梱カタログから 430 個のオプションを表示しています`
- `yt-dlp 2026.06.22 と照合しました。新規 3 件、未検出 1 件`
- `yt-dlp --help を読み込めませんでした。同梱カタログを表示しています`

## helpのみの新規オプション

`yt-dlp --help` にしかないオプションは次のルールで追加する。

- カテゴリはパーサーのカテゴリ変換結果を使う
- カテゴリ変換できない場合は `未分類・新規`
- 表示名は `Humanize(primarySwitch)` を使う
- 説明文は英語のまま
- `tags` 相当として `new` を付ける
- ログに `未翻訳の新規オプション: --example-option` を出す

## カタログのみの未検出オプション

カタログにあるがhelpにないオプションは、既定では表示維持する。理由は、`yt-dlp --help` のパース漏れや環境差で消えるとUIが不安定になるため。

将来的には設定で切り替える。

- `すべて表示`
- `現在のyt-dlpで検出された項目のみ表示`
- `未検出項目を薄く表示`

## 保存設定との互換性

保存設定は従来通り `primarySwitch` をキーにする。

primarySwitchの変更が必要になった場合は、カタログに `replaces` を追加して移行する。

```json
{
  "primarySwitch": "--new-option",
  "replaces": ["--old-option"]
}
```

読み込み時は `--new-option` が見つからない場合に `replaces` の旧キーを探す。

## 実装候補

追加するクラス:

- `Models/YtDlpOptionCatalog.cs`
- `Models/MergedOptionResult.cs`
- `Services/OptionCatalogService.cs`
- `Services/OptionMergeService.cs`
- `Services/YtDlpVersionService.cs`

既存クラスの変更:

- `YtDlpHelpParser`
  - `LoadOptionsAsync()` は「help由来の解析」に責務を絞る
  - `FallbackOptions()` は最終的にカタログへ移す
- `MainViewModel`
  - コンストラクタ直後にカタログを表示
  - help更新はバックグラウンドでマージ
  - `Categories.Clear()` 前に選択状態、展開状態、検索条件を退避する

## 最初に実装する最小構成

1. `yt-dlp-options.ja.json` を読み込めるようにする
2. カタログ由来の一覧を起動時に表示する
3. 既存 `YtDlpHelpParser` の結果とマージする
4. 既知オプションは日本語説明を維持する
5. helpのみの新規オプションを英語のまま追加する

この段階ではキャッシュ、未検出項目の薄表示、旧キー移行は後回しでよい。
