using System;
using System.IO;
using Microsoft.Xna.Framework;

namespace RoundsClone.Core.Audio;

/// <summary>
/// 程序化音频波形合成器 (Procedural Sound Synthesizer)。
/// 采用类 sfxr/bfxr 纯数学波形调制算法（正弦、方波、白噪声、扫频与 ADSR 包络），
/// 纯代码合成高质量 16-bit 44.1kHz Mono PCM WAV 音频，无需任何外部音效文件依赖。
/// </summary>
public static class ProceduralSoundGenerator
{
    public const int DefaultSampleRate = 44100;

    /// <summary>
    /// 根据音效类型合成对应的标准 16-bit PCM WAV 数据。
    /// </summary>
    public static byte[] GenerateWav(SoundType type)
    {
        return type switch
        {
            SoundType.Shoot => GenerateShoot(),
            SoundType.Explosion => GenerateExplosion(),
            SoundType.Bounce => GenerateBounce(),
            SoundType.Hit => GenerateHit(),
            SoundType.Freeze => GenerateFreeze(),
            SoundType.ToxicGas => GenerateToxicGas(),
            SoundType.Reload => GenerateReload(),
            SoundType.Dash => GenerateDash(),
            SoundType.Block => GenerateBlock(),
            SoundType.Jump => GenerateJump(),
            SoundType.Step => GenerateStep(),
            SoundType.UiHover => GenerateUiHover(),
            SoundType.UiClick => GenerateUiClick(),
            SoundType.BlackHole => GenerateBlackHole(),
            SoundType.Phoenix => GeneratePhoenix(),
            SoundType.DryFire => GenerateDryFire(),
            SoundType.Death => GenerateDeath(),
            SoundType.CardPick => GenerateCardPick(),
            SoundType.PoisonSizzle => GeneratePoisonSizzle(),
            SoundType.Teleport => GenerateTeleport(),
            SoundType.WallHit => GenerateWallHit(),
            _ => GenerateShoot()
        };
    }

    /// <summary>
    /// 将所有内置程序化音效一键导出为标准的 .wav 文件到指定磁盘目录。
    /// </summary>
    public static void ExportAllWavs(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        foreach (SoundType type in Enum.GetValues<SoundType>())
        {
            byte[] wavBytes = GenerateWav(type);
            string fileName = $"{type.ToString().ToLowerInvariant()}.wav";
            string filePath = Path.Combine(outputDirectory, fileName);
            File.WriteAllBytes(filePath, wavBytes);
        }
    }

    /// <summary>
    /// 将单声道浮点样本数组转换为标准的 44 字节 RIFF 头的 16-bit PCM WAV 字节数组。
    /// </summary>
    public static byte[] BuildWavPcm16(float[] samples, int sampleRate = DefaultSampleRate)
    {
        int subChunk2Size = samples.Length * 2;
        int chunkSize = 36 + subChunk2Size;

        using var ms = new MemoryStream(44 + subChunk2Size);
        using var writer = new BinaryWriter(ms);

        // 1. RIFF Header
        writer.Write((byte)'R'); writer.Write((byte)'I'); writer.Write((byte)'F'); writer.Write((byte)'F');
        writer.Write(chunkSize);
        writer.Write((byte)'W'); writer.Write((byte)'A'); writer.Write((byte)'V'); writer.Write((byte)'E');

        // 2. "fmt " Subchunk
        writer.Write((byte)'f'); writer.Write((byte)'m'); writer.Write((byte)'t'); writer.Write((byte)' ');
        writer.Write(16);                 // Subchunk1Size (16 for PCM)
        writer.Write((short)1);            // AudioFormat (1 = PCM)
        writer.Write((short)1);            // NumChannels (1 = Mono)
        writer.Write(sampleRate);          // SampleRate (44100)
        writer.Write(sampleRate * 2);      // ByteRate (SampleRate * NumChannels * BitsPerSample / 8)
        writer.Write((short)2);            // BlockAlign (NumChannels * BitsPerSample / 8)
        writer.Write((short)16);           // BitsPerSample (16 bits)

        // 3. "data" Subchunk
        writer.Write((byte)'d'); writer.Write((byte)'a'); writer.Write((byte)'t'); writer.Write((byte)'a');
        writer.Write(subChunk2Size);

        // 4. PCM Samples
        for (int i = 0; i < samples.Length; i++)
        {
            float s = Math.Clamp(samples[i], -1.0f, 1.0f);
            short val = (short)(s * 32767f);
            writer.Write(val);
        }

        return ms.ToArray();
    }

    /// <summary>
    /// 1. 子弹射击：干脆高能的激光下潜滑频（820Hz -> 140Hz）。
    /// </summary>
    public static byte[] GenerateShoot()
    {
        int sampleRate = DefaultSampleRate;
        float duration = 0.13f;
        int length = (int)(sampleRate * duration);
        float[] samples = new float[length];
        double phase = 0;

        for (int i = 0; i < length; i++)
        {
            float t = i / (float)sampleRate;
            float progress = t / duration;

            float freq = 820f * MathF.Pow(140f / 820f, progress);
            phase += 2.0 * Math.PI * freq / sampleRate;

            float sine = MathF.Sin((float)phase);
            float tri = (float)(2.0 / Math.PI * Math.Asin(Math.Sin(phase)));
            float raw = sine * 0.65f + tri * 0.35f;

            float env = (progress < 0.03f) ? (progress / 0.03f) : MathF.Exp(-progress * 6.5f);
            samples[i] = raw * env * 0.85f;
        }

        return BuildWavPcm16(samples, sampleRate);
    }

