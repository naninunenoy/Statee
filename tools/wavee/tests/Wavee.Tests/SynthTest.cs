using Shouldly;
using Wavee;

namespace Wavee.Tests;

public class SynthTest
{
    private static SfxDocument Doc(
        WaveForm wave = WaveForm.Sine,
        float duration = 0.1f,
        float attack = 0f,
        float decay = 0f,
        int seed = 1
    ) =>
        new()
        {
            Wave = wave,
            Duration = duration,
            Attack = attack,
            Decay = decay,
            Seed = seed,
            SampleRate = 44100,
        };

    [Fact]
    public void Render_サンプル数はdurationとsampleRateの積になる()
    {
        Synth.Render(Doc(duration: 0.5f)).Length.ShouldBe(22050);
    }

    [Fact]
    public void Render_全サンプルがマイナス1から1に収まる()
    {
        foreach (var wave in Enum.GetValues<WaveForm>())
        {
            var samples = Synth.Render(Doc(wave));
            samples.ShouldAllBe(s => s >= -1f && s <= 1f);
        }
    }

    [Fact]
    public void Render_同じ定義からは同じ波形が出る_決定論()
    {
        Synth
            .Render(Doc(WaveForm.Noise, seed: 42))
            .ShouldBe(Synth.Render(Doc(WaveForm.Noise, seed: 42)));
    }

    [Fact]
    public void Render_seedが違えばnoiseの波形が変わる()
    {
        Synth
            .Render(Doc(WaveForm.Noise, seed: 1))
            .ShouldNotBe(Synth.Render(Doc(WaveForm.Noise, seed: 2)));
    }

    [Fact]
    public void Render_attack中は音量が立ち上がり途中になる()
    {
        // 矩形波は生値 ±1 なので、attack 半ばのサンプルは volume(既定 0.5)未満になる
        var samples = Synth.Render(Doc(WaveForm.Square, attack: 0.05f));

        Math.Abs(samples[100]).ShouldBeLessThan(0.5f);
    }

    [Fact]
    public void Render_decay終端で音量がほぼゼロになる()
    {
        var samples = Synth.Render(Doc(WaveForm.Square, decay: 0.05f));

        Math.Abs(samples[^1]).ShouldBeLessThan(0.01f);
    }
}
