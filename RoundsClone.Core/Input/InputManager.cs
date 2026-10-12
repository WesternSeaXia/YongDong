using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using RoundsClone.Core.Common;
using RoundsClone.Core.Entities;
using RoundsClone.Core.Graphics;

namespace RoundsClone.Core.Input;

/// <summary>
/// 跨平台统一输入控制器。
/// 兼顾 Windows 端的键盘+鼠标精准操作，以及 Android 移动端的触控双虚拟摇杆+动作按钮。
/// 支持在 PC 上通过鼠标或按键实时模拟触控，方便跨平台交互测试。
/// </summary>
public class InputManager
{
    private KeyboardState _prevKeyboard;
    private KeyboardState _currKeyboard;
    private MouseState _prevMouse;
    private MouseState _currMouse;

    /// <summary>
    /// 是否在屏幕上渲染并启用虚拟摇杆界面（移动端或 PC 调试模式）。
    /// </summary>
    public bool ShowVirtualControls { get; set; } = true;

    // 左侧虚拟移动摇杆（加大尺寸并移至左手拇指舒适区）
    public Vector2 LeftStickCenter = new(310f, 800f);
    public float StickRadius = 132f;
    private Vector2 _leftKnobOffset = Vector2.Zero;
    private bool _leftStickActive = false;
    private int _leftStickTouchId = -1;

    // 右侧瞄准射击摇杆（右手拇指主控区）
    public Vector2 RightStickCenter = new(1610f, 800f);
    private Vector2 _rightKnobOffset = Vector2.Zero;
    private bool _rightStickActive = false;
    private int _rightStickTouchId = -1;
    private Vector2 _lastTouchAim = Vector2.UnitX;

    // 右手内弧形副武器按钮（位于右摇杆左上方自然拇指扇区）
    public Vector2 SecondaryButtonCenter = new(1385f, 635f);
    public float ButtonRadius = 82f;
    private bool _secondaryButtonPressed = false;

    // 右手内弧形主跳跃按钮（位于右摇杆左下侧，方便边移动边右手点跳）
    public Vector2 JumpButtonCenter = new(1310f, 875f);
    public float JumpButtonRadius = 78f;

    // 左手辅助跳跃按钮（位于左摇杆正上方）
    public Vector2 LeftJumpButtonCenter = new(310f, 525f);
    public float LeftJumpButtonRadius = 72f;
    private bool _jumpButtonPressed = false;

    // PC 鼠标模拟拖拽摇杆锁定状态 (0: 无, 1: 左摇杆, 2: 右摇杆)
    private int _mouseLockedStick = 0;

    private bool _touchJustPressed = false;

    /// <summary>
    /// 鼠标（或手指触点）在虚拟 1920x1080 世界空间下的实时坐标。
    /// </summary>
    public Vector2 VirtualMousePosition { get; private set; }

    /// <summary>
    /// 更新当前输入设备快照并解算虚拟摇杆状态。
    /// </summary>
    public void Update(VirtualViewport viewport)
    {
        _prevKeyboard = _currKeyboard;
        _currKeyboard = Keyboard.GetState();

        _prevMouse = _currMouse;
        _currMouse = Mouse.GetState();

        // 映射鼠标到虚拟逻辑空间
        VirtualMousePosition = viewport.ScreenToVirtual(new Vector2(_currMouse.X, _currMouse.Y));

        // 重置单帧按键状态
        _secondaryButtonPressed = false;
        _jumpButtonPressed = false;
        _touchJustPressed = false;

        // 收集触屏手势或鼠标触控模拟
        TouchCollection touchPoints = TouchPanel.GetState();
        if (touchPoints.Count > 0)
        {
            _mouseLockedStick = 0;
            // 将首个触点同步映射到 VirtualMousePosition，使移动端菜单/选卡/选角色按钮可直接触屏点击
            VirtualMousePosition = viewport.ScreenToVirtual(touchPoints[0].Position);
            foreach (var tp in touchPoints)
            {
                if (tp.State == TouchLocationState.Pressed)
                {
                    VirtualMousePosition = viewport.ScreenToVirtual(tp.Position);
                    _touchJustPressed = true;
                    break;
                }
            }
            UpdateTouches(touchPoints, viewport);
        }
        else
        {
            // 若无真实触点，释放触屏摇杆锁定并支持鼠标模拟拖拽测试
            _leftStickTouchId = -1;
            _rightStickTouchId = -1;
            UpdateMouseVirtualTouches();
        }
    }

