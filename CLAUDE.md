# CLAUDE.md

AI Agent がゲームの動作確認を自動で行うための汎用フレームワーク(Statee)と、
それを活用したサンプルゲーム(スイカゲーム)を開発するリポジトリ。

## ドキュメント構成(詳細はこちらを読む)

| ファイル | 内容 | いつ読むか |
|---|---|---|
| `docs/PLAN.md` | 目的・アーキテクチャ・技術スタック・開発フェーズ | 作業の全体像を把握するとき |
| `docs/MEMO.md` | 決定記録(D-xxx 形式。決定/背景/トレードオフ) | 設計判断の理由を知りたいとき・決定を変えるとき |
| `docs/GUIDELINE.md` | テスト設計・実装・コーディング規約・ワークフローの規律 | コードやテストを書くとき(必読) |

## 記録の義務

- 設計判断を新たに下した・変えたときは **docs/MEMO.md に D-xxx として記録**する
  (決定 / 背景 / トレードオフ / 状態)。計画が変わったら docs/PLAN.md も更新する。
- ドキュメント・コミュニケーションは日本語。コードの識別子・コメントは docs/GUIDELINE.md に従う。

## 開発の指針(要点のみ。詳細は docs/GUIDELINE.md)

- **Green means done**: テストの緑を信頼できる完了シグナルに保つ。
  機能実装は「スケルトン → 失敗するテスト → 実装 → リファクタ」の4段階で進め、各段階でコミット
- バグ修正は再現テスト(修正前に失敗するテスト)から始める
- 純C#層(Godot 非依存)にロジックを置き、xUnit + Shouldly で厚くテストする。
  Godot 層はエントリポイント・描画・入力のみ
- フレームワーク(`src/`)開発中はサンプルゲーム(`game/`)の事情を持ち込まない(D-013)
- 固定フレーム数・固定秒数の待機は書かない。State や本番側の状態シグナルを待つ
- 後方互換は求められない限り維持しない。警告ゼロを維持する
- `.cs` 編集時のフォーマット(dotnet format + CSharpier)は hooks が自動実行する。手動整形は不要

## 環境の知識

- 全レイヤー C# / **.NET 10**(net10.0 統一。検証済み → docs/MEMO.md D-016, D-017)
- Godot は **4.7.stable の .NET 版**を使う。標準版は C# を実行できない:
  `C:\Users\naninunenoy\Downloads\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64_console.exe`
- Godot の headless 実行: `<godot> --headless --path game/SuikaGame.Godot`。
  初回は `--import` が必要だが、import は完了後にクラッシュするので exit code を無視する(D-016)
- ソリューションは `Statee.slnx`。CSharpier はローカルツール(初回は `dotnet tool restore`)
- 主要ライブラリ: Arch(ECS)/ R3 / VitalRouter / ZLogger / ToonEncoder / UnitGenerator /
  ConsoleAppFramework / xUnit + Shouldly
