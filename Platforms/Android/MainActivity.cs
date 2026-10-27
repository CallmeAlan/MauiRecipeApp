using Android.App;
using Android.Content.PM;
using Android.OS;

namespace MauiRecipeApp
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        /// <summary>静态引用，供共享代码在页面 OnAppearing 时可靠获取当前 Activity（不依赖 Platform.CurrentActivity）</summary>
        public static MainActivity? Instance { get; private set; }

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Instance = this;

            // 延迟到窗口/主题完全就绪后再应用透明状态栏（避免被 MAUI 主题切换覆盖）
            Window?.DecorView?.Post(ApplyEdgeToEdge);
        }

        protected override void OnResume()
        {
            base.OnResume();
            // 每次回到前台再确保一次（保险）
            ApplyEdgeToEdge();
        }

        /// <summary>
        /// 透明状态栏 + 内容延伸到状态栏下方：
        /// 状态栏区域直接显示页面背景色，彻底消除 MAUI 默认紫色。
        /// TranslucentStatus 窗口标志为运行时强制，与主题无关，是最终生效状态。
        /// </summary>
        private void ApplyEdgeToEdge()
        {
            if (Window == null) return;

            // 注意：不要添加 TranslucentStatus 标志！
            // 它在 Android 5.0+ 上会让系统强制绘制"半透明黑色渐变"状态栏（即灰黑遮罩），
            // 且会无视 SetStatusBarColor 的设置。正确写法是 DrawsSystemBarBackgrounds + SetStatusBarColor。
            Window.AddFlags(global::Android.Views.WindowManagerFlags.DrawsSystemBarBackgrounds);
            AndroidX.Core.View.WindowCompat.SetDecorFitsSystemWindows(Window, false);
            // 不透明背景色：既显示页面背景色，又避免 Android 15+ 对透明状态栏自动叠加 scrim 遮罩
            Window.SetStatusBarColor(AppTheme.ToAndroidColor(AppTheme.GetBackgroundColor()));
            // 双保险：Android 15/16 可能忽略 SetStatusBarColor（强制 edge-to-edge），
            // 此时状态栏区域透出窗口根背景（默认白色）。把 DecorView 根背景也设为背景色。
            Window.DecorView?.SetBackgroundColor(AppTheme.ToAndroidColor(AppTheme.GetBackgroundColor()));
            AppTheme.ApplyStatusBarIcons(AppTheme.GetBackgroundColor());
        }
    }
}