    private void UpdateTouches(TouchCollection touches, VirtualViewport viewport)
    {
        bool leftStillHeld = false;
        bool rightStillHeld = false;

        // 第一遍：优先追踪已锁定的摇杆 TouchId（即便手指划出摇杆圆圈边界甚至跨屏，只要手指未抬起就绝不断开）
        foreach (var touch in touches)
        {
            if (touch.State == TouchLocationState.Released || touch.State == TouchLocationState.Invalid)
                continue;

            Vector2 vPos = viewport.ScreenToVirtual(touch.Position);

            if (touch.Id == _leftStickTouchId)
            {
                leftStillHeld = true;
                _leftStickActive = true;
                _leftKnobOffset = MathHelper2D.ClampLength(vPos - LeftStickCenter, StickRadius);
            }
            else if (touch.Id == _rightStickTouchId)
            {
                rightStillHeld = true;
                _rightStickActive = true;
                _rightKnobOffset = MathHelper2D.ClampLength(vPos - RightStickCenter, StickRadius);
                if (_rightKnobOffset.LengthSquared() > 144f)
                {
                    _lastTouchAim = MathHelper2D.SafeNormalize(_rightKnobOffset);
                }
            }
        }

        if (!leftStillHeld)
        {
            _leftStickTouchId = -1;
            _leftStickActive = false;
            _leftKnobOffset = Vector2.Zero;
        }

        if (!rightStillHeld)
        {
            _rightStickTouchId = -1;
            _rightStickActive = false;
            _rightKnobOffset = Vector2.Zero;
        }

        // 第二遍：处理其余未锁定在摇杆上的手指（响应技能/跳跃按键，或新按下捕获摇杆）
        foreach (var touch in touches)
        {
            if (touch.State == TouchLocationState.Released || touch.State == TouchLocationState.Invalid)
                continue;

            if (touch.Id == _leftStickTouchId || touch.Id == _rightStickTouchId)
                continue;

            Vector2 vPos = viewport.ScreenToVirtual(touch.Position);

            // 1. 检查副武器按钮
            if (Vector2.Distance(vPos, SecondaryButtonCenter) <= ButtonRadius * 1.18f)
            {
                _secondaryButtonPressed = true;
                continue;
            }

            // 2. 检查右手主跳跃按钮与左手辅助跳跃按钮
            if (Vector2.Distance(vPos, JumpButtonCenter) <= JumpButtonRadius * 1.18f ||
                Vector2.Distance(vPos, LeftJumpButtonCenter) <= LeftJumpButtonRadius * 1.18f)
            {
                _jumpButtonPressed = true;
                continue;
            }

            // 3. 若左摇杆空闲，检查左半屏摇杆激活区并锁定该手指 ID
            if (_leftStickTouchId == -1 &&
                vPos.X < VirtualViewport.VirtualWidth * 0.48f &&
                Vector2.Distance(vPos, LeftStickCenter) <= StickRadius * 2.1f)
            {
                _leftStickTouchId = touch.Id;
                _leftStickActive = true;
                _leftKnobOffset = MathHelper2D.ClampLength(vPos - LeftStickCenter, StickRadius);
                continue;
            }

            // 4. 若右摇杆空闲，检查右半屏摇杆激活区并锁定该手指 ID
            if (_rightStickTouchId == -1 &&
                vPos.X >= VirtualViewport.VirtualWidth * 0.52f &&
                Vector2.Distance(vPos, RightStickCenter) <= StickRadius * 2.1f)
            {
                _rightStickTouchId = touch.Id;
                _rightStickActive = true;
                _rightKnobOffset = MathHelper2D.ClampLength(vPos - RightStickCenter, StickRadius);
                if (_rightKnobOffset.LengthSquared() > 144f)
                {
                    _lastTouchAim = MathHelper2D.SafeNormalize(_rightKnobOffset);
                }
            }
        }
    }

