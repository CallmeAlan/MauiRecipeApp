using SQLite;
using Microsoft.Maui.Storage;

namespace MauiRecipeApp;

public class RecipeDbService
{
    private readonly SQLiteAsyncConnection _db;

    public RecipeDbService(string dbPath)
    {
        _db = new SQLiteAsyncConnection(dbPath);
    }

    /// <summary>
    /// 初始化数据库，不存在则创建表；老库自动迁移新增列。
    /// </summary>
    public async Task InitDbAsync()
    {
        await _db.CreateTableAsync<Recipe>();
        // 老库升级：新增缩略图列（新库 CreateTable 已带该列，此处只为老库补列，已存在时忽略异常）
        try
        {
            await _db.ExecuteAsync("ALTER TABLE Recipes ADD COLUMN ThumbnailPath TEXT");
        }
        catch (Exception)
        {
            // 列已存在，忽略
        }
    }

    /// <summary>
    /// 获取所有菜谱
    /// </summary>
    public async Task<List<Recipe>> GetAllRecipesAsync()
    {
        await InitDbAsync();
        return await _db.Table<Recipe>().ToListAsync();
    }
    /// <summary>
    /// 分页查询 + 搜索
    /// keyword为空：不搜索，返回全部分页数据
    /// </summary>
    /// <param name="keyword">搜索名称/分类</param>
    /// <param name="pageIndex">页码，从0开始</param>
    /// <param name="pageSize">每页条数</param>
    /// <returns>当前页菜谱列表</returns>
    public async Task<List<Recipe>> GetRecipesPageAsync(string keyword, int pageIndex, int pageSize = 20)
    {
        var query = _db.Table<Recipe>();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            // 搜索：名称或者分类包含关键词
            query = query.Where(r => r.Name.Contains(keyword) || r.Category.Contains(keyword));
        }

        var list = await query
            .OrderByDescending(r => r.Id)
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return list;
    }

    /// <summary>
    /// 获取总条数（用于分页显示总数量）
    /// </summary>
    public async Task<int> GetTotalCountAsync(string keyword)
    {
        var query = _db.Table<Recipe>();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(r => r.Name.Contains(keyword) || r.Category.Contains(keyword));
        }
        return await query.CountAsync();
    }

    /// <summary>
    /// 新增或更新菜谱
    /// Id=0 新增；Id>0 更新
    /// </summary>
    public async Task<int> SaveRecipeAsync(Recipe recipe)
    {
        if (recipe.Id == 0)
        {
            return await _db.InsertAsync(recipe);
        }
        else
        {
            return await _db.UpdateAsync(recipe);
        }
    }

    /// <summary>
    /// 删除菜谱
    /// </summary>
    public async Task<int> DeleteRecipeAsync(Recipe recipe)
    {
        return await _db.DeleteAsync(recipe);
    }

    /// <summary>
    /// 根据Id获取单条菜谱（编辑页面读取）
    /// </summary>
    public async Task<Recipe?> GetRecipeByIdAsync(int id)
    {
        return await _db.FindAsync<Recipe>(id);
    }
}
