namespace MessBreak.Logic;

/// <summary>
/// 戦闘のバランス値。ゲームバランスに関わる数値はここに集約し、ロジック内にハードコードしない
/// (docs/DESIGN.md)。時間はすべて tick 数(60 tick = 1 秒想定)、距離・速度はワールド単位。
/// </summary>
public sealed record BattleConfig
{
    /// <summary>1 秒あたりの tick 数。速度(毎秒)を 1 tick の移動量に換算する基準。</summary>
    public int TicksPerSecond { get; init; } = 60;

    // 部屋(原点 (0,0) 起点の矩形)。画面(320x180 の窓)より広く、カメラワークの余地を作る
    public float RoomWidth { get; init; } = 640f;
    public float RoomHeight { get; init; } = 360f;

    // プレイヤー
    public float PlayerRadius { get; init; } = 6f;
    public float PlayerSpeed { get; init; } = 90f;

    /// <summary>スプリント(左 Shift)中の移動速度。</summary>
    public float SprintSpeed { get; init; } = 140f;

    // 通常射撃(エイム方向へ弾を撃つ。低ダメージの繋ぎ)
    public int FireCooldownTicks { get; init; } = 12;
    public float BulletSpeed { get; init; } = 240f;
    public float BulletRadius { get; init; } = 3f;
    public int BulletDamage { get; init; } = 1;

    /// <summary>
    /// エイムアシストの吸着角(度)。発射方向と的の中心の角度差がこの範囲内なら
    /// 弾は的の中心へ向かう。0 でアシストなし。
    /// </summary>
    public float AimAssistDegrees { get; init; } = 10f;

    // キャラ切り替え(スキルセットの差し替え。スキルクールダウンはキャラごとに独立)
    public int SwitchCooldownTicks { get; init; } = 30;

    // デバッファーのスキル(範囲内の的に被ダメージ増幅デバフを付与。ダメージなし)
    public int DebuffDurationTicks { get; init; } = 300;
    public int DebuffDamageMultiplier { get; init; } = 2;

    // スキル(レティクル位置を爆心に範囲効果。倒す主役=高ダメージ)
    public int SkillCooldownTicks { get; init; } = 180;

    /// <summary>爆発中心のプレイヤーからの距離(向いている方向)。</summary>
    public float SkillRange { get; init; } = 80f;

    /// <summary>爆発の半径。</summary>
    public float SkillRadius { get; init; } = 40f;
    public int SkillDamage { get; init; } = 3;

    // ドッジ(向いている方向に高速移動・全区間無敵)
    public int DodgeTicks { get; init; } = 12;
    public int DodgeCooldownTicks { get; init; } = 30;
    public float DodgeSpeed { get; init; } = 240f;

    // 的(動かない・攻撃しない。撃破後にリスポーンする「当たる感」検証用ターゲット)
    public float TargetRadius { get; init; } = 8f;

    /// <summary>アタッカースキル単発(3)では倒れず、デバフ込みのコンボ(3×2)で一撃になる値。</summary>
    public int TargetMaxHp { get; init; } = 6;

    /// <summary>撃破からリスポーンまでの tick 数。</summary>
    public int TargetRespawnTicks { get; init; } = 30;

    /// <summary>リスポーン位置の壁からの最小距離。</summary>
    public float TargetSpawnMargin { get; init; } = 24f;

    /// <summary>リスポーン位置とプレイヤーの最小距離(密着スポーン防止)。</summary>
    public float TargetMinPlayerDistance { get; init; } = 60f;

    // 初期配置
    public System.Numerics.Vector2 PlayerSpawn { get; init; } = new(160f, 180f);
    public System.Numerics.Vector2 TargetSpawn { get; init; } = new(480f, 180f);
}
