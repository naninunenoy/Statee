using System.Collections.Generic;
using System.Linq;
using MessBreak.Logic;
using Statee.Core;

namespace MessBreak;

/// <summary>
/// ゲーム状態の State 公開。CaptureState はソケットスレッドで走るため、
/// メインスレッドが差し替える不変スナップショットを読むだけにする(docs/USING.md)。
/// 検証に必要な情報を全公開する(画面上の演出で隠すものも State では隠さない)。
/// </summary>
[StateeState("game/messbreak")]
public partial class GameState
{
    /// <summary>
    /// 生存している敵1体。Id はフレームを跨いで安定(GUIDELINE 3.4)。
    /// 撃破された敵はリストから消えるため、雑魚の全滅は Kind=Mob の不在で判定できる。
    /// </summary>
    public sealed record EnemyEntry(int Id, string Kind, int Hp, float X, float Y, int DebuffTicks);

    private sealed record Snapshot(
        int Seed,
        int TickCount,
        IReadOnlyList<string> StageRows,
        float TileSize,
        int PlayerHp,
        float PlayerX,
        float PlayerY,
        float FacingX,
        float FacingY,
        string PlayerAction,
        int DodgeCooldown,
        int FireCooldown,
        string ActiveCharacter,
        int SwitchCooldown,
        int AttackerSkillCooldown,
        int DebufferSkillCooldown,
        int BulletCount,
        IReadOnlyList<EnemyEntry> Enemies,
        bool ZoneCaptured,
        bool TurretPlaced,
        int TurretFireCooldown,
        bool BossAppeared,
        bool MissionCleared,
        int ShotCount,
        int HitCount,
        int KillCount,
        bool Paused
    );

    private volatile Snapshot _current = new(
        0,
        0,
        [],
        0f,
        0,
        0f,
        0f,
        0f,
        0f,
        "",
        0,
        0,
        "",
        0,
        0,
        0,
        0,
        [],
        false,
        false,
        0,
        false,
        false,
        0,
        0,
        0,
        false
    );

    [StateeField]
    public int Seed => _current.Seed;

    [StateeField]
    public int TickCount => _current.TickCount;

    /// <summary>ステージ形状を1行1文字列で公開する('#' が壁、それ以外は床)。</summary>
    [StateeField]
    public IReadOnlyList<string> StageRows => _current.StageRows;

    /// <summary>ステージのセル1辺のワールド単位長。座標と StageRows の対応付けに使う。</summary>
    [StateeField]
    public float TileSize => _current.TileSize;

    /// <summary>プレイヤーの残 HP(減らす手段は未実装で、当面は常に満タン)。</summary>
    [StateeField]
    public int PlayerHp => _current.PlayerHp;

    [StateeField]
    public float PlayerX => _current.PlayerX;

    [StateeField]
    public float PlayerY => _current.PlayerY;

    [StateeField]
    public float FacingX => _current.FacingX;

    [StateeField]
    public float FacingY => _current.FacingY;

    [StateeField]
    public string PlayerAction => _current.PlayerAction;

    [StateeField]
    public int DodgeCooldown => _current.DodgeCooldown;

    [StateeField]
    public int FireCooldown => _current.FireCooldown;

    [StateeField]
    public string ActiveCharacter => _current.ActiveCharacter;

    [StateeField]
    public int SwitchCooldown => _current.SwitchCooldown;

    [StateeField]
    public int AttackerSkillCooldown => _current.AttackerSkillCooldown;

    [StateeField]
    public int DebufferSkillCooldown => _current.DebufferSkillCooldown;

    [StateeField]
    public int BulletCount => _current.BulletCount;

    /// <summary>生存している敵の全体(雑魚も強敵も含む)。撃破済みの敵は含まれない。</summary>
    [StateeField]
    public IReadOnlyList<EnemyEntry> Enemies => _current.Enemies;

    [StateeField]
    public bool ZoneCaptured => _current.ZoneCaptured;

    [StateeField]
    public bool TurretPlaced => _current.TurretPlaced;

    [StateeField]
    public int TurretFireCooldown => _current.TurretFireCooldown;

    [StateeField]
    public bool BossAppeared => _current.BossAppeared;

    [StateeField]
    public bool MissionCleared => _current.MissionCleared;

    [StateeField]
    public int ShotCount => _current.ShotCount;

    [StateeField]
    public int HitCount => _current.HitCount;

    [StateeField]
    public int KillCount => _current.KillCount;

    /// <summary>ポーズメニュー(Esc)で論理 tick を止めているか。freeze とは独立。</summary>
    [StateeField]
    public bool Paused => _current.Paused;

    /// <summary>メインスレッドから呼ぶ。スナップショットを不可分に差し替える。</summary>
    public void Update(BattleLogic logic, bool paused)
    {
        _current = new Snapshot(
            logic.Seed,
            logic.TickCount,
            logic.Stage.Rows,
            logic.Stage.TileSize,
            logic.PlayerHp,
            logic.PlayerPos.X,
            logic.PlayerPos.Y,
            logic.PlayerFacing.X,
            logic.PlayerFacing.Y,
            logic.PlayerAction.ToString(),
            logic.DodgeCooldown,
            logic.FireCooldown,
            logic.ActiveCharacter.ToString(),
            logic.SwitchCooldown,
            logic.SkillCooldownOf(CharacterId.Attacker),
            logic.SkillCooldownOf(CharacterId.Debuffer),
            logic.Bullets.Count,
            logic
                .Enemies.Select(e => new EnemyEntry(
                    e.Id,
                    e.Kind.ToString(),
                    e.Hp,
                    e.Pos.X,
                    e.Pos.Y,
                    e.DebuffTicks
                ))
                .ToArray(),
            logic.ZoneCaptured,
            logic.TurretPlaced,
            logic.TurretFireCooldown,
            logic.BossAppeared,
            logic.MissionCleared,
            logic.ShotCount,
            logic.HitCount,
            logic.KillCount,
            paused
        );
    }
}
