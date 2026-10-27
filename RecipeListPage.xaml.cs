using Microsoft.Maui.Controls;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;

namespace MauiRecipeApp;

public partial class RecipeListPage : ContentPage
{
    private readonly RecipeDbService _dbService;

    // ✅ DI注入构造函数（唯一构造函数）
    public RecipeListPage(RecipeDbService dbService)
    {
        InitializeComponent();
        _dbService = dbService;
        BindingContext = this;
        // 构造时就隐藏导航栏（根治返回后导航栏再现）
        NavigationPage.SetHasNavigationBar(this, false);
    }

    private List<Recipe> _allRecipes = new();
    private List<Recipe> _filteredAll = new();
    private const int PageSize = 12;
    private int _loadedPageEnd = 0; // 已显示到 _filteredAll 的索引（不含）

    private ObservableCollection<Recipe> _recipeList = new();
    public ObservableCollection<Recipe> RecipeList
    {
        get => _recipeList;
        set { _recipeList = value; OnPropertyChanged(); }
    }
    private string _currentCategory = "全部";

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        ResetTopPadding();
        ApplySavedBackgroundColor();
        // 延迟强制刷新：根治从编辑页返回后 MAUI 残留导航栏高度导致内容下移的问题
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(250), ResetTopPadding);
        await LoadRecipes();
    }

    /// <summary>
    /// 隐藏导航栏 + 重置内容顶部 padding（状态栏高度）。
    /// pop 返回后 MAUI 可能残留导航栏布局高度，需要延迟重新应用一次。
    /// </summary>
    public void ResetTopPadding()
    {
        NavigationPage.SetHasNavigationBar(this, false);
        mainScroll.Padding = new Thickness(0, AppTheme.GetStatusBarHeightDp(), 0, 0);
    }

    /// <summary>
    /// pop 返回后强制刷新布局：重新应用无导航栏状态 + 顶部 padding，并整体重排。
    /// </summary>
    public void ForceRefreshTopPadding()
    {
        NavigationPage.SetHasNavigationBar(this, false);
        mainScroll.Padding = new Thickness(0, AppTheme.GetStatusBarHeightDp(), 0, 0);
        ForceLayout();
    }

    // 首次布局完成后：把右上角菜单按钮下移到状态栏下方
    private bool _menuBtnAdjusted;
    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (!_menuBtnAdjusted && height > 0)
        {
            double sbh = AppTheme.GetStatusBarHeightDp();
            AbsoluteLayout.SetLayoutBounds(btnMenu, new Rect(1, (sbh + 8) / height, 44, 44));
            _menuBtnAdjusted = true;
        }
    }

    /// <summary>
    /// 应用上次保存的背景颜色，并同步导航栏背景色（消除顶部白色留白）
    /// </summary>
    private void ApplySavedBackgroundColor()
    {
        var bg = AppTheme.GetBackgroundColor();
        BackgroundColor = bg;
        AppTheme.ApplyNavigationBar(this, bg);
        AppTheme.ApplyStatusBar(this, bg);
    }

    // 右上角菜单按钮：显示菜单浮层
    private void OnMenuClicked(object sender, EventArgs e)
    {
        menuOverlay.IsVisible = true;
    }

    // 点击浮层外部：关闭菜单
    private void OnMenuOverlayTapped(object sender, TappedEventArgs e)
    {
        menuOverlay.IsVisible = false;
    }

    // 菜单项：个人中心
    private async void OnMenuItemProfile(object sender, TappedEventArgs e)
    {
        menuOverlay.IsVisible = false;
        await Navigation.PushAsync(new ProfilePage());
    }

    private async Task LoadRecipes()
    {
        var list = await _dbService.GetAllRecipesAsync();
        _allRecipes = list;
        ApplyFilter();
    }

    /// <summary>
    /// 按当前分类/搜索词过滤出全量结果，然后只显示第一页。
    /// 集合整体替换（只发一次通知），避免 Clear+Add 逐条重绘。
    /// </summary>
    private void ApplyFilter()
    {
        var keyword = SearchBarRecipe.Text?.Trim() ?? string.Empty;
        var query = _allRecipes.AsQueryable();

        if (_currentCategory != "全部")
        {
            query = query.Where(r => r.Category == _currentCategory);
        }

        if (!string.IsNullOrEmpty(keyword))
        {
            query = query.Where(r =>
                r.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                r.Category.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        _filteredAll = query.ToList();
        // 重置分页：只显示第一页
        _loadedPageEnd = Math.Min(PageSize, _filteredAll.Count);
        RecipeList = new ObservableCollection<Recipe>(_filteredAll.Take(_loadedPageEnd));
    }

    /// <summary>
    /// 外层 ScrollView 滚动触底：加载下一页。
    /// （CollectionView 嵌在 ScrollView 里不会自己滚动，必须监听外层滚动位置）
    /// </summary>
    private void OnMainScrollScrolled(object? sender, ScrolledEventArgs e)
    {
        if (_loadedPageEnd >= _filteredAll.Count)
            return; // 已全部加载

        double viewport = mainScroll.Height;
        double content = mainScroll.ContentSize.Height;
        // 距离底部不足 300 个单位时预加载下一页
        if (content - viewport - e.ScrollY < 300)
        {
            LoadNextPage();
        }
    }

    /// <summary>追加下一页数据到现有集合</summary>
    private void LoadNextPage()
    {
        if (_loadedPageEnd >= _filteredAll.Count)
            return;

        int nextEnd = Math.Min(_loadedPageEnd + PageSize, _filteredAll.Count);
        for (int i = _loadedPageEnd; i < nextEnd; i++)
        {
            RecipeList.Add(_filteredAll[i]);
        }
        _loadedPageEnd = nextEnd;
    }

    private void FilterRecipes() => ApplyFilter();

    #region 分类按钮事件
    private void OnCategoryAll(object sender, EventArgs e)
    {
        _currentCategory = "全部";
        UpdateCategoryButtonStyle(sender);
        FilterRecipes();
    }
    private void OnCategoryMeat(object sender, EventArgs e)
    {
        _currentCategory = "荤菜";
        UpdateCategoryButtonStyle(sender);
        FilterRecipes();
    }
    private void OnCategoryVegetable(object sender, EventArgs e)
    {
        _currentCategory = "素菜";
        UpdateCategoryButtonStyle(sender);
        FilterRecipes();
    }
    private void OnCategoryStaple(object sender, EventArgs e)
    {
        _currentCategory = "主食";
        UpdateCategoryButtonStyle(sender);
        FilterRecipes();
    }
    private void OnCategorySoup(object sender, EventArgs e)
    {
        _currentCategory = "汤品";
        UpdateCategoryButtonStyle(sender);
        FilterRecipes();
    }
    private void OnCategoryDessert(object sender, EventArgs e)
    {
        _currentCategory = "甜品";
        UpdateCategoryButtonStyle(sender);
        FilterRecipes();
    }

    private void UpdateCategoryButtonStyle(object clickedBtnObj)
    {
        var buttons = new List<Button>
        {
            btnAll, btnMeat, btnVegetable, btnStaple, btnSoup, btnDessert
        };
        foreach (var btn in buttons)
        {
            if (btn == clickedBtnObj)
            {
                btn.BackgroundColor = Color.FromHex("#ffb84d");
                btn.TextColor = Colors.Black;
            }
            else
            {
                btn.BackgroundColor = Color.FromHex("#eeeeee");
                btn.TextColor = Colors.Black;
            }
        }
    }
    #endregion

    private CancellationTokenSource? _searchCts;
    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        // 防抖：停止输入 300ms 后才过滤，避免每敲一个字就全量重建列表
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;
        Task.Delay(300, token).ContinueWith(_ =>
        {
            if (!token.IsCancellationRequested)
                ApplyFilter();
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    // 悬浮加号：新建菜谱，从DI获取RecipeEditPage实例
    private async void OnAddRecipeClicked(object sender, EventArgs e)
    {
        var editPage = Handler!.MauiContext!.Services.GetRequiredService<RecipeEditPage>();
        await Navigation.PushAsync(editPage);
    }

    // 点击标题：按当前分类随机选一道菜，列表只显示这道菜
    private async void OnTitleTapped(object sender, TappedEventArgs e)
    {
        var candidates = _allRecipes
            .Where(r => _currentCategory == "全部" || r.Category == _currentCategory)
            .ToList();

        if (candidates.Count == 0)
        {
            await DisplayAlert("提示", "当前分类下暂无菜谱", "确定");
            return;
        }

        var pick = candidates[Random.Shared.Next(candidates.Count)];
        RecipeList.Clear();
        RecipeList.Add(pick);
    }

    // 卡片点击：打开编辑，给页面传入recipe
    private async void OnCardTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is Recipe recipe)
        {
            var editPage = Handler!.MauiContext!.Services.GetRequiredService<RecipeEditPage>();
            // 调用EditPage的Load方法填充数据，代替重载构造函数！！【重点】
            editPage.LoadRecipeForEdit(recipe);
            await Navigation.PushAsync(editPage);
        }
    }
}
