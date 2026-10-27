using Microsoft.Extensions.Logging;

#if ANDROID
using MauiRecipeApp.Platforms.Android;
#endif

namespace MauiRecipeApp
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if ANDROID
            // 自定义SearchBar：去掉搜索框底部下划线
            builder.ConfigureMauiHandlers(handlers =>
            {
                handlers.AddHandler<SearchBar, CustomSearchBarHandler>();
            });
#endif

#if DEBUG
            builder.Logging.AddDebug();
#endif

            string dbPath = Path.Combine(FileSystem.AppDataDirectory, "recipes.db3");
            builder.Services.AddSingleton<RecipeDbService>(_ => new RecipeDbService(dbPath));

            //注册页面
            builder.Services.AddTransient<RecipeListPage>();
            builder.Services.AddTransient<RecipeEditPage>();

            return builder.Build();
        }
    }
}
