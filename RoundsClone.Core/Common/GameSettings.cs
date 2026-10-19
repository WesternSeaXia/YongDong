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
    /// 全局主音量 (0.0f ~ 1.0f)。
    /// </summary>
    public float MasterVolume { get; set; } = 0.85f;

    /// <summary>
    /// 背景音乐音量 (0.0f ~ 1.0f)。
    /// </summary>
    public float BgmVolume { get; set; } = 0.55f;

    /// <summary>
    /// 战斗音效音量 (0.0f ~ 1.0f)。
    /// </summary>
    public float SfxVolume { get; set; } = 0.85f;

    /// <summary>
    /// 背景音乐播放模式 (0: 顺序循环, 1: 随机播放, 2: 单曲循环)。
    /// </summary>
    public int BgmPlayMode { get; set; } = 0;

    /// <summary>
    /// 上次选中的背景音乐曲目索引。
    /// </summary>
    public int SelectedBgmIndex { get; set; } = 0;

    /// <summary>
    /// AI 难度级别 (0: 简单, 1: 普通, 2: 地狱)。
    /// </summary>
    public int AiDifficulty { get; set; } = 1;

    /// <summary>
    /// 键位控制方案预设 (0: WASD + 空格跳跃, 1: 方向键 + 上跳跃, 2: WASD + W跳跃)。
    /// </summary>
    public int KeybindPreset { get; set; } = 0;

    /// <summary>
    /// 获取平台兼容的本地设置存储文件路径。
    /// 采用全英文字符目录名 ChaosGunner，防止跨平台/跨语言系统编码错误，并自动兼容迁移历史旧目录。
    /// </summary>
    private static string GetConfigFilePath()
    {
        string baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string appDir = Path.Combine(baseDir, "ChaosGunner");
        string chineseDir = Path.Combine(baseDir, "混乱枪豆人");
        string legacyDir = Path.Combine(baseDir, "RoundsClone");

        if (!Directory.Exists(appDir))
        {
            if (Directory.Exists(chineseDir))
            {
                try
                {
                    Directory.Move(chineseDir, appDir);
                }
                catch
                {
                    // 若重命名失败则保留原样
                }
            }
            else if (Directory.Exists(legacyDir))
            {
                try
                {
                    Directory.Move(legacyDir, appDir);
                }
                catch
                {
                    // 若重命名失败则保留原样
                }
            }

            if (!Directory.Exists(appDir))
            {
                Directory.CreateDirectory(appDir);
            }
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
