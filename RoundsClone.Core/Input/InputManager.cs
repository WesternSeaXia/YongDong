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
/// 职责边界：负责采集键盘、鼠标与多点触控硬件状态，解算动态虚拟摇杆与动作按钮的触控锁定逻辑，并输出统一的 <see cref="ControlInputState"/>；视觉渲染委托给 <see cref="VirtualControlsRenderer"/>。
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

    /// <summary>
    /// 设置项：左侧摇杆上推是否触发跳跃。
    /// </summary>
    public bool LeftStickJumpEnabled { get; set; } = false;

    /// <summary>
    /// 设置项：左侧摇杆是否同时控制瞄准方向。
    /// 开启后右侧发射摇杆取消，右侧仅保留“开火”、“技能(副武器)”、“跳跃”三个按键。
    /// </summary>
    public bool LeftStickControlsAim { get; set; } = false;

    /// <summary>
    /// 设置项：摇杆中心死区半径占总半径的比例（默认 0.18 即 18%）。
    /// </summary>
    public float StickDeadzoneRatio { get; set; } = 0.18f;

    // 左侧动态跟随移动摇杆（手指在左半屏按下瞬间锁定中心位置，松开后归位）
    public Vector2 LeftStickDefaultCenter = new(310f, 800f);
    public Vector2 LeftStickCenter = new(310f, 800f);
    public float StickRadius = 132f;
    private Vector2 _leftKnobOffset = Vector2.Zero;
    private bool _leftStickActive = false;
    private int _leftStickTouchId = -1;

    // 右侧瞄准射击摇杆（仅在 LeftStickControlsAim == false 时启用）
    public Vector2 RightStickCenter = new(1610f, 800f);
    private Vector2 _rightKnobOffset = Vector2.Zero;
    private bool _rightStickActive = false;
    private int _rightStickTouchId = -1;
    private Vector2 _lastTouchAim = Vector2.UnitX;

    // 右侧独立开火大按键（仅在 LeftStickControlsAim == true 时替代右摇杆显示）
    public Vector2 FireButtonCenter = new(1620f, 810f);
    public float FireButtonRadius = 106f;
    private bool _fireButtonPressed = false;

    // 右手内弧形副武器(技能)按钮（位于右摇杆/开火键左上方自然拇指扇区）
    public Vector2 SecondaryButtonCenter = new(1390f, 625f);
    public float ButtonRadius = 82f;
    private bool _secondaryButtonPressed = false;

    // 右手内弧形跳跃按钮（位于右摇杆/开火键左下侧）
    public Vector2 JumpButtonCenter = new(1310f, 875f);
    public float JumpButtonRadius = 80f;
    private bool _jumpButtonPressed = false;

    // PC 鼠标模拟拖拽摇杆锁定状态 (0: 无, 1: 左摇杆, 2: 右摇杆)
    private int _mouseLockedStick = 0;

    private bool _touchJustPressed = false;

    // --- 供 VirtualControlsRenderer 读取的只读视觉状态属性 ---
    public Vector2 LeftKnobOffset => _leftKnobOffset;
    public bool IsLeftStickActive => _leftStickActive;
    public Vector2 RightKnobOffset => _rightKnobOffset;
    public bool IsRightStickActive => _rightStickActive;
    public bool IsFireButtonPressed => _fireButtonPressed;
    public bool IsSecondaryButtonPressed => _secondaryButtonPressed;
    public bool IsJumpButtonPressed => _jumpButtonPressed;

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
        _fireButtonPressed = false;
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

    /// <summary>
    /// 处理真实多点触控屏的 TouchId 锁定与按键判定。
    /// </summary>
    private void UpdateTouches(TouchCollection touches, VirtualViewport viewport)
    {
        bool leftStillHeld = false;
        bool rightStillHeld = false;
        float deadzoneRadius = StickRadius * StickDeadzoneRatio;

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

                if (LeftStickControlsAim && _leftKnobOffset.Length() > deadzoneRadius)
                {
                    _lastTouchAim = MathHelper2D.SafeNormalize(_leftKnobOffset);
                }
            }
            else if (!LeftStickControlsAim && touch.Id == _rightStickTouchId)
            {
                rightStillHeld = true;
                _rightStickActive = true;
                _rightKnobOffset = MathHelper2D.ClampLength(vPos - RightStickCenter, StickRadius);
                if (_rightKnobOffset.Length() > deadzoneRadius)
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
            LeftStickCenter = LeftStickDefaultCenter;
        }

        if (!rightStillHeld || LeftStickControlsAim)
        {
            _rightStickTouchId = -1;
            _rightStickActive = false;
            _rightKnobOffset = Vector2.Zero;
        }

        // 第二遍：处理其余未锁定在摇杆上的手指（响应开火/技能/跳跃按键，或新按下捕获摇杆）
        foreach (var touch in touches)
        {
            if (touch.State == TouchLocationState.Released || touch.State == TouchLocationState.Invalid)
                continue;

            if (touch.Id == _leftStickTouchId || (!LeftStickControlsAim && touch.Id == _rightStickTouchId))
                continue;

            Vector2 vPos = viewport.ScreenToVirtual(touch.Position);

            // 1. 检查副武器(技能)按钮
            if (Vector2.Distance(vPos, SecondaryButtonCenter) <= ButtonRadius * 1.18f)
            {
                _secondaryButtonPressed = true;
                continue;
            }

            // 2. 检查跳跃按钮
            if (Vector2.Distance(vPos, JumpButtonCenter) <= JumpButtonRadius * 1.18f)
            {
                _jumpButtonPressed = true;
                continue;
            }

            // 3. 若开启了“左摇杆控制瞄准”，右侧为独立开火按键
            if (LeftStickControlsAim && Vector2.Distance(vPos, FireButtonCenter) <= FireButtonRadius * 1.18f)
            {
                _fireButtonPressed = true;
                continue;
            }

            // 4. 若左摇杆空闲，手指在左半屏按下的一瞬间，摇杆中心立即锁定在手指按下的位置，直到手指松开
            if (_leftStickTouchId == -1 && vPos.X < VirtualViewport.VirtualWidth * 0.48f && vPos.Y > 100f)
            {
                _leftStickTouchId = touch.Id;
                _leftStickActive = true;
                LeftStickCenter = vPos;
                _leftKnobOffset = Vector2.Zero;
                continue;
            }

            // 5. 若为双摇杆模式且右摇杆空闲，检查右半屏摇杆激活区并锁定该手指 ID
            if (!LeftStickControlsAim &&
                _rightStickTouchId == -1 &&
                vPos.X >= VirtualViewport.VirtualWidth * 0.52f &&
                Vector2.Distance(vPos, RightStickCenter) <= StickRadius * 2.1f)
            {
                _rightStickTouchId = touch.Id;
                _rightStickActive = true;
                _rightKnobOffset = MathHelper2D.ClampLength(vPos - RightStickCenter, StickRadius);
                if (_rightKnobOffset.Length() > deadzoneRadius)
                {
                    _lastTouchAim = MathHelper2D.SafeNormalize(_rightKnobOffset);
                }
            }
        }
    }

    /// <summary>
    /// 在桌面端支持用鼠标左键拖拽模拟虚拟摇杆与按钮点击。
    /// </summary>
    private void UpdateMouseVirtualTouches()
    {
        float deadzoneRadius = StickRadius * StickDeadzoneRatio;

        if (_currMouse.LeftButton == ButtonState.Pressed)
        {
            if (_mouseLockedStick == 1)
            {
                _leftStickActive = true;
                _leftKnobOffset = MathHelper2D.ClampLength(VirtualMousePosition - LeftStickCenter, StickRadius);
                if (LeftStickControlsAim && _leftKnobOffset.Length() > deadzoneRadius)
                {
                    _lastTouchAim = MathHelper2D.SafeNormalize(_leftKnobOffset);
                }
                return;
            }
            if (!LeftStickControlsAim && _mouseLockedStick == 2)
            {
                _rightStickActive = true;
                _rightKnobOffset = MathHelper2D.ClampLength(VirtualMousePosition - RightStickCenter, StickRadius);
                if (_rightKnobOffset.Length() > deadzoneRadius)
                {
                    _lastTouchAim = MathHelper2D.SafeNormalize(_rightKnobOffset);
                }
                return;
            }

            if (ShowVirtualControls)
            {
                if (Vector2.Distance(VirtualMousePosition, SecondaryButtonCenter) <= ButtonRadius)
                {
                    _secondaryButtonPressed = true;
                    return;
                }
                if (Vector2.Distance(VirtualMousePosition, JumpButtonCenter) <= JumpButtonRadius)
                {
                    _jumpButtonPressed = true;
                    return;
                }
                if (LeftStickControlsAim && Vector2.Distance(VirtualMousePosition, FireButtonCenter) <= FireButtonRadius)
                {
                    _fireButtonPressed = true;
                    return;
                }

                if (_prevMouse.LeftButton == ButtonState.Released)
                {
                    if (VirtualMousePosition.X < VirtualViewport.VirtualWidth * 0.45f && VirtualMousePosition.Y > 420f)
                    {
                        _mouseLockedStick = 1;
                        _leftStickActive = true;
                        LeftStickCenter = VirtualMousePosition;
                        _leftKnobOffset = Vector2.Zero;
                        return;
                    }
                    if (!LeftStickControlsAim && Vector2.Distance(VirtualMousePosition, RightStickCenter) <= StickRadius * 1.4f)
                    {
                        _mouseLockedStick = 2;
                        _rightStickActive = true;
                        _rightKnobOffset = MathHelper2D.ClampLength(VirtualMousePosition - RightStickCenter, StickRadius);
                        return;
                    }
                }
            }
        }
        else
        {
            _mouseLockedStick = 0;
            _leftStickActive = false;
            _leftKnobOffset = Vector2.Zero;
            LeftStickCenter = LeftStickDefaultCenter;
            _rightStickActive = false;
            _rightKnobOffset = Vector2.Zero;
        }
    }

    /// <summary>
    /// 获取整合了键盘、鼠标与虚拟触控后的综合玩家输入意图。
    /// 支持传入摄像机视口逆变换委托以确保动态摄像机缩放平移下鼠标准星的世界坐标精确对齐。
    /// </summary>
    public ControlInputState GetPlayerInputState(Actor player, Func<Vector2, Vector2>? screenToWorld = null)
    {
        ControlInputState input = default;
        float deadzoneRadius = StickRadius * StickDeadzoneRatio;

        // 1. 移动力矢量（带中心死区过滤与平滑线性映射）
        Vector2 move = Vector2.Zero;
        if (_currKeyboard.IsKeyDown(Keys.A) || _currKeyboard.IsKeyDown(Keys.Left)) move.X -= 1f;
        if (_currKeyboard.IsKeyDown(Keys.D) || _currKeyboard.IsKeyDown(Keys.Right)) move.X += 1f;

        if (_leftStickActive && _leftKnobOffset.Length() > deadzoneRadius)
        {
            float absX = MathF.Abs(_leftKnobOffset.X);
            float xDeadzone = deadzoneRadius * 0.7f;
            if (absX > xDeadzone)
            {
                float maxEffective = MathF.Max(1f, StickRadius * 0.78f - xDeadzone);
                float normalizedX = (absX - xDeadzone) / maxEffective;
                move.X += MathF.Sign(_leftKnobOffset.X) * MathHelper.Clamp(normalizedX, 0f, 1f);
            }
        }
        input.MoveVector = new Vector2(MathHelper.Clamp(move.X, -1f, 1f), 0f);

        // 2. 跳跃检测（根据设置项决定左侧摇杆上推是否触发跳跃）
        bool jumpKey = _currKeyboard.IsKeyDown(Keys.Space) || _currKeyboard.IsKeyDown(Keys.W) || _currKeyboard.IsKeyDown(Keys.Up);
        bool stickUpJump = LeftStickJumpEnabled &&
                           _leftStickActive &&
                           _leftKnobOffset.Length() > deadzoneRadius &&
                           _leftKnobOffset.Y < -StickRadius * 0.50f;
        input.IsJumpDown = jumpKey || _jumpButtonPressed || stickUpJump;

        // 3. 瞄准方向与射击（计算鼠标在世界空间中的准确坐标）
        Vector2 mouseWorldPos = screenToWorld != null ? screenToWorld(VirtualMousePosition) : VirtualMousePosition;

        if (LeftStickControlsAim)
        {
            if (_leftStickActive && _leftKnobOffset.Length() > deadzoneRadius)
            {
                _lastTouchAim = MathHelper2D.SafeNormalize(_leftKnobOffset);
            }

            if (VirtualViewport.IsMobile || _leftStickActive || _fireButtonPressed)
            {
                input.AimVector = _lastTouchAim;
                input.IsFireDown = _fireButtonPressed;
            }
            else
            {
                Vector2 aimDelta = mouseWorldPos - player.Body.Position;
                input.AimVector = MathHelper2D.SafeNormalize(aimDelta);
                input.IsFireDown = _currMouse.LeftButton == ButtonState.Pressed && _mouseLockedStick == 0 && !_secondaryButtonPressed && !_jumpButtonPressed;
            }
        }
        else
        {
            if (_rightStickActive && _rightKnobOffset.Length() > deadzoneRadius)
            {
                _lastTouchAim = MathHelper2D.SafeNormalize(_rightKnobOffset);
                input.AimVector = _lastTouchAim;
                input.IsFireDown = true;
            }
            else if (VirtualViewport.IsMobile)
            {
                input.AimVector = _lastTouchAim;
                input.IsFireDown = false;
            }
            else
            {
                Vector2 aimDelta = mouseWorldPos - player.Body.Position;
                input.AimVector = MathHelper2D.SafeNormalize(aimDelta);
                input.IsFireDown = _currMouse.LeftButton == ButtonState.Pressed && _mouseLockedStick == 0 && !_secondaryButtonPressed && !_jumpButtonPressed;
            }
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
    /// 渲染虚拟触控摇杆与技能按钮（委托给 <see cref="VirtualControlsRenderer"/>）。
    /// </summary>
    public void DrawTouchControls(ShapeBatcher batcher, SpriteBatch spriteBatch, ProceduralFont font, Actor? player = null)
    {
        VirtualControlsRenderer.Draw(batcher, spriteBatch, font, this, player);
    }
}
