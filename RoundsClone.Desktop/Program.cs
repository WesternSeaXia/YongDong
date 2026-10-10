using System;

namespace RoundsClone.Desktop;

/// <summary>
/// 桌面调试端启动入口点。
/// </summary>
public static class Program
{
    public static void Main(string[] args)
    {
        bool testFlow = args != null && System.Linq.Enumerable.Contains(args, "--test-flow");
        using var game = new RoundsClone.Core.RoundsGame(testFlow);
        game.Run();
    }
}