    /// <summary>
    /// 2. 剧烈爆炸：低通滤波重低音噪声 + 渐变下潜亚低音轰鸣。
    /// </summary>
    public static byte[] GenerateExplosion()
    {
        int sampleRate = DefaultSampleRate;
        float duration = 0.65f;
        int length = (int)(sampleRate * duration);
        float[] samples = new float[length];
        var rand = new Random(1337);
        float lowpass = 0f;
        double subPhase = 0;

        for (int i = 0; i < length; i++)
        {
            float t = i / (float)sampleRate;
            float progress = t / duration;

            float subFreq = 70f * MathF.Pow(25f / 70f, progress);
            subPhase += 2.0 * Math.PI * subFreq / sampleRate;
            float subBass = MathF.Sin((float)subPhase);

            float whiteNoise = (float)(rand.NextDouble() * 2.0 - 1.0);
            float filterAlpha = 0.40f * MathF.Exp(-progress * 4.5f) + 0.03f;
            lowpass += filterAlpha * (whiteNoise - lowpass);

            float env = (progress < 0.008f) ? (progress / 0.008f) : MathF.Exp(-progress * 4.8f);
            float mix = (lowpass * 0.70f + subBass * 0.45f) * env;
            samples[i] = MathF.Tanh(mix * 1.5f) * 0.90f;
        }

        return BuildWavPcm16(samples, sampleRate);
    }

    /// <summary>
    /// 3. 墙面反弹/折射：高音调短促清脆上升“叮”声（900Hz -> 1750Hz）。
    /// </summary>
    public static byte[] GenerateBounce()
    {
        int sampleRate = DefaultSampleRate;
        float duration = 0.09f;
        int length = (int)(sampleRate * duration);
        float[] samples = new float[length];
        double phase = 0;

        for (int i = 0; i < length; i++)
        {
            float t = i / (float)sampleRate;
            float progress = t / duration;
            float freq = 900f + (1750f - 900f) * progress;
            phase += 2.0 * Math.PI * freq / sampleRate;

            float sine = MathF.Sin((float)phase);
            float env = (progress < 0.02f) ? (progress / 0.02f) : MathF.Exp(-progress * 14f);
            samples[i] = sine * env * 0.75f;
        }

        return BuildWavPcm16(samples, sampleRate);
    }

    /// <summary>
    /// 4. 击中受击：带有冲击顿挫感的低音重击与打击火花破音。
    /// </summary>
    public static byte[] GenerateHit()
    {
        int sampleRate = DefaultSampleRate;
        float duration = 0.11f;
        int length = (int)(sampleRate * duration);
        float[] samples = new float[length];
        var rand = new Random(42);
        double phase = 0;

        for (int i = 0; i < length; i++)
        {
            float t = i / (float)sampleRate;
            float progress = t / duration;
            float freq = 220f * MathF.Pow(55f / 220f, progress);
            phase += 2.0 * Math.PI * freq / sampleRate;

            float body = MathF.Sin((float)phase);
            float noise = (float)(rand.NextDouble() * 2.0 - 1.0) * MathF.Exp(-progress * 25f);
            float env = (progress < 0.015f) ? (progress / 0.015f) : MathF.Exp(-progress * 9f);
            samples[i] = (body * 0.75f + noise * 0.35f) * env * 0.85f;
        }

        return BuildWavPcm16(samples, sampleRate);
    }

    /// <summary>
    /// 5. 极寒冰冻：清澈空灵的双音冰晶风铃共振回响。
    /// </summary>
    public static byte[] GenerateFreeze()
    {
        int sampleRate = DefaultSampleRate;
        float duration = 0.32f;
        int length = (int)(sampleRate * duration);
        float[] samples = new float[length];
        double p1 = 0, p2 = 0, pMod = 0;

        for (int i = 0; i < length; i++)
        {
            float t = i / (float)sampleRate;
            float progress = t / duration;

            pMod += 2.0 * Math.PI * 180f / sampleRate;
            float mod = 1.0f + 0.15f * MathF.Sin((float)pMod);

            p1 += 2.0 * Math.PI * (2150f * mod) / sampleRate;
            p2 += 2.0 * Math.PI * (3250f * mod) / sampleRate;

            float bell = MathF.Sin((float)p1) * 0.55f + MathF.Sin((float)p2) * 0.45f;
            float env = (progress < 0.01f) ? (progress / 0.01f) : MathF.Exp(-progress * 7.5f);
            samples[i] = bell * env * 0.70f;
        }

        return BuildWavPcm16(samples, sampleRate);
    }

