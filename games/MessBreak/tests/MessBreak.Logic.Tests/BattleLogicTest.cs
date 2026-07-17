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
    public void Tick_弾が的に当たる_HPが減り弾は消える()
    {
        var logic = Create(); // 的は 160 先。初期 Facing (1,0) = 的の方向
        logic.Tick(new TickInput(Fire: true));

        TickUntil(logic, () => logic.TargetHp < logic.Config.TargetMaxHp, 120);

        logic.TargetHp.ShouldBe(logic.Config.TargetMaxHp - logic.Config.BulletDamage);
        logic.Bullets.ShouldBeEmpty();
    }

    [Fact]
    public void Tick_弾が部屋の外に出る_消える()
    {
        var logic = Create();
        logic.Tick(new TickInput(AimDir: new Vector2(-1f, 0f))); // 的のいない左の壁へ
        logic.Tick(new TickInput(Fire: true));

        TickUntil(logic, () => logic.Bullets.Count == 0, 120);

        logic.TargetHp.ShouldBe(logic.Config.TargetMaxHp);
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
        var logic = Create();
        logic.Tick(new TickInput(Fire: true));

        TickUntil(logic, () => logic.TargetHp < logic.Config.TargetMaxHp, 120);

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

    // ---- 的の撃破とリスポーン ----

    /// <summary>的を撃破するまで撃ち続ける。</summary>
    private static void ShootUntilKilled(BattleLogic logic)
    {
        for (var i = 0; i < 600; i++)
        {
            if (logic.TargetHp == 0)
            {
                return;
            }
            logic.Tick(new TickInput(AimDir: logic.TargetPos - logic.PlayerPos, Fire: true));
        }
        throw new InvalidOperationException("600 tick 以内に的を撃破できませんでした");
    }

    [Fact]
    public void Tick_的のHPが0_KillCountが増えリスポーン待ちが始まる()
    {
        var logic = Create();

        ShootUntilKilled(logic);

        logic.KillCount.ShouldBe(1);
        logic.TargetRespawnCooldown.ShouldBe(logic.Config.TargetRespawnTicks);
    }

    [Fact]
    public void Tick_リスポーン待ちが明ける_HP全快で復活する()
    {
        var logic = Create();
        ShootUntilKilled(logic);

        TickUntil(logic, () => logic.TargetHp > 0, logic.Config.TargetRespawnTicks + 1);

        logic.TargetHp.ShouldBe(logic.Config.TargetMaxHp);
        logic.TargetRespawnCooldown.ShouldBe(0);
    }

    [Fact]
    public void Tick_リスポーン位置_部屋内かつプレイヤーから離れている()
    {
        var logic = Create();
        var config = logic.Config;

        // 乱数配置なので複数回リスポーンさせて全数検査する
        for (var kill = 0; kill < 5; kill++)
        {
            ShootUntilKilled(logic);
            TickUntil(logic, () => logic.TargetHp > 0, config.TargetRespawnTicks + 1);

            logic.TargetPos.X.ShouldBeInRange(
                config.TargetSpawnMargin,
                config.RoomWidth - config.TargetSpawnMargin
            );
            logic.TargetPos.Y.ShouldBeInRange(
                config.TargetSpawnMargin,
                config.RoomHeight - config.TargetSpawnMargin
            );
            (logic.TargetPos - logic.PlayerPos)
                .Length()
                .ShouldBeGreaterThanOrEqualTo(config.TargetMinPlayerDistance);
        }
    }

    [Fact]
    public void Tick_リスポーン待ち中_弾は当たらずすり抜ける()
    {
        var logic = Create();
        ShootUntilKilled(logic);
        var hits = logic.HitCount;

        logic.Tick(new TickInput(AimDir: logic.TargetPos - logic.PlayerPos, Fire: true));
        for (var i = 0; i < logic.Config.TargetRespawnTicks - 1; i++)
        {
            logic.Tick(TickInput.None);
        }

        logic.HitCount.ShouldBe(hits);
    }

    [Fact]
    public void Tick_同じseed_リスポーン位置の履歴が一致する_決定論()
    {
        var first = Create();
        var second = Create();
        for (var kill = 0; kill < 3; kill++)
        {
            ShootUntilKilled(first);
            TickUntil(first, () => first.TargetHp > 0, first.Config.TargetRespawnTicks + 1);
            ShootUntilKilled(second);
            TickUntil(second, () => second.TargetHp > 0, second.Config.TargetRespawnTicks + 1);

            second.TargetPos.ShouldBe(first.TargetPos);
        }
    }

    // ---- スキル(向いている方向の一定距離先に範囲爆発) ----

    /// <summary>初期の向き(1,0)の SkillRange 先=爆心に的がいる配置。</summary>
    private static BattleConfig TargetAtSkillCenter() =>
        new() { TargetSpawn = new Vector2(160f + 80f, 180f), SkillRange = 80f };

    [Fact]
    public void Tick_スキル入力_範囲内の的にスキルダメージが入る()
    {
        var logic = Create(TargetAtSkillCenter());

        logic.Tick(new TickInput(Skill: true));

        logic.TargetHp.ShouldBe(Math.Max(0, logic.Config.TargetMaxHp - logic.Config.SkillDamage));
    }

    [Fact]
    public void Tick_スキル入力_範囲外の的にはダメージが入らない()
    {
        var logic = Create(); // 的は 320 先。爆心 80 + 半径 40 + 的 8 では届かない

        logic.Tick(new TickInput(Skill: true));

        logic.TargetHp.ShouldBe(logic.Config.TargetMaxHp);
    }

    [Fact]
    public void Tick_スキル入力_SkillBurstイベントが爆心位置で発生する()
    {
        var logic = Create();

        logic.Tick(new TickInput(Skill: true));

        var center = logic.PlayerPos + logic.PlayerFacing * logic.Config.SkillRange;
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

        var expected = logic.PlayerPos + new Vector2(0.6f, 0.8f) * logic.Config.SkillRange;
        var burst = logic.Events.Single(e => e.Kind == BattleEventKind.SkillBurst);
        burst.Pos.X.ShouldBe(expected.X, 0.001f);
        burst.Pos.Y.ShouldBe(expected.Y, 0.001f);
    }

    [Fact]
    public void Tick_スキル発動後_クールダウンが始まり連発できない()
    {
        var logic = Create(TargetAtSkillCenter());
        logic.Tick(new TickInput(Skill: true));
        var hpAfterFirst = logic.TargetHp;

        logic.Tick(new TickInput(Skill: true));

        logic.SkillCooldown.ShouldBe(logic.Config.SkillCooldownTicks - 1);
        logic.TargetHp.ShouldBe(hpAfterFirst); // 2 発目は不発
    }

    [Fact]
    public void Tick_クールダウン経過後_スキルを再発動できる()
    {
        var logic = Create(new BattleConfig { SkillCooldownTicks = 5 });
        logic.Tick(new TickInput(Skill: true));
        TickUntil(logic, () => logic.SkillCooldown == 0, 10);

        logic.Tick(new TickInput(Skill: true));

        logic.Events.ShouldContain(e => e.Kind == BattleEventKind.SkillBurst);
    }

    [Fact]
    public void Tick_スキルで撃破_KillCountが増えリスポーン待ちが始まる()
    {
        var logic = Create(TargetAtSkillCenter() with { TargetMaxHp = 3 }); // SkillDamage 3 で一撃

        logic.Tick(new TickInput(Skill: true));

        logic.TargetHp.ShouldBe(0);
        logic.KillCount.ShouldBe(1);
        logic.TargetRespawnCooldown.ShouldBe(logic.Config.TargetRespawnTicks);
    }

    [Fact]
    public void Tick_スキル命中_射撃の命中統計には数えない()
    {
        var logic = Create(TargetAtSkillCenter());

        logic.Tick(new TickInput(Skill: true));

        logic.ShotCount.ShouldBe(0);
        logic.HitCount.ShouldBe(0);
    }

    [Fact]
    public void Tick_ドッジ中のスキル入力_発動しない()
    {
        var logic = Create(TargetAtSkillCenter() with { DodgeTicks = 10 });
        logic.Tick(new TickInput(new Vector2(0f, 1f), Dodge: true));

        logic.Tick(new TickInput(Skill: true));

        logic.TargetHp.ShouldBe(logic.Config.TargetMaxHp);
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
    public void Tick_デバッファースキル_的にデバフが付きダメージは入らない()
    {
        var logic = Create(TargetAtSkillCenter() with { SwitchCooldownTicks = 1 });
        logic.Tick(new TickInput(SwitchTo: CharacterId.Debuffer));

        logic.Tick(new TickInput(Skill: true));

        logic.TargetDebuffTicks.ShouldBe(logic.Config.DebuffDurationTicks);
        logic.TargetHp.ShouldBe(logic.Config.TargetMaxHp);
        logic.Events.ShouldContain(
            new BattleEvent(BattleEventKind.TargetDebuffed, logic.TargetPos)
        );
    }

    [Fact]
    public void Tick_範囲外のデバッファースキル_デバフは付かない()
    {
        var logic = Create(new BattleConfig { SwitchCooldownTicks = 1 }); // 的は 320 先
        logic.Tick(new TickInput(SwitchTo: CharacterId.Debuffer));

        logic.Tick(new TickInput(Skill: true));

        logic.TargetDebuffTicks.ShouldBe(0);
    }

    [Fact]
    public void Tick_デバフ中の被弾_ダメージが倍率分になる()
    {
        var logic = Create(TargetAtSkillCenter() with { SwitchCooldownTicks = 1 });
        logic.Tick(new TickInput(SwitchTo: CharacterId.Debuffer));
        logic.Tick(new TickInput(Skill: true)); // デバフ付与

        logic.Tick(new TickInput(AimDir: logic.TargetPos - logic.PlayerPos, Fire: true));
        TickUntil(logic, () => logic.HitCount == 1, 120);

        var expected =
            logic.Config.TargetMaxHp
            - logic.Config.BulletDamage * logic.Config.DebuffDamageMultiplier;
        logic.TargetHp.ShouldBe(expected);
    }

    [Fact]
    public void Tick_デバフ_持続時間が切れると等倍に戻る()
    {
        var logic = Create(
            TargetAtSkillCenter() with
            {
                SwitchCooldownTicks = 1,
                DebuffDurationTicks = 5,
            }
        );
        logic.Tick(new TickInput(SwitchTo: CharacterId.Debuffer));
        logic.Tick(new TickInput(Skill: true));
        TickUntil(logic, () => logic.TargetDebuffTicks == 0, 10);

        logic.Tick(new TickInput(AimDir: logic.TargetPos - logic.PlayerPos, Fire: true));
        TickUntil(logic, () => logic.HitCount == 1, 120);

        logic.TargetHp.ShouldBe(logic.Config.TargetMaxHp - logic.Config.BulletDamage);
    }

    [Fact]
    public void Tick_リスポーン後の的_デバフは引き継がない()
    {
        var logic = Create(TargetAtSkillCenter() with { SwitchCooldownTicks = 1 });
        logic.Tick(new TickInput(SwitchTo: CharacterId.Debuffer));
        logic.Tick(new TickInput(Skill: true)); // デバフ付与
        ShootUntilKilled(logic);

        TickUntil(logic, () => logic.TargetHp > 0, logic.Config.TargetRespawnTicks + 1);

        logic.TargetDebuffTicks.ShouldBe(0);
    }

    [Fact]
    public void Tick_コンボ_デバフから切り替えてアタッカースキルで満タンの的を一撃()
    {
        // TargetMaxHp 6 = SkillDamage 3 × DebuffDamageMultiplier 2。コンボでのみ一撃になる
        var logic = Create(TargetAtSkillCenter() with { SwitchCooldownTicks = 1 });
        logic.Tick(new TickInput(SwitchTo: CharacterId.Debuffer)); // デバッファーへ
        logic.Tick(new TickInput(Skill: true)); // デバフ付与
        logic.Tick(new TickInput(SwitchTo: CharacterId.Attacker)); // アタッカーへ戻す

        logic.Tick(new TickInput(Skill: true)); // 大技

        logic.TargetHp.ShouldBe(0);
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
    public void Tick_命中_TargetHitイベントが発生する()
    {
        var logic = Create();
        logic.Tick(new TickInput(Fire: true));

        TickUntil(logic, () => logic.HitCount == 1, 120);

        logic.Events.ShouldContain(e => e.Kind == BattleEventKind.TargetHit);
    }

    [Fact]
    public void Tick_撃破_TargetKilledイベントが的の位置で発生する()
    {
        var logic = Create();
        var targetPos = logic.TargetPos;

        ShootUntilKilled(logic);

        logic.Events.ShouldContain(new BattleEvent(BattleEventKind.TargetKilled, targetPos));
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
    public void Tick_吸着角の内側にズレた発射_弾は的の中心へ向かう()
    {
        var logic = Create(new BattleConfig { AimAssistDegrees = 10f });
        // 的は真右(1,0)。約 5.7 度上へずらしてもアシストで吸われる
        logic.Tick(new TickInput(AimDir: new Vector2(1f, -0.1f)));

        logic.Tick(new TickInput(Fire: true));

        var toTarget = Vector2.Normalize(logic.TargetPos - logic.PlayerPos);
        logic.Bullets[0].Dir.X.ShouldBe(toTarget.X, 0.001f);
        logic.Bullets[0].Dir.Y.ShouldBe(toTarget.Y, 0.001f);
    }

    [Fact]
    public void Tick_吸着角の外側の発射_補正されない()
    {
        var logic = Create(new BattleConfig { AimAssistDegrees = 10f });
        // 約 45 度上=コーン外
        var aim = Vector2.Normalize(new Vector2(1f, -1f));
        logic.Tick(new TickInput(AimDir: aim));

        logic.Tick(new TickInput(Fire: true));

        logic.Bullets[0].Dir.X.ShouldBe(aim.X, 0.001f);
        logic.Bullets[0].Dir.Y.ShouldBe(aim.Y, 0.001f);
    }

    [Fact]
    public void Tick_リスポーン待ち中の発射_アシストされない()
    {
        var logic = Create(new BattleConfig { AimAssistDegrees = 10f });
        ShootUntilKilled(logic);
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
