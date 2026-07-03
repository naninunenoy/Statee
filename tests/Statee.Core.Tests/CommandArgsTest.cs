using Shouldly;

namespace Statee.Core.Tests;

public class CommandArgsTest
{
    [Fact]
    public void GetString_存在するキー_値を返す()
    {
        var args = new CommandArgs(new Dictionary<string, string> { ["message"] = "hello" });

        args.GetString("message").ShouldBe("hello");
    }

    [Fact]
    public void GetString_存在しないキー_nullを返す()
    {
        var args = new CommandArgs(new Dictionary<string, string> { ["message"] = "hello" });

        args.GetString("missing").ShouldBeNull();
    }

    [Fact]
    public void GetString_引数辞書がnull_nullを返す()
    {
        var args = new CommandArgs(null);

        args.GetString("message").ShouldBeNull();
    }

    [Fact]
    public void GetInt_数値の文字列_その数値を返す()
    {
        var args = new CommandArgs(new Dictionary<string, string> { ["tail"] = "42" });

        args.GetInt("tail", 7).ShouldBe(42);
    }

    [Fact]
    public void GetInt_存在しないキー_既定値を返す()
    {
        var args = new CommandArgs(new Dictionary<string, string>());

        args.GetInt("tail", 7).ShouldBe(7);
    }

    [Fact]
    public void GetInt_数値でない文字列_FormatExceptionを投げる()
    {
        var args = new CommandArgs(new Dictionary<string, string> { ["tail"] = "abc" });

        Should.Throw<FormatException>(() => args.GetInt("tail", 7));
    }
}
