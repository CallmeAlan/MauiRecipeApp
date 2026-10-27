
using Microsoft.Maui.Controls;
using Microsoft.Maui.Media;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using System.ComponentModel;
using System.Text.RegularExpressions;

namespace MauiRecipeApp;

public partial class RecipeEditPage : ContentPage
{
    private readonly RecipeDbService _dbService;
    private Recipe _currentRecipe = new();
    private string _selectedImagePath = string.Empty;

    // ---- 动态步骤列表 ----
    // 三个并行列表分别持有数据/多行输入框/编号Label，与 stepsContainer.Children 的行一一对应
    private readonly List<StepItem> _steps = new();
    private readonly List<Editor> _stepEditors = new();
    private readonly List<Label> _stepNumbers = new();
    private readonly List<Button> _stepRemoveBtns = new();

    // ✅ DI只认【无参构造】，不能有带Recipe的构造函数
    public RecipeEditPage(RecipeDbService dbService)
    {
        InitializeComponent();
        _dbService = dbService;
        ResetNewRecipeUI();
    }

    // 与主页共用同一背景色设置，并同步导航栏背景色
    protected override void OnAppearing()
    {
        base.OnAppearing();
        var bg = AppTheme.GetBackgroundColor();
        BackgroundColor = bg;
        AppTheme.ApplyNavigationBar(this, bg);
        AppTheme.ApplyStatusBar(this, bg);
        // 透明状态栏后：内容顶部下移状态栏高度，避免被遮挡
        contentScroll.Padding = new Thickness(15, 15 + AppTheme.GetStatusBarHeightDp(), 15, 15);
    }

    /// <summary>
    /// 新建菜谱：清空表单，隐藏删除按钮
    /// </summary>
    private void ResetNewRecipeUI()
    {
        _currentRecipe = new Recipe();
        _selectedImagePath = string.Empty;

        txtName.Text = string.Empty;
        pickerCategory.SelectedItem = null;
        txtDuration.Text = string.Empty;
        edtIngredients.Text = string.Empty;
        imgPreview.Source = null;

        // 步骤列表：重置为空并默认给一行空步骤（1、）
        ClearSteps();
        AddStep();

        btnDelete.IsVisible = false;
    }

    /// <summary>
    /// 外部调用：加载已有菜谱进行编辑（替代重载构造函数，DI标准做法）
    /// </summary>
    public void LoadRecipeForEdit(Recipe recipe)
    {
        _currentRecipe = recipe;
        _selectedImagePath = recipe.ImagePath;

        txtName.Text = recipe.Name;
        pickerCategory.SelectedItem = recipe.Category;
        txtDuration.Text = recipe.DurationMinutes.ToString();
        edtIngredients.Text = recipe.Ingredients;
        // 步骤：智能解析（兼容新旧格式，多行步骤不会被拆开）
        ClearSteps();
        var stepTexts = ParseStepsToTexts(recipe.Steps);
        foreach (var text in stepTexts)
        {
            AddStep(text, focus: false);
        }
        if (_steps.Count == 0)
        {
            AddStep();
        }
        // 图片加载：支持两种来源
        //  1) 绝对路径的本地文件（用户选择的照片，存 AppData）
        //  2) 相对资源名（Resources\Images 内置默认图，如 foodType01.jpg，由 MAUI 解析到 drawable）
        if (!string.IsNullOrEmpty(recipe.ImagePath))
        {
            if (Path.IsPathRooted(recipe.ImagePath) && File.Exists(recipe.ImagePath))
            {
                imgPreview.Source = ImageSource.FromFile(recipe.ImagePath);
            }
            else
            {
                // 内置资源名：交给 MAUI 按资源加载（drawable/foodType01 等）
                imgPreview.Source = ImageSource.FromFile(recipe.ImagePath);
            }
        }
        else
        {
            imgPreview.Source = null;
        }
        btnDelete.IsVisible = true;
    }

    // 选择图片
    private async void OnPickImageClicked(object sender, EventArgs e)
    {
        try
        {
            var photo = await MediaPicker.PickPhotoAsync();
            if (photo == null) return;

            // 读取图片流，并保存到AppData
            using var stream = await photo.OpenReadAsync();

            string targetFileName = $"{Guid.NewGuid()}{Path.GetExtension(photo.FullPath)}";
            string targetPath = Path.Combine(FileSystem.AppDataDirectory, targetFileName);

            using var fs = new FileStream(targetPath, FileMode.Create, FileAccess.Write);
            await stream.CopyToAsync(fs);

            // ✅ 保存路径，并且直接从本地文件加载预览（修复图片不刷新）
            _selectedImagePath = targetPath;
            imgPreview.Source = ImageSource.FromFile(targetPath);
        }
        catch (Exception ex)
        {
            await DisplayAlert("错误", ex.Message, "确定");
        }
    }

