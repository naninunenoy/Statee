using System.Text;
using ChibiRuby;
using ChibiRuby.Compiler;

namespace Statee.Scenario;

/// <summary>
/// Ruby で書かれた動作確認シナリオを実行する(ChibiRuby 埋め込み)。
/// Ruby へ公開する語彙は send / state / wait / assert + expect の5つだけに保つ(D-034)。
/// - send(command, *"key=value")  … 任意コマンド。payload(TOON)文字列を返す
/// - state(path)                  … State 取得の糖衣
/// - wait(path, field, op, value, timeout_ms = nil) … 条件待機(D-028)の糖衣
/// - assert(condition, message = nil) … 偽ならシナリオ失敗
/// - expect(description)          … レポート用の期待説明。ワイヤには何も送らない(D-034)
/// コマンドのエラー応答は Ruby の例外になる(rescue で異常系シナリオも書ける)。
/// </summary>
public sealed class ScenarioRunner(
    IScenarioClient client,
    TextWriter output,
    IStepRecorder? recorder = null
)
{
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

                return InvokeOrRaise(s, command, args);
            }
        );

        mrb.DefineMethod(
            mrb.ObjectClass,
            mrb.Intern("state"u8),
            (s, _) =>
                InvokeOrRaise(
                    s,
                    "state",
                    new Dictionary<string, string> { ["path"] = ArgumentToString(s, 0) }
                )
        );

        mrb.DefineMethod(
            mrb.ObjectClass,
            mrb.Intern("wait"u8),
            (s, _) =>
            {
                var args = new Dictionary<string, string>
                {
                    ["path"] = ArgumentToString(s, 0),
                    ["field"] = ArgumentToString(s, 1),
                    ["op"] = ArgumentToString(s, 2),
                    ["value"] = ArgumentToString(s, 3),
                };
                if (s.GetArgumentCount() >= 5)
                {
                    args["timeoutMs"] = ArgumentToString(s, 4);
                }

                return InvokeOrRaise(s, "wait", args);
            }
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

        // Ruby の予約語彙 Kernel#send(メソッド動的呼び出し)を上書きせず、
        // シナリオでは send をコマンド送信として使えるようにエイリアスする
        using var compiler = MRubyCompiler.Create(mrb);
        compiler.LoadSourceCode("alias send send_command"u8.ToArray());
    }

    private MRubyValue InvokeOrRaise(
        MRubyState mrb,
        string command,
        Dictionary<string, string>? args
    )
    {
        try
        {
            return mrb.NewString(client.Invoke(command, args));
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
