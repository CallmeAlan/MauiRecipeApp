using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace MauiRecipeApp;

public partial class ProfilePage : ContentPage
{
    public ProfilePage()
    {
        InitializeComponent();
    }

    // 进入页面：应用全局背景色 + 更新当前颜色预览 + 同步导航栏背景色
    protected override void OnAppearing()
    {
        base.OnAppearing();
        var bg = AppTheme.GetBackgroundColor();
        BackgroundColor = bg;
        colorPreview.BackgroundColor = bg;
        AppTheme.ApplyNavigationBar(this, bg);
        AppTheme.ApplyStatusBar(this, bg);
        // 透明状态栏后：内容顶部下移状态栏高度，避免被遮挡
        profileScroll.Padding = new Thickness(0, AppTheme.GetStatusBarHeightDp(), 0, 0);
    }

    // 点击"背景颜色"：跳转到背景颜色设置页
    private async void OnBgColorTapped(object sender, TappedEventArgs e)
    {
        await Navigation.PushAsync(new BgColorSettingsPage());
    }

    // 点击"导入菜谱"：选择 JSON 文件 → 解析 → 批量写入数据库
    private async void OnImportTapped(object sender, TappedEventArgs e)
    {
        try
        {
            var file = await PickJsonFileAsync();
            if (file == null) return; // 用户取消

            var json = await File.ReadAllTextAsync(file.FullPath);
            var result = await ImportRecipesAsync(json);
            await DisplayAlert("导入结果", result, "确定");
        }
        catch (Exception ex)
        {
            await DisplayAlert("导入失败", ex.Message, "确定");
        }
    }

    // 文件选择器：限定 JSON/文本文件
    private async Task<FileResult?> PickJsonFileAsync()
    {
        var fileType = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            [DevicePlatform.Android] = new[] { "application/json", "text/plain", "text/json" },
            [DevicePlatform.iOS] = new[] { "public.json", "public.plain-text" },
            [DevicePlatform.MacCatalyst] = new[] { "public.json", "public.plain-text" },
            [DevicePlatform.WinUI] = new[] { ".json", ".txt" },
            [DevicePlatform.Tizen] = new[] { "application/json", "text/plain" },
        });
        return await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "选择要导入的菜谱 JSON 文件",
            FileTypes = fileType,
        });
    }

    // 解析并导入 JSON 中的菜谱列表
    private async Task<string> ImportRecipesAsync(string json)
    {
        ImportRoot? root;
        try
        {
            root = JsonSerializer.Deserialize<ImportRoot>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            return "文件格式不正确：无法解析为有效的 JSON。请使用导入模板格式。";
        }

        if (root?.Recipes == null || root.Recipes.Count == 0)
        {
            return "文件中没有找到任何菜谱（需要顶层 recipes 数组）。";
        }

        var dbService = Handler!.MauiContext!.Services.GetRequiredService<RecipeDbService>();
        var validCategories = RecipeImages.CategoryDefaultImages.Keys;

        int ok = 0, skipped = 0, failed = 0;
        foreach (var item in root.Recipes)
        {
            try
            {
                // 名称必填
                if (string.IsNullOrWhiteSpace(item.Name))
                {
                    skipped++;
                    continue;
                }
                // 分类必须合法（荤菜/素菜/主食/汤品/甜品）
                var category = item.Category?.Trim() ?? string.Empty;
                if (!validCategories.Contains(category))
                {
                    skipped++;
                    continue;
                }

                var recipe = new Recipe
                {
                    Name = item.Name.Trim(),
                    Category = category,
                    DurationMinutes = Math.Max(0, item.DurationMinutes),
                    Ingredients = item.Ingredients?.Trim() ?? string.Empty,
                    // 步骤数组：编号自动生成，\u001F 分隔（兼容多行步骤内容）
                    Steps = item.Steps != null && item.Steps.Count > 0
                        ? string.Join("\u001F", item.Steps.Select((s, i) => $"{i + 1}、{s}"))
                        : string.Empty,
                    // 图片：提供有效本地路径则使用；否则按分类自动配默认图
                    ImagePath = !string.IsNullOrWhiteSpace(item.Image) && File.Exists(item.Image)
                        ? item.Image
                        : (RecipeImages.GetDefaultImageForCategory(category) ?? string.Empty),
                };
                // 同步生成缩略图（本地图片才生成，默认图资源名自动跳过）
                recipe.ThumbnailPath = await ImageThumbnail.CreateAsync(recipe.ImagePath) ?? string.Empty;
                await dbService.SaveRecipeAsync(recipe);
                ok++;
            }
            catch
            {
                failed++;
            }
        }

        var msg = $"成功导入 {ok} 条菜谱";
        if (skipped > 0) msg += $"，跳过 {skipped} 条（名称为空或分类无效）";
        if (failed > 0) msg += $"，失败 {failed} 条";
        msg += "。";
        return msg;
    }

    // 点击"导出菜谱"：全部菜谱导出为 JSON（与导入格式一致），调起系统分享
    private async void OnExportTapped(object sender, TappedEventArgs e)
    {
        try
        {
            var dbService = Handler!.MauiContext!.Services.GetRequiredService<RecipeDbService>();
            var recipes = await dbService.GetAllRecipesAsync();
            if (recipes == null || recipes.Count == 0)
            {
                await DisplayAlert("导出菜谱", "当前没有可导出的菜谱。", "确定");
                return;
            }

            var root = new ImportRoot
            {
                Recipes = recipes.Select(r => new ImportRecipe
                {
                    Name = r.Name,
                    Category = r.Category,
                    DurationMinutes = r.DurationMinutes,
                    Ingredients = r.Ingredients,
                    // 步骤：\u001F 分隔存储（带"n、"编号），导出时拆回数组并去掉编号前缀，导入时自动重新编号
                    Steps = SplitSteps(r.Steps),
                    // 图片：仅保留本机存在的本地路径；跨设备导入时该路径无效，导入端会自动按分类配默认图
                    Image = !string.IsNullOrWhiteSpace(r.ImagePath) && File.Exists(r.ImagePath)
                        ? r.ImagePath
                        : string.Empty,
                }).ToList()
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };
            var json = JsonSerializer.Serialize(root, options);

            string filePath = Path.Combine(FileSystem.CacheDirectory,
                $"recipes_export_{DateTime.Now:yyyyMMdd_HHmmss}.json");
            await File.WriteAllTextAsync(filePath, json);

            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "导出菜谱",
                File = new ShareFile(filePath, "application/json"),
            });
        }
        catch (Exception ex)
        {
            await DisplayAlert("导出失败", ex.Message, "确定");
        }
    }

    // 步骤存储格式："1、内容\u001F2、内容"，拆回数组并去掉编号前缀
    private static List<string> SplitSteps(string stored)
    {
        if (string.IsNullOrWhiteSpace(stored)) return new List<string>();
        return stored
            .Split('\u001F', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => System.Text.RegularExpressions.Regex.Replace(s, @"^\s*\d+、\s*", string.Empty))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
    }
}

/// <summary>导入文件根结构：{"recipes": [...]}</summary>
public class ImportRoot
{
    public List<ImportRecipe> Recipes { get; set; } = new();
}

/// <summary>导入的单条菜谱（字段与 Recipe 对应，步骤为数组）</summary>
public class ImportRecipe
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public string Ingredients { get; set; } = string.Empty;
    public List<string> Steps { get; set; } = new();
    public string Image { get; set; } = string.Empty;
}
