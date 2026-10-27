namespace MauiRecipeApp;

/// <summary>
/// 分类 → 默认图片映射（Resources\Images 下的内置资源，MAUI 按资源名加载）。
/// 编辑页新增菜谱与导入功能共用。
/// </summary>
public static class RecipeImages
{
    /// <summary>注意：MAUI Resizetizer 要求资源文件名必须全小写</summary>
    public static readonly Dictionary<string, string> CategoryDefaultImages = new()
    {
        ["荤菜"] = "foodtype01.jpg",
        ["素菜"] = "foodtype02.jpg",
        ["主食"] = "foodtype03.jpg",
        ["汤品"] = "foodtype04.jpg",
        ["甜品"] = "foodtype05.jpg",
    };

    /// <summary>根据分类获取默认图片资源名（未知分类返回 null）</summary>
    public static string? GetDefaultImageForCategory(string? category)
        => category != null && CategoryDefaultImages.TryGetValue(category, out var img) ? img : null;
}
