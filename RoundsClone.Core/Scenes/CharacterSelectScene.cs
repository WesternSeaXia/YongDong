using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RoundsClone.Core.Common;
using RoundsClone.Core.Entities;
using RoundsClone.Core.Graphics;
using RoundsClone.Core.SubWeapons;

namespace RoundsClone.Core.Scenes;

/// <summary>
/// 角色配置与初始副武器选择场景。
/// 提供斗士外观主题色挑选、眼神表情定制（可爱/猥琐/搞怪）、初始副武器（格挡/冲刺）装配，以及实时线条手脚 IK 动态预览。
/// </summary>
public class CharacterSelectScene : IScene
{
    private readonly SceneManager _manager;
    private readonly Action _exitAction;

    // 候选色彩
    private readonly (string Name, Color Color)[] _colorOptions =
    {
        ("霓虹青蓝", ColorPalette.PlayerBlue),
        ("熔岩赤红", ColorPalette.PlayerRed),
        ("生化翡翠", ColorPalette.PlayerGreen),
        ("太阳耀金", ColorPalette.PlayerYellow)
    };
    private int _selectedColorIndex = 0;

    // 候选眼神表情配置（含经典、可爱、猥琐、搞怪风格）
    private readonly (string Name, EyeStyle Style)[] _eyeOptions =
    {
        ("经典斗士", EyeStyle.Classic),
        ("水汪萌眼", EyeStyle.Cute),
        ("猥琐眯眼", EyeStyle.Sneaky),
        ("智慧斗鸡", EyeStyle.Derp),
        ("腹黑笑眼", EyeStyle.Happy),
        ("狂暴死鱼", EyeStyle.Crazy)
    };
    private int _selectedEyeIndex = 0;

    // 候选初始副武器
    private readonly (string Name, string Desc, IconType Icon, Func<ISubWeapon> Factory)[] _subWeaponOptions =
    {
        ("力场格挡", "展开反弹护盾，偏转反弹一切子弹并施加强劲击退脉冲。", IconType.Shield, () => new BlockSubWeapon()),
        ("疾风冲刺", "沿方向爆发瞬态矢量冲量，突破重围并附带无敌帧闪避。", IconType.Dash, () => new DashSubWeapon())
    };
    private int _selectedSubWeaponIndex = 0;

    // 预览实体
    private Actor _previewActor;
    private float _previewTimer = 0f;

    // UI 按钮区域
    private Rectangle _btnAiDifficulty;
    private Rectangle _btnConfirm;
    private Rectangle _btnBack;
    private int _lastHoveredItem = -1;

    public CharacterSelectScene(SceneManager manager, Action exitAction)
    {
        _manager = manager;
        _exitAction = exitAction;

        float actorY = VirtualViewport.IsMobile ? 275f : 325f;
        _previewActor = new Actor(new Vector2(VirtualViewport.VirtualWidth * 0.5f, actorY), ColorPalette.PlayerBlue)
        {
            MaxHealth = VirtualViewport.IsMobile ? 220f : 165f
        };
        _previewActor.RecalculateHealthScaling();

        int cx = VirtualViewport.VirtualWidth / 2;
        if (VirtualViewport.IsMobile)
        {
            _btnAiDifficulty = new Rectangle(cx - 290, 825, 580, 62);
            _btnConfirm = new Rectangle(cx - 290, 905, 580, 96);
            _btnBack = new Rectangle(50, 38, 220, 78);
        }
        else
        {
            _btnAiDifficulty = new Rectangle(cx - 220, 852, 440, 52);
            _btnConfirm = new Rectangle(cx - 200, 926, 400, 68);
            _btnBack = new Rectangle(80, 60, 160, 50);
        }
    }

    public void Enter()
    {
        _previewActor.ThemeColor = _colorOptions[_selectedColorIndex].Color;
        _previewActor.EyeStyle = _eyeOptions[_selectedEyeIndex].Style;
        _previewActor.SubWeapon = _subWeaponOptions[_selectedSubWeaponIndex].Factory();
    }

