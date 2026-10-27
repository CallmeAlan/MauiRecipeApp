using System.Text.RegularExpressions;

namespace MauiRecipeApp;

public partial class BgColorSettingsPage : ContentPage
{
    private const string CustomColorsKey = "CustomBgColors";

    // 预设背景色（主页原本的6个色块迁移到这里）
    private static readonly string[] PresetColors =
    {
        "#E8F5E9", "#FFF8E1", "#E3F2FD", "#FCE4EC", "#F3E5F5", "#FFFFFF"
    };

    public BgColorSettingsPage()
    {
        InitializeComponent();
        LoadAllColors();
    }

    // 进入页面：应用当前背景色并高亮选中的色块，同步导航栏背景色
    protected override void OnAppearing()
    {
        base.OnAppearing();
        var bg = AppTheme.GetBackgroundColor();
        BackgroundColor = bg;
        AppTheme.ApplyNavigationBar(this, bg);
        AppTheme.ApplyStatusBar(this, bg);
        // 透明状态栏后：内容顶部下移状态栏高度，避免被遮挡
        colorScroll.Padding = new Thickness(15, 15 + AppTheme.GetStatusBarHeightDp(), 15, 15);
        HighlightSelectedColor();
    }

    /// <summary>
    /// 预设 + 自定义颜色，全部渲染到4列色块网格
    /// </summary>
    private void LoadAllColors()
    {
        var list = new List<string>(PresetColors);
        list.AddRange(GetCustomColors());

        colorGrid.Children.Clear();
        colorGrid.RowDefinitions.Clear();

        int rows = (list.Count + 3) / 4;
        for (int r = 0; r < rows; r++)
        {
            colorGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        for (int i = 0; i < list.Count; i++)
        {
            var btn = new Button
            {
                Text = string.Empty,
                WidthRequest = 52,
                HeightRequest = 52,
                CornerRadius = 26,
                BackgroundColor = Color.FromArgb(list[i]),
                BorderColor = Color.FromArgb("#999999"),
                BorderWidth = 1,
                CommandParameter = list[i],
                HorizontalOptions = LayoutOptions.Center,
                Padding = 0,
            };
            btn.Clicked += OnColorSelected;

            Grid.SetRow(btn, i / 4);
            Grid.SetColumn(btn, i % 4);
            colorGrid.Children.Add(btn);
        }
    }

    // 读取用户自定义颜色（分号分隔存储）
    private List<string> GetCustomColors()
    {
        var raw = Preferences.Default.Get(CustomColorsKey, string.Empty);
        return string.IsNullOrEmpty(raw)
            ? new List<string>()
            : raw.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();
    }

    private void SaveCustomColors(IEnumerable<string> colors)
    {
        Preferences.Default.Set(CustomColorsKey, string.Join(";", colors));
    }

    // 点击色块：设置为全局背景色并高亮，状态栏/导航栏立即同步
    private void OnColorSelected(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is string hex)
        {
            Preferences.Default.Set("PageBgColor", hex);
            var bg = Color.FromArgb(hex);
            BackgroundColor = bg;
            AppTheme.ApplyNavigationBar(this, bg);
            AppTheme.ApplyStatusBar(this, bg);
            HighlightSelectedColor();
        }
    }

    // 高亮当前选中的色块（橙色粗边框），其余恢复灰色细边框
    private void HighlightSelectedColor()
    {
        var current = Preferences.Default.Get("PageBgColor", "#E8F5E9");
        foreach (var child in colorGrid.Children)
        {
            if (child is Button b && b.CommandParameter is string hex)
            {
                bool isSelected = string.Equals(hex, current, StringComparison.OrdinalIgnoreCase);
                b.BorderColor = isSelected ? Color.FromArgb("#ff782b") : Color.FromArgb("#999999");
                b.BorderWidth = isSelected ? 3 : 1;
            }
        }
    }

    // "+ 新增颜色"：输入颜色编码并保存
    private async void OnAddColorClicked(object sender, EventArgs e)
    {
        var input = await DisplayPromptAsync(
            "新增颜色",
            "请输入颜色编码，例如：#FF8A65 或 FFC107",
            "确定", "取消", "#");

        if (string.IsNullOrWhiteSpace(input)) return;

        var hex = NormalizeHex(input);
        if (hex == null)
        {
            await DisplayAlert("格式错误", "请输入合法的颜色编码，如 #RRGGBB", "确定");
            return;
        }

        var customs = GetCustomColors();
        if (customs.Any(c => string.Equals(c, hex, StringComparison.OrdinalIgnoreCase)))
        {
            await DisplayAlert("提示", "这个颜色已经存在了", "确定");
            return;
        }

        customs.Add(hex);
        SaveCustomColors(customs);

        // 重新渲染：新增色块立即出现
        LoadAllColors();
        HighlightSelectedColor();
    }

    // 校验并规范化颜色编码：#RGB / #RRGGBB / #RRGGBBAA 均可，自动补全#号
    private string? NormalizeHex(string input)
    {
        var s = input.Trim();
        if (!s.StartsWith('#')) s = "#" + s;
        return Regex.IsMatch(s, @"^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$") ? s : null;
    }
}