    /// <summary>
    /// 6. 烟雾/毒气弥漫：柔和温润的烟雾弹气体释放声 (Soft Pressurized Smoke Mist Release)。
    /// 消除刺耳前置爆破与高频撕裂尖啸，采用中低频暖调带通滤波（1150Hz -> 750Hz 柔和气雾）
    /// 配合平滑缓升长尾渐出包络，多次高频连发也能保持温和不吵闹的弥漫气流听感。
    /// </summary>
    public static byte[] GenerateToxicGas()
    {
        int sampleRate = DefaultSampleRate;
        float duration = 0.85f;
        int length = (int)(sampleRate * duration);
        float[] samples = new float[length];
        var rand = new Random(77);

        // 双段状态变量滤波器（SVF）状态变量（温和带通与低频腔体）
        float lp1 = 0f, bp1 = 0f;
        float lp2 = 0f, bp2 = 0f;
        double popPhase = 0;

        for (int i = 0; i < length; i++)
        {
            float t = i / (float)sampleRate;
            float progress = t / duration;

            // 1. 极其轻柔的低频微气阀开启声 (Soft Sub-Valve Release: 0 ~ 30ms 极微弱低频噗声，杜绝刺耳尖响)
            float pop = 0f;
            if (t < 0.030f)
            {
                float popProg = t / 0.030f;
                float popFreq = 110f * (1f - popProg * 0.5f);
                popPhase += 2.0 * Math.PI * popFreq / sampleRate;
                pop = MathF.Sin((float)popPhase) * (1f - popProg) * 0.05f;
            }

            // 2. 纯白噪声源
            float white = (float)(rand.NextDouble() * 2.0 - 1.0);

            // 3. 柔和气雾带通滤波 (Soft Mist Hiss: 1150Hz 缓慢温和降至 750Hz 暖色雾流)
            float cutoff1 = MathF.Max(650f, 1150f - 400f * progress);
            float f1 = 2f * MathF.Sin(MathF.PI * cutoff1 / sampleRate);
            float q1 = 0.65f; // 阻尼适中，完全消除高频锐利共鸣
            lp1 += f1 * bp1;
            float hp1 = white - lp1 - q1 * bp1;
            bp1 += f1 * hp1;

            // 4. 气罐内腔低频扩散垫底 (Low Chamber Spread: 480Hz 降至 320Hz 带通)
            float cutoff2 = MathF.Max(260f, 480f - 160f * progress);
            float f2 = 2f * MathF.Sin(MathF.PI * cutoff2 / sampleRate);
            float q2 = 0.70f;
            lp2 += f2 * bp2;
            float hp2 = white - lp2 - q2 * bp2;
            bp2 += f2 * hp2;

            // 5. 平滑缓升包络 (75ms 渐入软启动 -> 350ms 平缓喷射 -> 柔和指数级消散，彻底消除突兀感)
            float env;
            if (progress < 0.088f)
            {
                env = MathF.Sin(progress / 0.088f * (MathHelper.Pi * 0.5f));
            }
            else if (progress < 0.45f)
            {
                float p = (progress - 0.088f) / (0.45f - 0.088f);
                env = 1.0f - p * 0.15f;
            }
            else
            {
                float p = (progress - 0.45f) / 0.55f;
                env = 0.85f * MathF.Pow(1.0f - p, 1.8f);
            }

            // 6. 微弱气流轻柔波动
            float turbulence = 1.0f + 0.08f * MathF.Sin(2f * MathF.PI * 12.0f * t);

            // 仅使用滤波后温暖气雾，杜绝任何未过滤刺耳白噪
            float gasHiss = (bp1 * 0.55f + bp2 * 0.35f) * turbulence;
            float mix = (gasHiss * env + pop) * 0.52f;

            samples[i] = MathF.Tanh(mix);
        }

        return BuildWavPcm16(samples, sampleRate);
    }

    /// <summary>
    /// 7. 装填上膛：机械滑块卡扣双段清脆咔哒音。
    /// </summary>
    public static byte[] GenerateReload()
    {
        int sampleRate = DefaultSampleRate;
        float duration = 0.15f;
        int length = (int)(sampleRate * duration);
        float[] samples = new float[length];
        double p1 = 0, p2 = 0;

        for (int i = 0; i < length; i++)
        {
            float t = i / (float)sampleRate;
            float s1 = 0f;
            if (t < 0.045f)
            {
                float p = t / 0.045f;
                p1 += 2.0 * Math.PI * 650f / sampleRate;
                s1 = MathF.Sin((float)p1) * MathF.Exp(-p * 10f);
            }

            float s2 = 0f;
            if (t >= 0.06f && t < 0.14f)
            {
                float p = (t - 0.06f) / 0.08f;
                float f = 1200f * MathF.Pow(400f / 1200f, p);
                p2 += 2.0 * Math.PI * f / sampleRate;
                s2 = MathF.Sin((float)p2) * MathF.Exp(-p * 8f);
            }

            samples[i] = (s1 * 0.65f + s2 * 0.85f) * 0.80f;
        }

        return BuildWavPcm16(samples, sampleRate);
    }

    /// <summary>
    /// 8. 冲刺位移：空气受压撕裂的呼啸音。
    /// </summary>
    public static byte[] GenerateDash()
    {
        int sampleRate = DefaultSampleRate;
        float duration = 0.18f;
        int length = (int)(sampleRate * duration);
        float[] samples = new float[length];
        var rand = new Random(99);
        float lp = 0f;
        double p = 0;

        for (int i = 0; i < length; i++)
        {
            float t = i / (float)sampleRate;
            float progress = t / duration;
            float freq = 380f * (1.0f - progress * 0.7f);
            p += 2.0 * Math.PI * freq / sampleRate;
            float sine = MathF.Sin((float)p);

            float noise = (float)(rand.NextDouble() * 2.0 - 1.0);
            lp += 0.15f * (noise - lp);

            float env = MathF.Sin(progress * MathHelper.Pi);
            samples[i] = (sine * 0.4f + lp * 0.6f) * env * 0.70f;
        }

        return BuildWavPcm16(samples, sampleRate);
    }