    // 保存菜谱
    private async void OnSaveRecipeClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtName.Text))
        {
            await DisplayAlert("提示", "请输入菜谱名称", "确定");
            return;
        }
        if (pickerCategory.SelectedItem is not string category)
        {
            await DisplayAlert("提示", "请选择分类", "确定");
            return;
        }
        if (!int.TryParse(txtDuration.Text, out int duration))
        {
            await DisplayAlert("提示", "耗时请输入数字", "确定");
            return;
        }

        _currentRecipe.Name = txtName.Text;
        _currentRecipe.Category = category;
        _currentRecipe.DurationMinutes = duration;
        _currentRecipe.Ingredients = edtIngredients.Text;
        // 步骤：按 \u001F 分隔拼回 "1、xxx\u001F2、yyy" 格式（编号自动生成）。
        // 注意：不能用 \n 分隔——步骤内容可能包含换行（多行输入），用 \n 会无法区分步骤边界。
        _currentRecipe.Steps = string.Join(StepSeparator.ToString(), _steps.Select((s, i) => $"{i + 1}、{s.Text}"));

        if (!string.IsNullOrEmpty(_selectedImagePath))
        {
            // 用户选择了新图片：删除旧本地文件与旧缩略图（内置资源名时 File.Exists=false，不会误删资源）
            if (!string.IsNullOrEmpty(_currentRecipe.ImagePath) && File.Exists(_currentRecipe.ImagePath))
            {
                File.Delete(_currentRecipe.ImagePath);
            }
            ImageThumbnail.DeleteIfExists(_currentRecipe.ThumbnailPath);

            _currentRecipe.ImagePath = _selectedImagePath;
            // 同步生成缩略图（首页列表用）；失败则留空，列表自动回退原图
            _currentRecipe.ThumbnailPath = await ImageThumbnail.CreateAsync(_selectedImagePath) ?? string.Empty;
        }
        else if (string.IsNullOrEmpty(_currentRecipe.ImagePath))
        {
            // 未选图片且当前无图（仅新增菜谱时）：按分类自动给默认内置图。
            // 编辑已有菜谱时当前有图，不会覆盖用户已有图片。
            var defaultImage = RecipeImages.GetDefaultImageForCategory(category);
            if (defaultImage != null)
            {
                _currentRecipe.ImagePath = defaultImage;
            }
            // 内置资源图无需缩略图
            _currentRecipe.ThumbnailPath = string.Empty;
        }

        await _dbService.SaveRecipeAsync(_currentRecipe);
        await Navigation.PopAsync();
    }

    // 删除菜谱
    private async void OnDeleteRecipeClicked(object sender, EventArgs e)
    {
        var confirm = await DisplayAlert("确认删除", $"确定删除【{_currentRecipe.Name}】？", "删除", "取消");
        if (!confirm) return;

        if (!string.IsNullOrEmpty(_currentRecipe.ImagePath) && File.Exists(_currentRecipe.ImagePath))
        {
            File.Delete(_currentRecipe.ImagePath);
        }
        ImageThumbnail.DeleteIfExists(_currentRecipe.ThumbnailPath);
        await _dbService.DeleteRecipeAsync(_currentRecipe);
        await Navigation.PopAsync();
    }

    // ================= 动态步骤列表 =================

    /// <summary>
    /// 追加一行步骤（默认聚焦新行输入框）。编号在 RefreshStepNumbers 中统一按行序生成。
    /// </summary>
    private void AddStep(string? text = null, bool focus = true)
    {
        var item = new StepItem { Text = text ?? string.Empty };
        _steps.Add(item);

        var numberLabel = new Label
        {
            FontSize = 15,
            TextColor = Color.FromArgb("#333333"),
            VerticalOptions = LayoutOptions.Center,
            WidthRequest = 34,
        };
        // 多行输入框（Editor）：回车 = 换行，新增步骤用"＋ 添加步骤"按钮
        var editor = new Editor
        {
            Placeholder = $"步骤 {_steps.Count}",
            AutoSize = EditorAutoSizeOption.TextChanges,
            VerticalOptions = LayoutOptions.Center,
            MinimumHeightRequest = 44,
        };
        // 关键：动态创建的输入框必须绑定到 StepItem.Text（双向），
        // 否则用户输入的内容只留在控件里，保存时读不到，导致步骤内容丢失。
        editor.BindingContext = item;
        editor.SetBinding(Editor.TextProperty, nameof(StepItem.Text));

        var removeBtn = new Button
        {
            Text = "✕",
            FontSize = 14,
            TextColor = Color.FromArgb("#999999"),
            BackgroundColor = Colors.Transparent,
            WidthRequest = 36,
            HeightRequest = 36,
            Padding = 0,
            CommandParameter = item,
            VerticalOptions = LayoutOptions.Center,
        };
        removeBtn.Clicked += OnRemoveStepClicked;
        _stepRemoveBtns.Add(removeBtn);

        var row = new Grid
        {
            ColumnSpacing = 6,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto },
            },
        };
        row.Add(numberLabel, 0);
        row.Add(editor, 1);
        row.Add(removeBtn, 2);

        stepsContainer.Children.Add(row);
        _stepNumbers.Add(numberLabel);
        _stepEditors.Add(editor);
        RefreshStepNumbers();
        if (focus)
        {
            editor.Focus();
        }
    }

    /// <summary>
    /// 删除指定步骤，删除后编号自动重排（至少保留一行）
    /// </summary>
    private void RemoveStep(StepItem item)
    {
        int idx = _steps.IndexOf(item);
        if (idx < 0) return;

        _steps.RemoveAt(idx);
        _stepEditors.RemoveAt(idx);
        _stepNumbers.RemoveAt(idx);
        _stepRemoveBtns.RemoveAt(idx);
        stepsContainer.Children.RemoveAt(idx);

        if (_steps.Count == 0)
        {
            AddStep(); // 至少保留一行
        }
        else
        {
            RefreshStepNumbers();
        }
    }

    /// <summary>
    /// 清空全部步骤行
    /// </summary>
    private void ClearSteps()
    {
        _steps.Clear();
        _stepEditors.Clear();
        _stepNumbers.Clear();
        _stepRemoveBtns.Clear();
        stepsContainer.Children.Clear();
    }

    /// <summary>
    /// 步骤分隔符：\u001F（单位分隔符，普通文本不会出现）。
    /// 步骤内容允许包含换行（多行输入），因此不能用 \n 区分步骤。
    /// </summary>
    private const char StepSeparator = '\u001F';

    /// <summary>
    /// 将数据库中的 Steps 字符串解析为步骤文本列表（去掉编号），兼容两种格式：
    ///  1) 新格式：以 \u001F 分隔，每段 = 一个步骤（段内可含 \n 换行）
    ///  2) 旧格式：以 \n 分隔，按"行首编号"分组——编号行开新步骤，无编号行并入上一步
    /// </summary>
    private static List<string> ParseStepsToTexts(string? steps)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(steps)) return result;

        // 新格式：出现分隔符 → 按 \u001F 分段，每段一个步骤
        var blocks = steps.Split(StepSeparator);
        if (blocks.Length > 1)
        {
            foreach (var block in blocks)
            {
                var text = Regex.Replace(block, @"^\s*\d+\s*[、.．]\s*", "").Trim();
                result.Add(text);
            }
            return result;
        }

        // 旧格式：按行处理，行首带编号 → 新步骤；无编号 → 并入上一步（保留换行）
        foreach (var line in steps.Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (Regex.IsMatch(line, @"^\s*\d+\s*[、.．]"))
            {
                result.Add(Regex.Replace(line, @"^\s*\d+\s*[、.．]\s*", "").Trim());
            }
            else if (result.Count > 0)
            {
                result[^1] += "\n" + line.Trim();
            }
            else
            {
                // 整段都没有编号（最早期的纯文本数据）：整体作为第一步
                result.Add(line.Trim());
            }
        }
        return result;
    }

    /// <summary>
    /// 统一重编号：第 i 行显示 "i+1、"
    /// </summary>
    private void RefreshStepNumbers()
    {
        for (int i = 0; i < _stepNumbers.Count; i++)
        {
            _stepNumbers[i].Text = $"{i + 1}、";
        }
    }

    // "＋ 添加步骤"按钮
    private void OnAddStepClicked(object? sender, EventArgs e)
    {
        AddStep();
    }

    // 行内 ✕ 按钮：删除该行
    private void OnRemoveStepClicked(object? sender, EventArgs e)
    {
        if (sender is not Button btn || btn.CommandParameter is not StepItem item) return;
        RemoveStep(item);
    }

    // 印章水印浓度（0.1 ~ 1.0，越小印章越淡/越透明，默认 0.6）
    private double _sealOpacity = 1;

    // 分享：生成当前页面长截图（含全部内容），调起系统分享
    private async void OnShareClicked(object sender, EventArgs e)
    {
        try
        {
            // 截图前临时隐藏：底部按钮区、选图按钮、添加步骤按钮、每行删除按钮（截完恢复）
            string? filePath = null;
            SetShareHiddenElements(true);
            try
            {
                filePath = await CaptureLongScreenshotAsync();
            }
            finally
            {
                SetShareHiddenElements(false);
            }

            if (string.IsNullOrEmpty(filePath))
            {
                await DisplayAlert("提示", "截图生成失败，请重试", "确定");
                return;
            }

            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "分享菜谱",
                File = new ShareFile(filePath)
            });
        }
        catch (Exception ex)
        {
            await DisplayAlert("错误", ex.Message, "确定");
        }
    }

    /// <summary>
    /// 截图时隐藏/恢复不应出现在分享图中的元素（删除/保存按钮、选图、添加步骤、行删除按钮）。
    /// </summary>
    private void SetShareHiddenElements(bool hidden)
    {
        if (btnArea != null) btnArea.IsVisible = !hidden;
        if (btnPickImage != null) btnPickImage.IsVisible = !hidden;
        if (btnAddStep != null) btnAddStep.IsVisible = !hidden;
        foreach (var btn in _stepRemoveBtns)
        {
            if (btn != null) btn.IsVisible = !hidden;
        }
    }

