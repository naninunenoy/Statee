using System.Text;
using ChibiRuby;
using ChibiRuby.Compiler;

namespace Statee.Scenario;

/// <summary>
/// Ruby で書かれた動作確認シナリオを実行する(ChibiRuby 埋め込み)。
/// Ruby へ公開する語彙は send / state / wait / assert + expect の5つに、
/// マルチインスタンス検証(D-051)向けの target / on / wait_all を加えたもの。
/// - send(command, *"key=value")  … 任意コマンド。payload(TOON)文字列を返す
/// - state(path)                  … State 取得の糖衣
/// - wait(path, field, op, value, timeout_ms = nil) … 条件待機(D-028)の糖衣
/// - assert(condition, message = nil) … 偽ならシナリオ失敗
/// - expect(description)          … レポート用の期待説明。ワイヤには何も送らない(D-034)
/// - target(name, port: N)        … 名前付きターゲットへ接続する(D-051 複数ターゲット接続)
/// - on(name) { ... }             … ブロック内の send/state/wait の宛先を name に切り替える(宛先指定)
/// - wait_all([name, ...], path, field, op, value, timeout_ms = nil)
///                                 … 列挙した全ターゲットが同じ条件を満たすまで順に待つ
///                                   (クロスインスタンス wait。D-051)
/// コマンドのエラー応答は Ruby の例外になる(rescue で異常系シナリオも書ける)。
/// </summary>
public sealed class ScenarioRunner(
    IScenarioClient client,
    TextWriter output,
    IStepRecorder? recorder = null,
    Func<int, IScenarioClient>? connect = null
)
{
    private readonly Dictionary<string, IScenarioClient> _targets = [];
    private readonly Stack<string> _targetStack = new();

    /// <summary>シナリオを実行する。成功なら 0、失敗(assert 失敗・コマンドエラー・構文エラー)なら 1。</summary>
    public int Run(string rubySource)
    {
        using var mrb = MRubyState.Create();
        DefineVocabulary(mrb);
        try
        {
            using var compiler = MRubyCompiler.Create(mrb);
            compiler.LoadSourceCode(Encoding.UTF8.GetBytes(rubySource));
            return 0;
        }
        catch (Exception e) when (e is MRubyRaiseException or MRubyCompileException)
        {
            output.WriteLine($"シナリオ失敗: {e.Message}");
            return 1;
        }
    }

    private void DefineVocabulary(MRubyState mrb)
    {
        mrb.DefineMethod(
            mrb.ObjectClass,
            mrb.Intern("send_command"u8),
            (s, _) =>
            {
                var command = ArgumentToString(s, 0);
                var rest = s.GetRestArgumentsAfter(1);
                Dictionary<string, string>? args = null;
                foreach (var pair in rest)
                {
                    var text = ValueToString(s, pair);
                    var separator = text.IndexOf('=');
                    if (separator <= 0)
                    {
                        s.Raise(
                            s.StandardErrorClass,
                            s.NewString($"引数は key=value 形式で指定する: {text}")
                        );
                    }

                    args ??= [];
                    args[text[..separator]] = text[(separator + 1)..];
                }

                return InvokeOrRaise(s, CurrentClient(s), command, args);
            }
        );

        mrb.DefineMethod(
            mrb.ObjectClass,
            mrb.Intern("state"u8),
            (s, _) =>
                InvokeOrRaise(
                    s,
                    CurrentClient(s),
                    "state",
                    new Dictionary<string, string> { ["path"] = ArgumentToString(s, 0) }
                )
        );

        mrb.DefineMethod(
            mrb.ObjectClass,
            mrb.Intern("wait"u8),
            (s, _) => InvokeOrRaise(s, CurrentClient(s), "wait", WaitArgs(s, argOffset: 0))
        );

        mrb.DefineMethod(
            mrb.ObjectClass,
            mrb.Intern("assert"u8),
            (s, _) =>
            {
                var condition = s.GetArgumentAt(0);
                if (condition.Falsy)
                {
                    var message =
                        s.GetArgumentCount() >= 2
                            ? ValueToString(s, s.GetArgumentAt(1))
                            : "assert が偽";
                    s.Raise(s.StandardErrorClass, s.NewString(message));
                }

                return MRubyValue.Nil;
            }
        );

        mrb.DefineMethod(
            mrb.ObjectClass,
            mrb.Intern("expect"u8),
            (s, _) =>
            {
                recorder?.BeginExpectation(ArgumentToString(s, 0));
                return MRubyValue.Nil;
            }
        );

        mrb.DefineMethod(
            mrb.ObjectClass,
            mrb.Intern("target"u8),
            (s, _) =>
            {
                if (connect is null)
                {
                    s.Raise(
                        s.StandardErrorClass,
                        s.NewString("target は接続ファクトリが未設定のため使えない")
                    );
                }
                var name = ArgumentToString(s, 0);
                var port = (int)s.GetKeywordArgument(s.Intern("port"u8)).IntegerValue;
                _targets[name] = connect!(port);
                return MRubyValue.Nil;
            }
        );

        mrb.DefineMethod(
            mrb.ObjectClass,
            mrb.Intern("on"u8),
            (s, _) =>
            {
                var name = ArgumentToString(s, 0);
                var block = s.GetBlockArgument();
                if (block is null)
                {
                    s.Raise(s.StandardErrorClass, s.NewString("on にはブロックが必要"));
                }
                _targetStack.Push(name);
                try
                {
                    return s.Send(block, s.Intern("call"u8));
                }
                finally
                {
                    _targetStack.Pop();
                }
            }
        );

        mrb.DefineMethod(
            mrb.ObjectClass,
            mrb.Intern("wait_all"u8),
            (s, _) =>
            {
                var names = s.GetArgumentAt(0).As<RArray>();
                var args = WaitArgs(s, argOffset: 1);
                foreach (var nameValue in names)
                {
                    InvokeOrRaise(s, ResolveClient(s, ValueToString(s, nameValue)), "wait", args);
                }
                return MRubyValue.Nil;
            }
        );

        // Ruby の予約語彙 Kernel#send(メソッド動的呼び出し)を上書きせず、
        // シナリオでは send をコマンド送信として使えるようにエイリアスする
        using var compiler = MRubyCompiler.Create(mrb);
        compiler.LoadSourceCode("alias send send_command"u8.ToArray());
    }

    /// <summary>wait/wait_all 共通の引数(path, field, op, value, timeout_ms?)を組み立てる。</summary>
    private static Dictionary<string, string> WaitArgs(MRubyState mrb, int argOffset)
    {
        var args = new Dictionary<string, string>
        {
            ["path"] = ArgumentToString(mrb, argOffset),
            ["field"] = ArgumentToString(mrb, argOffset + 1),
            ["op"] = ArgumentToString(mrb, argOffset + 2),
            ["value"] = ArgumentToString(mrb, argOffset + 3),
        };
        if (mrb.GetArgumentCount() > argOffset + 4)
        {
            args["timeoutMs"] = ArgumentToString(mrb, argOffset + 4);
        }
        return args;
    }

    /// <summary>on() のスタックが空でなければその宛先、無ければ既定のターゲット。</summary>
    private IScenarioClient CurrentClient(MRubyState mrb) =>
        _targetStack.Count > 0 ? ResolveClient(mrb, _targetStack.Peek()) : client;

    private IScenarioClient ResolveClient(MRubyState mrb, string name)
    {
        if (_targets.TryGetValue(name, out var target))
        {
            return target;
        }
        mrb.Raise(mrb.StandardErrorClass, mrb.NewString($"未知のターゲット: {name}"));
        throw new InvalidOperationException($"未知のターゲット: {name}"); // Raise が例外を投げるため到達しない
    }

    private MRubyValue InvokeOrRaise(
        MRubyState mrb,
        IScenarioClient target,
        string command,
        Dictionary<string, string>? args
    )
    {
        try
        {
            return mrb.NewString(target.Invoke(command, args));
        }
        catch (Exception e) when (e is not MRubyRaiseException)
        {
            mrb.Raise(mrb.StandardErrorClass, mrb.NewString(e.Message));
            return MRubyValue.Nil; // Raise が例外を投げるため到達しない
        }
    }

    private static string ArgumentToString(MRubyState mrb, int index) =>
        ValueToString(mrb, mrb.GetArgumentAt(index));

    private static string ValueToString(MRubyState mrb, MRubyValue value) =>
        Encoding.UTF8.GetString(mrb.Stringify(value).AsSpan());
}
