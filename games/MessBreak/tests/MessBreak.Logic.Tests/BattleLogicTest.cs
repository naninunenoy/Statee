using System.Numerics;
using Shouldly;

namespace MessBreak.Logic.Tests;

public class BattleLogicTest
{
    private const int Seed = 1;

    private static BattleLogic Create(BattleConfig? config = null) =>
        new(config ?? new BattleConfig(), Seed);

    /// <summary>条件が成立するまで入力なしで進める。固定 tick 待ちを書かないためのヘルパ。</summary>
    private static void TickUntil(BattleLogic logic, Func<bool> condition, int maxTicks = 600)
    {
        for (var i = 0; i < maxTicks; i++)
        {
            if (condition())
            {
                return;
            }
            logic.Tick(TickInput.None);
        }
        throw new InvalidOperationException($"{maxTicks} tick 以内に条件が成立しませんでした");
    }

    /// <summary>生存中の指定種別の敵。いなければ失敗。</summary>
    private static Enemy EnemyOf(BattleLogic logic, EnemyKind kind) =>
        logic.EnemyOf(kind) ?? throw new InvalidOperationException($"{kind} は生存していません");

    /// <summary>指定種別の敵を撃破するまで、その敵へ向けて撃ち続ける。</summary>
    private static void ShootUntilKilled(BattleLogic logic, EnemyKind kind)
    {
        for (var i = 0; i < 900; i++)
        {
            var enemy = logic.EnemyOf(kind);
            if (enemy is null)
            {
                return;
            }
            logic.Tick(new TickInput(AimDir: enemy.Pos - logic.PlayerPos, Fire: true));
        }
        throw new InvalidOperationException($"900 tick 以内に {kind} を撃破できませんでした");
    }

    /// <summary>雑魚がプレイヤーの真右(初期 Facing の先)にいる配置。</summary>
    private static BattleConfig MobStraightRight() => new() { MobSpawn = new Vector2(480f, 180f) };

    /// <summary>初期の向き(1,0)の SkillRange 先=爆心に雑魚がいる配置。</summary>
    private static BattleConfig MobAtSkillCenter() =>
        new() { MobSpawn = new Vector2(160f + 80f, 180f) }; // 既定の SkillRange は 80

    // ---- プレイヤー HP ----

    [Fact]
    public void 初期状態_プレイヤーHPは設定の最大値()
    {
        var logic = Create(new BattleConfig { PlayerMaxHp = 7 });

        logic.PlayerHp.ShouldBe(7);
    }

    [Fact]
    public void Tick_ダメージ源が無い間_プレイヤーHPは減らない()
    {
        var logic = Create();

        TickUntil(logic, () => logic.TickCount >= 60);

        logic.PlayerHp.ShouldBe(logic.Config.PlayerMaxHp);
    }

    // ---- プレイヤー移動 ----

    [Fact]
    public void Tick_右方向の移動入力_毎秒速度を1tick分だけ移動する()
    {
        var logic = Create();
        var before = logic.PlayerPos;

        logic.Tick(new TickInput(new Vector2(1f, 0f)));

        var expected = before.X + logic.Config.PlayerSpeed / logic.Config.TicksPerSecond;
        logic.PlayerPos.X.ShouldBe(expected, 0.001f);
        logic.PlayerPos.Y.ShouldBe(before.Y, 0.001f);
    }

    [Fact]
    public void Tick_斜め方向の移動入力_移動量は直進と同じになる()
    {
        var logic = Create();
        var before = logic.PlayerPos;

        logic.Tick(new TickInput(new Vector2(1f, 1f)));

        var moved = (logic.PlayerPos - before).Length();
        moved.ShouldBe(logic.Config.PlayerSpeed / logic.Config.TicksPerSecond, 0.001f);
    }

    [Fact]
    public void Tick_左の壁に向かって移動し続ける_半径の位置で止まる()
    {
        var logic = Create();

        for (var i = 0; i < 600; i++)
        {
            logic.Tick(new TickInput(new Vector2(-1f, 0f)));
        }

        logic.PlayerPos.X.ShouldBe(logic.Config.PlayerRadius, 0.001f);
    }

    [Fact]
    public void Tick_スプリント入力つきの移動_スプリント速度で移動する()
    {
        var logic = Create();
        var before = logic.PlayerPos;

        logic.Tick(new TickInput(new Vector2(1f, 0f), Sprint: true));

        var expected = before.X + logic.Config.SprintSpeed / logic.Config.TicksPerSecond;
        logic.PlayerPos.X.ShouldBe(expected, 0.001f);
    }

    // ---- エイム(移動と独立) ----

    [Fact]
    public void Tick_エイム入力あり_Facingがエイム方向を向く()
    {
        var logic = Create();

        logic.Tick(new TickInput(AimDir: new Vector2(0f, 1f)));

        logic.PlayerFacing.ShouldBe(new Vector2(0f, 1f));
    }

