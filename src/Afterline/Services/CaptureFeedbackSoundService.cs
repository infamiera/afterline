using System.Windows.Media;

namespace Afterline.Services;

/// <summary>
/// Plays a short, locally-generated confirmation tone. The waveform is cached in
/// LocalAppData, so feedback never adds work to the image capture path or retains
/// capture data in memory.
/// </summary>
public static class CaptureFeedbackSoundService
{
    private static readonly object Gate = new();
    private static readonly MediaPlayer Player = new();

    public static void Play(string? choice, int volumePercent)
    {
        if (string.Equals(choice, "Off", StringComparison.OrdinalIgnoreCase) || volumePercent <= 0)
            return;

        try
        {
            string sound = choice is "Chime" or "Soft" ? choice : "Shutter";
            string path = EnsureWaveFile(sound);
            lock (Gate)
            {
                Player.Stop();
                Player.Open(new Uri(path, UriKind.Absolute));
                Player.Volume = Math.Clamp(volumePercent, 0, 100) / 100d;
                Player.Play();
            }
        }
        catch
        {
            // Feedback is optional and must never affect a successfully saved capture.
        }
    }

    private static string EnsureWaveFile(string sound)
    {
        string directory = Path.Combine(AppPaths.LocalDataRoot, "Audio");
        string path = Path.Combine(directory, $"capture-{sound.ToLowerInvariant()}.wav");
        if (File.Exists(path)) return path;

        Directory.CreateDirectory(directory);
        lock (Gate)
        {
            if (File.Exists(path)) return path;
            using var stream = File.Create(path);
            using var writer = new BinaryWriter(stream);
            const int sampleRate = 44100;
            short[] samples = BuildSamples(sound, sampleRate);
            writer.Write("RIFF".ToCharArray());
            writer.Write(36 + samples.Length * sizeof(short));
            writer.Write("WAVEfmt ".ToCharArray());
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(sampleRate);
            writer.Write(sampleRate * sizeof(short));
            writer.Write((short)sizeof(short));
            writer.Write((short)16);
            writer.Write("data".ToCharArray());
            writer.Write(samples.Length * sizeof(short));
            foreach (short sample in samples) writer.Write(sample);
        }
        return path;
    }

    private static short[] BuildSamples(string sound, int rate)
    {
        double seconds = sound == "Chime" ? .22 : .13;
        var samples = new short[(int)(rate * seconds)];
        for (int i = 0; i < samples.Length; i++)
        {
            double t = i / (double)rate;
            double frequency = sound switch { "Chime" => t < .11 ? 880 : 1320, "Soft" => 620, _ => t < .035 ? 1550 : 920 };
            double envelope = Math.Max(0, 1 - t / seconds);
            double amplitude = sound == "Soft" ? .13 : .20;
            samples[i] = (short)(Math.Sin(2 * Math.PI * frequency * t) * short.MaxValue * amplitude * envelope);
        }
        return samples;
    }
}
