# Statee.Generator 開発指針

- **netstandard2.0 縛り**(Roslyn コンポーネントの要件)。net10 の API は使えない。
  record / init を使うための `IsExternalInit` ポリフィルを消さない
- Attribute(Statee.Core の `[StateeState]` / `[StateeField]`)の仕様変更は
  ここと `tests/Statee.Generator.Tests` を同時に更新する
- 診断を追加したら `AnalyzerReleases.Unshipped.md` にも追記する(RS2008 対策)。
  診断 ID は `STATEE0xx` の連番
- テストは生成コードの文字列比較でなく、**コンパイル・ロードして振る舞いで検証**する
  (生成コードの整形変更でテストが割れないように)
- 生成コードには手書きコードと衝突しない前提を置かない。
  衝突しうる仕様(メンバー名など)を増やすなら診断で守る