    /// <summary>
    /// 9. 护盾格挡/弹反：清脆悦耳、宛如金属冷兵器精准弹刀/坚固力场格挡的“叮~”一声金属高频震颤回响 (Crisp Metallic Parry Ding)。
    /// 包含瞬间金属硬敲击冲击瞬态 (Metallic Attack Transient)、多频非谐波金属泛音振荡 (Inharmonic Chime Overtones) 与晶莹清透的悠长颤音余韵。
    /// </summary>
    public static byte[] GenerateBlock()
    {
        int sampleRate = DefaultSampleRate;
        float duration = 0.48f;
        int length = (int)(sampleRate * duration);
        float[] samples = new float[length];
        var rand = new Random(101);

        double p1 = 0, p2 = 0, p3 = 0, p4 = 0;
        // 基准“叮”金属振动多重非谐波泛音组（精准呈现冷兵器完美弹反刀锋金石声）
        float f1 = 2150f; // 核心纯净高亮金属基频
        float f2 = 5420f; // 高阶金属晶莹闪光泛音
        float f3 = 8680f; // 极高频硬质刀锋火花音
        float f4 = 1180f; // 中低频扎实金石共鸣底核

        for (int i = 0; i < length; i++)
        {
            float t = i / (float)sampleRate;
            float progress = t / duration;

            // 1. 瞬间金属硬接触冲击瞬态 (0 ~ 8ms 的硬敲击微噪波瞬态)
            float strike = 0f;
            if (t < 0.008f)
            {
                float sp = t / 0.008f;
                float noise = (float)(rand.NextDouble() * 2.0 - 1.0);
                strike = noise * MathF.Exp(-sp * 7f) * 0.45f;
            }

            // 2. 多重非谐波金属振动波相干叠加
            p1 += 2.0 * Math.PI * f1 / sampleRate;
            p2 += 2.0 * Math.PI * f2 / sampleRate;
            p3 += 2.0 * Math.PI * f3 / sampleRate;
            p4 += 2.0 * Math.PI * f4 / sampleRate;

            // 空间金属颤音微频波动
            float tremolo = 1.0f + 0.08f * MathF.Sin(2f * MathF.PI * 14f * t);

            float s1 = MathF.Sin((float)p1) * MathF.Exp(-progress * 7.5f) * 0.65f;  // 主音 2150Hz 悠长余韵
            float s2 = MathF.Sin((float)p2) * MathF.Exp(-progress * 22.0f) * 0.35f; // 高音 5420Hz 清脆亮光
            float s3 = MathF.Sin((float)p3) * MathF.Exp(-progress * 45.0f) * 0.22f; // 尖音 8680Hz 瞬间火花
            float s4 = MathF.Sin((float)p4) * MathF.Exp(-progress * 11.0f) * 0.30f; // 底音 1180Hz 厚重实心底核

            float ding = (s1 + s2 + s3 + s4) * tremolo;

            // 3. 初始 2ms 极速防爆音微淡入
            float attack = (t < 0.002f) ? (t / 0.002f) : 1.0f;
            float mix = (ding * attack + strike) * 0.90f;

            // 软饱和平滑，保证高频“叮”声穿透力强且绝不破音
            samples[i] = MathF.Tanh(mix);
        }

        return BuildWavPcm16(samples, sampleRate);
    }

    /// <summary>
    /// 10. 跳跃腾空：短促轻快、富有弹性的上扬跃升音（160Hz -> 520Hz）。
    /// </summary>
    public static byte[] GenerateJump()
    {
        int sampleRate = DefaultSampleRate;
        float duration = 0.12f;
        int length = (int)(sampleRate * duration);
        float[] samples = new float[length];
        double phase = 0;

        for (int i = 0; i < length; i++)
        {
            float t = i / (float)sampleRate;
            float progress = t / duration;
            float freq = 160f + 360f * MathF.Pow(progress, 0.75f);
            phase += 2.0 * Math.PI * freq / sampleRate;

            float sine = MathF.Sin((float)phase);
            float tri = (float)(2.0 / Math.PI * Math.Asin(Math.Sin(phase)));
            float raw = sine * 0.70f + tri * 0.30f;

            float env = (progress < 0.02f) ? (progress / 0.02f) : MathF.Exp(-progress * 9f);
            samples[i] = raw * env * 0.75f;
        }

        return BuildWavPcm16(samples, sampleRate);
    }

    /// <summary>
    /// 11. 行走脚步：清脆轻快、带弹性轻微阻尼顿挫感的“啪嗒”接触声。
    /// </summary>
    public static byte[] GenerateStep()
    {
        int sampleRate = DefaultSampleRate;
        float duration = 0.062f;
        int length = (int)(sampleRate * duration);
        float[] samples = new float[length];
        var rand = new Random(101);
        double phase = 0;
        float noiseFilter = 0f;

        for (int i = 0; i < length; i++)
        {
            float t = i / (float)sampleRate;
            float progress = t / duration;

            // 频率双段快速下滑：前半段清脆微高频“啪”(700Hz->240Hz)，后半段实心接地“嗒”(240Hz->90Hz)
            float freq = 700f * MathF.Pow(90f / 700f, MathF.Pow(progress, 0.65f));
            phase += 2.0 * Math.PI * freq / sampleRate;

            float sine = MathF.Sin((float)phase);
            float tri = (float)(2.0 / Math.PI * Math.Asin(Math.Sin(phase)));
            float body = sine * 0.70f + tri * 0.30f;

            // 微弱地面接触摩擦白噪声
            float white = (float)(rand.NextDouble() * 2.0 - 1.0);
            noiseFilter += 0.25f * (white - noiseFilter);

            // 包络：极快 2ms 上冲，随后急剧指数衰减
            float env = (progress < 0.035f) ? (progress / 0.035f) : MathF.Exp(-progress * 22f);
            samples[i] = (body * 0.82f + noiseFilter * 0.25f) * env * 0.75f;
        }

        return BuildWavPcm16(samples, sampleRate);
    }

