using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Media;
using RoundsClone.Core.Common;

namespace RoundsClone.Core.Audio;

/// <summary>
/// 背景音乐播放循环模式。
/// </summary>
public enum BgmPlayMode
{
    /// <summary>
    /// 顺序循环播放全部曲目。
    /// </summary>
    Sequential = 0,

    /// <summary>
    /// 随机播放曲库中的下一首曲目。
    /// </summary>
    Random = 1,

    /// <summary>
    /// 单曲循环播放当前曲目。
    /// </summary>
    Single = 2
}

/// <summary>
/// 全局音频与背景音乐管理器 (Audio Manager)。
/// 统一管理游戏内所有程序化音效合成、背景音乐轮流播放、三维通道混音与音量分级控制（主音量、BGM、SFX）。
/// 完全兼容无音频输出设备或测试环境，遇到音频初始化异常自动静音降级，保障游戏绝对稳定运行。
/// </summary>
public class AudioManager : IDisposable
{
    private readonly Dictionary<SoundType, SoundEffect> _soundCache = new();
    private readonly List<Song> _bgmTracks = new();
    private readonly List<string> _bgmDisplayNames = new();
    private readonly Random _rand = new();

    private bool _initialized = false;
    private bool _audioDeviceAvailable = true;
    private bool _bgmActive = false;
    private int _currentBgmIndex = 0;

    private float _masterVolume = 0.85f;
    private float _bgmVolume = 0.55f;
    private float _sfxVolume = 0.85f;
    private bool _isMuted = false;

    /// <summary>
    /// 全局主音量 (0.0f ~ 1.0f)。
    /// </summary>
    public float MasterVolume
    {
        get => _masterVolume;
        set
        {
            _masterVolume = MathHelper.Clamp(value, 0f, 1f);
            ApplyBgmVolume();
            GameSettings.Current.MasterVolume = _masterVolume;
        }
    }

    /// <summary>
    /// 背景音乐音量 (0.0f ~ 1.0f)。
    /// </summary>
    public float BgmVolume
    {
        get => _bgmVolume;
        set
        {
            _bgmVolume = MathHelper.Clamp(value, 0f, 1f);
            ApplyBgmVolume();
            GameSettings.Current.BgmVolume = _bgmVolume;
        }
    }

    /// <summary>
    /// 游戏音效音量 (0.0f ~ 1.0f)。
    /// </summary>
    public float SfxVolume
    {
        get => _sfxVolume;
        set
        {
            _sfxVolume = MathHelper.Clamp(value, 0f, 1f);
            GameSettings.Current.SfxVolume = _sfxVolume;
        }
    }

    /// <summary>
    /// 是否全局静音。
    /// </summary>
    public bool IsMuted
    {
        get => _isMuted;
        set
        {
            _isMuted = value;
            ApplyBgmVolume();
        }
    }

    /// <summary>
    /// 当前播放循环模式。
    /// </summary>
    public BgmPlayMode PlayMode { get; set; } = BgmPlayMode.Sequential;

    /// <summary>
    /// 当前正在播放的曲目索引 (0 ~ TrackCount - 1)。
    /// </summary>
    public int CurrentBgmIndex => _currentBgmIndex;

    /// <summary>
    /// 曲库总曲目数。
    /// </summary>
    public int TrackCount => _bgmTracks.Count;

    /// <summary>
    /// 所有曲目的展示名称列表。
    /// </summary>
    public IReadOnlyList<string> BgmDisplayNames => _bgmDisplayNames;

    /// <summary>
    /// 当前正在播放的 BGM 完整标签（含索引编号）。
    /// </summary>
    public string CurrentBgmTitle => _bgmDisplayNames.Count > 0 && _currentBgmIndex < _bgmDisplayNames.Count
        ? $"[{_currentBgmIndex + 1:D2}/{_bgmDisplayNames.Count:D2}] {_bgmDisplayNames[_currentBgmIndex]}"
        : "无";

    /// <summary>
    /// 当前正在播放的 BGM 纯曲目名。
    /// </summary>
    public string CurrentBgmPureTitle => _bgmDisplayNames.Count > 0 && _currentBgmIndex < _bgmDisplayNames.Count
        ? _bgmDisplayNames[_currentBgmIndex]
        : "无";

