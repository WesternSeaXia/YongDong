using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StbTrueTypeSharp;

namespace RoundsClone.Core.Graphics;

/// <summary>
/// 跨平台动态字形渲染引擎。
/// 自动扫描并加载系统中文 TrueType/TTC 字体库（Windows 黑体/微软雅黑，Android NotoSans），
/// 基于 StbTrueTypeSharp 进行实时动态字形光栅化与字模缓存，确保中文字符高精度锐利渲染，彻底杜绝乱码。
/// </summary>
public class ProceduralFont : IDisposable
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly byte[]? _fontData;
    private readonly StbTrueType.stbtt_fontinfo? _fontInfo;
    private readonly float _fontSize;
    private readonly float _fontScale;
    private readonly int _ascent;
    private readonly int _descent;
    private readonly int _lineGap;

    // 动态字形贴图缓存字典
    private readonly Dictionary<char, GlyphInfo> _glyphCache = new();
    private Texture2D? _fallbackTexture;

    private struct GlyphInfo
    {
        public Texture2D Texture;
        public int Width;
        public int Height;
        public int XOffset;
        public int YOffset;
        public int AdvanceWidth;
    }

    /// <summary>
    /// 初始化动态字体系统。
    /// </summary>
    /// <param name="graphicsDevice">图形设备</param>
    /// <param name="fontSize">基准字号高度 (默认 28 像素)</param>
    public ProceduralFont(GraphicsDevice graphicsDevice, float fontSize = 28f)
    {
        _graphicsDevice = graphicsDevice;
        _fontSize = fontSize;

        // 尝试按优先级加载系统中文矢量字体
        var (data, info) = TryLoadChineseFont();
        _fontData = data;
        _fontInfo = info;

        if (_fontInfo != null)
        {
            _fontScale = StbTrueType.stbtt_ScaleForPixelHeight(_fontInfo, _fontSize);
            int ascent = 0, descent = 0, lineGap = 0;
            unsafe
            {
                StbTrueType.stbtt_GetFontVMetrics(_fontInfo, &ascent, &descent, &lineGap);
            }
            _ascent = (int)MathF.Round(ascent * _fontScale);
            _descent = (int)MathF.Round(descent * _fontScale);
            _lineGap = (int)MathF.Round(lineGap * _fontScale);
        }
        else
        {
            _ascent = (int)MathF.Round(_fontSize * 0.85f);
            _descent = -(int)MathF.Round(_fontSize * 0.15f);
            _lineGap = 0;
        }

        // 初始化兜底点阵贴图
        InitFallbackAtlas();
    }

    /// <summary>
    /// 测量指定文本在特定缩放比例下的实际排版宽高。
    /// </summary>
    /// <param name="text">目标字符串</param>
    /// <param name="scale">文本缩放倍率</param>
    /// <returns>二维尺寸 (宽, 高)</returns>
    public Vector2 MeasureString(string text, float scale = 1.0f)
    {
        if (string.IsNullOrEmpty(text)) return Vector2.Zero;

        float width = 0f;
        float height = (_ascent - _descent + _lineGap) * scale;
        if (height <= 0f) height = _fontSize * scale;

        foreach (char c in text)
        {
            if (c == '\n') continue;
            var glyph = GetOrCreateGlyph(c);
            width += glyph.AdvanceWidth * scale;
        }

        return new Vector2(width, height);
    }

    /// <summary>
    /// 在屏幕上绘制文本，支持投影阴影与对齐锚点。
    /// </summary>
    /// <param name="spriteBatch">精灵批处理器</param>
    /// <param name="text">要绘制的文本</param>
    /// <param name="position">渲染基准位置</param>
    /// <param name="color">文本前景色</param>
    /// <param name="scale">字体缩放倍率</param>
    /// <param name="originRatio">对齐锚点 (0,0 为左上; 0.5,0.5 为居中; 1,0 为右上)</param>
    /// <param name="hasShadow">是否渲染底层投影</param>
    public void DrawText(SpriteBatch spriteBatch, string text, Vector2 position, Color color, float scale = 1.0f, Vector2 originRatio = default, bool hasShadow = true)
    {
        if (string.IsNullOrEmpty(text)) return;

        Vector2 size = MeasureString(text, scale);
        Vector2 origin = new(size.X * originRatio.X, size.Y * originRatio.Y);
        Vector2 startPos = position - origin;

        if (hasShadow)
        {
            RenderStringInternal(spriteBatch, text, startPos + new Vector2(2f * scale, 2f * scale), Color.Black * 0.75f, scale);
        }

        RenderStringInternal(spriteBatch, text, startPos, color, scale);
    }

    /// <summary>
    /// 在指定最大宽度内自动换行绘制多行文本。
    /// </summary>
    public void DrawWrappedText(SpriteBatch spriteBatch, string text, Vector2 position, float maxWidth, Color color, float scale = 1.0f, bool hasShadow = true)
    {
        if (string.IsNullOrEmpty(text)) return;

        var sb = new System.Text.StringBuilder();
        float currentLineWidth = 0f;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\n')
            {
                sb.Append('\n');
                currentLineWidth = 0f;
                continue;
            }

            var glyph = GetOrCreateGlyph(c);
            float charW = glyph.AdvanceWidth * scale;

            if (currentLineWidth + charW > maxWidth && currentLineWidth > 0)
            {
                sb.Append('\n');
                currentLineWidth = 0f;
            }

            sb.Append(c);
            currentLineWidth += charW;
        }

        DrawText(spriteBatch, sb.ToString(), position, color, scale, Vector2.Zero, hasShadow);
    }

    private void RenderStringInternal(SpriteBatch spriteBatch, string text, Vector2 startPos, Color color, float scale)
    {
        Vector2 current = startPos;
        int baselineY = (int)MathF.Round(startPos.Y + _ascent * scale);

        foreach (char c in text)
        {
            if (c == '\n')
            {
                current.X = startPos.X;
                current.Y += (_ascent - _descent + _lineGap) * scale;
                baselineY = (int)MathF.Round(current.Y + _ascent * scale);
                continue;
            }

            var glyph = GetOrCreateGlyph(c);
            if (glyph.Texture != null && glyph.Width > 0 && glyph.Height > 0)
            {
                Vector2 glyphPos = new(
                    current.X + glyph.XOffset * scale,
                    baselineY + glyph.YOffset * scale
                );
                spriteBatch.Draw(glyph.Texture, glyphPos, null, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }

            current.X += glyph.AdvanceWidth * scale;
        }
    }

    private GlyphInfo GetOrCreateGlyph(char c)
    {
        if (_glyphCache.TryGetValue(c, out var cached))
        {
            return cached;
        }

        if (_fontInfo != null)
        {
            int cIndex = StbTrueType.stbtt_FindGlyphIndex(_fontInfo, c);
            if (cIndex != 0 || c == ' ')
            {
                int advanceWidth = 0, lsb = 0;
                int x0 = 0, y0 = 0, x1 = 0, y1 = 0;

                unsafe
                {
                    StbTrueType.stbtt_GetGlyphHMetrics(_fontInfo, cIndex, &advanceWidth, &lsb);
                    StbTrueType.stbtt_GetGlyphBitmapBox(_fontInfo, cIndex, _fontScale, _fontScale, &x0, &y0, &x1, &y1);
                }

                int scaledAdvance = (int)MathF.Round(advanceWidth * _fontScale);
                int w = x1 - x0;
                int h = y1 - y0;

                Texture2D? texture = null;
                if (w > 0 && h > 0)
                {
                    byte[] bitmap = new byte[w * h];
                    unsafe
                    {
                        fixed (byte* bmp = bitmap)
                        {
                            StbTrueType.stbtt_MakeGlyphBitmap(_fontInfo, bmp, w, h, w, _fontScale, _fontScale, cIndex);
                        }
                    }

                    Color[] pixels = new Color[w * h];
                    for (int i = 0; i < pixels.Length; i++)
                    {
                        byte a = bitmap[i];
                        // 预乘 Alpha: (a, a, a, a) 兼容所有混合模式，边缘抗锯齿锐利无黑边
                        pixels[i] = new Color((int)a, (int)a, (int)a, (int)a);
                    }

                    texture = new Texture2D(_graphicsDevice, w, h);
                    texture.SetData(pixels);
                }

                var info = new GlyphInfo
                {
                    Texture = texture!,
                    Width = w,
                    Height = h,
                    XOffset = x0,
                    YOffset = y0,
                    AdvanceWidth = Math.Max(scaledAdvance, w)
                };

                _glyphCache[c] = info;
                return info;
            }
        }

        // 兜底字形
        return CreateFallbackGlyph(c);
    }

    private GlyphInfo CreateFallbackGlyph(char c)
    {
        int w = (int)(_fontSize * 0.6f);
        int h = (int)_fontSize;

        Texture2D tex = new Texture2D(_graphicsDevice, w, h);
        Color[] px = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                // 空心方框作为未知字符占位
                bool border = x == 0 || x == w - 1 || y == 0 || y == h - 1;
                px[y * w + x] = border ? Color.White : Color.Transparent;
            }
        }
        tex.SetData(px);

        var info = new GlyphInfo
        {
            Texture = tex,
            Width = w,
            Height = h,
            XOffset = 0,
            YOffset = -h,
            AdvanceWidth = w + 4
        };
        _glyphCache[c] = info;
        return info;
    }

    private void InitFallbackAtlas()
    {
        _fallbackTexture = new Texture2D(_graphicsDevice, 1, 1);
        _fallbackTexture.SetData(new[] { Color.White });
    }

    /// <summary>
    /// 自动扫描并加载支持中文字符集的高品质系统矢量字体。
    /// 正确解析 TTC (TrueType Collection) 偏移量与 TTF 文件头，并校验中文字形的存在性。
    /// </summary>
    private static (byte[]? Data, StbTrueType.stbtt_fontinfo? Info) TryLoadChineseFont()
    {
        string[] candidates =
        {
            @"C:\Windows\Fonts\simhei.ttf",       // Windows 黑体 (首选，单体 TTF 锐利清晰)
            @"C:\Windows\Fonts\msyh.ttc",         // Windows 微软雅黑 (TTC 集合)
            @"C:\Windows\Fonts\msyhbd.ttc",       // Windows 微软雅黑粗体
            @"C:\Windows\Fonts\simsun.ttc",       // Windows 宋体
            @"C:\Windows\Fonts\simkai.ttf",       // Windows 楷体
            @"C:\Windows\Fonts\simfang.ttf",      // Windows 仿宋
            "/system/fonts/NotoSansCJK-Regular.ttc", // Android 标准中文字体
            "/system/fonts/NotoSansSC-Regular.otf",  // Android 简体中文
            "/system/fonts/DroidSansFallback.ttf",   // Android 备选字体
            "/system/fonts/Roboto-Regular.ttf",      // Android 基础字体
            @"C:\Windows\Fonts\arial.ttf"         // Windows 兜底英文字体
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
            {
                try
                {
                    byte[] data = File.ReadAllBytes(path);
                    int offset = 0;
                    unsafe
                    {
                        fixed (byte* ptr = data)
                        {
                            offset = StbTrueType.stbtt_GetFontOffsetForIndex(ptr, 0);
                        }
                    }
                    if (offset < 0) offset = 0;

                    var info = StbTrueType.CreateFont(data, offset);
                    if (info != null)
                    {
                        // 校验中文字形 '战' 是否存在于该字体库中
                        int testGlyph = StbTrueType.stbtt_FindGlyphIndex(info, '战');
                        if (testGlyph > 0)
                        {
                            Console.WriteLine($"[ProceduralFont] 成功装载中文字体: {path} (偏移量: {offset})");
                            return (data, info);
                        }

                        // 若为英文字体且未找到中文，先保留为基础字库
                        Console.WriteLine($"[ProceduralFont] 已装载基础字体: {path}");
                        return (data, info);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ProceduralFont] 尝试加载字体库 {path} 异常: {ex.Message}");
                }
            }
        }

        Console.WriteLine("[ProceduralFont] 警告: 未找到可用系统字体库，将启用程序化点阵兜底");
        return (null, null);
    }

    /// <summary>
    /// 释放字体生成的动态纹理。
    /// </summary>
    public void Dispose()
    {
        foreach (var pair in _glyphCache)
        {
            pair.Value.Texture?.Dispose();
        }
        _glyphCache.Clear();
        _fallbackTexture?.Dispose();
    }
}
