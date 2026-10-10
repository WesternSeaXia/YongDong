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
/// 提供斗士外观主题色挑选、初始副武器（格挡 / 冲刺）装配，以及实时 Q 弹形变手脚联动动态预览。
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
    private Rectangle _btnConfirm;
    private Rectangle _btnBack;

    public CharacterSelectScene(SceneManager manager, Action exitAction)
    {
        _manager = manager;
        _exitAction = exitAction;

        _previewActor = new Actor(new Vector2(VirtualViewport.VirtualWidth * 0.5f, 380f), ColorPalette.PlayerBlue);

        int cx = VirtualViewport.VirtualWidth / 2;
        _btnConfirm = new Rectangle(cx - 200, 930, 400, 70);
        _btnBack = new Rectangle(80, 60, 160, 50);
    }

    public void Enter()
    {
        _previewActor.ThemeColor = _colorOptions[_selectedColorIndex].Color;
        _previewActor.SubWeapon = _subWeaponOptions[_selectedSubWeaponIndex].Factory();
    }

    public void Update(float dt)
    {
        _previewTimer += dt;
        Vector2 mousePos = _manager.Input.VirtualMousePosition;
        bool click = _manager.Input.IsLeftClickJustPressed();

        // 模拟瞄准朝向鼠标
        Vector2 aim = MathHelper2D.SafeNormalize(mousePos - _previewActor.Body.Position);
        var mockInput = new ControlInputState
        {
            AimVector = aim,
            MoveVector = new Vector2(MathF.Sin(_previewTimer * 2f) * 0.4f, 0f)
        };
        _previewActor.Update(dt, mockInput, null!, _manager.Particles, _manager.Shake, _ => { });

        // 色彩点击切换
        float colorStartX = VirtualViewport.VirtualWidth * 0.5f - 240f;
        for (int i = 0; i < _colorOptions.Length; i++)
        {
            Rectangle colorRect = new((int)(colorStartX + i * 130), 600, 100, 50);
            if (colorRect.Contains(mousePos) && click)
            {
                _selectedColorIndex = i;
                _previewActor.ThemeColor = _colorOptions[i].Color;
            }
        }

        // 副武器点击切换
        int cardW = 340;
        int cardH = 130;
        int gap = 50;
        int totalW = cardW * 2 + gap;
        int startX = (VirtualViewport.VirtualWidth - totalW) / 2;

        for (int i = 0; i < _subWeaponOptions.Length; i++)
        {
            Rectangle weaponCard = new(startX + i * (cardW + gap), 720, cardW, cardH);
            if (weaponCard.Contains(mousePos) && click)
            {
                _selectedSubWeaponIndex = i;
                _previewActor.SubWeapon = _subWeaponOptions[i].Factory();
            }
        }

        // 确认配置并进入关卡三选一
        if (_btnConfirm.Contains(mousePos) && click)
        {
            _manager.PlayerColor = _colorOptions[_selectedColorIndex].Color;
            _manager.PlayerSubWeaponFactory = _subWeaponOptions[_selectedSubWeaponIndex].Factory;

            // AI 使用互补配置
            _manager.AiColor = _selectedColorIndex == 1 ? ColorPalette.PlayerBlue : ColorPalette.PlayerRed;
            _manager.AiSubWeaponFactory = _selectedSubWeaponIndex == 0 ? () => new DashSubWeapon() : () => new BlockSubWeapon();

            _manager.ResetMatch();
            _manager.ChangeScene(new StageSelectScene(_manager, _exitAction));
        }

        // 返回主菜单
        if (_btnBack.Contains(mousePos) && click)
        {
            _manager.ChangeScene(new MainMenuScene(_manager, _exitAction));
        }
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        var batcher = _manager.ShapeBatcher;
        var font = _manager.Font;
        Vector2 mousePos = _manager.Input.VirtualMousePosition;

        // 顶部标题
        font.DrawText(spriteBatch, "角色选择", new Vector2(VirtualViewport.VirtualWidth * 0.5f, 110f), Color.White, 1.8f, new Vector2(0.5f, 0.5f));

        // 角色预览基座环
        Vector2 pPos = _previewActor.Body.Position;
        batcher.DrawCircleOutline(spriteBatch, pPos + new Vector2(0, 45f), 70f, _previewActor.ThemeColor * 0.4f, 3f);
        _previewActor.Draw(batcher, spriteBatch);

        // 1. 色彩选择色块
        float colorStartX = VirtualViewport.VirtualWidth * 0.5f - 240f;
        for (int i = 0; i < _colorOptions.Length; i++)
        {
            Rectangle colorRect = new((int)(colorStartX + i * 130), 600, 100, 50);
            bool selected = i == _selectedColorIndex;
            bool hover = colorRect.Contains(mousePos);

            batcher.DrawRoundedRect(spriteBatch, colorRect, 6f, _colorOptions[i].Color * (selected ? 0.9f : 0.4f));
            batcher.DrawRectangleOutline(spriteBatch, colorRect, selected ? Color.White : (hover ? ColorPalette.TextWhite : ColorPalette.WallOutline), selected ? 3f : 1.5f);
        }

        // 2. 副武器选择卡片
        int cardW = 340;
        int cardH = 130;
        int gap = 50;
        int totalW = cardW * 2 + gap;
        int startX = (VirtualViewport.VirtualWidth - totalW) / 2;

        for (int i = 0; i < _subWeaponOptions.Length; i++)
        {
            Rectangle weaponCard = new(startX + i * (cardW + gap), 720, cardW, cardH);
            bool selected = i == _selectedSubWeaponIndex;
            bool hover = weaponCard.Contains(mousePos);

            Color bg = selected ? ColorPalette.ButtonActive : (hover ? ColorPalette.ButtonHover : ColorPalette.BackgroundCard);
            Color border = selected ? ColorPalette.PlayerBlue : ColorPalette.WallOutline;

            batcher.DrawRoundedRect(spriteBatch, weaponCard, 10f, bg);
            batcher.DrawRectangleOutline(spriteBatch, weaponCard, border, selected ? 3.5f : 2f);

            // 绘制副武器图标与名称
            Vector2 iconPos = new(weaponCard.X + 75, weaponCard.Center.Y);
            ProceduralIcons.DrawIcon(batcher, spriteBatch, _subWeaponOptions[i].Icon, iconPos, 55f, selected ? Color.White : ColorPalette.PlayerBlue);

            font.DrawText(spriteBatch, _subWeaponOptions[i].Name, new Vector2(weaponCard.X + 135, weaponCard.Center.Y), selected ? Color.White : ColorPalette.TextWhite, 1.1f, new Vector2(0f, 0.5f));
        }

        // 3. 底部确认与返回按钮
        bool confirmHover = _btnConfirm.Contains(mousePos);
        batcher.DrawRoundedRect(spriteBatch, _btnConfirm, 10f, confirmHover ? ColorPalette.PlayerBlue : ColorPalette.ButtonHover);
        batcher.DrawRectangleOutline(spriteBatch, _btnConfirm, Color.White, confirmHover ? 3.5f : 2f);
        font.DrawText(spriteBatch, "开始战斗", new Vector2(_btnConfirm.Center.X, _btnConfirm.Center.Y), Color.White, 1.1f, new Vector2(0.5f, 0.5f));

        bool backHover = _btnBack.Contains(mousePos);
        batcher.DrawRoundedRect(spriteBatch, _btnBack, 6f, backHover ? ColorPalette.ButtonHover : ColorPalette.BackgroundCard);
        batcher.DrawRectangleOutline(spriteBatch, _btnBack, ColorPalette.WallOutline, 1.5f);
        font.DrawText(spriteBatch, "返回", new Vector2(_btnBack.Center.X, _btnBack.Center.Y), ColorPalette.TextWhite, 0.8f, new Vector2(0.5f, 0.5f));
    }

    public void Exit() { }
}