#if ANDROID
    // 安卓：长截图——重新测量让内容全部展开，一次性绘制成完整长图
    private async Task<string?> CaptureLongScreenshotAsync()
    {
        var contentView = contentLayout.Handler?.PlatformView as global::Android.Views.View;
        if (contentView == null) return null;

        int width = contentView.Width;
        if (width <= 0) return null;

        try
        {
            // 1. 重新测量：宽度固定，高度不限制 → 内容全部展开（不含按钮区）
            contentView.Measure(
                global::Android.Views.View.MeasureSpec.MakeMeasureSpec(width, global::Android.Views.MeasureSpecMode.Exactly),
                global::Android.Views.View.MeasureSpec.MakeMeasureSpec(0, global::Android.Views.MeasureSpecMode.Unspecified));
            int fullHeight = contentView.MeasuredHeight;
            if (fullHeight <= 0) return null;

            int originalHeight = contentView.Height;

            // 2. 布局到完整高度（截完立即恢复，用户看不到变化）
            contentView.Layout(0, 0, width, fullHeight);

            // 3. 绘制到长图位图
            var bitmap = global::Android.Graphics.Bitmap.CreateBitmap(
                width, fullHeight, global::Android.Graphics.Bitmap.Config.Argb8888);
            var canvas = new global::Android.Graphics.Canvas(bitmap);

            // 用页面背景色填充画布，避免透明/黑底
            var bg = BackgroundColor ?? Colors.White;
            canvas.DrawColor(global::Android.Graphics.Color.Argb(
                (int)(bg.Alpha * 255),
                (int)(bg.Red * 255),
                (int)(bg.Green * 255),
                (int)(bg.Blue * 255)));

            contentView.Draw(canvas);

            // 4. 恢复原布局
            contentView.Layout(0, 0, width, originalHeight);

            // 4.5 在菜谱名称卡片右侧空白处盖印章水印（正片叠底，模拟真实盖印效果）
            await DrawSealWatermarkAsync(canvas, contentView, width, fullHeight);

            // 5. 保存为 PNG
            string filePath = Path.Combine(FileSystem.CacheDirectory, $"recipe_share_{DateTime.Now:HHmmss}.png");
            using var fs = File.Create(filePath);
            bitmap.Compress(global::Android.Graphics.Bitmap.CompressFormat.Png, 100, fs);
            bitmap.Recycle();
            return filePath;
        }
        catch
        {
            return null;
        }
    }
