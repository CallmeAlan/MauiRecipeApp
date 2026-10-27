using Android.Views;
using Microsoft.Maui.Handlers;

namespace MauiRecipeApp.Platforms.Android;

/// <summary>
/// 自定义 SearchBar Handler：去掉安卓原生搜索框底部的下划线
/// </summary>
public class CustomSearchBarHandler : SearchBarHandler
{
    protected override void ConnectHandler(AndroidX.AppCompat.Widget.SearchView platformView)
    {
        base.ConnectHandler(platformView);

        try
        {
            // search_plate 是安卓 SearchView 底部下划线的背景层，将其设为透明
            var plateId = platformView.Resources?.GetIdentifier(
                "search_plate", "id", platformView.Context?.PackageName);

            if (plateId is int id && id != 0)
            {
                var plate = platformView.FindViewById(id);
                plate?.SetBackgroundColor(global::Android.Graphics.Color.Transparent);
            }
        }
        catch
        {
            // 部分主题下可能找不到该资源，忽略即可
        }
    }
}
