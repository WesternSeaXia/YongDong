using System;
using System.IO;
using System.Text.Json;

namespace RoundsClone.Core.Common;

/// <summary>
/// 游戏全局配置持久化模型。
/// 职责边界：管理跨平台游戏设置（虚拟触控显示、左摇杆跳跃、左摇杆瞄准、死区比例、动态摄像机等）在本地磁盘的序列化读取与存储。
/// </summary>
public class GameSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>
    /// 全局活跃设置单例。
    /// </summary>
    public static GameSettings Current { get; private set; } = new();

    /// <summary>
    /// 是否显示并启用屏幕虚拟触控控件。
    /// </summary>
    public bool ShowVirtualControls { get; set; } = true;

    /// <summary>
    /// 左侧虚拟摇杆上推是否触发跳跃。
    /// </summary>
    public bool LeftStickJumpEnabled { get; set; } = false;

    /// <summary>
    /// 左侧虚拟摇杆是否同时控制瞄准方向（开启后右侧取消摇杆，改为独立开火/技能/跳跃键）。
    /// </summary>
    public bool LeftStickControlsAim { get; set; } = false;

    /// <summary>
    /// 虚拟摇杆中心死区半径占总半径的比例（默认 0.18 即 18%）。
    /// </summary>
    public float StickDeadzoneRatio { get; set; } = 0.18f;

    /// <summary>
    /// 是否开启双人对战自动追踪平滑动态摄像机。
    /// </summary>
    public bool DynamicCameraEnabled { get; set; } = true;

    /// <summary>
    /// 获取平台兼容的本地设置存储文件路径。
    /// </summary>
    private static string GetConfigFilePath()
    {
        string baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string appDir = Path.Combine(baseDir, "RoundsClone");
        if (!Directory.Exists(appDir))
        {
            Directory.CreateDirectory(appDir);
        }
        return Path.Combine(appDir, "settings.json");
    }

    /// <summary>
    /// 从本地磁盘加载持久化配置；若文件不存在或读取损坏则重置为默认值。
    /// </summary>
    public static void Load()
    {
        try
        {
            string path = GetConfigFilePath();
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                var loaded = JsonSerializer.Deserialize<GameSettings>(json);
                if (loaded != null)
                {
                    Current = loaded;
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GameSettings] 加载设置配置失败，使用默认配置: {ex.Message}");
        }

        Current = new GameSettings();
        Save();
    }

    /// <summary>
    /// 将当前设置即时保存到本地磁盘。
    /// </summary>
    public static void Save()
    {
        try
        {
            string path = GetConfigFilePath();
            string json = JsonSerializer.Serialize(Current, JsonOptions);
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GameSettings] 保存设置配置失败: {ex.Message}");
        }
    }
}