    [Fact]
    public void Tick_移動入力のみ_Facingは移動方向を向く()
    {
        var logic = Create(); // 初期 Facing は (1,0)

        logic.Tick(new TickInput(new Vector2(0f, 1f)));

        logic.PlayerFacing.ShouldBe(new Vector2(0f, 1f));
    }

    [Fact]
    public void Tick_移動しながらエイム入力_移動方向ではなくエイム方向を向く()
    {
        var logic = Create();

        logic.Tick(new TickInput(new Vector2(0f, 1f), AimDir: new Vector2(-1f, 0f)));

        logic.PlayerFacing.ShouldBe(new Vector2(-1f, 0f));
    }

    [Fact]
    public void Tick_非構えで移動しながら射撃_移動方向へ弾が出る()
    {
        var logic = Create(); // 腰だめ: エイム入力なしでも撃てる

        logic.Tick(new TickInput(new Vector2(0f, 1f), Fire: true));

        logic.Bullets.Count.ShouldBe(1);
        logic.Bullets[0].Dir.ShouldBe(new Vector2(0f, 1f));
    }

    [Fact]
    public void Tick_エイム入力なし_Facingは直前の向きを維持する()
    {
        var logic = Create();
        logic.Tick(new TickInput(AimDir: new Vector2(0f, 1f)));

        logic.Tick(TickInput.None);

        logic.PlayerFacing.ShouldBe(new Vector2(0f, 1f));
    }

    [Fact]
    public void Tick_移動しながらエイム_移動とエイムが両立する()
    {
        var logic = Create();
        var before = logic.PlayerPos;

        logic.Tick(new TickInput(new Vector2(1f, 0f), AimDir: new Vector2(0f, -1f)));

        logic.PlayerPos.X.ShouldBeGreaterThan(before.X);
        logic.PlayerFacing.ShouldBe(new Vector2(0f, -1f));
    }

    // ---- 射撃 ----

    [Fact]
    public void Tick_発射入力_エイム方向の弾が生成される()
    {
        var logic = Create();
        logic.Tick(new TickInput(AimDir: new Vector2(0f, 1f)));

        logic.Tick(new TickInput(Fire: true));

        logic.Bullets.Count.ShouldBe(1);
        logic.Bullets[0].Dir.ShouldBe(new Vector2(0f, 1f));
    }

    [Fact]
    public void Tick_弾_毎tickエイム方向へ速度分だけ進む()
    {
        var logic = Create();
        logic.Tick(new TickInput(Fire: true)); // 初期 Facing (1,0) へ発射
        var before = logic.Bullets[0].Pos;

        logic.Tick(TickInput.None);

        var expected = before.X + logic.Config.BulletSpeed / logic.Config.TicksPerSecond;
        logic.Bullets[0].Pos.X.ShouldBe(expected, 0.001f);
    }

    [Fact]
    public void Tick_発射を押し続ける_クールダウンごとに1発だけ出る()
    {
        var logic = Create(new BattleConfig { FireCooldownTicks = 10 });

        for (var i = 0; i < 10; i++)
        {
            logic.Tick(new TickInput(Fire: true));
        }

        logic.Bullets.Count.ShouldBe(1);
    }

    [Fact]
    public void Tick_クールダウン経過後の発射入力_次の弾が出る()
    {
        var logic = Create(new BattleConfig { FireCooldownTicks = 10 });
        logic.Tick(new TickInput(Fire: true));
        TickUntil(logic, () => logic.FireCooldown == 0);

        logic.Tick(new TickInput(Fire: true));

        logic.Bullets.Count.ShouldBe(2);
    }

    [Fact]
    public void Tick_複数の弾_IDが一意で安定している()
    {
        var logic = Create(new BattleConfig { FireCooldownTicks = 1 });
        logic.Tick(new TickInput(Fire: true));
        var firstId = logic.Bullets[0].Id;
        TickUntil(logic, () => logic.FireCooldown == 0);

        logic.Tick(new TickInput(Fire: true));

        logic.Bullets.Count.ShouldBe(2);
        logic.Bullets[0].Id.ShouldBe(firstId);
        logic.Bullets[1].Id.ShouldNotBe(firstId);
    }

    [Fact]
    public void Tick_移動しながら発射_移動と発射が両立する()
    {
        var logic = Create();
        var before = logic.PlayerPos;

        logic.Tick(new TickInput(new Vector2(0f, 1f), Fire: true));

        logic.PlayerPos.Y.ShouldBeGreaterThan(before.Y);
        logic.Bullets.Count.ShouldBe(1);
    }

