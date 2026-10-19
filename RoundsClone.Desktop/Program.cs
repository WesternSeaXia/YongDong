using System;

namespace RoundsClone.Desktop;

/// <summary>
/// 桌面调试端启动入口点。
/// </summary>
public static class Program
{
    public static void Main(string[] args)
    {
        if (args != null && System.Linq.Enumerable.Contains(args, "--export-audio"))
        {
            string outDir = "Audio";
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--export-audio")
                {
                    outDir = args[i + 1];
                    break;
                }
            }
            RoundsClone.Core.Audio.ProceduralSoundGenerator.ExportAllWavs(outDir);
            Console.WriteLine($"[Audio] Exported all procedural WAV files to: {System.IO.Path.GetFullPath(outDir)}");
            return;
        }

        bool testFlow = args != null && System.Linq.Enumerable.Contains(args, "--test-flow");
        using var game = new RoundsClone.Core.RoundsGame(testFlow);
        game.Run();
    }
}
