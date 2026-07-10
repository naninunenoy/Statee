---
name: new-game
description: >-
  Statee を組み込んだ新しいゲームの雛形(純C#ロジック / xUnit テスト / Godot 層)を生成する。
  「新しいゲームを作りたい」「ゲームの雛形を作って」等の依頼で使う。
  引数: ゲーム名(PascalCase。例: /new-game Othello)
---

# 新しいゲームのスキャフォールド

`game/<Name>.Logic` + `tests/<Name>.Logic.Tests` + `game/<Name>.Godot` の3プロジェクトと
専用ソリューション `game/<Name>.slnx` を生成し(フレームワークの `Statee.slnx` には
入れない。D-046)、ビルド・テスト・headless 起動が通ることまで確認する。

生成物は**動く最小構成**(カウンタを進めるだけのプレースホルダ)。
ここにあるテンプレートが正典であり、既存サンプル(SuikaGame / RogueGame)は
変更されうるので写経元にしない。境界の掟・配線の背景は docs/USING.md を参照。

## 手順

1. ゲーム名 `<Name>`(PascalCase)を引数から取る。無ければ聞く。
   `game/<Name>.Logic` 等が既に存在したら中断して報告する
2. 下のテンプレートどおりにファイルを作る(`<Name>` を置換。
   `<name>` は小文字化した State パス用)
3. 専用ソリューション `game/<Name>.slnx` を作る(テンプレート参照)
4. `dotnet build game/<Name>.slnx` と `dotnet test game/<Name>.slnx --no-build` が緑になること
5. `<godot> --headless --path game/<Name>.Godot --import` を実行
   (完了後にクラッシュするので exit code は無視。D-016)
6. headless 起動(バックグラウンド)→ `ping` → `state --path game/<name>` →
   `send --command step` で StepCount が増える → `quit`(exit 0)を確認して報告する
7. 以降の進め方(GUIDELINE の4段階、プレースホルダの置き換え)を案内する

## テンプレート

### game/<Name>.slnx

デバッグでフレームワーク側へステップインできるよう、参照している `src/` も
ビューとして含める。

```xml
<Solution>
  <Folder Name="/game/">
    <Project Path="<Name>.Godot/<Name>.Godot.csproj">
      <BuildType Project="Debug" />
    </Project>
    <Project Path="<Name>.Logic/<Name>.Logic.csproj" />
  </Folder>
  <Folder Name="/tests/">
    <Project Path="../tests/<Name>.Logic.Tests/<Name>.Logic.Tests.csproj" />
  </Folder>
  <Folder Name="/src/">
    <Project Path="../src/Statee.Core/Statee.Core.csproj" />
    <Project Path="../src/Statee.Generator/Statee.Generator.csproj" />
    <Project Path="../src/Statee.Remote/Statee.Remote.csproj" />
  </Folder>
</Solution>
```

### game/<Name>.Logic/<Name>.Logic.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace><Name>.Logic</RootNamespace>
  </PropertyGroup>

</Project>
```

### game/<Name>.Logic/GameLogic.cs

```csharp
namespace <Name>.Logic;

/// <summary>
/// ゲームの中核状態機械のプレースホルダ。ルール・状態遷移はすべてこの層に置き、
/// Godot 層には持ち込まない(docs/USING.md「境界の掟」)。
/// 乱数を使う場合はこのシードだけを源にし、決定論を保つ。
/// </summary>
public sealed class GameLogic(int seed)
{
    /// <summary>生成に使ったシード。State で公開して再現性を検証できるようにする。</summary>
    public int Seed { get; } = seed;

    /// <summary>進めたターン数(プレースホルダ)。</summary>
    public int StepCount { get; private set; }

    /// <summary>1ターン進める(プレースホルダ)。実ゲームのアクションに置き換える。</summary>
    public void Step()
    {
        StepCount++;
    }
}
```

### tests/<Name>.Logic.Tests/<Name>.Logic.Tests.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.4" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="Shouldly" Version="4.3.0" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\game\<Name>.Logic\<Name>.Logic.csproj" />
  </ItemGroup>

</Project>
```

### tests/<Name>.Logic.Tests/GameLogicTest.cs

```csharp
using Shouldly;

namespace <Name>.Logic.Tests;

public class GameLogicTest
{
    [Fact]
    public void Step_1回進める_StepCountが1になる()
    {
        var game = new GameLogic(seed: 1);

        game.Step();

        game.StepCount.ShouldBe(1);
    }
}
```

### game/<Name>.Godot/<Name>.Godot.csproj

```xml
<Project Sdk="Godot.NET.Sdk/4.7.0">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <RootNamespace><Name></RootNamespace>
    <Nullable>enable</Nullable>
    <!-- Godot は .godot/mono/temp/bin から実行するため、NuGet 依存 DLL を出力へコピーする必要がある -->
    <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="ZLogger" Version="2.5.10" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\<Name>.Logic\<Name>.Logic.csproj" />
    <ProjectReference Include="..\..\src\Statee.Core\Statee.Core.csproj" />
    <ProjectReference
      Include="..\..\src\Statee.Generator\Statee.Generator.csproj"
      OutputItemType="Analyzer"
      ReferenceOutputAssembly="false" />
    <ProjectReference Include="..\..\src\Statee.Remote\Statee.Remote.csproj" />
  </ItemGroup>
</Project>
```

### game/<Name>.Godot/project.godot

```ini
config_version=5

[application]

config/name="<Name>"
run/main_scene="res://main.tscn"
config/features=PackedStringArray("4.7", "C#", "Forward Plus")

[display]

window/size/viewport_width=960
window/size/viewport_height=540

[dotnet]

project/assembly_name="<Name>.Godot"
```

### game/<Name>.Godot/main.tscn

```
[gd_scene load_steps=2 format=3]

[ext_resource type="Script" path="res://Main.cs" id="1"]

[node name="Main" type="Node2D"]
script = ExtResource("1")
```

### game/<Name>.Godot/GameState.cs

```csharp
using Statee.Core;

namespace <Name>;

/// <summary>
/// ゲーム状態の State 公開。CaptureState はソケットスレッドで走るため、
/// メインスレッドが差し替える不変スナップショットを読むだけにする(docs/USING.md)。
/// 実ゲームではフィールドを増やし、検証に必要な情報を全公開する
/// (画面上の演出で隠すものも State では隠さない)。
/// </summary>
[StateeState("game/<name>")]
public partial class GameState
{
    private sealed record Snapshot(int Seed, int StepCount);

    private volatile Snapshot _current = new(0, 0);

    [StateeField]
    public int Seed => _current.Seed;

    [StateeField]
    public int StepCount => _current.StepCount;

    /// <summary>メインスレッドから呼ぶ。スナップショットを不可分に差し替える。</summary>
    public void Update(int seed, int stepCount)
    {
        _current = new Snapshot(seed, stepCount);
    }
}
```

### game/<Name>.Godot/Main.cs

using はアルファベット順(ゲーム名により正しい位置が変わる。hooks のフォーマッタが
自動修正するので厳密でなくてよい)。

```csharp
using System;
using System.IO;
using <Name>.Logic;
using Godot;
using Microsoft.Extensions.Logging;
using Statee.Core;
using Statee.Remote;
using ZLogger;

namespace <Name>;

/// <summary>
/// <Name> の Godot 層エントリポイント。描画・入力・Statee 配線だけを担い、
/// ゲームルールはすべて <Name>.Logic に置く(docs/USING.md「境界の掟」)。
/// </summary>
public partial class Main : Node2D
{
    private const int DefaultPort = 9310;
    private const int DefaultSeed = 12345;

    private readonly MainThreadDispatcher _dispatcher = new();
    private readonly TimeControl _time = new();
    private readonly GameState _state = new();

    private GameLogic _logic = null!;
    private StateeTcpServer? _server;
    private ILoggerFactory? _loggerFactory;
    private ILogger _logger = null!;

    /// <summary>キー入力 → アクションの配線1件。この表が _UnhandledInput の処理と
    /// game/input State の両方の情報源になる(実装と公開情報を乖離させない)。</summary>
    private sealed record KeyBinding(Key Key, string Publishes, string Explain, Action Publish);

    private KeyBinding[] _keyBindings = [];

    public override void _Ready()
    {
        // freeze 中も Statee のコマンド処理(Pump)を動かし続ける
        ProcessMode = ProcessModeEnum.Always;

        var buffer = new LogBuffer(1024);
        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddProvider(new BufferLoggerProvider(buffer));
            builder.AddZLoggerConsole();
        });
        _logger = _loggerFactory.CreateLogger<Main>();

        _logic = new GameLogic(ParseIntArg("--seed=", DefaultSeed));
        _keyBindings =
        [
            new KeyBinding(Key.Space, "step", "1ターン進める(プレースホルダ)", ActStep),
        ];

        RefreshView();
        StartStatee(buffer);
        _logger.ZLogInformation($"<Name> 起動 seed={_logic.Seed}");
    }

    public override void _Process(double delta)
    {
        _dispatcher.Pump();
        if (!_time.IsFrozen)
        {
            _time.OnFrame();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } keyEvent)
        {
            return;
        }
        foreach (var binding in _keyBindings)
        {
            if (binding.Key == keyEvent.Keycode)
            {
                binding.Publish();
                return;
            }
        }
    }

    public override void _Draw()
    {
        // プレースホルダ描画。実ゲームの描画に置き換える
        DrawString(
            ThemeDB.FallbackFont,
            new Vector2(16, 32),
            $"<Name>  seed={_logic.Seed}  step={_logic.StepCount}",
            fontSize: 20
        );
    }

    public override void _ExitTree()
    {
        _server?.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(2));
        _loggerFactory?.Dispose();
    }

    /// <summary>プレイヤーのアクション(プレースホルダ)。</summary>
    private void ActStep()
    {
        _logic.Step();
        RefreshView();
    }

    /// <summary>アクション後の状態を State と描画へ反映する。</summary>
    private void RefreshView()
    {
        _state.Update(_logic.Seed, _logic.StepCount);
        QueueRedraw();
    }

    private void StartStatee(LogBuffer buffer)
    {
        var host = new StateeHost(buffer) { MainThreadDispatcher = _dispatcher };
        host.RegisterStateProvider(_state);
        // キーバインドの State 公開。配線表から導出するため実装と乖離しない
        var keyEntries = Array.ConvertAll(
            _keyBindings,
            binding => new
            {
                Key = binding.Key.ToString(),
                Publishes = binding.Publishes,
                Explain = binding.Explain,
            }
        );
        host.RegisterStateProvider(
            new SnapshotStateProvider("game/input", () => new { Keys = keyEntries })
        );
        host.RegisterTimeControl(_time);
        // ping は組み込みではない。疎通確認の起点なので必ず登録する
        host.RegisterCommand(
            "ping",
            args =>
            {
                var message = args.GetString("message") ?? "ping";
                _logger.ZLogInformation($"ping を受信: {message}");
                return new { Pong = true, Message = message };
            }
        );
        // ゲーム状態を変えるコマンドはメインスレッドで実行する
        host.RegisterMainThreadCommand(
            "step",
            _ =>
            {
                ActStep();
                _logger.ZLogInformation($"step → {_logic.StepCount}");
                return new { StepCount = _logic.StepCount };
            }
        );
        host.RegisterMainThreadCommand(
            "key",
            args =>
            {
                var name =
                    args.GetString("key")
                    ?? throw new InvalidOperationException("key を指定すること(例: space)");
                var key = Enum.Parse<Key>(name, ignoreCase: true);
                var viewport = GetViewport();
                viewport.PushInput(new InputEventKey { Keycode = key, Pressed = true });
                viewport.PushInput(new InputEventKey { Keycode = key, Pressed = false });
                _logger.ZLogInformation($"key {key}");
                return new { Key = key.ToString() };
            }
        );
        host.RegisterMainThreadCommand(
            "screenshot",
            args =>
            {
                var path =
                    args.GetString("path")
                    ?? throw new InvalidOperationException("path を指定すること(絶対パス)");
                var image =
                    GetViewport().GetTexture()?.GetImage()
                    ?? throw new InvalidOperationException(
                        "描画が無いため撮影できない(headless では screenshot は使えない)"
                    );
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
                var error = image.SavePng(path);
                if (error != Error.Ok)
                {
                    throw new InvalidOperationException($"スクリーンショット保存失敗: {error}");
                }
                _logger.ZLogInformation($"screenshot path={path}");
                return new { Path = Path.GetFullPath(path) };
            }
        );
        host.RegisterMainThreadCommand(
            "quit",
            _ =>
            {
                _logger.ZLogInformation($"quit を受信。終了する");
                GetTree().Quit();
                return new { Quitting = true };
            }
        );
        _server = new StateeTcpServer(host, ParseIntArg("--port=", DefaultPort));
        _server.Start();
        _logger.ZLogInformation($"Statee 待ち受け開始 port={_server.Port}");
    }

    /// <summary>デリゲートでスナップショットを返す State プロバイダ。ソケットスレッドから呼ばれる。</summary>
    private sealed class SnapshotStateProvider(string path, Func<object> capture) : IStateProvider
    {
        public string Path => path;

        public object CaptureState() => capture();
    }

    private static int ParseIntArg(string prefix, int defaultValue)
    {
        foreach (var arg in OS.GetCmdlineUserArgs())
        {
            if (
                arg.StartsWith(prefix, StringComparison.Ordinal)
                && int.TryParse(arg[prefix.Length..], out var value)
            )
            {
                return value;
            }
        }

        return defaultValue;
    }
}
```

### game/<Name>.Godot/CLAUDE.md

```markdown
# <Name>.Godot 開発指針

- **ゲームルールをここに書かない**。ルール・状態遷移は <Name>.Logic の仕事。
  ここは描画・入力・Statee 配線だけ(in: ロジックのメソッド、out: プロパティ読み出し)
- 検証用 State(game/<name>)には検証に必要な情報を全公開する。
  画面上の演出で隠すものも State では隠さない
- Godot.NET.Sdk は ImplicitUsings 無効。System 系 using を明示する。
  NuGet 依存には CopyLocalLockFileAssemblies が必要
```

## 生成後の案内

- プレースホルダ(GameLogic.Step / step コマンド / Space キー)を実ゲームの
  アクションに置き換えていく。進め方は docs/GUIDELINE.md の4段階
  (スケルトン → 失敗するテスト → 実装 → リファクタ。各段階でコミット)
- 動作確認は `/verify --path game/<Name>.Godot`
- 決定論設計にするなら、全アクションをロジック層で記録(ActionLog)して State 公開すると
  リプレイ検証(記録 → 同一シード再起動 → 再生 → State 一致)まで到達できる
