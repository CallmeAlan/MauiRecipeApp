using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using SQLite;

namespace MauiRecipeApp
{
    public partial class App : Application
    {
        private readonly IServiceProvider _serviceProvider;
        public App(IServiceProvider serviceProvider)
        {
            InitializeComponent();
            // 直接从DI拿到主页，套NavigationPage
            var homePage = serviceProvider.GetRequiredService<RecipeListPage>();
            var nav = new NavigationPage(homePage);
            // 导航完成后强制同步状态栏/导航栏，防止 MAUI 在导航时把状态栏重置为默认紫色
            nav.Pushed += OnNavigationChanged;
            nav.Popped += OnNavigationChanged;
            MainPage = nav;
        }

        /// <summary>
        /// 导航（push/pop）完成后：统一同步状态栏透明、导航栏背景色，并确保首页始终隐藏导航栏。
        /// 这是比页面 OnAppearing 更晚的时机，能覆盖 MAUI 导航期间的紫色重置。
        /// </summary>
        private void OnNavigationChanged(object? sender, NavigationEventArgs e)
        {
            if (sender is not NavigationPage nav) return;
            if (nav.CurrentPage == null) return;

            // 首页始终隐藏导航栏（根治从编辑页返回后导航栏再现）
            if (nav.CurrentPage is RecipeListPage)
            {
                NavigationPage.SetHasNavigationBar(nav.CurrentPage, false);
                // pop 返回后 Android 15+ 系统 insets 可能残留导致内容下移：
                // 延迟到动画结束后，强制刷新系统 insets + 页面层/导航层重排
                nav.CurrentPage.Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(400), () =>
                {
                    AppTheme.RefreshWindowInsets();
                    if (nav.CurrentPage is RecipeListPage page)
                    {
                        page.ForceRefreshTopPadding();
                    }
                    nav.ForceLayout();
                });
            }

            var bg = AppTheme.GetBackgroundColor();
            // 同步导航栏背景/文字颜色
            nav.BarBackgroundColor = bg;
            nav.BarTextColor = AppTheme.GetBarTextColor(bg);
            // 强制状态栏透明 + 图标颜色
            AppTheme.ApplyStatusBar(nav.CurrentPage, bg);
        }
    }
}
