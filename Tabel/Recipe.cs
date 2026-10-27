using SQLite;

namespace MauiRecipeApp;

[Table("Recipes")]
public class Recipe
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public string Ingredients { get; set; } = string.Empty;
    public string Steps { get; set; } = string.Empty;

    // 新增：图片本地路径
    public string ImagePath { get; set; } = string.Empty;

    // 新增：缩略图路径（首页列表用，避免加载原图卡顿；老数据为空时自动回退原图）
    public string ThumbnailPath { get; set; } = string.Empty;

    /// <summary>
    /// 列表展示用图片：优先缩略图（存在时），否则回退原图。
    /// 兼容老菜谱（无缩略图）与内置默认图（资源名，非本地路径）。
    /// </summary>
    [Ignore]
    public string DisplayImage =>
        !string.IsNullOrWhiteSpace(ThumbnailPath) && File.Exists(ThumbnailPath)
            ? ThumbnailPath
            : ImagePath;
}