    /// <summary>
    /// 12. 选中/悬停：纯净清透、高科技轻盈的微频上扬提示音。
    /// </summary>
    public static byte[] GenerateUiHover()
    {
        int sampleRate = DefaultSampleRate;
        float duration = 0.032f;
        int length = (int)(sampleRate * duration);
        float[] samples = new float[length];
        double phase = 0;

        for (int i = 0; i < length; i++)
        {
            float t = i / (float)sampleRate;
            float progress = t / duration;

            float freq = 1250f + 320f * progress;
            phase += 2.0 * Math.PI * freq / sampleRate;

            float sine = MathF.Sin((float)phase);
            float env = MathF.Sin(progress * MathHelper.Pi);
            samples[i] = sine * env * 0.40f;
        }

        return BuildWavPcm16(samples, sampleRate);
    }

    /// <summary>
    /// 13. 点击/确认：干脆利落、富有弹力顿挫感的确认点击音。
    /// </summary>
    public static byte[] GenerateUiClick()
    {
        int sampleRate = DefaultSampleRate;
        float duration = 0.068f;
        int length = (int)(sampleRate * duration);
        float[] samples = new float[length];
        double p1 = 0, p2 = 0;

        for (int i = 0; i < length; i++)
        {
            float t = i / (float)sampleRate;
            float progress = t / duration;

            // 双脉冲合成：第一段清脆机械卡扣触发行程(1750Hz->920Hz)，第二段回弹实心顿挫(520Hz->220Hz)
            float f1 = 1750f * MathF.Pow(920f / 1750f, progress);
            p1 += 2.0 * Math.PI * f1 / sampleRate;
            float clickTick = MathF.Sin((float)p1) * MathF.Exp(-progress * 35f);

            float f2 = 520f * MathF.Pow(220f / 520f, progress);
            p2 += 2.0 * Math.PI * f2 / sampleRate;
            float clickBody = MathF.Sin((float)p2) * MathF.Exp(-progress * 18f);

            float mix = clickTick * 0.55f + clickBody * 0.65f;
            samples[i] = mix * 0.85f;
        }

        return BuildWavPcm16(samples, sampleRate);
    }

    /// <summary>
    /// 14. 黑洞坍缩/引力场：低通深邃重力嗡鸣、时空扭曲调制与吸附呼啸声。
    /// </summary>
    public static byte[] GenerateBlackHole()
    {
        int sampleRate = DefaultSampleRate;
        float duration = 1.35f;
        int length = (int)(sampleRate * duration);
        float[] samples = new float[length];
        double phaseOsc1 = 0, phaseOsc2 = 0;
        Random rng = new(1337);
        float noiseState = 0f;

        for (int i = 0; i < length; i++)
        {
            float t = i / (float)sampleRate;
            float progress = t / duration;

            // 1. 引力旋涡调制频率：从 160Hz 逐渐下沉压缩至 48Hz，叠加 8.5Hz 的强重力透镜颤音 (tremolo/vibrato)
            float vibrato = MathF.Sin(2f * MathF.PI * 8.5f * t) * (18f * (1f - progress * 0.5f));
            float baseFreq = MathHelper.Lerp(150f, 45f, MathF.Pow(progress, 0.7f)) + vibrato;
            phaseOsc1 += 2.0 * Math.PI * Math.Max(20f, baseFreq) / sampleRate;

            // 2. 次低频副震荡波（产生双频拍频阻尼感）
            float subFreq = baseFreq * 0.5f;
            phaseOsc2 += 2.0 * Math.PI * subFreq / sampleRate;

            float sine = MathF.Sin((float)phaseOsc1);
            float sub = MathF.Sin((float)phaseOsc2);

            // 3. 产生低通滤波深空暗物质湍流噪声
            float white = (float)(rng.NextDouble() * 2.0 - 1.0);
            noiseState = noiseState * 0.94f + white * 0.06f; // 一阶低通

            // 4. 时空坍缩吸入包络 (Inward Implosion Envelope): 前期吸入蓄力膨胀，后期骤然聚变坍缩衰减
            float envelope;
            if (progress < 0.25f)
            {
                envelope = progress / 0.25f; // 渐入
            }
            else if (progress < 0.75f)
            {
                envelope = 1.0f + 0.2f * MathF.Sin((progress - 0.25f) / 0.5f * MathHelper.Pi);
            }
            else
            {
                float decay = (progress - 0.75f) / 0.25f;
                envelope = MathF.Max(0f, 1.2f * (1f - decay * decay));
            }

            // 混合波形与过载饱和（时空扭曲微失真）
            float mixed = (sine * 0.55f + sub * 0.35f + noiseState * 0.30f) * envelope;
            float saturated = MathF.Tanh(mixed * 1.4f);

            samples[i] = MathHelper.Clamp(saturated * 0.85f, -1.0f, 1.0f);
        }

        return BuildWavPcm16(samples, sampleRate);
    }