#else
    // 其他平台：降级为单屏截图（不做长图拼接）
    private async Task<string?> CaptureLongScreenshotAsync()
    {
        var screenshot = await Screenshot.Default.CaptureAsync();
        if (screenshot == null) return null;

        string filePath = Path.Combine(FileSystem.CacheDirectory, $"recipe_share_{DateTime.Now:HHmmss}.png");
        using var fs = File.Create(filePath);
        await screenshot.CopyToAsync(fs);
        return filePath;
    }
#endif

#if ANDROID
    /// <summary>
    /// 分享长截图盖印章水印：默认放在「菜谱名称」卡片右侧空白处（标签右侧），
    /// 水印图为白底红印（白字红章），用正片叠底绘制，白色区域不改变原图、红色区域呈现印泥色。
    /// </summary>
    private async Task DrawSealWatermarkAsync(global::Android.Graphics.Canvas canvas, global::Android.Views.View contentView, int canvasWidth, int canvasHeight)
    {
        try
        {
            var seal = await LoadSealBitmapAsync();
            if (seal == null) return;

            try
            {
                float sealSize;
                float dstX;
                float dstY;

                // 定位菜谱名称卡片（cardName）在长图坐标系中的位置
                var nameCard = cardName.Handler?.PlatformView as global::Android.Views.View;
                if (nameCard != null)
                {
                    // 递归累加父级 Left/Top，得到卡片相对 contentView 的坐标（此时已按全高重新布局）
                    int cardLeft = 0;
                    int cardTop = 0;
                    var v = nameCard;
                    while (v != null && !ReferenceEquals(v, contentView))
                    {
                        cardLeft += v.Left;
                        cardTop += v.Top;
                        v = v.Parent as global::Android.Views.View;
                    }

                    int cardWidth = nameCard.Width;
                    int cardHeight = nameCard.Height;

                    // 印章尺寸：不超过卡片高度的 60%，同时不超过画布宽度的 13%（防止过大盖住其他内容）
                    sealSize = Math.Min(canvasWidth * 0.13f, cardHeight * 0.6f);

                    // 位置：卡片右上角内侧（“菜谱名称”标签右侧的空白处），
                    // 竖直方向上略偏上，避开输入框文字
                    float rightGap = Math.Max(10f, cardWidth * 0.03f);
                    dstX = cardLeft + cardWidth - sealSize - rightGap;
                    dstY = cardTop + Math.Max(6f, (cardHeight - sealSize) * 0.35f);
                }
                else
                {
                    // 兜底：找不到卡片时回退到右下角
                    sealSize = canvasWidth * 0.18f;
                    float margin = canvasWidth * 0.04f;
                    dstX = canvasWidth - sealSize - margin;
                    dstY = canvasHeight - sealSize - margin;
                }

                var dst = new global::Android.Graphics.RectF(dstX, dstY, dstX + sealSize, dstY + sealSize);

                var paint = new global::Android.Graphics.Paint
                {
                    AntiAlias = true,
                    FilterBitmap = true
                };

                // 透明度 → 颜色缩放因子：Multiply 模式下 Paint.Alpha 不生效，
                // 用颜色矩阵把印章红色按浓度调淡（白色区域不受影响，仍透出原图）。
                float factor = (float)(0.25 + 0.75 * Math.Clamp(_sealOpacity, 0.1, 1.0));
                var cm = new global::Android.Graphics.ColorMatrix();
                cm.Set(new float[]
                {
                    factor, 0, 0, 0, 0,
                    0, factor, 0, 0, 0,
                    0, 0, factor, 0, 0,
                    0, 0, 0, 1, 0
                });
                paint.SetColorFilter(new global::Android.Graphics.ColorMatrixColorFilter(cm));

                // 正片叠底：白色像素对原图无影响，红色印章像素叠加成印泥色
                paint.SetXfermode(new global::Android.Graphics.PorterDuffXfermode(
                    global::Android.Graphics.PorterDuff.Mode.Multiply));
                canvas.DrawBitmap(seal, null, dst, paint);
            }
            finally
            {
                seal.Recycle();
            }
        }
        catch
        {
            // 水印加载或绘制失败不阻断分享
        }
    }

    /// <summary>
    /// 从 App 包内加载印章水印图（Resources/Raw/seal.png，MauiAsset）。
    /// </summary>
    private async Task<global::Android.Graphics.Bitmap?> LoadSealBitmapAsync()
    {
        try
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync("seal.png");
            var opts = new global::Android.Graphics.BitmapFactory.Options { InScaled = false };
            return global::Android.Graphics.BitmapFactory.DecodeStream(stream, null, opts);
        }
        catch
        {
            return null;
        }
    }
#endif
}

/// <summary>
/// 单行步骤条目（动态步骤列表的数据单元，支持属性通知供绑定使用）
/// </summary>
public class StepItem : INotifyPropertyChanged
{
    private string _text = string.Empty;
    public string Text
    {
        get => _text;
        set
        {
            if (_text != value)
            {
                _text = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