    public void Update(float dt)
    {
        _previewTimer += dt;
        Vector2 mousePos = _manager.Input.VirtualMousePosition;
        bool click = _manager.Input.IsLeftClickJustPressed();

        // 模拟瞄准朝向鼠标与左右轻快踱步，展示线条手脚步态与眼神互动
        Vector2 aim = MathHelper2D.SafeNormalize(mousePos - _previewActor.Body.Position);
        var mockInput = new ControlInputState
        {
            AimVector = aim,
            MoveVector = new Vector2(MathF.Sin(_previewTimer * 2.2f) * 0.55f, 0f)
        };
        _previewActor.Update(dt, mockInput, null!, _manager.Particles, _manager.Shake, _ => { });

        bool mobile = VirtualViewport.IsMobile;

        // 1. 色彩点击切换
        int colorW = mobile ? 190 : 100;
        int colorH = mobile ? 72 : 46;
        int colorStep = mobile ? 225 : 130;
        int colorY = mobile ? 430 : 500;
        float colorStartX = VirtualViewport.VirtualWidth * 0.5f - ((_colorOptions.Length - 1) * colorStep + colorW) * 0.5f;

        int eyeBtnW = mobile ? 245 : 136;
        int eyeBtnH = mobile ? 84 : 54;
        int eyeGap = mobile ? 22 : 18;
        int eyeY = mobile ? 545 : 595;
        int totalEyeW = _eyeOptions.Length * eyeBtnW + (_eyeOptions.Length - 1) * eyeGap;
        int eyeStartX = (VirtualViewport.VirtualWidth - totalEyeW) / 2;

        int cardW = mobile ? 540 : 340;
        int cardH = mobile ? 175 : 125;
        int gap = mobile ? 60 : 50;
        int cardY = mobile ? 675 : 710;
        int totalW = cardW * 2 + gap;
        int startX = (VirtualViewport.VirtualWidth - totalW) / 2;

        // 悬停交互音效探测
        int hovered = -1;
        for (int i = 0; i < _colorOptions.Length; i++)
        {
            Rectangle colorRect = new((int)(colorStartX + i * colorStep), colorY, colorW, colorH);
            if (colorRect.Contains(mousePos)) { hovered = 10 + i; break; }
        }
        if (hovered == -1)
        {
            for (int i = 0; i < _eyeOptions.Length; i++)
            {
                Rectangle eyeRect = new(eyeStartX + i * (eyeBtnW + eyeGap), eyeY, eyeBtnW, eyeBtnH);
                if (eyeRect.Contains(mousePos)) { hovered = 20 + i; break; }
            }
        }
        if (hovered == -1)
        {
            for (int i = 0; i < _subWeaponOptions.Length; i++)
            {
                Rectangle weaponCard = new(startX + i * (cardW + gap), cardY, cardW, cardH);
                if (weaponCard.Contains(mousePos)) { hovered = 30 + i; break; }
            }
        }
        if (hovered == -1)
        {
            if (_btnAiDifficulty.Contains(mousePos)) hovered = 39;
            else if (_btnConfirm.Contains(mousePos)) hovered = 40;
            else if (_btnBack.Contains(mousePos)) hovered = 41;
        }

        if (hovered != -1 && hovered != _lastHoveredItem)
        {
            _manager.Audio.PlayUiHover();
        }
        _lastHoveredItem = hovered;

        // 1. 色彩点击切换
        for (int i = 0; i < _colorOptions.Length; i++)
        {
            Rectangle colorRect = new((int)(colorStartX + i * colorStep), colorY, colorW, colorH);
            if (colorRect.Contains(mousePos) && click)
            {
                _manager.Audio.PlayUiClick();
                _selectedColorIndex = i;
                _previewActor.ThemeColor = _colorOptions[i].Color;
                _previewActor.Squash.ApplySquash(Vector2.UnitY, 0.22f);
            }
        }

        // 2. 眼神配置点击切换
        for (int i = 0; i < _eyeOptions.Length; i++)
        {
            Rectangle eyeRect = new(eyeStartX + i * (eyeBtnW + eyeGap), eyeY, eyeBtnW, eyeBtnH);
            if (eyeRect.Contains(mousePos) && click)
            {
                _manager.Audio.PlayUiClick();
                _selectedEyeIndex = i;
                _previewActor.EyeStyle = _eyeOptions[i].Style;
                _previewActor.Squash.ApplyStretch(Vector2.UnitY, 0.25f);
            }
        }

        // 3. 副武器点击切换
        for (int i = 0; i < _subWeaponOptions.Length; i++)
        {
            Rectangle weaponCard = new(startX + i * (cardW + gap), cardY, cardW, cardH);
            if (weaponCard.Contains(mousePos) && click)
            {
                _manager.Audio.PlayUiClick();
                _selectedSubWeaponIndex = i;
                _previewActor.SubWeapon = _subWeaponOptions[i].Factory();
                _previewActor.Squash.ApplySquash(Vector2.UnitY, 0.20f);
            }
        }

        // 4. AI 难度点击切换
        if (_btnAiDifficulty.Contains(mousePos) && click)
        {
            _manager.Audio.PlayUiClick();
            GameSettings.Current.AiDifficulty = (GameSettings.Current.AiDifficulty + 1) % 3;
            GameSettings.Save();
        }

        // 5. 确认配置并进入关卡三选一
        if (_btnConfirm.Contains(mousePos) && click)
        {
            _manager.Audio.PlayUiClick();
            _manager.PlayerColor = _colorOptions[_selectedColorIndex].Color;
            _manager.PlayerEyeStyle = _eyeOptions[_selectedEyeIndex].Style;
            _manager.PlayerSubWeaponFactory = _subWeaponOptions[_selectedSubWeaponIndex].Factory;

            // AI 使用互补色彩、副武器以及搞怪/猥琐眼神
            _manager.AiColor = _selectedColorIndex == 1 ? ColorPalette.PlayerBlue : ColorPalette.PlayerRed;
            _manager.AiEyeStyle = _eyeOptions[(_selectedEyeIndex + 2) % _eyeOptions.Length].Style;
            _manager.AiSubWeaponFactory = _selectedSubWeaponIndex == 0 ? () => new DashSubWeapon() : () => new BlockSubWeapon();

            _manager.ResetMatch();
            _manager.ChangeScene(new StageSelectScene(_manager, _exitAction));
        }

        // 6. 返回主菜单
        if (_btnBack.Contains(mousePos) && click)
        {
            _manager.Audio.PlayUiClick();
            _manager.ChangeScene(new MainMenuScene(_manager, _exitAction));
        }
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        var batcher = _manager.ShapeBatcher;
        var font = _manager.Font;
        Vector2 mousePos = _manager.Input.VirtualMousePosition;
        bool mobile = VirtualViewport.IsMobile;

        // 顶部标题
        font.DrawText(spriteBatch, "角色选择", new Vector2(VirtualViewport.VirtualWidth * 0.5f, mobile ? 75f : 95f), Color.White, mobile ? 2.3f : 1.8f, new Vector2(0.5f, 0.5f));

        // 角色预览基座平台与光环
        Vector2 pPos = _previewActor.Body.Position;
        float groundY = pPos.Y + _previewActor.Radius;
        batcher.DrawLine(spriteBatch, new Vector2(pPos.X - 105f, groundY + 2f), new Vector2(pPos.X + 105f, groundY + 2f), _previewActor.ThemeColor * 0.55f, 4f);
        batcher.DrawCircleOutline(spriteBatch, pPos, mobile ? 95f : 78f, _previewActor.ThemeColor * 0.28f, 2.5f);
        _previewActor.Draw(batcher, spriteBatch);

        // 1. 色彩选择色块
        int colorW = mobile ? 190 : 100;
        int colorH = mobile ? 72 : 46;
        int colorStep = mobile ? 225 : 130;
        int colorY = mobile ? 430 : 500;
        float colorStartX = VirtualViewport.VirtualWidth * 0.5f - ((_colorOptions.Length - 1) * colorStep + colorW) * 0.5f;

        for (int i = 0; i < _colorOptions.Length; i++)
        {
            Rectangle colorRect = new((int)(colorStartX + i * colorStep), colorY, colorW, colorH);
            bool selected = i == _selectedColorIndex;
            bool hover = colorRect.Contains(mousePos);

            batcher.DrawRoundedRect(spriteBatch, colorRect, 8f, _colorOptions[i].Color * (selected ? 0.92f : 0.42f));
            batcher.DrawRectangleOutline(spriteBatch, colorRect, selected ? Color.White : (hover ? ColorPalette.TextWhite : ColorPalette.WallOutline), selected ? 3.5f : 1.8f);
            if (mobile)
            {
                font.DrawText(spriteBatch, _colorOptions[i].Name, new Vector2(colorRect.Center.X, colorRect.Center.Y), Color.White, 1.0f, new Vector2(0.5f, 0.5f));
            }
        }

        // 2. 眼神配置选择按钮组
        int eyeBtnW = mobile ? 245 : 136;
        int eyeBtnH = mobile ? 84 : 54;
        int eyeGap = mobile ? 22 : 18;
        int eyeY = mobile ? 545 : 595;
        int totalEyeW = _eyeOptions.Length * eyeBtnW + (_eyeOptions.Length - 1) * eyeGap;
        int eyeStartX = (VirtualViewport.VirtualWidth - totalEyeW) / 2;

        for (int i = 0; i < _eyeOptions.Length; i++)
        {
            Rectangle eyeRect = new(eyeStartX + i * (eyeBtnW + eyeGap), eyeY, eyeBtnW, eyeBtnH);
            bool selected = i == _selectedEyeIndex;
            bool hover = eyeRect.Contains(mousePos);

            Color bg = selected ? ColorPalette.ButtonActive : (hover ? ColorPalette.ButtonHover : ColorPalette.BackgroundCard);
            Color border = selected ? _previewActor.ThemeColor : ColorPalette.WallOutline;

            batcher.DrawRoundedRect(spriteBatch, eyeRect, 10f, bg);
            batcher.DrawRectangleOutline(spriteBatch, eyeRect, border, selected ? 3.5f : 2f);
            font.DrawText(spriteBatch, _eyeOptions[i].Name, new Vector2(eyeRect.Center.X, eyeRect.Center.Y), selected ? Color.White : ColorPalette.TextWhite, mobile ? 1.18f : 0.82f, new Vector2(0.5f, 0.5f));
        }

        // 3. 副武器选择卡片
        int cardW = mobile ? 540 : 340;
        int cardH = mobile ? 175 : 125;
        int gap = mobile ? 60 : 50;
        int cardY = mobile ? 675 : 710;
        int totalW = cardW * 2 + gap;
        int startX = (VirtualViewport.VirtualWidth - totalW) / 2;

        for (int i = 0; i < _subWeaponOptions.Length; i++)
        {
            Rectangle weaponCard = new(startX + i * (cardW + gap), cardY, cardW, cardH);
            bool selected = i == _selectedSubWeaponIndex;
            bool hover = weaponCard.Contains(mousePos);

            Color bg = selected ? ColorPalette.ButtonActive : (hover ? ColorPalette.ButtonHover : ColorPalette.BackgroundCard);
            Color border = selected ? ColorPalette.PlayerBlue : ColorPalette.WallOutline;

            batcher.DrawRoundedRect(spriteBatch, weaponCard, 12f, bg);
            batcher.DrawRectangleOutline(spriteBatch, weaponCard, border, selected ? 4f : 2f);

            Vector2 iconPos = new(weaponCard.X + (mobile ? 105 : 75), weaponCard.Center.Y);
            ProceduralIcons.DrawIcon(batcher, spriteBatch, _subWeaponOptions[i].Icon, iconPos, mobile ? 72f : 52f, selected ? Color.White : ColorPalette.PlayerBlue);

            font.DrawText(spriteBatch, _subWeaponOptions[i].Name, new Vector2(weaponCard.X + (mobile ? 185 : 135), weaponCard.Center.Y), selected ? Color.White : ColorPalette.TextWhite, mobile ? 1.5f : 1.1f, new Vector2(0f, 0.5f));
        }

        // 4. AI 难度直观切换按钮
        bool diffHover = _btnAiDifficulty.Contains(mousePos);
        int currentDiff = GameSettings.Current.AiDifficulty;
        string diffLabel = currentDiff switch
        {
            0 => "AI 难度: 【简单】 (新手练手 / 射击迟疑)  [点此切换]",
            1 => "AI 难度: 【普通】 (战术特工 / 均衡对抗)  [点此切换]",
            2 => "AI 难度: 【地狱】 (绝壁弹反 / 极限预判)  [点此切换]",
            _ => "AI 难度: 【普通】 (战术特工)  [点此切换]"
        };
        Color diffAccent = currentDiff switch
        {
            0 => ColorPalette.PlayerGreen,
            1 => ColorPalette.PlayerBlue,
            _ => ColorPalette.PlayerRed
        };

        batcher.DrawRoundedRect(spriteBatch, _btnAiDifficulty, mobile ? 10f : 8f, diffHover ? ColorPalette.ButtonHover : ColorPalette.BackgroundCard);
        batcher.DrawRectangleOutline(spriteBatch, _btnAiDifficulty, diffHover ? Color.White : diffAccent, diffHover ? 3.5f : 2.0f);
        batcher.DrawRectangle(spriteBatch, new Rectangle(_btnAiDifficulty.X, _btnAiDifficulty.Y, 8, _btnAiDifficulty.Height), diffAccent);
        font.DrawText(spriteBatch, diffLabel, new Vector2(_btnAiDifficulty.Center.X, _btnAiDifficulty.Center.Y), diffHover ? Color.White : diffAccent, mobile ? 1.05f : 0.84f, new Vector2(0.5f, 0.5f));

        // 5. 底部确认与返回按钮
        bool confirmHover = _btnConfirm.Contains(mousePos);
        batcher.DrawRoundedRect(spriteBatch, _btnConfirm, 12f, confirmHover ? ColorPalette.PlayerBlue : ColorPalette.ButtonHover);
        batcher.DrawRectangleOutline(spriteBatch, _btnConfirm, Color.White, confirmHover ? 4f : 2.5f);
        font.DrawText(spriteBatch, "开始战斗", new Vector2(_btnConfirm.Center.X, _btnConfirm.Center.Y), Color.White, mobile ? 1.55f : 1.1f, new Vector2(0.5f, 0.5f));

        bool backHover = _btnBack.Contains(mousePos);
        batcher.DrawRoundedRect(spriteBatch, _btnBack, 8f, backHover ? ColorPalette.ButtonHover : ColorPalette.BackgroundCard);
        batcher.DrawRectangleOutline(spriteBatch, _btnBack, ColorPalette.WallOutline, 2f);
        font.DrawText(spriteBatch, "返回", new Vector2(_btnBack.Center.X, _btnBack.Center.Y), ColorPalette.TextWhite, mobile ? 1.2f : 0.8f, new Vector2(0.5f, 0.5f));
    }

    public void Exit() { }
}