    /// <summary>
    /// 15. 凤凰涅槃：圣洁空灵的天使圣咏与金光升腾共鸣和弦 (Celestial Phoenix Resurrection Choir & Golden Chimes)。
    /// 包含 F 大调九和弦空灵泛音层叠、4.5Hz 天使合唱颤音、金钟钟鸣共鸣与神圣治愈回响。
    /// </summary>
    public static byte[] GeneratePhoenix()
    {
        int sampleRate = DefaultSampleRate;
        float duration = 1.45f;
        int length = (int)(sampleRate * duration);
        float[] samples = new float[length];

        // 圣咏和弦音高：F4, A4, C5, E5, G5, A5
        float[] chordFreqs = { 349.23f, 440.00f, 523.25f, 659.25f, 783.99f, 880.00f };
        float[] chordWeights = { 0.32f, 0.26f, 0.22f, 0.16f, 0.14f, 0.10f };
        double[] phases = new double[chordFreqs.Length];
        double bellPhase = 0;

        for (int i = 0; i < length; i++)
        {
            float t = i / (float)sampleRate;
            float progress = t / duration;

            // 1. 天使圣咏合唱颤音 (4.5Hz 柔和音高颤动与合唱微频差)
            float choirVibrato = MathF.Sin(2f * MathF.PI * 4.5f * t) * 2.2f;

            float chordSum = 0f;
            for (int k = 0; k < chordFreqs.Length; k++)
            {
                float freq = chordFreqs[k] + choirVibrato * (0.8f + k * 0.15f);
                phases[k] += 2.0 * Math.PI * freq / sampleRate;
                float sin = MathF.Sin((float)phases[k]);
                // 叠加二次谐波温润泛音
                float harmonic = MathF.Sin((float)(phases[k] * 2.0)) * 0.25f;
                chordSum += (sin + harmonic) * chordWeights[k];
            }

            // 2. 金光钟鸣脆响 (Golden Chime: 1318.5Hz 高纯度泛音钟鸣，前置清脆后延绵)
            bellPhase += 2.0 * Math.PI * 1318.51 / sampleRate;
            float bell = MathF.Sin((float)bellPhase) * MathF.Exp(-t * 3.8f) * 0.24f
                       + MathF.Sin((float)(bellPhase * 2.76)) * MathF.Exp(-t * 6.0f) * 0.12f;

            // 3. 圣光升腾包络：120ms 柔和涌起 -> 800ms 圣洁平原 -> 柔和回响渐隐
            float env;
            if (progress < 0.085f)
            {
                env = MathF.Sin(progress / 0.085f * (MathHelper.Pi * 0.5f));
            }
            else if (progress < 0.60f)
            {
                float p = (progress - 0.085f) / (0.60f - 0.085f);
                env = 1.0f - p * 0.18f;
            }
            else
            {
                float p = (progress - 0.60f) / 0.40f;
                env = 0.82f * MathF.Pow(1.0f - p, 2.0f);
            }

            float mix = chordSum * env * 0.78f + bell;
            samples[i] = MathHelper.Clamp(MathF.Tanh(mix * 1.1f) * 0.88f, -1.0f, 1.0f);
        }

        return BuildWavPcm16(samples, sampleRate);
    }

    /// <summary>
    /// 16. 空弹仓击空声：撞针撞击空膛的清脆机械咔哒微金属碰撞 (Dry Fire Mechanical Snap)。
    /// </summary>
    public static byte[] GenerateDryFire()
    {
        int sampleRate = DefaultSampleRate;
        float duration = 0.055f;
        int length = (int)(sampleRate * duration);
        float[] samples = new float[length];
        var rand = new Random(101);
        double phase = 0;
        float lp = 0f, bp = 0f;

        for (int i = 0; i < length; i++)
        {
            float t = i / (float)sampleRate;
            float progress = t / duration;

            // 撞针金属高频清脆微鸣 (2400Hz 快速指数衰减)
            phase += 2.0 * Math.PI * 2400.0 / sampleRate;
            float ping = MathF.Sin((float)phase) * MathF.Exp(-t * 180f) * 0.65f;

            // 机械滑块微摩擦噪声 (SVF 带通 3400Hz)
            float white = (float)(rand.NextDouble() * 2.0 - 1.0);
            float f = 2f * MathF.Sin(MathF.PI * 3400f / sampleRate);
            lp += f * bp;
            float hp = white - lp - 0.5f * bp;
            bp += f * hp;
            float snap = bp * MathF.Exp(-t * 220f) * 0.45f;

            samples[i] = MathHelper.Clamp((ping + snap) * 0.85f, -1.0f, 1.0f);
        }

        return BuildWavPcm16(samples, sampleRate);
    }

