---
name: verify
description: >-
  変更が実際のターゲット(headless Godot + Statee)で動くことを E2E で確認する。
  「E2E で確認して」「headless で動かして」「実機確認して」等の依頼、
  またはフレームワーク・ゲームの変更をコミットする前の動作確認で使う。
---

# E2E 動作確認(headless Godot + Statee)

変更をテストの緑だけでなく、実際のターゲットを起動して State / ログで確認する手順。
判定・照合だけを任せたい場合は `statee-checker` エージェントに委譲してもよい。

## 0. 前提

- Godot は **.NET 版**を使う(標準版は C# を実行できない)。
  パスは環境変数 `GODOT_BIN` に設定されている(D-066)。未設定ならユーザーに設定を依頼する
- ターゲットは引数(`--path <dir>`)または依頼の文脈から決める。
  指定が無ければ確認対象に応じて選ぶ:
  - `sandbox/PingTarget.Godot` … フレームワーク(`src/`)の疎通・プロトコル確認用の最小ターゲット
  - `samples/*.Godot` … 各ゲームのシナリオ確認用。公開している State・コマンドは
    ゲームごとに異なるので、`state --path game/…` と各ゲームの Main.cs の登録を情報源にする
- 通信は TCP localhost、既定ポート **9310**(D-018)

## 1. ビルド

```powershell
dotnet build <ターゲットのディレクトリ>   # 例: dotnet build samples/SuikaGame.Godot
```

注意: `Statee.Mcp` は MCP サーバー実行中だと exe がロックされてビルドに失敗する。
ソリューション全体ではなく対象プロジェクトだけをビルドすること。

## 2. ターゲット起動(バックグラウンド)

```powershell
& $env:GODOT_BIN --headless --path <ターゲットのディレクトリ> -- --port=9310 --seed=12345
```

(sh の場合は `"$GODOT_BIN" --headless ...`)

- **バックグラウンドで起動**する(フォアグラウンドだと待ち受けたまま返ってこない)
- `--seed=` で乱数シードを注入すると決定論的に観測できる(対応しているターゲットのみ)
- 初回のみアセットインポートが必要:
  `<godot> --headless --path <target> --import`
  → **完了後にクラッシュする(exit 0xC0000005)ので exit code は無視**(D-016)
- 起動直後は待ち受け前で接続拒否されることがある。固定スリープではなく
  ping を再試行して待つ

## 3. 操作と観測

基本は MCP ツール `statee_cli`。使えなければ
`dotnet run --project src/Statee.Cli --no-build -- <同じ引数>`。

| 目的 | コマンド例 |
|---|---|
| 疎通 | `ping --message hello` |
| 不変情報 | `state --path system/platform` |
| フレーム進行 | `state --path system/runtime`(Frame / UptimeSeconds) |
| ゲーム状態 | `state --path game/…`(パス・内容はゲームごと。例: SuikaGame は game/board) |
| 条件待機 | `send --command wait --arg path=game/board,field=Score,op=ge,value=1`(D-028) |
| 任意コマンド | `send --command <ゲームが登録したコマンド> --arg key=value` |
| ログ | `logs --tail 20` |
| 終了 | `quit`(exit 0 で正常終了することも確認対象) |

- **複数引数はカンマ区切り**: `--arg key1=v1,key2=v2`(`--arg` の繰り返しは最後だけが残る)
- 出力は TOON 形式。`exit: 0` 以外や `error:` は失敗
- **固定フレーム数・固定秒数の待機は書かない**。`wait` か state の再取得で条件成立を待つ
- Ruby シナリオ(Statee.Scenario、語彙は send / state / wait / assert + expect。D-029, D-034)は
  `dotnet run --project src/Statee.Scenario -- run --script <foo.rb> --port 9310` で実行できる
- 人間向け HTML レポート(`--report-dir` / `--report-state`。D-034)を出す場合は
  **--headless を付けずに**ターゲットを起動する(headless は描画が無くスクショ不可)

## 4. 後始末と報告

- 確認後は必ず `quit` でターゲットを終了する(プロセスを残さない)
- 報告は冒頭に判定(✅ OK / ❌ NG)、各項目の「期待 → 実際」を出力の根拠付きで。
  NG は再現コマンドと関連ログを添える
