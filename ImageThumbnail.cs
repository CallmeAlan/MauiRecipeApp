using System.IO;
using Microsoft.Maui.Storage;

namespace MauiRecipeApp;

/// <summary>
/// 缩略图生成工具：为本地图片生成一张最长边 400px 的小图存到缓存目录。
/// - 首页列表加载缩略图，避免直接解码手机原图导致卡顿
/// - 内置默认图（资源名，非本地文件）不生成缩略图，列表自动回退原图
/// - 非 Android 平台或生成失败时返回 null，列表自动回退原图，功能不受影响
/// </summary>
public static class ImageThumbnail
{
    private const int MaxEdge = 400;

    /// <summary>
    /// 为本地图片生成缩略图，返回缩略图路径；源不是本地文件或失败时返回 null。
    /// </summary>
    public static async Task<string?> CreateAsync(string? sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            return null;

        try
        {
            var thumbDir = Path.Combine(FileSystem.CacheDirectory, "thumbs");
            Directory.CreateDirectory(thumbDir);
            string thumbPath = Path.Combine(thumbDir, $"{Guid.NewGuid():N}_thumb.jpg");

            bool ok = await Task.Run(() => CreateThumbnail(sourcePath, thumbPath));
            return ok ? thumbPath : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>删除缩略图文件（换图/删菜谱时清理缓存，防残留）</summary>
    public static void DeleteIfExists(string? thumbPath)
    {
        if (!string.IsNullOrWhiteSpace(thumbPath) && File.Exists(thumbPath))
        {
            try { File.Delete(thumbPath); } catch { }
        }
    }

    private static bool CreateThumbnail(string src, string dst)
    {
#if ANDROID
        try
        {
            // 先只读尺寸，计算采样率，避免一次性解码大图
            using var boundsOpts = new Android.Graphics.BitmapFactory.Options { InJustDecodeBounds = true };
            Android.Graphics.BitmapFactory.DecodeFile(src, boundsOpts);
            if (boundsOpts.OutWidth <= 0 || boundsOpts.OutHeight <= 0)
                return false;

            int sample = 1;
            while (boundsOpts.OutWidth / (sample * 2) >= MaxEdge && boundsOpts.OutHeight / (sample * 2) >= MaxEdge)
                sample *= 2;

            using var decodeOpts = new Android.Graphics.BitmapFactory.Options { InSampleSize = sample };
            using var bitmap = Android.Graphics.BitmapFactory.DecodeFile(src, decodeOpts);
            if (bitmap == null)
                return false;

            int w = bitmap.Width, h = bitmap.Height;
            if (w > MaxEdge || h > MaxEdge)
            {
                double scale = Math.Min((double)MaxEdge / w, (double)MaxEdge / h);
                int nw = Math.Max(1, (int)(w * scale));
                int nh = Math.Max(1, (int)(h * scale));
                using var scaled = Android.Graphics.Bitmap.CreateScaledBitmap(bitmap, nw, nh, true);
                using var fs = new FileStream(dst, FileMode.Create, FileAccess.Write);
                scaled.Compress(Android.Graphics.Bitmap.CompressFormat.Jpeg, 85, fs);
            }
            else
            {
                using var fs = new FileStream(dst, FileMode.Create, FileAccess.Write);
                bitmap.Compress(Android.Graphics.Bitmap.CompressFormat.Jpeg, 85, fs);
            }
            return File.Exists(dst);
        }
        catch
        {
            return false;
        }
#else
        return false;
#endif
    }
}
