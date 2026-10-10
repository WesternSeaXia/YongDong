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

    // 左侧虚拟移动摇杆
    public Vector2 LeftStickCenter = new(240f, 880f);
    public float StickRadius = 85f;
    private Vector2 _leftKnobOffset = Vector2.Zero;
    private bool _leftStickActive = false;

    // 右侧瞄准射击摇杆
    public Vector2 RightStickCenter = new(1680f, 880f);
    private Vector2 _rightKnobOffset = Vector2.Zero;
    private bool _rightStickActive = false;

    // 副武器虚拟按键 (右侧摇杆上方)
    public Vector2 SecondaryButtonCenter = new(1750f, 700f);
    public float ButtonRadius = 50f;
    private bool _secondaryButtonPressed = false;

    // 跳跃虚拟按键 (左侧摇杆上方)
    public Vector2 JumpButtonCenter = new(170f, 700f);
    private bool _jumpButtonPressed = false;

    /// <summary>
    /// 鼠标在虚拟 1920x1080 世界空间下的实时坐标。
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

        // 重置触控状态
        _secondaryButtonPressed = false;
        _jumpButtonPressed = false;

        // 收集触屏手势或鼠标触控模拟
        TouchCollection touchPoints = TouchPanel.GetState();
        if (touchPoints.Count > 0)
        {
            UpdateTouches(touchPoints, viewport);
        }
        else
        {
            // 若无真实多点触屏，通过鼠标按下模拟触控摇杆与按键测试
            UpdateMouseVirtualTouches();
        }
    }

    private void UpdateTouches(TouchCollection touches, VirtualViewport viewport)
    {
        bool leftTouched = false;
        bool rightTouched = false;

        foreach (var touch in touches)
        {
            Vector2 vPos = viewport.ScreenToVirtual(touch.Position);

            // 检查副武器按钮
            if (Vector2.Distance(vPos, SecondaryButtonCenter) <= ButtonRadius)
            {
                _secondaryButtonPressed = true;
            }

            // 检查跳跃按钮
            if (Vector2.Distance(vPos, JumpButtonCenter) <= ButtonRadius)
            {
                _jumpButtonPressed = true;
            }

            // 检查左半屏移动摇杆
            if (vPos.X < VirtualViewport.VirtualWidth * 0.5f && Vector2.Distance(vPos, LeftStickCenter) <= StickRadius * 1.6f)
            {
                leftTouched = true;
                Vector2 delta = vPos - LeftStickCenter;
                _leftKnobOffset = MathHelper2D.ClampLength(delta, StickRadius);
            }

            // 检查右半屏瞄准摇杆
            if (vPos.X >= VirtualViewport.VirtualWidth * 0.5f && Vector2.Distance(vPos, RightStickCenter) <= StickRadius * 1.6f)
            {
                rightTouched = true;
                Vector2 delta = vPos - RightStickCenter;
                _rightKnobOffset = MathHelper2D.ClampLength(delta, StickRadius);
            }
        }

        _leftStickActive = leftTouched;
        if (!leftTouched) _leftKnobOffset = Vector2.Zero;

        _rightStickActive = rightTouched;
        if (!rightTouched) _rightKnobOffset = Vector2.Zero;
    }

    private void UpdateMouseVirtualTouches()
    {
        if (_currMouse.LeftButton == ButtonState.Pressed)
        {
            if (Vector2.Distance(VirtualMousePosition, SecondaryButtonCenter) <= ButtonRadius)
            {
                _secondaryButtonPressed = true;
            }
            if (Vector2.Distance(VirtualMousePosition, JumpButtonCenter) <= ButtonRadius)
            {
                _jumpButtonPressed = true;
            }
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

        // 叠加触控左摇杆
        if (_leftStickActive && _leftKnobOffset.LengthSquared() > 100f)
        {
            move.X += _leftKnobOffset.X / StickRadius;
        }
        input.MoveVector = new Vector2(MathHelper.Clamp(move.X, -1f, 1f), 0f);

        // 2. 跳跃检测
        bool jumpKey = _currKeyboard.IsKeyDown(Keys.Space) || _currKeyboard.IsKeyDown(Keys.W) || _currKeyboard.IsKeyDown(Keys.Up);
        bool touchJump = _jumpButtonPressed || (_leftStickActive && _leftKnobOffset.Y < -StickRadius * 0.65f);
        input.IsJumpDown = jumpKey || touchJump;

        // 3. 瞄准方向
        if (_rightStickActive && _rightKnobOffset.LengthSquared() > 100f)
        {
            // 触控右摇杆直接决定瞄准角
            input.AimVector = MathHelper2D.SafeNormalize(_rightKnobOffset);
            input.IsFireDown = true; // 拉动右摇杆自动射击
        }
        else
        {
            // 鼠标朝向瞄准
            Vector2 aimDelta = VirtualMousePosition - player.Body.Position;
            input.AimVector = MathHelper2D.SafeNormalize(aimDelta);
            input.IsFireDown = _currMouse.LeftButton == ButtonState.Pressed;
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
    /// 检查鼠标左键是否为本帧刚被点击。
    /// </summary>
    public bool IsLeftClickJustPressed() => _currMouse.LeftButton == ButtonState.Pressed && _prevMouse.LeftButton != ButtonState.Pressed;

    /// <summary>
    /// 渲染半透明霓虹虚拟触控摇杆与按键。
    /// </summary>
    public void DrawTouchControls(ShapeBatcher batcher, SpriteBatch spriteBatch, ProceduralFont font)
    {
        if (!ShowVirtualControls) return;

        Color baseRing = Color.White * 0.22f;
        Color knobColor = ColorPalette.PlayerBlue * 0.45f;

        // 1. 左侧移动摇杆
        batcher.DrawFilledCircle(spriteBatch, LeftStickCenter, StickRadius, Color.Black * 0.25f);
        batcher.DrawCircleOutline(spriteBatch, LeftStickCenter, StickRadius, baseRing, 2.5f);
        Vector2 leftKnobPos = LeftStickCenter + _leftKnobOffset;
        batcher.DrawFilledCircle(spriteBatch, leftKnobPos, StickRadius * 0.38f, knobColor);

        // 2. 左侧跳跃快捷键
        Color jumpBtnColor = _jumpButtonPressed ? ColorPalette.PlayerGreen * 0.6f : Color.White * 0.25f;
        batcher.DrawFilledCircle(spriteBatch, JumpButtonCenter, ButtonRadius, jumpBtnColor);
        batcher.DrawCircleOutline(spriteBatch, JumpButtonCenter, ButtonRadius, Color.White * 0.4f, 2f);
        font.DrawText(spriteBatch, "跳跃", JumpButtonCenter, Color.White, 0.65f, new Vector2(0.5f, 0.5f));

        // 3. 右侧瞄准射击摇杆
        batcher.DrawFilledCircle(spriteBatch, RightStickCenter, StickRadius, Color.Black * 0.25f);
        batcher.DrawCircleOutline(spriteBatch, RightStickCenter, StickRadius, baseRing, 2.5f);
        Vector2 rightKnobPos = RightStickCenter + _rightKnobOffset;
        batcher.DrawFilledCircle(spriteBatch, rightKnobPos, StickRadius * 0.38f, ColorPalette.PlayerRed * 0.45f);

        // 4. 右侧副武器按钮
        Color secBtnColor = _secondaryButtonPressed ? ColorPalette.BlockShield * 0.8f : ColorPalette.BlockShield * 0.35f;
        batcher.DrawFilledCircle(spriteBatch, SecondaryButtonCenter, ButtonRadius, secBtnColor);
        batcher.DrawCircleOutline(spriteBatch, SecondaryButtonCenter, ButtonRadius, Color.White * 0.5f, 2f);
        font.DrawText(spriteBatch, "副武器", SecondaryButtonCenter, Color.White, 0.65f, new Vector2(0.5f, 0.5f));
    }
}
