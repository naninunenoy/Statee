---
name: export-web
description: >-
  C# の Godot プロジェクトをブラウザ向けに Web エクスポートする。
  「Web 出力して」「ブラウザで動かして」「HTML5 エクスポート」等の依頼、
  または 0環境から godot-dn2cpp の資材を GitHub から取得して出荷するときに使う。
---

# C# Godot の Web エクスポート(godot-dn2cpp)

公式 Godot 4 は C# を Web に出せない。フォークエディタ
[godot-dn2cpp](https://github.com/takuma-komatsu/godot-dn2cpp) が IL を C++ に
トランスパイルし、WASM side module としてリンクする(D-082)。

**`GODOT_BIN`(公式 .NET 版)は使わない。** ABI は 4.7.1-stable 固定で、混ぜると
失敗せずに壊れる。取得・エクスポートは `tools/export-web.sh`(Windows は
`tools/export-web.ps1`)に任せる。

## 0. 0環境 — GitHub から資材を取得する

ピンは `tools/godot-dn2cpp.pin`(既定 `4.7.1-dn2cpp.4.0`)。latest は追わない。

通常はスクリプトが未設定の変数を埋める:

```sh
# 資材だけ取得(エディタ + Web テンプレート)
tools/export-web.sh --fetch-only

# Web テンプレートだけ(Linux でも可。エディタのプレビルドは Windows/macOS のみ)
tools/export-web.sh --fetch-template-only
```

取得先(キャッシュ。リポジトリには置かない):

| OS | 既定 |
|---|---|
| macOS / Linux | `$XDG_CACHE_HOME/godot-dn2cpp/<version>`(無ければ `~/.cache/…`) |
| Windows | `%LOCALAPPDATA%\godot-dn2cpp\<version>` |

成功すると次の2つが埋まる:

| 変数 | 指すもの |
|---|---|
| `GODOT_DN2CPP_BIN` | フォークエディタの実行ファイル(Windows は `Godot-dn2cpp.console.exe`) |
| `GODOT_DN2CPP_WEB_TEMPLATE` | **同じリリース**の `*-web-template.zip`(**展開しない**) |

既に両変数が実在ファイルを指していれば取得を省略する。

### スクリプトが使えないときの手動手順(プレビルドがあるホスト)

ホストは **Windows 10/11 x86_64** または **macOS 13+ Apple Silicon**。
Linux 向けエディタは Releases に無い → 次節「フォーク自前ビルド」。

1. リリース https://github.com/takuma-komatsu/godot-dn2cpp/releases/tag/4.7.1-dn2cpp.4.0
2. `SHA256SUMS.txt` と次を取る
   - macOS: `Godot-4.7.1-dn2cpp.4.0-macos-arm64.zip`
   - Windows: `Godot-4.7.1-dn2cpp.4.0-windows-x86_64.zip`
   - 共通: `godot-4.7.1-dn2cpp.4.0-web-template.zip`
3. `shasum -a 256 -c SHA256SUMS.txt`(Windows は `Get-FileHash -Algorithm SHA256`)
4. エディタ zip を展開する。テンプレート zip は**展開しない**
5. macOS: `xattr -dr com.apple.quarantine Godot-dn2cpp.app`
6. 変数を設定する
   - `GODOT_DN2CPP_BIN` = 展開した `Contents/MacOS/Godot` または `Godot-dn2cpp.console.exe`
   - `GODOT_DN2CPP_WEB_TEMPLATE` = ダウンロードした web-template.zip のフルパス

## 0b. フォーク自前ビルド(Linux、またはプレビルドを使わないとき)

**`godot-dn2cpp` を scons するだけでは足りない。** エディタに dn2cpp の
toolchain(`bin/GodotSharp/Dn2Cpp/`)を同梱する必要があり、その正本は
dn2cpp リポジトリの `gates/setup-godot-fork.sh` である。
Web テンプレートは同じ emcc で焼いたものだけを使う(リリースの zip と混ぜない)。

コピー可能なコマンドはピン込みで出せる:

```sh
tools/export-web.sh --print-build-fork
```

### 手順

1. 依存: Godot 公式 [Compiling for LinuxBSD](https://docs.godotengine.org/en/4.7/engine_details/development/compiling/compiling_for_linuxbsd.html)
   に加え **.NET 10 SDK** と **Python 3.10+**。cmake / ninja / Emscripten は
   次の `setup-buildtools.sh` / `setup-emsdk.sh` がピン版を展開する
2. **この Statee リポジトリの外**に、兄弟ディレクトリとして clone する
   (setup スクリプトが `../godot-dn2cpp` を見る)。ピンは `tools/godot-dn2cpp.pin`

```sh
ws="${DN2CPP_WORKSPACE:-$HOME/src}"
mkdir -p "$ws"
cd "$ws"
git clone https://github.com/takuma-komatsu/dn2cpp.git
git clone https://github.com/takuma-komatsu/godot-dn2cpp.git
git -C dn2cpp checkout "$(awk -F= '/^DN2CPP_COMMIT=/{print $2}' /path/to/Statee/tools/godot-dn2cpp.pin)"
git -C godot-dn2cpp fetch origin --tags
git -C godot-dn2cpp checkout "$(awk -F= '/^FORK_TAG=/{print $2}' /path/to/Statee/tools/godot-dn2cpp.pin)"
```

3. エディタ + toolchain 同梱 + Web テンプレート

```sh
cd "$ws/dn2cpp"
./gates/setup-buildtools.sh
./gates/setup-emsdk.sh
./gates/setup-godot-fork.sh      # scons。初回は数十分〜数時間
./gates/setup-godot-fork-web.sh
```

4. 変数を設定する。**実行ファイルだけを別場所へコピーしない**
   (`GodotSharp/` が隣に必要)

```sh
root="${DN2CPP_GODOT_FORK_ROOT:-$HOME/.cache/dn2cpp-godot-fork}"
export GODOT_DN2CPP_BIN="$(cat "$root/editor.txt")"
export GODOT_DN2CPP_WEB_TEMPLATE="$root/web_template.zip"
```

Linux のエディタ実体は概ね
`$ws/godot-dn2cpp/bin/godot.linuxbsd.editor.x86_64.mono`。
`editor.txt` を優先する。

5. このリポジトリで `tools/export-web.sh --path samples/SuikaGame.Godot`

エージェントがこの節を走らせるのは、プレビルドが無いホストで Web 出力を
求められたときだけ。clone / scons を「念のため」始めない。
メンテナのリリース切り(`docs/RELEASE.md`)とは別経路。

## 1. エクスポート

```sh
tools/export-web.sh --path samples/SuikaGame.Godot
# Windows: tools/export-web.ps1 -Path samples/SuikaGame.Godot
```

- ターゲットは `--path` または依頼の文脈。無指定なら今触っている `*.Godot`
- 最初の煙テストは D-065 済みの `samples/SuikaGame.Godot`。
  `sandbox/PingTarget.Godot` は ExportRelease でも `Statee.Remote` が残るので避ける
- 出力既定は `artifacts/web/<プロジェクト名>/`(gitignore 済み)
- スクリプトが `export_presets.cfg` を生成する(テンプレート絶対パスがマシン依存。コミットしない)
- 必須プリセット: `dotnet/export_backend=dn2cpp`(値 `2`)、カスタムテンプレート=上記 zip、
  `variant/extensions_support=true`、`variant/thread_support=false`

## 2. 成否の判定

Godot の exit 0 と「警告付き完了」を成功としない。次を両方満たすこと:

1. `<target>/**/dn2cpp/logs/export-*.log` に
   `missing tools it cannot build without` / `CMake Error` / `emcc: error` が無い
2. 出力に `index.html` と `.wasm` と `.pck` がある

失敗したらログ末尾を根拠に報告する。固定秒数待ちはしない。

## 3. ブラウザで開く

`file://` では動かない。

```sh
python3 -m http.server 8060 --directory artifacts/web/SuikaGame.Godot
```

`http://127.0.0.1:8060/` を開く。スレッド無しなので COOP/COEP は不要。
`--serve` を付けるとスクリプトがこのサーバをフォアグラウンドで立てる。

v1 の完了シグナルは **成果物 + HTTP 200**。Statee の `ping` は使わない
(ブラウザに生 TCP が無く、ExportRelease では待ち受け自体が無い。D-065)。

## 4. 報告

- 冒頭に判定(OK / NG)
- `GODOT_DN2CPP_BIN` / `GODOT_DN2CPP_WEB_TEMPLATE` のフルパス(取得したか既存か)
- `index.html` のフルパスと配信 URL
- NG なら export ログの該当箇所

## やってはいけないこと

- `GODOT_BIN` で `--export-release`
- テンプレート zip を展開して中身を指定する
- **godot-dn2cpp だけを scons して終わる**(toolchain が同梱されずエクスポートが落ちる)
- 自前ビルドのエディタに Releases の Web テンプレートを混ぜる(emcc が一致しない)
- ネット同期サンプル(Reversi / RaidBoss)や `Task.Run` / `HttpClient` 依存を
  未確認のまま「ブラウザで遊べた」と書く