    /// <summary>
    /// 17. 角色倒地终结重音：沉重浑厚的超低频震荡与深沉冲击波 (Heavy Impact Doom Shockwave)。
    /// 纯粹的深沉下潜重音与厚重共鸣，无聒噪高频，赋予阵亡瞬间强烈的力量感与战局终结定格。
    /// </summary>
    public static byte[] GenerateDeath()
    {
        int sampleRate = DefaultSampleRate;
        float duration = 0.88f;
        int length = (int)(sampleRate * duration);
        float[] samples = new float[length];
        double p1 = 0, p2 = 0;

        for (int i = 0; i < length; i++)
        {
            float t = i / (float)sampleRate;
            float progress = t / duration;

            // 1. 低频俯冲冲击：110Hz 迅速下潜至 26Hz
            float freq1 = 110f * MathF.Pow(26f / 110f, MathF.Pow(progress, 0.45f));
            p1 += 2.0 * Math.PI * freq1 / sampleRate;
            float subDive = MathF.Sin((float)p1);

            // 2. 持续深沉地脉共振 (38Hz 次低频)
            p2 += 2.0 * Math.PI * 38.0 / sampleRate;
            float subHum = MathF.Sin((float)p2) * 0.45f;

            // 3. 初始重击钝挫 (0 ~ 30ms 重拳冲击)
            float thud = (t < 0.035f) ? MathF.Sin(t / 0.035f * MathHelper.Pi) * 0.6f : 0f;

            // 包络：强劲瞬态冲击 -> 饱满次低频延音 -> 沉稳消散
            float env = MathF.Exp(-progress * 3.8f);
            float mix = (subDive * 0.75f + subHum + thud) * env;

            // 软饱和过载，赋予深沉冲击质感
            samples[i] = MathHelper.Clamp(MathF.Tanh(mix * 1.6f) * 0.95f, -1.0f, 1.0f);
        }

        return BuildWavPcm16(samples, sampleRate);
    }

    /// <summary>
    /// 18. 选卡确认强化音：清澈明亮的大三和弦琶音升华 (Radiant Upgrade Fanfare Arpeggio)。
    /// C5 -> E5 -> G5 -> C6 快速阶梯涌动，赋予卡牌强化获得神力的正反馈。
    /// </summary>
    public static byte[] GenerateCardPick()
    {
        int sampleRate = DefaultSampleRate;
        float duration = 0.58f;
        int length = (int)(sampleRate * duration);
        float[] samples = new float[length];

        float[] notes = { 523.25f, 659.25f, 783.99f, 1046.50f };
        double[] phases = new double[notes.Length];

        for (int i = 0; i < length; i++)
        {
            float t = i / (float)sampleRate;
            float mix = 0f;

            for (int k = 0; k < notes.Length; k++)
            {
                float noteStart = k * 0.065f;
                if (t >= noteStart)
                {
                    float noteT = t - noteStart;
                    phases[k] += 2.0 * Math.PI * notes[k] / sampleRate;
                    float sin = MathF.Sin((float)phases[k]);
                    float overtone = MathF.Sin((float)(phases[k] * 2.0)) * 0.28f;
                    float noteEnv = MathF.Exp(-noteT * 5.2f);
                    mix += (sin + overtone) * noteEnv * 0.26f;
                }
            }

            samples[i] = MathHelper.Clamp(MathF.Tanh(mix * 1.2f) * 0.85f, -1.0f, 1.0f);
        }

        return BuildWavPcm16(samples, sampleRate);
    }

    /// <summary>
    /// 19. 剧毒腐蚀声：柔和细腻、三重起伏的“呲-呲-呲”气泡腐蚀嘶嘶音 (Soft Acid Fizz "Siss-Siss-Siss")。
    /// 严格控制音量与高频毛刺，杜绝嘈杂刺耳。
    /// </summary>
    public static byte[] GeneratePoisonSizzle()
    {
        int sampleRate = DefaultSampleRate;
        float duration = 0.28f;
        int length = (int)(sampleRate * duration);
        float[] samples = new float[length];
        var rand = new Random(173);

        // 状态变量（双极带通滤波器）
        float lp = 0f, bp = 0f;
        float centerFreq = 3400f; // 典型的温和气体/酸液“呲”声频段
        float f = 2f * MathF.Sin(MathF.PI * centerFreq / sampleRate);
        float q = 0.45f; // 温和的阻尼因子，消除金属感尖锐啸叫

        // 三段“呲-呲-呲”的脉冲起始时间与峰值
        float[] burstStarts = { 0.00f, 0.085f, 0.170f };
        float burstDuration = 0.065f;

        for (int i = 0; i < length; i++)
        {
            float t = i / (float)sampleRate;

            // 白噪声源
            float white = (float)(rand.NextDouble() * 2.0 - 1.0);

            // 带通滤波
            lp += f * bp;
            float hp = white - lp - q * bp;
            bp += f * hp;

            // 计算三重“呲-呲-呲”包络叠加
            float burstEnvelope = 0f;
            for (int k = 0; k < burstStarts.Length; k++)
            {
                float bStart = burstStarts[k];
                if (t >= bStart && t < bStart + burstDuration)
                {
                    float localT = (t - bStart) / burstDuration; // 0 ~ 1
                    // 快速上升（15%）+ 柔和指数衰减（85%）
                    float localEnv = localT < 0.15f
                        ? localT / 0.15f
                        : MathF.Exp(-(localT - 0.15f) * 4.8f);

                    // 微微随节奏衰减，第1声最清晰，后两声更柔
                    float intensity = 1.0f - k * 0.15f;
                    burstEnvelope += localEnv * intensity;
                }
            }

            // 轻微随机气泡微抖动 (Bubble Fizz Micro-jitters)
            float bubbleJitter = 1.0f + 0.25f * (float)(rand.NextDouble() - 0.5);

            // 整体音量严格控制在 0.22f 以内，极为温润柔和，完全不吵
            float softGain = 0.22f;
            samples[i] = MathHelper.Clamp(bp * burstEnvelope * bubbleJitter * softGain, -1.0f, 1.0f);
        }

        return BuildWavPcm16(samples, sampleRate);
    }