    private void UpdateMouseVirtualTouches()
    {
        if (_currMouse.LeftButton == ButtonState.Pressed)
        {
            if (_mouseLockedStick == 1)
            {
                _leftStickActive = true;
                _leftKnobOffset = MathHelper2D.ClampLength(VirtualMousePosition - LeftStickCenter, StickRadius);
                return;
            }
            if (_mouseLockedStick == 2)
            {
                _rightStickActive = true;
                _rightKnobOffset = MathHelper2D.ClampLength(VirtualMousePosition - RightStickCenter, StickRadius);
                if (_rightKnobOffset.LengthSquared() > 144f)
                {
                    _lastTouchAim = MathHelper2D.SafeNormalize(_rightKnobOffset);
                }
                return;
            }

            if (Vector2.Distance(VirtualMousePosition, SecondaryButtonCenter) <= ButtonRadius)
            {
                _secondaryButtonPressed = true;
                return;
            }
            if (Vector2.Distance(VirtualMousePosition, JumpButtonCenter) <= JumpButtonRadius ||
                Vector2.Distance(VirtualMousePosition, LeftJumpButtonCenter) <= LeftJumpButtonRadius)
            {
                _jumpButtonPressed = true;
                return;
            }

            if (_prevMouse.LeftButton == ButtonState.Released && ShowVirtualControls)
            {
                if (Vector2.Distance(VirtualMousePosition, LeftStickCenter) <= StickRadius * 1.4f)
                {
                    _mouseLockedStick = 1;
                    _leftStickActive = true;
                    _leftKnobOffset = MathHelper2D.ClampLength(VirtualMousePosition - LeftStickCenter, StickRadius);
                    return;
                }
                if (Vector2.Distance(VirtualMousePosition, RightStickCenter) <= StickRadius * 1.4f)
                {
                    _mouseLockedStick = 2;
                    _rightStickActive = true;
                    _rightKnobOffset = MathHelper2D.ClampLength(VirtualMousePosition - RightStickCenter, StickRadius);
                    return;
                }
            }
        }
        else
        {
            _mouseLockedStick = 0;
            _leftStickActive = false;
            _leftKnobOffset = Vector2.Zero;
            _rightStickActive = false;
            _rightKnobOffset = Vector2.Zero;
        }
    }

    /// <summary>
    /// 获取整合了键盘、鼠标与虚拟触控后的综合玩家输入意图。
    /// </summary>
    public ControlInputState GetPlayerInputState(Actor player)
    {
        ControlInputState input = default;

        // 1. 移动力矢量
        Vector2 move = Vector2.Zero;
        if (_currKeyboard.IsKeyDown(Keys.A) || _currKeyboard.IsKeyDown(Keys.Left)) move.X -= 1f;
        if (_currKeyboard.IsKeyDown(Keys.D) || _currKeyboard.IsKeyDown(Keys.Right)) move.X += 1f;

        // 叠加触控左摇杆（即使上推斜向移动，水平分量也能满速响应）
        if (_leftStickActive && MathF.Abs(_leftKnobOffset.X) > 12f)
        {
            float normalizedX = _leftKnobOffset.X / (StickRadius * 0.72f);
            move.X += MathHelper.Clamp(normalizedX, -1f, 1f);
        }
        input.MoveVector = new Vector2(MathHelper.Clamp(move.X, -1f, 1f), 0f);

        // 2. 跳跃检测（支持右手跳跃键、左手跳跃键，以及左摇杆上推跳跃）
        bool jumpKey = _currKeyboard.IsKeyDown(Keys.Space) || _currKeyboard.IsKeyDown(Keys.W) || _currKeyboard.IsKeyDown(Keys.Up);
        bool touchJump = _jumpButtonPressed || (_leftStickActive && _leftKnobOffset.Y < -StickRadius * 0.45f);
        input.IsJumpDown = jumpKey || touchJump;

        // 3. 瞄准方向与射击
        if (_rightStickActive && _rightKnobOffset.LengthSquared() > 144f)
        {
            // 触控右摇杆直接决定瞄准角并自动开火
            _lastTouchAim = MathHelper2D.SafeNormalize(_rightKnobOffset);
            input.AimVector = _lastTouchAim;
            input.IsFireDown = true;
        }
        else if (VirtualViewport.IsMobile)
        {
            // 移动端松开右摇杆时保持最后瞄准朝向，不随左摇杆触点乱跳
            input.AimVector = _lastTouchAim;
            input.IsFireDown = false;
        }
        else
        {
            // PC 端鼠标朝向瞄准
            Vector2 aimDelta = VirtualMousePosition - player.Body.Position;
            input.AimVector = MathHelper2D.SafeNormalize(aimDelta);
            input.IsFireDown = _currMouse.LeftButton == ButtonState.Pressed && _mouseLockedStick == 0 && !_secondaryButtonPressed && !_jumpButtonPressed;
        }

        // 4. 副武器触发
        bool secondaryKey = _currMouse.RightButton == ButtonState.Pressed || _currKeyboard.IsKeyDown(Keys.LeftShift) || _currKeyboard.IsKeyDown(Keys.E);
        input.IsSecondaryDown = secondaryKey || _secondaryButtonPressed;

        return input;
    }

