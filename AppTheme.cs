namespace MauiRecipeApp;

/// <summary>
/// 全局主题辅助类：统一读取背景色偏好，并计算导航栏文字颜色
/// </summary>
public static class AppTheme
{
    public const string DefaultBgHex = "#E8F5E9";

    /// <summary>
    /// 获取用户选择的背景颜色（未设置时返回默认浅绿）
    /// </summary>
    public static Color GetBackgroundColor()
        => Color.FromArgb(Preferences.Default.Get("PageBgColor", DefaultBgHex));

    /// <summary>
    /// 根据背景颜色亮度决定导航栏文字用深色还是白色
    /// </summary>
    public static Color GetBarTextColor(Color bg)
    {
        // 相对亮度公式（0~1），>0.5 算浅色背景 → 深色文字
        double luminance = 0.299 * bg.Red + 0.587 * bg.Green + 0.114 * bg.Blue;
        return luminance > 0.5 ? Colors.Black : Colors.White;
    }

    /// <summary>
    /// 将页面所在的 NavigationPage 导航栏背景/文字颜色同步为页面背景色
    /// </summary>
    public static void ApplyNavigationBar(Page page, Color bg)
    {
        if (page.Parent is NavigationPage nav)
        {
            nav.BarBackgroundColor = bg;
            nav.BarTextColor = GetBarTextColor(bg);
        }
    }

    /// <summary>
    /// 将 MAUI Color 转为安卓不透明颜色（ARGB）。
    /// 状态栏必须用不透明颜色：Android 15+ 对透明状态栏会自动叠加 40% 黑色 scrim 遮罩，
    /// 不透明颜色则直接显示、不会触发遮罩。
    /// </summary>
    public static global::Android.Graphics.Color ToAndroidColor(Color c)
        => global::Android.Graphics.Color.Argb(
            (int)Math.Round(c.Alpha * 255),
            (int)Math.Round(c.Red * 255),
            (int)Math.Round(c.Green * 255),
            (int)Math.Round(c.Blue * 255));

    /// <summary>
    /// 应用状态栏：背景色（不透明，直接显示页面背景色）+ 图标颜色按背景亮度适配
    /// 通过 MainActivity.Instance 获取窗口（不依赖 Platform.CurrentActivity，避免静默失效）
    /// </summary>
    public static void ApplyStatusBar(Page page, Color bg)
    {
#if ANDROID
        var window = MainActivity.Instance?.Window;
        if (window == null) return;

        void Apply()
        {
            // 注意：不要添加 TranslucentStatus 标志！
            // 它在 Android 5.0+ 上会让系统强制绘制"半透明黑色渐变"状态栏（即灰黑遮罩），
            // 且会无视 SetStatusBarColor 的设置。正确写法是 DrawsSystemBarBackgrounds + SetStatusBarColor。
            window.AddFlags(global::Android.Views.WindowManagerFlags.DrawsSystemBarBackgrounds);
            AndroidX.Core.View.WindowCompat.SetDecorFitsSystemWindows(window, false);
            // 不透明背景色：既显示页面背景色，又避免 Android 15+ 对透明状态栏自动叠加 scrim 遮罩
            window.SetStatusBarColor(ToAndroidColor(bg));
            // 双保险：Android 15/16 可能忽略 SetStatusBarColor（强制 edge-to-edge），
            // 此时状态栏区域会透出窗口根背景（默认白色）。把 DecorView 根背景也设为背景色，
            // 确保有导航栏页面（AppBarLayout 透明区域）的状态栏永远显示页面背景色。
            window.DecorView?.SetBackgroundColor(ToAndroidColor(bg));
            ApplyStatusBarIcons(bg);
        }

        // 立即应用一次，再延迟 300ms 应用一次：
        // 覆盖 MAUI 导航动画/Toolbar 创建期间可能把状态栏重置为默认紫色的情况
        window.DecorView?.Post(Apply);
        window.DecorView?.PostDelayed(Apply, 300);
#endif
    }

#if ANDROID
    /// <summary>
    /// 从 Activity 应用状态栏（App 启动时调用，状态栏背景色 + 内容延伸）
    /// </summary>
    public static void ApplyStatusBar(global::Android.App.Activity activity, Color bg)
    {
        if (activity?.Window != null)
        {
            AndroidX.Core.View.WindowCompat.SetDecorFitsSystemWindows(activity.Window, false);
            activity.Window.SetStatusBarColor(ToAndroidColor(bg));
            // 双保险：同上，Android 15/16 忽略 SetStatusBarColor 时状态栏透出 DecorView 根背景
            activity.Window.DecorView?.SetBackgroundColor(ToAndroidColor(bg));
            ApplyStatusBarIcons(bg);
        }
    }

    /// <summary>
    /// 设置状态栏图标颜色（浅色背景→深色图标，深色背景→白色图标）
    /// </summary>
    public static void ApplyStatusBarIcons(Color bg)
    {
        var window = MainActivity.Instance?.Window;
        if (window == null) return;

        // SYSTEM_UI_FLAG_LIGHT_STATUS_BAR = 0x2000（Android 6.0+），浅背景时图标变深色
        const int lightStatusBarFlag = 0x2000;
        var decorView = window.DecorView;
        if (decorView == null) return;
        int flags = (int)decorView.SystemUiVisibility;
        if (GetBarTextColor(bg) == Colors.Black)
        {
            flags |= lightStatusBarFlag;
        }
        else
        {
            flags &= ~lightStatusBarFlag;
        }
        decorView.SystemUiVisibility = (global::Android.Views.StatusBarVisibility)flags;
    }

    /// <summary>
    /// 获取安卓状态栏高度（dp，MAUI布局单位）
    /// </summary>
    public static double GetStatusBarHeightDp()
    {
        var res = MainActivity.Instance?.Resources;
        if (res == null) return 0;

        int id = res.GetIdentifier("status_bar_height", "dimen", "android");
        int px = id > 0 ? res.GetDimensionPixelSize(id) : 0;
        return px / DeviceDisplay.MainDisplayInfo.Density;
    }
    /// <summary>
    /// 强制重新应用系统窗口 insets（安全区）。
    /// Android 15+ 强制 edge-to-edge，pop 返回时 insets 状态可能残留导致内容下移；
    /// 调用 RequestApplyInsets 让系统重新分发 insets，页面容器重新布局。
    /// </summary>
    public static void RefreshWindowInsets()
    {
#if ANDROID
        var window = MainActivity.Instance?.Window;
        var decorView = window?.DecorView;
        if (decorView == null) return;
        decorView.RequestApplyInsets();
#endif
    }
#endif
}