    /// <summary>
    /// 20. 时空折跃瞬移音：超快瞬态相位折跃与疾速破空抽气破音 (Ultra-Fast Quantum Blink & Snap Whoosh)。
    /// 极短时值 (135ms)，瞬间高频下潜与真空吸附抽空感，极致体现“快”与“瞬身”。
    /// </summary>
    public static byte[] GenerateTeleport()
    {
        int sampleRate = DefaultSampleRate;
        float duration = 0.135f; // 仅 135ms，极速干脆
        int length = (int)(sampleRate * duration);
        float[] samples = new float[length];
        var rand = new Random(209);

        double phase = 0;
        double fmPhase = 0;
        float lp = 0f, bp = 0f;

        for (int i = 0; i < length; i++)
        {
            float t = i / (float)sampleRate;
            float progress = t / duration; // 0 ~ 1

            // 1. 瞬态高频快速下坠 Chirp (4800Hz 瞬间在 40ms 内暴降至 720Hz 再收拢至 320Hz)
            float freq = progress < 0.35f
                ? MathHelper.Lerp(4800f, 720f, MathF.Pow(progress / 0.35f, 0.45f))
                : MathHelper.Lerp(720f, 320f, (progress - 0.35f) / 0.65f);

            // 细微量子相位调制 (FM Phase Modulation)
            fmPhase += 2.0 * Math.PI * 850.0 / sampleRate;
            float fmMod = MathF.Sin((float)fmPhase) * 160f;

            phase += 2.0 * Math.PI * MathF.Max(60f, freq + fmMod) / sampleRate;
            float sine = MathF.Sin((float)phase);
            float harmonic = MathF.Sin((float)(phase * 2.0)) * 0.28f;

            // 极速起音包络：3ms 极速推至顶峰，随后陡峭指数衰减，绝不拖泥带水
            float chirpEnv = progress < 0.025f
                ? progress / 0.025f
                : MathF.Exp(-(progress - 0.025f) * 11.5f);

            // 2. 破空残影气流与真空抽气感 (Fast Whoosh & Vacuum Snap)
            float white = (float)(rand.NextDouble() * 2.0 - 1.0);
            float bpCutoff = MathHelper.Lerp(3600f, 600f, MathF.Pow(progress, 0.5f));
            float f = 2f * MathF.Sin(MathF.PI * bpCutoff / sampleRate);
            lp += f * bp;
            float hp = white - lp - 0.42f * bp;
            bp += f * hp;

            float noiseEnv = progress < 0.04f
                ? progress / 0.04f
                : MathF.Exp(-(progress - 0.04f) * 9.0f);

            // 混合：高亢瞬移折跃啸叫 + 破空推力
            float mix = (sine + harmonic) * chirpEnv * 0.92f + bp * noiseEnv * 0.65f;

            samples[i] = MathHelper.Clamp(MathF.Tanh(mix * 1.5f) * 0.95f, -1.0f, 1.0f);
        }

        return BuildWavPcm16(samples, sampleRate);
    }

    /// <summary>
    /// 21. 子弹撞墙碎裂：子弹无剩余反跳次数撞击硬质墙面/地形时的碎弹与撞击音 (Solid Wall Impact & Shatter Crack)。
    /// 包含瞬间硬接触碰撞点 (1800Hz -> 260Hz 骤降冲击) 与清脆石砾碎屑高频白噪带通共鸣 (2500Hz 碎弹散落)。
    /// </summary>
    public static byte[] GenerateWallHit()
    {
        int sampleRate = DefaultSampleRate;
        float duration = 0.085f;
        int length = (int)(sampleRate * duration);
        float[] samples = new float[length];
        var rand = new Random(307);

        double phase = 0;
        float lp = 0f, bp = 0f;

        for (int i = 0; i < length; i++)
        {
            float t = i / (float)sampleRate;
            float progress = t / duration;

            // 1. 硬质撞击基核瞬态：在 0 ~ 20ms 内由 1800Hz 极速下潜至 260Hz 的实体碰撞敲击
            float freq = progress < 0.25f
                ? MathHelper.Lerp(1800f, 260f, progress / 0.25f)
                : MathHelper.Lerp(260f, 120f, (progress - 0.25f) / 0.75f);
            phase += 2.0 * Math.PI * freq / sampleRate;
            float impact = MathF.Sin((float)phase);

            // 极速起音与陡峭指数衰减
            float impactEnv = progress < 0.015f
                ? progress / 0.015f
                : MathF.Exp(-(progress - 0.015f) * 22f);

            // 2. 子弹碎裂与碎石微粒崩落白噪 (2200Hz ~ 2800Hz 带通微粒噪声)
            float white = (float)(rand.NextDouble() * 2.0 - 1.0);
            float bpCutoff = 2500f * (1.0f - progress * 0.45f);
            float f = 2f * MathF.Sin(MathF.PI * bpCutoff / sampleRate);
            lp += f * bp;
            float hp = white - lp - 0.45f * bp;
            bp += f * hp;

            float debrisEnv = progress < 0.02f
                ? progress / 0.02f
                : MathF.Exp(-(progress - 0.02f) * 16f);

            // 3. 混合并加入微饱和
            float mix = impact * impactEnv * 0.75f + bp * debrisEnv * 0.45f;
            samples[i] = MathHelper.Clamp(MathF.Tanh(mix * 1.6f) * 0.85f, -1.0f, 1.0f);
        }

        return BuildWavPcm16(samples, sampleRate);
    }
}