    [Fact]
    public void Tick_弾が雑魚に当たる_HPが減り弾は消える()
    {
        var logic = Create(MobStraightRight()); // 雑魚は 320 先。初期 Facing (1,0) = 雑魚の方向
        logic.Tick(new TickInput(Fire: true));

        TickUntil(logic, () => EnemyOf(logic, EnemyKind.Mob).Hp < logic.Config.MobMaxHp, 120);

        EnemyOf(logic, EnemyKind.Mob)
            .Hp.ShouldBe(logic.Config.MobMaxHp - logic.Config.BulletDamage);
        logic.Bullets.ShouldBeEmpty();
    }

    [Fact]
    public void Tick_弾が部屋の外に出る_消える()
    {
        var logic = Create();
        logic.Tick(new TickInput(AimDir: new Vector2(-1f, 0f))); // 敵のいない左の壁へ
        logic.Tick(new TickInput(Fire: true));

        TickUntil(logic, () => logic.Bullets.Count == 0, 120);

        EnemyOf(logic, EnemyKind.Mob).Hp.ShouldBe(logic.Config.MobMaxHp);
    }

    // ---- 命中統計 ----

    [Fact]
    public void Tick_発射_ShotCountが増える()
    {
        var logic = Create();

        logic.Tick(new TickInput(Fire: true));

        logic.ShotCount.ShouldBe(1);
    }

    [Fact]
    public void Tick_命中_HitCountが増える()
    {
        var logic = Create(MobStraightRight());
        logic.Tick(new TickInput(Fire: true));

        TickUntil(logic, () => EnemyOf(logic, EnemyKind.Mob).Hp < logic.Config.MobMaxHp, 120);

        logic.HitCount.ShouldBe(1);
    }

    [Fact]
    public void Tick_弾が外れて消える_HitCountは増えない()
    {
        var logic = Create();
        logic.Tick(new TickInput(AimDir: new Vector2(-1f, 0f)));
        logic.Tick(new TickInput(Fire: true));

        TickUntil(logic, () => logic.Bullets.Count == 0, 120);

        logic.ShotCount.ShouldBe(1);
        logic.HitCount.ShouldBe(0);
    }

    // ---- 敵エリアの制圧(雑魚撃破) ----

    [Fact]
    public void 初期状態_雑魚が1体だけいて未制圧()
    {
        var logic = Create();

        logic.Enemies.Count.ShouldBe(1);
        var mob = EnemyOf(logic, EnemyKind.Mob);
        mob.Pos.ShouldBe(logic.Config.MobSpawn);
        mob.Hp.ShouldBe(logic.Config.MobMaxHp);
        logic.ZoneCaptured.ShouldBeFalse();
    }

    [Fact]
    public void Tick_雑魚を撃破_制圧されZoneCapturedイベントが出る()
    {
        var logic = Create();
        var mobPos = EnemyOf(logic, EnemyKind.Mob).Pos;

        ShootUntilKilled(logic, EnemyKind.Mob);

        logic.EnemyOf(EnemyKind.Mob).ShouldBeNull();
        logic.ZoneCaptured.ShouldBeTrue();
        logic.KillCount.ShouldBe(1);
        logic.Events.ShouldContain(new BattleEvent(BattleEventKind.EnemyKilled, mobPos));
        logic.Events.ShouldContain(new BattleEvent(BattleEventKind.ZoneCaptured, mobPos));
    }

    // ---- タレット設置 ----

    /// <summary>スロットとボス出現ポイントの至近にプレイヤーを置く配置(移動を挟まず検証する)。</summary>
    private static BattleConfig NearSlotAndSpawnPoint() =>
        new()
        {
            PlayerSpawn = new Vector2(430f, 180f), // スロット (440,180) から 10
            BossSpawn = new Vector2(460f, 180f), // 出現ポイントから 30 ≤ AttractRange 40
        };

    [Fact]
    public void Tick_未制圧での設置入力_設置されない()
    {
        var logic = Create(NearSlotAndSpawnPoint());

        logic.Tick(new TickInput(Interact: true));

        logic.TurretPlaced.ShouldBeFalse();
    }

    [Fact]
    public void Tick_制圧済みでスロットから遠い設置入力_設置されない()
    {
        var logic = Create(); // プレイヤー (160,180)、スロット (440,180) = 遠い
        ShootUntilKilled(logic, EnemyKind.Mob);

        logic.Tick(new TickInput(Interact: true));

        logic.TurretPlaced.ShouldBeFalse();
    }

    [Fact]
    public void Tick_制圧済みでスロット付近の設置入力_設置されイベントが出る()
    {
        var logic = Create(NearSlotAndSpawnPoint());
        ShootUntilKilled(logic, EnemyKind.Mob);

        logic.Tick(new TickInput(Interact: true));

        logic.TurretPlaced.ShouldBeTrue();
        logic.Events.ShouldContain(
            new BattleEvent(BattleEventKind.TurretPlaced, logic.Config.TurretSlot)
        );
        // 出現ポイントも範囲内だが、発動するのは最寄りの1つ(スロット)だけ
        logic.BossAppeared.ShouldBeFalse();
    }

