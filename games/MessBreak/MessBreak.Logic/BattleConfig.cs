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

    // キャラごとのバランス値(スキル CD・範囲・効果量は CharacterConfig で個別調整)
    public CharacterConfig Attacker { get; init; } = new();
    public CharacterConfig Debuffer { get; init; } = new();

    /// <summary>指定キャラのバランス値を返す。</summary>
    public CharacterConfig CharacterOf(CharacterId id) =>
        id == CharacterId.Attacker ? Attacker : Debuffer;

    // ドッジ(向いている方向に高速移動・全区間無敵)
    public int DodgeTicks { get; init; } = 12;
    public int DodgeCooldownTicks { get; init; } = 30;
    public float DodgeSpeed { get; init; } = 240f;

    // 敵エリアの雑魚(動かない・攻撃しない。倒すとエリア制圧=設置スロット解放)
    public float MobRadius { get; init; } = 8f;

    /// <summary>アタッカースキル単発(3)で倒せる値。</summary>
    public int MobMaxHp { get; init; } = 3;

    public System.Numerics.Vector2 MobSpawn { get; init; } = new(480f, 120f);

    // 強敵(出現ポイントをアトラクトすると出現し、プレイヤーを追跡する。攻撃はまだしない)
    public float BossRadius { get; init; } = 14f;
    public int BossMaxHp { get; init; } = 20;
    public float BossSpeed { get; init; } = 40f;

    /// <summary>強敵の出現ポイント(アトラクトの対象)。</summary>
    public System.Numerics.Vector2 BossSpawn { get; init; } = new(560f, 180f);

    /// <summary>アトラクトが届く、プレイヤーと出現ポイントの最大距離。</summary>
    public float AttractRange { get; init; } = 40f;

    // タレット(エリア制圧で解放される設置スロットに置く。射程内の敵を自動射撃)
    public System.Numerics.Vector2 TurretSlot { get; init; } = new(440f, 180f);

    /// <summary>設置が届く、プレイヤーとスロットの最大距離。</summary>
    public float PlaceRange { get; init; } = 40f;

    public float TurretRange { get; init; } = 160f;
    public int TurretFireCooldownTicks { get; init; } = 30;
    public int TurretBulletDamage { get; init; } = 1;

    // 初期配置
    public System.Numerics.Vector2 PlayerSpawn { get; init; } = new(160f, 180f);
}