    /// <summary>
    /// 获取指定索引的曲目名称。
    /// </summary>
    public string GetTrackName(int index)
    {
        if (index >= 0 && index < _bgmDisplayNames.Count)
        {
            return _bgmDisplayNames[index];
        }
        return "未知曲目";
    }

    /// <summary>
    /// 获取当前播放模式的中文文本描述。
    /// </summary>
    public string GetPlayModeName()
    {
        return PlayMode switch
        {
            BgmPlayMode.Sequential => "顺序循环",
            BgmPlayMode.Random => "随机播放",
            BgmPlayMode.Single => "单曲循环",
            _ => "顺序循环"
        };
    }

    /// <summary>
    /// 在顺序循环、随机播放、单曲循环之间循环切换。
    /// </summary>
    public void CyclePlayMode()
    {
        PlayMode = (BgmPlayMode)(((int)PlayMode + 1) % 3);
        GameSettings.Current.BgmPlayMode = (int)PlayMode;
        GameSettings.Save();
        Console.WriteLine($"[AudioManager] BGM PlayMode switched to: {GetPlayModeName()}");
    }

    /// <summary>
    /// 初始化音频子系统：利用程序化合成器直接在内存中生成并缓存全部 SoundEffect 实例。
    /// </summary>
    public void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        try
        {
            foreach (SoundType type in Enum.GetValues<SoundType>())
            {
                byte[] wavBytes = ProceduralSoundGenerator.GenerateWav(type);
                using var ms = new MemoryStream(wavBytes);
                var sfx = SoundEffect.FromStream(ms);
                _soundCache[type] = sfx;
            }
            Console.WriteLine($"[AudioManager] Successfully synthesized and cached {_soundCache.Count} procedural sound effects.");
            LoadExternalSfxOverrides();
        }
        catch (NoAudioHardwareException ex)
        {
            _audioDeviceAvailable = false;
            Console.WriteLine($"[AudioManager] No audio hardware detected, sound disabled gracefully: {ex.Message}");
        }
        catch (Exception ex)
        {
            _audioDeviceAvailable = false;
            Console.WriteLine($"[AudioManager] Audio initialization warning, falling back to silent mode: {ex.Message}");
        }
    }

    /// <summary>
    /// 从 Content 加载并管理全量 17 首背景音乐，支持自选切歌、顺序循环、随机播放及单曲循环。
    /// </summary>
    public void LoadBgm(ContentManager content)
    {
        (string assetName, string displayName)[] tracks = new[]
        {
            ("BGM/Club Diver", "俱乐部跳水 (Club Diver)"),
            ("BGM/Video Dungeon Boss", "视频地牢Boss (Video Dungeon Boss)"),
            ("BGM/Shiny Tech2", "闪亮科技II (Shiny Tech II)"),
            ("BGM/Galactic Rap", "银河说唱 (Galactic Rap)"),
            ("BGM/Cloud Dancer", "云舞者 (Cloud Dancer)"),
            ("BGM/Ethernight Club", "以太夜俱乐部 (Ethernight Club)"),
            ("BGM/Voxel Revolution", "体素革命 (Voxel Revolution)"),
            ("BGM/Newer Wave", "新浪潮 (Newer Wave)"),
            ("BGM/Beauty Flow", "美之流 (Beauty Flow)"),
            ("BGM/Bleeping Demo", "哔哔声演示 (Bleeping Demo)"),
            ("BGM/Captain Scurvy", "坏血病队长 (Captain Scurvy)"),
            ("BGM/Desert of Lost Souls", "迷失灵魂的沙漠 (Desert of Lost Souls)"),
            ("BGM/Getting it Done", "完成任务 (Getting it Done)"),
            ("BGM/Laser Groove", "激光槽 (Laser Groove)"),
            ("BGM/Laserpack", "激光包 (Laserpack)"),
            ("BGM/Pookatori and Friends", "普卡托里与朋友们 (Pookatori and Friends)"),
            ("BGM/Realizer", "实现器 (Realizer)")
        };

        _bgmTracks.Clear();
        _bgmDisplayNames.Clear();

        foreach (var (assetName, displayName) in tracks)
        {
            try
            {
                var song = content.Load<Song>(assetName);
                _bgmTracks.Add(song);
                _bgmDisplayNames.Add(displayName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AudioManager] Warning loading BGM track '{assetName}': {ex.Message}");
            }
        }

        Console.WriteLine($"[AudioManager] Successfully loaded {_bgmTracks.Count} BGM tracks for automatic cycling and selection.");

        // 读取持久化音频配置
        var settings = GameSettings.Current;
        _masterVolume = settings.MasterVolume;
        _bgmVolume = settings.BgmVolume;
        _sfxVolume = settings.SfxVolume;
        PlayMode = (BgmPlayMode)MathHelper.Clamp(settings.BgmPlayMode, 0, 2);
        if (_bgmTracks.Count > 0)
        {
            _currentBgmIndex = MathHelper.Clamp(settings.SelectedBgmIndex, 0, _bgmTracks.Count - 1);
        }

        if (_bgmTracks.Count > 0 && !_bgmActive)
        {
            StartBgm();
        }
    }

    /// <summary>
    /// 开启背景音乐循环。
    /// </summary>
    public void StartBgm()
    {
        if (_bgmTracks.Count == 0 || !_audioDeviceAvailable) return;
        _bgmActive = true;
        PlayCurrentBgm();
    }

    /// <summary>
    /// 播放当前索引的背景音乐曲目。
    /// </summary>
    public void PlayCurrentBgm()
    {
        if (_bgmTracks.Count == 0 || !_audioDeviceAvailable) return;
        try
        {
            var song = _bgmTracks[_currentBgmIndex];
            MediaPlayer.IsRepeating = false;
            ApplyBgmVolume();
            MediaPlayer.Play(song);
            Console.WriteLine($"[AudioManager] Now playing BGM {CurrentBgmTitle}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AudioManager] Error playing BGM: {ex.Message}");
        }
    }

    /// <summary>
    /// 直接选取并播放指定索引的背景音乐。
    /// </summary>
    /// <param name="index">曲目在曲库中的索引</param>
    public void PlayTrack(int index)
    {
        if (_bgmTracks.Count == 0 || !_audioDeviceAvailable) return;
        _currentBgmIndex = (index % _bgmTracks.Count + _bgmTracks.Count) % _bgmTracks.Count;
        _bgmActive = true;
        GameSettings.Current.SelectedBgmIndex = _currentBgmIndex;
        GameSettings.Save();
        PlayCurrentBgm();
    }

    /// <summary>
    /// 轮流切到下一首背景音乐。
    /// </summary>
    /// <param name="userTriggered">是否由玩家手动触发切换（手动切换时即使是单曲循环也会跳到下一首）</param>
    public void PlayNextBgm(bool userTriggered = false)
    {
        if (_bgmTracks.Count == 0) return;

        if (!userTriggered && PlayMode == BgmPlayMode.Single)
        {
            // 单曲循环自然播毕，重新播放当前曲目
            PlayCurrentBgm();
            return;
        }

        if (PlayMode == BgmPlayMode.Random && _bgmTracks.Count > 1)
        {
            int nextIndex;
            do
            {
                nextIndex = _rand.Next(_bgmTracks.Count);
            } while (nextIndex == _currentBgmIndex);
            _currentBgmIndex = nextIndex;
        }
        else
        {
            _currentBgmIndex = (_currentBgmIndex + 1) % _bgmTracks.Count;
        }

        GameSettings.Current.SelectedBgmIndex = _currentBgmIndex;
        GameSettings.Save();
        PlayCurrentBgm();
    }

    /// <summary>
    /// 切换到上一首背景音乐。
    /// </summary>
    public void PlayPrevBgm()
    {
        if (_bgmTracks.Count == 0) return;

        if (PlayMode == BgmPlayMode.Random && _bgmTracks.Count > 1)
        {
            int prevIndex;
            do
            {
                prevIndex = _rand.Next(_bgmTracks.Count);
            } while (prevIndex == _currentBgmIndex);
            _currentBgmIndex = prevIndex;
        }
        else
        {
            _currentBgmIndex = (_currentBgmIndex - 1 + _bgmTracks.Count) % _bgmTracks.Count;
        }

        GameSettings.Current.SelectedBgmIndex = _currentBgmIndex;
        GameSettings.Save();
        PlayCurrentBgm();
    }

    private void ApplyBgmVolume()
    {
        try
        {
            if (_audioDeviceAvailable)
            {
                MediaPlayer.Volume = _isMuted ? 0f : _masterVolume * _bgmVolume;
            }
        }
        catch { }
    }

    private float _bgmCooldown = 0f;
    private float _toxicGasCooldown = 0f;
    private float _poisonSizzleCooldown = 0f;

    /// <summary>
    /// 帧更新：检测当前曲目是否自然播放完毕，根据播放模式无缝流转下一首，并衰减音效冷却计时器。
    /// </summary>
    public void Update(float dt)
    {
        if (_toxicGasCooldown > 0f)
        {
            _toxicGasCooldown -= dt;
        }

        if (_poisonSizzleCooldown > 0f)
        {
            _poisonSizzleCooldown -= dt;
        }

        if (!_audioDeviceAvailable || !_bgmActive) return;

        if (_bgmCooldown > 0f)
        {
            _bgmCooldown -= dt;
            return;
        }

        try
        {
            if (_bgmTracks.Count > 0 && MediaPlayer.State == MediaState.Stopped)
            {
                _bgmCooldown = 0.5f;
                PlayNextBgm(userTriggered: false);
            }
        }
        catch { }
    }

    /// <summary>
    /// 触发播放指定音效。
    /// </summary>
    public void Play(SoundType type, float volumeScale = 1.0f, float pitchOffset = 0.0f, bool randomPitch = true)
    {
        if (!_audioDeviceAvailable || _isMuted || _masterVolume <= 0.001f || _sfxVolume <= 0.001f) return;
        if (!_initialized) Initialize();

        if (_soundCache.TryGetValue(type, out var sfx))
        {
            try
            {
                float pitch = pitchOffset;
                if (randomPitch)
                {
                    pitch += (float)(_rand.NextDouble() * 0.16 - 0.08);
                }
                pitch = MathHelper.Clamp(pitch, -1.0f, 1.0f);

                float finalVolume = MathHelper.Clamp(_masterVolume * _sfxVolume * volumeScale, 0.0f, 1.0f);
                sfx.Play(finalVolume, pitch, 0.0f);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AudioManager] Play sound '{type}' error: {ex.Message}");
            }
        }
    }

    public void PlayShoot(float volume = 0.80f) => Play(SoundType.Shoot, volume, 0.0f, true);
    public void PlayExplosion(float volume = 0.95f) => Play(SoundType.Explosion, volume, -0.05f, true);
    public void PlayBounce(float volume = 0.70f) => Play(SoundType.Bounce, volume, 0.0f, true);
    public void PlayHit(float volume = 0.85f) => Play(SoundType.Hit, volume, 0.0f, true);
    public void PlayFreeze(float volume = 0.75f) => Play(SoundType.Freeze, volume, 0.0f, false);
    public void PlayToxicGas(float volume = 0.40f)
    {
        // 毒云连发限频降噪：140ms 冷却防止散射/弹跳多毒云瞬间音量乘数叠加轰鸣
        if (_toxicGasCooldown > 0f) return;
        _toxicGasCooldown = 0.14f;
        Play(SoundType.ToxicGas, volume, (float)(_rand.NextDouble() * 0.08 - 0.04), true);
    }
    public void PlayReload(float volume = 0.75f) => Play(SoundType.Reload, volume, 0.0f, false);
    public void PlayDash(float volume = 0.75f) => Play(SoundType.Dash, volume, 0.0f, true);
    public void PlayBlock(float volume = 0.85f) => Play(SoundType.Block, volume, 0.0f, true);
    public void PlayJump(float volume = 0.75f) => Play(SoundType.Jump, volume, 0.0f, true);
    public void PlayStep(float volume = 0.55f, float pitchOffset = 0.0f) => Play(SoundType.Step, volume, pitchOffset, true);
    public void PlayUiHover(float volume = 0.45f) => Play(SoundType.UiHover, volume, 0.0f, false);
    public void PlayUiClick(float volume = 0.75f) => Play(SoundType.UiClick, volume, 0.0f, false);
    public void PlayBlackHole(float volume = 0.85f) => Play(SoundType.BlackHole, volume, 0.0f, true);
    public void PlayPhoenix(float volume = 0.88f) => Play(SoundType.Phoenix, volume, 0.0f, false);
    public void PlayDryFire(float volume = 0.65f) => Play(SoundType.DryFire, volume, 0.0f, true);
    public void PlayDeath(float volume = 0.95f) => Play(SoundType.Death, volume, 0.0f, false);
    public void PlayCardPick(float volume = 0.85f) => Play(SoundType.CardPick, volume, 0.0f, false);
    public void PlayPoisonSizzle(float volume = 0.30f)
    {
        if (_poisonSizzleCooldown > 0f) return;
        _poisonSizzleCooldown = 0.26f;
        Play(SoundType.PoisonSizzle, volume, 0.0f, true);
    }
    public void PlayTeleport(float volume = 0.85f) => Play(SoundType.Teleport, volume, 0.05f, true);
    public void PlayWallHit(float volume = 0.65f) => Play(SoundType.WallHit, volume, 0.0f, true);

    /// <summary>
    /// 自动扫描并装载外部音效资源（如 jump.wav、Hit19.wav、BlackHole24.wav、Boom37.wav、Shoot33.wav 等），无缝覆盖对应的程序化音效。
    /// </summary>
    private void LoadExternalSfxOverrides()
    {
        var searchDirs = new List<string>
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Content", "SFX"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Content"),
            Path.Combine(Directory.GetCurrentDirectory(), "Content", "SFX"),
            Path.Combine(Directory.GetCurrentDirectory(), "RoundsClone.Desktop", "Content", "SFX"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "网上素材", "音效")
        };

        var overrideMapping = new Dictionary<SoundType, string[]>
        {
            [SoundType.Jump] = new[] { "jump.wav", "Jump.wav" },
            [SoundType.Hit] = new[] { "Hit19.wav", "hit19.wav", "hit.wav", "Hit.wav" },
            [SoundType.Freeze] = new[] { "ice.wav", "Ice.wav", "freeze.wav", "Freeze.wav" },
            [SoundType.ToxicGas] = new[] { "smoke.wav", "Smoke.wav", "gas.wav", "Gas.wav", "toxic.wav", "Toxic.wav", "toxic_gas.wav" },
            [SoundType.BlackHole] = new[] { "BlackHole24.wav", "blackhole24.wav", "blackhole.wav", "BlackHole.wav" },
            [SoundType.Explosion] = new[] { "Boom37.wav", "boom37.wav", "boom.wav", "explosion.wav", "Explosion.wav" },
            [SoundType.Shoot] = new[] { "Shoot33.wav", "shoot33.wav", "shoot.wav", "Shoot.wav" },
            [SoundType.Step] = new[] { "step.wav", "Step.wav", "footstep.wav", "Footstep.wav" },
            [SoundType.Block] = new[] { "shield.wav", "Shield.wav", "block.wav", "Block.wav", "parry.wav", "Parry.wav", "ding.wav", "Ding.wav" },
            [SoundType.UiHover] = new[] { "ui_hover.wav", "hover.wav", "Hover.wav" },
            [SoundType.UiClick] = new[] { "ui_click.wav", "click.wav", "Click.wav" },
            [SoundType.Phoenix] = new[] { "Angelic12.wav", "angelic12.wav", "phoenix.wav", "Phoenix.wav" },
            [SoundType.DryFire] = new[] { "dry_fire.wav", "dryfire.wav", "empty.wav", "click.wav" },
            [SoundType.Death] = new[] { "death.wav", "heavy_hit.wav", "ko.wav", "gameover.wav" },
            [SoundType.CardPick] = new[] { "card_pick.wav", "cardpick.wav", "powerup.wav", "upgrade.wav" },
            [SoundType.PoisonSizzle] = new[] { "poison_sizzle.wav", "sizzle.wav", "acid.wav" },
            [SoundType.Teleport] = new[] { "teleport.wav", "blink.wav", "warp.wav" },
            [SoundType.WallHit] = new[] { "wall_hit.wav", "wallhit.wav", "impact.wav", "bullet_hit.wav", "wall.wav" }
        };

        foreach (var (soundType, candidateFileNames) in overrideMapping)
        {
            string? foundFile = null;
            foreach (var dir in searchDirs)
            {
                if (!Directory.Exists(dir)) continue;

                foreach (var fileName in candidateFileNames)
                {
                    string candidatePath = Path.Combine(dir, fileName);
                    if (File.Exists(candidatePath))
                    {
                        foundFile = candidatePath;
                        break;
                    }
                }
                if (foundFile != null) break;
            }

            if (foundFile != null)
            {
                var sfx = LoadSoundEffectSafe(foundFile);
                if (sfx != null)
                {
                    if (_soundCache.TryGetValue(soundType, out var oldSfx))
                    {
                        oldSfx.Dispose();
                    }
                    _soundCache[soundType] = sfx;
                    Console.WriteLine($"[AudioManager] Successfully loaded external sound effect for {soundType}: {foundFile} (Duration: {sfx.Duration.TotalMilliseconds:F0}ms)");
                }
            }
        }
    }

    /// <summary>
    /// 安全装载外部 WAV 音效文件，自动兼容标准 16-bit PCM 与 8-bit PCM 格式。
    /// </summary>
    private SoundEffect? LoadSoundEffectSafe(string filePath)
    {
        try
        {
            using var fs = File.OpenRead(filePath);
            return SoundEffect.FromStream(fs);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AudioManager] Native WAV load note for '{filePath}': {ex.Message}. Converting to standard 16-bit PCM...");
            try
            {
                byte[] raw = File.ReadAllBytes(filePath);
                byte[] pcm16 = ConvertWavToPcm16(raw);
                using var ms = new MemoryStream(pcm16);
                return SoundEffect.FromStream(ms);
            }
            catch (Exception ex2)
            {
                Console.WriteLine($"[AudioManager] Fallback conversion failed for '{filePath}': {ex2.Message}");
                return null;
            }
        }
    }

    /// <summary>
    /// 将 8-bit PCM 或非标 WAV 字节自动解析并重采样/格式化为标准的 16-bit PCM WAV。
    /// </summary>
    private static byte[] ConvertWavToPcm16(byte[] raw)
    {
        if (raw.Length < 44) throw new InvalidDataException("WAV file too short");

        int dataIndex = -1;
        for (int i = 12; i < raw.Length - 8; i++)
        {
            if (raw[i] == 'd' && raw[i + 1] == 'a' && raw[i + 2] == 't' && raw[i + 3] == 'a')
            {
                dataIndex = i;
                break;
            }
        }
        if (dataIndex == -1) throw new InvalidDataException("Data chunk not found in WAV");

        int dataSize = BitConverter.ToInt32(raw, dataIndex + 4);
        int sampleStartIndex = dataIndex + 8;
        int sampleCount = Math.Min(dataSize, raw.Length - sampleStartIndex);

        float[] samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            byte b = raw[sampleStartIndex + i];
            samples[i] = (b - 128f) / 128f;
        }

        int sampleRate = 44100;
        for (int i = 12; i < raw.Length - 16; i++)
        {
            if (raw[i] == 'f' && raw[i + 1] == 'm' && raw[i + 2] == 't' && raw[i + 3] == ' ')
            {
                sampleRate = BitConverter.ToInt32(raw, i + 12);
                break;
            }
        }

        return ProceduralSoundGenerator.BuildWavPcm16(samples, sampleRate);
    }

    public void Dispose()
    {
        foreach (var sfx in _soundCache.Values)
        {
            sfx.Dispose();
        }
        _soundCache.Clear();
        _bgmTracks.Clear();
    }
}