    /// <summary>
    /// 检查指定按键是否为本帧刚被按下。
    /// </summary>
    public bool IsKeyJustPressed(Keys key) => _currKeyboard.IsKeyDown(key) && !_prevKeyboard.IsKeyDown(key);

    /// <summary>
    /// 检查鼠标左键或触屏点击是否为本帧刚被触发。
    /// </summary>
    public bool IsLeftClickJustPressed() => (_currMouse.LeftButton == ButtonState.Pressed && _prevMouse.LeftButton != ButtonState.Pressed) || _touchJustPressed;

    /// <summary>
    /// 渲染符合双手人体工学的半透明霓虹虚拟触控摇杆与技能按键。
    /// </summary>
    public void DrawTouchControls(ShapeBatcher batcher, SpriteBatch spriteBatch, ProceduralFont font, Actor? player = null)
    {
        if (!ShowVirtualControls) return;

        Color baseRing = Color.White * 0.28f;
        Color leftActiveRing = _leftStickActive ? ColorPalette.PlayerBlue * 0.75f : baseRing;
        Color rightActiveRing = _rightStickActive ? ColorPalette.PlayerRed * 0.75f : baseRing;

        // 1. 左侧移动摇杆（底盘 + 上推跳提示弧 + 摇杆帽）
        batcher.DrawFilledCircle(spriteBatch, LeftStickCenter, StickRadius, Color.Black * 0.32f);
        batcher.DrawCircleOutline(spriteBatch, LeftStickCenter, StickRadius, leftActiveRing, _leftStickActive ? 4f : 2.5f);
        batcher.DrawCircleOutline(spriteBatch, LeftStickCenter, StickRadius * 0.45f, Color.White * 0.10f, 1.5f);
        if (_leftStickActive)
        {
            batcher.DrawLine(spriteBatch, LeftStickCenter, LeftStickCenter + _leftKnobOffset, ColorPalette.PlayerBlue * 0.5f, 4f);
        }
        Vector2 leftKnobPos = LeftStickCenter + _leftKnobOffset;
        batcher.DrawFilledCircle(spriteBatch, leftKnobPos, StickRadius * 0.40f, ColorPalette.PlayerBlue * (_leftStickActive ? 0.75f : 0.45f));
        batcher.DrawCircleOutline(spriteBatch, leftKnobPos, StickRadius * 0.40f, Color.White * 0.65f, 2f);
        font.DrawText(spriteBatch, "移动", LeftStickCenter + new Vector2(0, StickRadius + 24f), Color.White * 0.55f, 0.75f, new Vector2(0.5f, 0.5f));

        // 2. 左手辅助跳跃按钮（左摇杆正上方）
        Color leftJumpColor = _jumpButtonPressed ? ColorPalette.PlayerGreen * 0.75f : Color.Black * 0.35f;
        batcher.DrawFilledCircle(spriteBatch, LeftJumpButtonCenter, LeftJumpButtonRadius, leftJumpColor);
        batcher.DrawCircleOutline(spriteBatch, LeftJumpButtonCenter, LeftJumpButtonRadius, ColorPalette.PlayerGreen * 0.65f, 3f);
        font.DrawText(spriteBatch, "跳跃", LeftJumpButtonCenter, Color.White * 0.9f, 0.9f, new Vector2(0.5f, 0.5f));

        // 3. 右侧瞄准射击摇杆（底盘 + 准星连线 + 摇杆帽）
        batcher.DrawFilledCircle(spriteBatch, RightStickCenter, StickRadius, Color.Black * 0.32f);
        batcher.DrawCircleOutline(spriteBatch, RightStickCenter, StickRadius, rightActiveRing, _rightStickActive ? 4f : 2.5f);
        batcher.DrawCircleOutline(spriteBatch, RightStickCenter, StickRadius * 0.45f, Color.White * 0.10f, 1.5f);
        if (_rightStickActive)
        {
            batcher.DrawLine(spriteBatch, RightStickCenter, RightStickCenter + _rightKnobOffset, ColorPalette.PlayerRed * 0.55f, 4f);
        }
        Vector2 rightKnobPos = RightStickCenter + _rightKnobOffset;
        batcher.DrawFilledCircle(spriteBatch, rightKnobPos, StickRadius * 0.40f, ColorPalette.PlayerRed * (_rightStickActive ? 0.78f : 0.45f));
        batcher.DrawCircleOutline(spriteBatch, rightKnobPos, StickRadius * 0.40f, Color.White * 0.65f, 2f);
        font.DrawText(spriteBatch, "瞄准射击", RightStickCenter + new Vector2(0, StickRadius + 24f), Color.White * 0.55f, 0.75f, new Vector2(0.5f, 0.5f));

        // 4. 右手内弧形主跳跃按钮（右摇杆左下侧）
        Color rightJumpColor = _jumpButtonPressed ? ColorPalette.PlayerGreen * 0.75f : Color.Black * 0.38f;
        batcher.DrawFilledCircle(spriteBatch, JumpButtonCenter, JumpButtonRadius, rightJumpColor);
        batcher.DrawCircleOutline(spriteBatch, JumpButtonCenter, JumpButtonRadius, ColorPalette.PlayerGreen * 0.75f, 3.5f);
        font.DrawText(spriteBatch, "跳跃", JumpButtonCenter, Color.White, 0.98f, new Vector2(0.5f, 0.5f));

        // 5. 右手内弧形副武器按钮（右摇杆左上方，支持实时冷却显示）
        bool subReady = player?.SubWeapon == null || player.SubWeapon.IsReady;
        string subName = player?.SubWeapon?.Name ?? "副武器";
        Color secBgColor = _secondaryButtonPressed
            ? ColorPalette.BlockShield * 0.85f
            : (subReady ? ColorPalette.BlockShield * 0.38f : Color.Black * 0.45f);
        Color secBorderColor = subReady ? ColorPalette.BlockShield : ColorPalette.TextGray * 0.5f;

        batcher.DrawFilledCircle(spriteBatch, SecondaryButtonCenter, ButtonRadius, secBgColor);
        batcher.DrawCircleOutline(spriteBatch, SecondaryButtonCenter, ButtonRadius, secBorderColor, subReady ? 4f : 2.5f);
        if (!subReady && player?.SubWeapon != null)
        {
            string cdText = $"{player.SubWeapon.CurrentCooldown:0.0}s";
            font.DrawText(spriteBatch, cdText, SecondaryButtonCenter, ColorPalette.PlayerYellow, 0.95f, new Vector2(0.5f, 0.5f));
        }
        else
        {
            font.DrawText(spriteBatch, subName, SecondaryButtonCenter, Color.White, 0.92f, new Vector2(0.5f, 0.5f));
        }
    }
}
