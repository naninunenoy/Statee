# Declaree

宣言的 UI フレームワーク(docs/adr/D-035.md)。UI を Godot 非依存の C# record ツリー
(`UiNode`)で宣言し、`Declaree.Godot` が Control に変換する。UI ツリーは
`Declaree.Statee` 経由で幾何(Rect)込みの State として公開でき、AI Agent が
画面を構造化データとして観測・操作できる。

```
Declaree         … IR(UiNode)+ UiTree.Describe(純C#。xUnit でテスト)
Declaree.Godot   … UiRenderer(UiNode → Control、全再構築方式)+ UiSnapshot(Rect 採取)
Declaree.Statee  … UiStateProvider(記述子スナップショットを State 公開)
```

## Godot Control 体系への対応状況

方針: 網羅を目指さず、**ターゲット(ゲーム)が要求したものだけを足す**(YAGNI)。

### 対応済みの語彙

| Declaree | Godot | 備考 |
|---|---|---|
| `VBox` | VBoxContainer | |
| `HBox` | HBoxContainer | |
| `Margin(all, child)` | MarginContainer | 四辺等幅のみ |
| `Label(text)` | Label | |
| `Button(text, onClick)` | Button | `Disabled` 対応。押下でイベント ID を発行 |
| `Visible`(全ノード共通) | CanvasItem.Visible | |
| `MinWidth` / `MinHeight`(全ノード共通) | Control.CustomMinimumSize | |
| `UiRect`(記述子のみ) | Control.GetGlobalRect() | IR には無く UiSnapshot が実行時に採取 |

### 未対応(主なもの)

| カテゴリ | Godot のコンポーネント・機能 |
|---|---|
| コンテナ | GridContainer / ScrollContainer / PanelContainer / TabContainer / SplitContainer / CenterContainer / FlowContainer |
| 表示 | RichTextLabel / TextureRect / ProgressBar / ColorRect |
| ボタン系 | CheckBox / CheckButton / OptionButton / MenuButton |
| テキスト・値の入力 | LineEdit / TextEdit / SpinBox / Slider(値を運ぶイベントモデルが未設計) |
| 選択・一覧 | ItemList / Tree |
| ポップアップ | Popup / Dialog 系 |
| レイアウト制御 | アンカー(中央寄せ等)/ SizeFlags(Expand/Fill)/ テーマ |

質的な制約:

- イベントは `OnClick`(ID のみ)だけ。LineEdit の TextChanged のような
  「値を運ぶイベント」は `dispatch(eventId, payload)` への拡張判断が必要
- アンカー・SizeFlags が無いため「画面中央にメニュー」のような実用レイアウトは未対応。
  SuikaGame UI の Declaree 化(次ターゲット)で最初に必要になる見込み

## 状態更新のモデル

Flutter の StatefulWidget / setState に相当する**リアクティブ機構は持たない**。
UI は「状態 → ツリー」の純関数として書き、状態が変わったらホスト側が
再構築を呼ぶ(PingTarget の `Dispatch` → `BuildUi` → `RebuildUi` 参照)。
R3 等で状態を購読して再構築を呼ぶ配線はホストの責任。

## headless での注意

headless 実行ではビューポートが project.godot の window 設定に関わらず 64x64 になり、
UI が画面外に出るとクリックのヒットテストが外れる。UI を置くターゲットは
`GetWindow().Size` を実行時に明示すること(sandbox/PingTarget.Godot/Main.cs 参照)。