    [Fact]
    public void Tick_設置済みの再設置入力_二重には置けずイベントも出ない()
    {
        var logic = Create(NearSlotAndSpawnPoint());
        ShootUntilKilled(logic, EnemyKind.Mob);
        logic.Tick(new TickInput(Interact: true));

        logic.Tick(new TickInput(Interact: true));

        logic.Events.ShouldNotContain(e => e.Kind == BattleEventKind.TurretPlaced);
    }

    [Fact]
    public void Tick_タレット射程内に敵_自動射撃しタレット弾は命中統計に数えない()
    {
        var logic = Create(NearSlotAndSpawnPoint());
        ShootUntilKilled(logic, EnemyKind.Mob);
        logic.Tick(new TickInput(Interact: true));
        logic.Tick(new TickInput(Interact: true)); // 強敵 (460,180) はタレット射程 160 内
        var shots = logic.ShotCount;
        var hits = logic.HitCount;

        TickUntil(logic, () => EnemyOf(logic, EnemyKind.Boss).Hp < logic.Config.BossMaxHp, 120);

        logic.ShotCount.ShouldBe(shots); // タレットの発射・命中はプレイヤーの統計に混ぜない
        logic.HitCount.ShouldBe(hits);
        logic.TurretFireCooldown.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Tick_タレット発射_FromTurretの弾とTurretFiredイベントが出る()
    {
        // 出現ポイント(既定 560,180)はスロット(440,180)から離れており、タレット弾の飛翔を観測できる
        var logic = Create(new BattleConfig { PlayerSpawn = new Vector2(430f, 180f) });
        ShootUntilKilled(logic, EnemyKind.Mob);
        logic.Tick(new TickInput(Interact: true));
        for (
            var i = 0;
            i < 600
                && (logic.Config.BossSpawn - logic.PlayerPos).Length() > logic.Config.AttractRange;
            i++
        )
        {
            logic.Tick(new TickInput(new Vector2(1f, 0f))); // 出現ポイントまで歩く
        }

        logic.Tick(new TickInput(Interact: true));
        TickUntil(logic, () => logic.Bullets.Any(b => b.FromTurret), 120);

        logic.Events.ShouldContain(
            new BattleEvent(BattleEventKind.TurretFired, logic.Config.TurretSlot)
        );
    }

    [Fact]
    public void Tick_射程内に敵がいない_タレットは撃たない()
    {
        var logic = Create(NearSlotAndSpawnPoint());
        ShootUntilKilled(logic, EnemyKind.Mob); // 以後、敵は 0 体
        logic.Tick(new TickInput(Interact: true));

        for (var i = 0; i < 60; i++)
        {
            logic.Tick(TickInput.None);
            logic.Bullets.ShouldAllBe(b => !b.FromTurret);
        }
    }

    // ---- アトラクト(強敵の出現) ----

    [Fact]
    public void Tick_出現ポイントから遠いアトラクト入力_強敵は出ない()
    {
        var logic = Create(); // プレイヤー (160,180)、出現ポイント (560,180) = 遠い

        logic.Tick(new TickInput(Interact: true));

        logic.BossAppeared.ShouldBeFalse();
        logic.EnemyOf(EnemyKind.Boss).ShouldBeNull();
    }

    [Fact]
    public void Tick_出現ポイント付近のアトラクト入力_強敵が出現しイベントが出る()
    {
        var logic = Create(NearSlotAndSpawnPoint());

        logic.Tick(new TickInput(Interact: true));

        logic.BossAppeared.ShouldBeTrue();
        var boss = EnemyOf(logic, EnemyKind.Boss);
        boss.Pos.ShouldBe(logic.Config.BossSpawn);
        boss.Hp.ShouldBe(logic.Config.BossMaxHp);
        logic.Events.ShouldContain(
            new BattleEvent(BattleEventKind.BossAppeared, logic.Config.BossSpawn)
        );
    }

    [Fact]
    public void Tick_二度目のアトラクト入力_強敵は増えない()
    {
        var logic = Create(NearSlotAndSpawnPoint());
        logic.Tick(new TickInput(Interact: true));

        logic.Tick(new TickInput(Interact: true));

        logic.Enemies.Count(e => e.Kind == EnemyKind.Boss).ShouldBe(1);
    }

    [Fact]
    public void Tick_強敵_毎tickプレイヤーへ速度分だけ近づく()
    {
        var logic = Create(NearSlotAndSpawnPoint());
        logic.Tick(new TickInput(Interact: true));
        var before = (EnemyOf(logic, EnemyKind.Boss).Pos - logic.PlayerPos).Length();

        logic.Tick(TickInput.None);

        var after = (EnemyOf(logic, EnemyKind.Boss).Pos - logic.PlayerPos).Length();
        (before - after).ShouldBe(logic.Config.BossSpeed / logic.Config.TicksPerSecond, 0.001f);
    }

    [Fact]
    public void Tick_強敵がプレイヤーに接触_半径の和の距離で止まる()
    {
        var logic = Create(NearSlotAndSpawnPoint());
        logic.Tick(new TickInput(Interact: true));
        var contact = logic.Config.BossRadius + logic.Config.PlayerRadius;

        TickUntil(
            logic,
            () =>
                (EnemyOf(logic, EnemyKind.Boss).Pos - logic.PlayerPos).Length() <= contact + 0.001f
        );
        logic.Tick(TickInput.None);

        (EnemyOf(logic, EnemyKind.Boss).Pos - logic.PlayerPos).Length().ShouldBe(contact, 0.001f);
    }

    [Fact]
    public void Tick_強敵を撃破_ミッション達成イベントが出る()
    {
        var logic = Create(NearSlotAndSpawnPoint());
        logic.Tick(new TickInput(Interact: true));

        ShootUntilKilled(logic, EnemyKind.Boss);

        logic.MissionCleared.ShouldBeTrue();
        logic.Events.ShouldContain(e => e.Kind == BattleEventKind.MissionCleared);
        logic.KillCount.ShouldBe(1);
    }

    // ---- スキル(向いている方向の一定距離先に範囲爆発) ----

    [Fact]
    public void Tick_スキル入力_範囲内の雑魚にスキルダメージが入る()
    {
        var logic = Create(MobAtSkillCenter() with { MobMaxHp = 5 });

        logic.Tick(new TickInput(Skill: true));

        EnemyOf(logic, EnemyKind.Mob).Hp.ShouldBe(5 - logic.Config.Attacker.SkillDamage);
    }

    [Fact]
    public void Tick_スキル入力_範囲外の雑魚にはダメージが入らない()
    {
        var logic = Create(MobStraightRight()); // 雑魚は 320 先。爆心 80 + 半径 40 + 敵 8 では届かない

        logic.Tick(new TickInput(Skill: true));

        EnemyOf(logic, EnemyKind.Mob).Hp.ShouldBe(logic.Config.MobMaxHp);
    }

    [Fact]
    public void Tick_スキル入力_SkillBurstイベントが爆心位置で発生する()
    {
        var logic = Create();

        logic.Tick(new TickInput(Skill: true));

        var center = logic.PlayerPos + logic.PlayerFacing * logic.Config.Attacker.SkillRange;
        logic.Events.ShouldContain(new BattleEvent(BattleEventKind.SkillBurst, center));
    }

    [Fact]
    public void Tick_AimPointつきスキル_射程内ならその位置が爆心になる()
    {
        var logic = Create();
        var point = logic.PlayerPos + new Vector2(30f, -40f); // 距離 50 ≤ 射程 80

        logic.Tick(new TickInput(Skill: true, AimPoint: point));

        logic.Events.ShouldContain(new BattleEvent(BattleEventKind.SkillBurst, point));
    }

    [Fact]
    public void Tick_射程外のAimPoint_その方向の射程上限が爆心になる()
    {
        var logic = Create();
        var point = logic.PlayerPos + new Vector2(300f, 400f); // 距離 500 > 射程 80

        logic.Tick(new TickInput(Skill: true, AimPoint: point));

        var expected = logic.PlayerPos + new Vector2(0.6f, 0.8f) * logic.Config.Attacker.SkillRange;
        var burst = logic.Events.Single(e => e.Kind == BattleEventKind.SkillBurst);
        burst.Pos.X.ShouldBe(expected.X, 0.001f);
        burst.Pos.Y.ShouldBe(expected.Y, 0.001f);
    }

    [Fact]
    public void Tick_スキル発動後_クールダウンが始まり連発できない()
    {
        var logic = Create(MobAtSkillCenter() with { MobMaxHp = 10 });
        logic.Tick(new TickInput(Skill: true));
        var hpAfterFirst = EnemyOf(logic, EnemyKind.Mob).Hp;

        logic.Tick(new TickInput(Skill: true));

        logic.SkillCooldown.ShouldBe(logic.Config.Attacker.SkillCooldownTicks - 1);
        EnemyOf(logic, EnemyKind.Mob).Hp.ShouldBe(hpAfterFirst); // 2 発目は不発
    }

    [Fact]
    public void Tick_クールダウン経過後_スキルを再発動できる()
    {
        var logic = Create(
            new BattleConfig { Attacker = new CharacterConfig { SkillCooldownTicks = 5 } }
        );
        logic.Tick(new TickInput(Skill: true));
        TickUntil(logic, () => logic.SkillCooldown == 0, 10);

        logic.Tick(new TickInput(Skill: true));

        logic.Events.ShouldContain(e => e.Kind == BattleEventKind.SkillBurst);
    }

    [Fact]
    public void Tick_スキルで雑魚を撃破_制圧されKillCountが増える()
    {
        var logic = Create(MobAtSkillCenter()); // MobMaxHp 3 = SkillDamage 3 で一撃

        logic.Tick(new TickInput(Skill: true));

        logic.EnemyOf(EnemyKind.Mob).ShouldBeNull();
        logic.ZoneCaptured.ShouldBeTrue();
        logic.KillCount.ShouldBe(1);
    }

    [Fact]
    public void Tick_スキル_範囲内の複数の敵に同時に効く()
    {
        var config = new BattleConfig
        {
            PlayerSpawn = new Vector2(240f, 180f),
            MobSpawn = new Vector2(260f, 180f),
            BossSpawn = new Vector2(250f, 180f), // 出現ポイントから 10 ≤ AttractRange
        };
        var logic = Create(config);
        logic.Tick(new TickInput(Interact: true));

        logic.Tick(new TickInput(Skill: true, AimPoint: new Vector2(255f, 180f)));

        logic.EnemyOf(EnemyKind.Mob).ShouldBeNull(); // 雑魚 3 は一撃
        EnemyOf(logic, EnemyKind.Boss)
            .Hp.ShouldBe(logic.Config.BossMaxHp - logic.Config.Attacker.SkillDamage);
    }

    [Fact]
    public void Tick_スキル命中_射撃の命中統計には数えない()
    {
        var logic = Create(MobAtSkillCenter() with { MobMaxHp = 10 });

        logic.Tick(new TickInput(Skill: true));

        logic.ShotCount.ShouldBe(0);
        logic.HitCount.ShouldBe(0);
    }

    [Fact]
    public void Tick_ドッジ中のスキル入力_発動しない()
    {
        var logic = Create(MobAtSkillCenter() with { DodgeTicks = 10 });
        logic.Tick(new TickInput(new Vector2(0f, 1f), Dodge: true));

        logic.Tick(new TickInput(Skill: true));

        EnemyOf(logic, EnemyKind.Mob).Hp.ShouldBe(logic.Config.MobMaxHp);
        logic.SkillCooldown.ShouldBe(0);
    }

    // ---- キャラ切り替えとデバフ ----

    [Fact]
    public void Tick_キャラ2を指定_デバッファーに切り替わりイベントが出る()
    {
        var logic = Create();

        logic.Tick(new TickInput(SwitchTo: CharacterId.Debuffer));

        logic.ActiveCharacter.ShouldBe(CharacterId.Debuffer);
        logic.Events.ShouldContain(
            new BattleEvent(BattleEventKind.CharacterSwitched, logic.PlayerPos)
        );
    }

    [Fact]
    public void Tick_操作中のキャラを指定_何も起きずクールダウンも消費しない()
    {
        var logic = Create();

        logic.Tick(new TickInput(SwitchTo: CharacterId.Attacker));

        logic.ActiveCharacter.ShouldBe(CharacterId.Attacker);
        logic.SwitchCooldown.ShouldBe(0);
        logic.Events.ShouldBeEmpty();
    }

    [Fact]
    public void Tick_切り替えクールダウン中_切り替わらない()
    {
        var logic = Create();
        logic.Tick(new TickInput(SwitchTo: CharacterId.Debuffer));

        logic.Tick(new TickInput(SwitchTo: CharacterId.Attacker));

        logic.ActiveCharacter.ShouldBe(CharacterId.Debuffer); // 2 回目は不発
    }

    [Fact]
    public void Tick_クールダウン経過後_キャラ1指定でアタッカーに戻る()
    {
        var logic = Create(new BattleConfig { SwitchCooldownTicks = 5 });
        logic.Tick(new TickInput(SwitchTo: CharacterId.Debuffer));
        TickUntil(logic, () => logic.SwitchCooldown == 0, 10);

        logic.Tick(new TickInput(SwitchTo: CharacterId.Attacker));

        logic.ActiveCharacter.ShouldBe(CharacterId.Attacker);
    }

    [Fact]
    public void Tick_スキルクールダウンはキャラごとに独立している()
    {
        var logic = Create();
        logic.Tick(new TickInput(Skill: true)); // アタッカーのスキルを消費

        logic.Tick(new TickInput(SwitchTo: CharacterId.Debuffer));

        logic.SkillCooldown.ShouldBe(0); // デバッファー側は未消費
        logic.SkillCooldownOf(CharacterId.Attacker).ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Tick_キャラごとに異なるスキルクールダウンを設定できる()
    {
        var logic = Create(
            new BattleConfig
            {
                SwitchCooldownTicks = 1,
                Attacker = new CharacterConfig { SkillCooldownTicks = 100 },
                Debuffer = new CharacterConfig { SkillCooldownTicks = 50 },
            }
        );

        logic.Tick(new TickInput(Skill: true)); // アタッカーで発動
        logic.Tick(new TickInput(SwitchTo: CharacterId.Debuffer));
        logic.Tick(new TickInput(Skill: true)); // デバッファーで発動

        logic.SkillCooldownOf(CharacterId.Attacker).ShouldBe(98); // 100 から 2 tick 経過
        logic.SkillCooldownOf(CharacterId.Debuffer).ShouldBe(50);
    }

    [Fact]
    public void Tick_デバッファースキル_雑魚にデバフが付きダメージは入らない()
    {
        var logic = Create(MobAtSkillCenter() with { SwitchCooldownTicks = 1 });
        logic.Tick(new TickInput(SwitchTo: CharacterId.Debuffer));

        logic.Tick(new TickInput(Skill: true));

        var mob = EnemyOf(logic, EnemyKind.Mob);
        mob.DebuffTicks.ShouldBe(logic.Config.Debuffer.DebuffDurationTicks);
        mob.Hp.ShouldBe(logic.Config.MobMaxHp);
        logic.Events.ShouldContain(new BattleEvent(BattleEventKind.EnemyDebuffed, mob.Pos));
    }

    [Fact]
    public void Tick_範囲外のデバッファースキル_デバフは付かない()
    {
        var logic = Create(MobStraightRight() with { SwitchCooldownTicks = 1 }); // 雑魚は 320 先
        logic.Tick(new TickInput(SwitchTo: CharacterId.Debuffer));

        logic.Tick(new TickInput(Skill: true));

        EnemyOf(logic, EnemyKind.Mob).DebuffTicks.ShouldBe(0);
    }

    [Fact]
    public void Tick_デバフ中の被弾_ダメージが倍率分になる()
    {
        var logic = Create(MobAtSkillCenter() with { SwitchCooldownTicks = 1, MobMaxHp = 10 });
        logic.Tick(new TickInput(SwitchTo: CharacterId.Debuffer));
        logic.Tick(new TickInput(Skill: true)); // デバフ付与

        logic.Tick(
            new TickInput(AimDir: EnemyOf(logic, EnemyKind.Mob).Pos - logic.PlayerPos, Fire: true)
        );
        TickUntil(logic, () => logic.HitCount == 1, 120);

        var expected =
            10 - logic.Config.BulletDamage * logic.Config.Debuffer.DebuffDamageMultiplier;
        EnemyOf(logic, EnemyKind.Mob).Hp.ShouldBe(expected);
    }

    [Fact]
    public void Tick_デバフ_持続時間が切れると等倍に戻る()
    {
        var logic = Create(
            MobAtSkillCenter() with
            {
                SwitchCooldownTicks = 1,
                MobMaxHp = 10,
                Debuffer = new CharacterConfig { DebuffDurationTicks = 5 },
            }
        );
        logic.Tick(new TickInput(SwitchTo: CharacterId.Debuffer));
        logic.Tick(new TickInput(Skill: true));
        TickUntil(logic, () => EnemyOf(logic, EnemyKind.Mob).DebuffTicks == 0, 10);

        logic.Tick(
            new TickInput(AimDir: EnemyOf(logic, EnemyKind.Mob).Pos - logic.PlayerPos, Fire: true)
        );
        TickUntil(logic, () => logic.HitCount == 1, 120);

        EnemyOf(logic, EnemyKind.Mob).Hp.ShouldBe(10 - logic.Config.BulletDamage);
    }

    [Fact]
    public void Tick_コンボ_デバフから切り替えてアタッカースキルで満タンの雑魚を一撃()
    {
        // MobMaxHp 6 = SkillDamage 3 × DebuffDamageMultiplier 2。コンボでのみ一撃になる
        var logic = Create(MobAtSkillCenter() with { SwitchCooldownTicks = 1, MobMaxHp = 6 });
        logic.Tick(new TickInput(SwitchTo: CharacterId.Debuffer)); // デバッファーへ
        logic.Tick(new TickInput(Skill: true)); // デバフ付与
        logic.Tick(new TickInput(SwitchTo: CharacterId.Attacker)); // アタッカーへ戻す

        logic.Tick(new TickInput(Skill: true)); // 大技

        logic.EnemyOf(EnemyKind.Mob).ShouldBeNull();
        logic.KillCount.ShouldBe(1);
    }

    // ---- イベント(Godot 層が音・エフェクトに翻訳する) ----

    [Fact]
    public void Tick_発射_BulletFiredイベントが発射位置で発生する()
    {
        var logic = Create();

        logic.Tick(new TickInput(Fire: true));

        logic.Events.ShouldContain(new BattleEvent(BattleEventKind.BulletFired, logic.PlayerPos));
    }

    [Fact]
    public void Tick_命中_EnemyHitイベントが発生する()
    {
        var logic = Create(MobStraightRight());
        logic.Tick(new TickInput(Fire: true));

        TickUntil(logic, () => logic.HitCount == 1, 120);

        logic.Events.ShouldContain(e => e.Kind == BattleEventKind.EnemyHit);
    }

    [Fact]
    public void Tick_何も起きないtick_イベントはクリアされる()
    {
        var logic = Create();
        logic.Tick(new TickInput(Fire: true));

        logic.Tick(TickInput.None);

        logic.Events.ShouldBeEmpty();
    }

    // ---- エイムアシスト ----

    [Fact]
    public void Tick_吸着角の内側にズレた発射_弾は敵の中心へ向かう()
    {
        var logic = Create(MobStraightRight() with { AimAssistDegrees = 10f });
        // 雑魚は真右(1,0)。約 5.7 度上へずらしてもアシストで吸われる
        logic.Tick(new TickInput(AimDir: new Vector2(1f, -0.1f)));

        logic.Tick(new TickInput(Fire: true));

        var toMob = Vector2.Normalize(EnemyOf(logic, EnemyKind.Mob).Pos - logic.PlayerPos);
        logic.Bullets[0].Dir.X.ShouldBe(toMob.X, 0.001f);
        logic.Bullets[0].Dir.Y.ShouldBe(toMob.Y, 0.001f);
    }

    [Fact]
    public void Tick_吸着角の外側の発射_補正されない()
    {
        var logic = Create(MobStraightRight() with { AimAssistDegrees = 10f });
        // 約 45 度上=コーン外
        var aim = Vector2.Normalize(new Vector2(1f, -1f));
        logic.Tick(new TickInput(AimDir: aim));

        logic.Tick(new TickInput(Fire: true));

        logic.Bullets[0].Dir.X.ShouldBe(aim.X, 0.001f);
        logic.Bullets[0].Dir.Y.ShouldBe(aim.Y, 0.001f);
    }

    [Fact]
    public void Tick_敵が全滅している_補正されない()
    {
        var logic = Create(MobStraightRight() with { AimAssistDegrees = 10f });
        ShootUntilKilled(logic, EnemyKind.Mob);
        TickUntil(logic, () => logic.FireCooldown == 0);
        var aim = new Vector2(1f, -0.1f);
        logic.Tick(new TickInput(AimDir: aim));

        logic.Tick(new TickInput(Fire: true));

        var expected = Vector2.Normalize(aim);
        logic.Bullets[^1].Dir.X.ShouldBe(expected.X, 0.001f);
        logic.Bullets[^1].Dir.Y.ShouldBe(expected.Y, 0.001f);
    }

    // ---- ドッジ ----

    [Fact]
    public void Tick_移動入力なしのドッジ入力_ドッジは発動しない()
    {
        var logic = Create();
        var before = logic.PlayerPos;

        logic.Tick(new TickInput(Vector2.Zero, Dodge: true));

        logic.PlayerAction.ShouldBe(PlayerAction.Free);
        logic.PlayerPos.ShouldBe(before);
        logic.DodgeCooldown.ShouldBe(0); // 不発ではクールダウンも消費しない
    }

    [Fact]
    public void Tick_移動入力つきドッジ_移動方向に移動しエイム入力保持中は向きが維持される()
    {
        var logic = Create();
        logic.Tick(new TickInput(AimDir: new Vector2(1f, 0f)));
        var before = logic.PlayerPos;

        logic.Tick(new TickInput(new Vector2(0f, 1f), AimDir: new Vector2(1f, 0f), Dodge: true));

        logic.PlayerPos.Y.ShouldBe(
            before.Y + logic.Config.DodgeSpeed / logic.Config.TicksPerSecond,
            0.001f
        );
        logic.PlayerFacing.ShouldBe(new Vector2(1f, 0f));
    }

    [Fact]
    public void Tick_ドッジ中の発射入力_弾が出ない()
    {
        var logic = Create(new BattleConfig { DodgeTicks = 10 });
        logic.Tick(new TickInput(new Vector2(0f, 1f), Dodge: true));

        logic.Tick(new TickInput(Fire: true));

        logic.Bullets.ShouldBeEmpty();
    }

    [Fact]
    public void Tick_ドッジ後のクールダウン中_再ドッジできない()
    {
        var logic = Create(new BattleConfig { DodgeTicks = 5, DodgeCooldownTicks = 100 });
        logic.Tick(new TickInput(new Vector2(0f, 1f), Dodge: true));
        TickUntil(logic, () => logic.PlayerAction == PlayerAction.Free);

        logic.Tick(new TickInput(new Vector2(0f, 1f), Dodge: true));

        logic.PlayerAction.ShouldBe(PlayerAction.Free);
    }

    [Fact]
    public void Tick_クールダウン経過後_再ドッジできる()
    {
        var logic = Create(new BattleConfig { DodgeTicks = 5, DodgeCooldownTicks = 8 });
        logic.Tick(new TickInput(new Vector2(0f, 1f), Dodge: true));
        TickUntil(logic, () => logic.PlayerAction == PlayerAction.Free && logic.DodgeCooldown == 0);

        logic.Tick(new TickInput(new Vector2(0f, 1f), Dodge: true));

        logic.PlayerAction.ShouldBe(PlayerAction.Dodge);
    }
}
