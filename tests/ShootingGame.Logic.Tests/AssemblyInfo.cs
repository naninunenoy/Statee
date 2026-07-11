// Arch の World.Create / 共有 ComponentRegistry はスレッド安全でないため、
// World を持つ ShootingLogic を並列のテストコレクションで同時生成すると
// エンティティの生成・クエリが壊れる。コレクション間の並列実行を無効にする
[assembly: CollectionBehavior(DisableTestParallelization = true)]
