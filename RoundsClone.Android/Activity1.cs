using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Microsoft.Xna.Framework;
using RoundsClone.Core;

namespace RoundsClone.Android
{
    /// <summary>
    /// Android 端主入口 Activity。
    /// 强制横屏全屏沉浸式渲染，并挂载核心跨平台游戏宿主 RoundsGame。
    /// </summary>
    [Activity(
        Label = "@string/app_name",
        MainLauncher = true,
        Icon = "@drawable/icon",
        AlwaysRetainTaskState = true,
        LaunchMode = LaunchMode.SingleInstance,
        ScreenOrientation = ScreenOrientation.SensorLandscape,
        ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden | ConfigChanges.ScreenSize
    )]
    public class Activity1 : AndroidGameActivity
    {
        private RoundsGame _game;
        private View _view;

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            // 启用无标题全屏与刘海/挖孔区域延伸，最大化手机屏幕可视面积
            RequestWindowFeature(WindowFeatures.NoTitle);
            if (Window != null)
            {
                Window.AddFlags(WindowManagerFlags.Fullscreen | WindowManagerFlags.KeepScreenOn);
                if (System.OperatingSystem.IsAndroidVersionAtLeast(28) && Window.Attributes != null)
                {
                    Window.Attributes.LayoutInDisplayCutoutMode = LayoutInDisplayCutoutMode.ShortEdges;
                }
            }

            _game = new RoundsGame();
            _view = _game.Services.GetService(typeof(View)) as View;

            SetContentView(_view);
            ApplyImmersiveFullScreen();
            _game.Run();
        }

        public override void OnWindowFocusChanged(bool hasFocus)
        {
            base.OnWindowFocusChanged(hasFocus);
            if (hasFocus)
            {
                ApplyImmersiveFullScreen();
            }
        }

        private void ApplyImmersiveFullScreen()
        {
            if (_view != null)
            {
#pragma warning disable CS0618
                _view.SystemUiVisibility = (StatusBarVisibility)(
                    SystemUiFlags.LayoutStable |
                    SystemUiFlags.LayoutHideNavigation |
                    SystemUiFlags.LayoutFullscreen |
                    SystemUiFlags.HideNavigation |
                    SystemUiFlags.Fullscreen |
                    SystemUiFlags.ImmersiveSticky);
#pragma warning restore CS0618
            }
        }
    }
}
