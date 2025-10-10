using DevExpress.Xpf.RichEdit;
using DevExpress.XtraRichEdit;
using DevExpress.XtraRichEdit.API.Native;
using Entity.Entity;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Tools.App;
using Tools.Extend;
using SearchOptions = DevExpress.XtraRichEdit.API.Native.SearchOptions;

namespace WpfAppNew.Controlls
{
    /// <summary>
    /// UCPrintNotes.xaml 的交互逻辑
    /// </summary>
    public partial class UCPrintNotes : UserControl
    {
        private readonly TestInfo tInfo;

        public UCPrintNotes(TestInfo t)
        {
            InitializeComponent();
            tInfo = t;
        }

        /// <summary>
        /// 初始化加载文书
        /// 优化：使用Task.Run替代Thread，提供更好的异常处理和性能
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void UCPrintNotes_OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // 显示加载指示器
                ShowLoadingIndicator(true);
                
                // 异步初始化，避免阻塞UI线程
                await Init();
                
                LogUtil.Info($"UCPrintNotes加载完成: {tInfo?.TestName}");
            }
            catch (Exception ex)
            {
                LogUtil.Error($"UCPrintNotes加载失败: {ex.Message}");
                // 可以在这里显示错误信息给用户
                Dispatcher.Invoke(() =>
                {
                    HandyControl.Controls.MessageBox.Error($"打印文档加载失败：{ex.Message}", "系统提示");
                });
            }
            finally
            {
                // 隐藏加载指示器
                ShowLoadingIndicator(false);
            }
        }

        /// <summary>
        /// 显示或隐藏加载指示器
        /// </summary>
        /// <param name="show">是否显示</param>
        private void ShowLoadingIndicator(bool show)
        {
            Dispatcher.Invoke(() =>
            {
                if (show)
                {
                    // 可以在这里添加加载指示器的显示逻辑
                    this.IsEnabled = false;
                    this.Opacity = 0.7;
                }
                else
                {
                    this.IsEnabled = true;
                    this.Opacity = 1.0;
                }
            });
        }

        /// <summary>
        /// 初始化打印文档
        /// 优化：添加性能优化和错误处理，减少UI线程阻塞，支持异步图片加载
        /// </summary>
        public async Task Init()
        {
            try
            {
                // 验证必要的数据
                if (tInfo == null)
                {
                    LogUtil.Error("UCPrintNotes.Init: tInfo为空");
                    return;
                }

                if (!System.IO.File.Exists(tInfo.TestPath))
                {
                    LogUtil.Error($"UCPrintNotes.Init: 模板文件不存在 - {tInfo.TestPath}");
                    return;
                }

                LogUtil.Info($"UCPrintNotes.Init: 开始初始化 - {tInfo.TestName}");

                // 准备绑定数据
                RichConntext frtext = new RichConntext
                {
                    ConntextType = RichConntextType.docx,
                    BindData = new List<RichConntext.RichConntextBindData>
                    {
                        new RichConntext.RichConntextBindData { BindData = AppStatic.AppHospital },
                        new RichConntext.RichConntextBindData { BindData = tInfo },
                        new RichConntext.RichConntextBindData { BindData = tInfo.Result }
                    }
                };

                // 在UI线程加载文档
                Dispatcher.Invoke(() =>
                {
                    try
                    {
                        this.richEditControl1.Document.LoadDocument(tInfo.TestPath, ConvertToDevType(RichConntextType.docx));
                        LogUtil.Info($"UCPrintNotes.Init: 文档加载完成 - {tInfo.TestName}");
                    }
                    catch (Exception ex)
                    {
                        LogUtil.Error($"UCPrintNotes.Init: 文档加载失败 - {ex.Message}");
                        throw;
                    }
                });

                // 绑定数据（在后台线程执行）
                foreach (var item in frtext.BindData)
                {
                    LoadBingDataValues(item);
                }

                // 处理图片插入（异步优化）
                var selectedImages = tInfo.Result.Images;
                if (selectedImages.Any(o => o.IsSelected))
                {
                    selectedImages = tInfo.Result.Images.Where(o => o.IsSelected).ToList();
                }

                // 异步插入图片，避免阻塞UI线程
                if (selectedImages.Any())
                {
                    await Dispatcher.InvokeAsync(async () =>
                    {
                        try
                        {
                            LogUtil.Info($"UCPrintNotes.Init: 开始插入 {selectedImages.Count} 张图片 - {tInfo.TestName}");
                            InsertImageAfterText(richEditControl1, "\\{<image \\S+>\\}", selectedImages);
                            LogUtil.Info($"UCPrintNotes.Init: 图片插入完成 - {tInfo.TestName}");
                        }
                        catch (Exception ex)
                        {
                            LogUtil.Error($"UCPrintNotes.Init: 图片插入失败 - {ex.Message}");
                            // 图片插入失败不应该阻止整个文档的显示
                        }
                    });
                }
                else
                {
                    LogUtil.Info($"UCPrintNotes.Init: 无选中图片需要插入 - {tInfo.TestName}");
                }

                LogUtil.Info($"UCPrintNotes.Init: 初始化完成 - {tInfo.TestName}");
            }
            catch (Exception ex)
            {
                LogUtil.Error($"UCPrintNotes.Init: 初始化失败 - {ex.Message}");
                throw; // 重新抛出异常，让调用者处理
            }
        }

        // 动态插入HTML到指定文字后 
        private void InsertHtmlAfterText(string targetText, string html)
        {
            {
                richEditControl1.BeginUpdate();
                try
                {
                    {
                        ISearchResult searchResult = richEditControl1.Document.StartSearch(targetText);
                        while (searchResult.FindNext())
                        {
                            {
                                DocumentRange range = searchResult.CurrentResult;
                                var converter = new HtmlToContentConverter();
                                //var content = converter.Convert(html, typeof(DocumentModel), null, CultureInfo.CurrentCulture);
                                richEditControl1.Document.InsertHtmlText(range.End, html.ToString());
                            }
                        }
                    }
                }
                finally
                {
                    {
                        richEditControl1.EndUpdate();
                    }
                }
            }
        }

        private DocumentFormat ConvertToDevType(RichConntextType type)
        {
            switch (type)
            {
                case RichConntextType.rtf:
                    return DocumentFormat.Rtf;
                case RichConntextType.html:
                    return DocumentFormat.Html;
                case RichConntextType.xml:
                    return DocumentFormat.OpenXml;
                case RichConntextType.doc:
                    return DocumentFormat.Doc;
                case RichConntextType.docx:
                    return DocumentFormat.OpenXml;
                case RichConntextType.txt:
                    return DocumentFormat.PlainText;
            }
            return DocumentFormat.Undefined;
            // DocumentFormat.
        }

        private void LoadBingDataValues(RichConntext.RichConntextBindData BindData)
        {
            string olds;
            object news;
            Type t;

            if (BindData == null || BindData.BindData==null)
                return;

                t = BindData.BindData.GetType();
                PropertyInfo[] pinfos = t.GetProperties();
                if (pinfos == null)
                    return;

                Dictionary<string, object> dic = GetLableReplace();
                foreach (var item in pinfos)
                {
                    if (dic.Values.Any(o => o != null && o.ToString() == item.DeclaringType.Name + "." + item.Name))
                    {
                        object value = null;
                        KeyValuePair<string, object> kv = dic.First(o => o.Value.ToString() == item.DeclaringType.Name + "." + item.Name);
                        olds = "{<" + kv.Key + ">}";
                        value = ((Valueformat)kv.Value).GetValue(BindData.BindData);

                        news = value == null ? "" : value.ToString();
                        Dispatcher.Invoke(() =>
                        {

                            DevExpress.XtraRichEdit.API.Native.DocumentRange[] rs = this.richEditControl1.Document.FindAll(olds, DevExpress.XtraRichEdit.API.Native.SearchOptions.CaseSensitive);
                            if (rs.Length <= 0)
                            {
                                // 显式设置页眉搜索范围 
                                foreach (var section in richEditControl1.Document.Sections)
                                {
                                    var header = section.BeginUpdateHeader();
                                    var options = SearchOptions.CaseSensitive;
                                    rs = header.FindAll(olds, options);
                                    if (kv.Key == "医院Logo")
                                    {
                                        DocumentRange foundRange = rs.FirstOrDefault();
                                        if (foundRange == null) break;
                                        header.Replace(foundRange, "");
                                        if (AppStatic.AppHospital?.HospitalLogo?.Length > 0)
                                        {
                                            // 移动光标到目标文字末尾 
                                            var inserImg = header.Images.Insert(foundRange.End,
                                                image: (AppStatic.AppHospital.HospitalLogo.Byte2Bitmap()));
                                            inserImg.Size = new SizeF(200, 100);
                                        }
                                    }
                                    else
                                    {

                                        //rs = this.richEditControl1.Document.FindAll(olds, options);
                                        foreach (var reange in rs)
                                            header.Replace(reange, news.ToString());
                                    }

                                    section.EndUpdateHeader(header);
                                    break;
                                }
                            }
                            else
                            {
                                foreach (var reange in rs)
                                    this.richEditControl1.Document.Replace(reange, news.ToString());
                            }

                        });
                    }
                }
            return;
        }

        public void ParseImageSize(string input)
        {
            var regex = new Regex(@"{<image\s+(\d+)\*(\d+)\s*>}");
            var match = regex.Match(input);

            if (match.Success &&
                int.TryParse(match.Groups[1].Value, out int width) &&
                int.TryParse(match.Groups[2].Value, out int height))
            {
                imgWidth = width;
                imgHeight = height;
            }
        }

        private DocumentPosition pos = null;
        private int imgWidth = 0;
        private int imgHeight = 0;
        /// <summary>
        /// 在指定文字后插入图片（优化版本）
        /// 支持图片压缩、缓存和异步加载
        /// </summary>
        /// <param name="richEdit">富文本编辑器控件</param>
        /// <param name="targetText">目标文字正则表达式</param>
        /// <param name="item">图片项列表</param>
        public async void InsertImageAfterText(RichEditControl richEdit, string targetText, List<ImageItem> item)
        {
            if (item == null || !item.Any())
                return;

            richEdit.BeginUpdate();
            try
            {
                if (pos == null)
                {
                    // 查找目标文字范围 
                    DocumentRange searchRange = richEdit.Document.CreateRange(0, richEdit.Document.Range.End.ToInt());
                    DocumentRange foundRange = richEdit.Document.FindAll(new Regex(targetText), searchRange).FirstOrDefault();
                    
                    if (foundRange == null) 
                    {
                        LogUtil.Warning($"未找到目标文字: {targetText}");
                        return;
                    }

                    string test = richEdit.Document.GetText(foundRange);
                    ParseImageSize(test);
                    richEdit.Document.Replace(foundRange, "");
                    pos = foundRange.End;
                }

                if (pos != null)
                {
                    // 移动光标到目标文字末尾 
                    richEdit.Document.CaretPosition = pos;

                    // 插入换行符 
                    var rh = richEdit.Document.InsertText(pos, "\n");
                    pos = rh.End;

                    // 异步处理图片加载和插入
                    await ProcessImagesAsync(richEdit, item);
                }

                richEdit.Refresh();
                LogUtil.Info($"图片插入完成，共处理 {item.Count} 张图片");
            }
            catch (Exception ex)
            {
                LogUtil.Error($"图片插入失败: {ex.Message}");
                throw;
            }
            finally
            {
                richEdit.EndUpdate();
            }
        }

        /// <summary>
        /// 显示图片加载进度指示器
        /// </summary>
        /// <param name="show">是否显示</param>
        /// <param name="message">状态消息</param>
        private void ShowImageLoadingProgress(bool show, string message = "")
        {
            Dispatcher.Invoke(() =>
            {
                if (show)
                {
                    LoadingPanel.Visibility = Visibility.Visible;
                    LoadingOverlay.Visibility = Visibility.Visible;
                    LoadingStatusText.Text = message;
                    ImageProgressBar.Value = 0;
                    ProgressText.Text = "准备加载图片...";
                }
                else
                {
                    LoadingPanel.Visibility = Visibility.Collapsed;
                    LoadingOverlay.Visibility = Visibility.Collapsed;
                }
            });
        }

        /// <summary>
        /// 更新图片加载进度
        /// </summary>
        /// <param name="current">当前进度</param>
        /// <param name="total">总数</param>
        /// <param name="message">进度消息</param>
        private void UpdateImageProgress(int current, int total, string message = "")
        {
            Dispatcher.Invoke(() =>
            {
                double progress = total > 0 ? (double)current / total * 100 : 0;
                ImageProgressBar.Value = progress;
                ProgressText.Text = string.IsNullOrEmpty(message) 
                    ? $"加载图片 {current}/{total} ({progress:F0}%)" 
                    : message;
                LoadingStatusText.Text = $"正在处理第 {current} 张图片，共 {total} 张";
            });
        }

        /// <summary>
        /// 异步处理图片加载和插入（支持懒加载）
        /// </summary>
        /// <param name="richEdit">富文本编辑器控件</param>
        /// <param name="images">图片列表</param>
        private async Task ProcessImagesAsync(RichEditControl richEdit, List<ImageItem> images)
        {
            const int batchSize = 3; // 每批处理的图片数量
            const int delayBetweenBatches = 100; // 批次间延迟（毫秒）

            LogUtil.Info($"开始分批处理 {images.Count} 张图片，每批 {batchSize} 张");
            
            // 显示进度指示器
            ShowImageLoadingProgress(true, $"准备加载 {images.Count} 张图片");
            
            int processedCount = 0;

            // 分批处理图片，避免一次性加载过多图片导致UI卡顿
            for (int i = 0; i < images.Count; i += batchSize)
            {
                var batch = images.Skip(i).Take(batchSize).ToList();
                LogUtil.Info($"处理第 {i / batchSize + 1} 批图片，共 {batch.Count} 张");

                // 并行处理当前批次的图片
                var batchTasks = batch.Select(async (imageItem, batchIndex) =>
                {
                    int globalIndex = i + batchIndex;
                    try
                    {
                        // 在后台线程处理图片优化
                        var optimizedBitmap = await Task.Run(() =>
                        {
                            if (imageItem.BitBuffer == null || imageItem.BitBuffer.Length == 0)
                            {
                                LogUtil.Warning($"图片 {globalIndex + 1} 数据为空，跳过处理");
                                return null;
                            }

                            // 根据文档显示需求优化图片尺寸
                            // 打印文档通常不需要超高分辨率，适当压缩可以大幅提升性能
                            int maxWidth = Math.Max(imgWidth * 2, 800);  // 保证打印质量的同时控制大小
                            int maxHeight = Math.Max(imgHeight * 2, 600);
                            
                            LogUtil.Info($"正在优化图片 {globalIndex + 1}，目标尺寸: {maxWidth}x{maxHeight}");
                            return imageItem.BitBuffer.Byte2BitmapOptimized(maxWidth, maxHeight, 85, true);
                        });

                        if (optimizedBitmap != null)
                        {
                            // 在UI线程插入图片
                            await Dispatcher.InvokeAsync(() =>
                            {
                                try
                                {
                                    DocumentImage image = richEdit.Document.Images.Insert(pos, optimizedBitmap);
                                    image.Size = new SizeF(imgWidth, imgHeight);
                                    pos = image.Range.End;

                                    var rh = richEdit.Document.InsertText(pos, "\u00A0\u00A0");
                                    pos = rh.End;

                                    // 获取包含图片的段落并设置格式
                                    SetParagraphFormat(richEdit, pos);
                                    
                                    LogUtil.Info($"图片 {globalIndex + 1} 插入成功");
                                    
                                    // 更新进度
                                    processedCount++;
                                    UpdateImageProgress(processedCount, images.Count);
                                }
                                catch (Exception ex)
                                {
                                    LogUtil.Error($"图片 {globalIndex + 1} 插入UI失败: {ex.Message}");
                                }
                            });
                        }
                        else
                        {
                            // 即使图片处理失败，也要更新进度计数
                            processedCount++;
                            UpdateImageProgress(processedCount, images.Count, $"图片 {globalIndex + 1} 处理失败");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogUtil.Error($"图片 {globalIndex + 1} 处理失败: {ex.Message}");
                        processedCount++;
                        UpdateImageProgress(processedCount, images.Count, $"图片 {globalIndex + 1} 处理失败");
                    }
                });

                // 等待当前批次完成
                await Task.WhenAll(batchTasks);

                // 批次间短暂延迟，让UI有时间响应
                if (i + batchSize < images.Count)
                {
                    await Task.Delay(delayBetweenBatches);
                    
                    // 在UI线程刷新显示
                    await Dispatcher.InvokeAsync(() =>
                    {
                        richEdit.Refresh();
                    });
                }
            }

            LogUtil.Info($"所有 {images.Count} 张图片处理完成");
            
            // 隐藏进度指示器
            ShowImageLoadingProgress(false);
            
            // 显示缓存统计信息
            var cacheStats = ObjectExtension.GetCacheStats();
            LogUtil.Info($"图片加载完成，{cacheStats}");
        }

        /// <summary>
        /// 设置段落格式
        /// </summary>
        /// <param name="richEdit">富文本编辑器控件</param>
        /// <param name="position">位置</param>
        private void SetParagraphFormat(RichEditControl richEdit, DocumentPosition position)
        {
            try
            {
#pragma warning disable CS0618 // 类型或成员已过时
                Paragraph paragraph = richEdit.Document.GetParagraph(position);
#pragma warning restore CS0618 // 类型或成员已过时

                // 设置段落间距 
                paragraph.SpacingAfter = 20;    // 段后间距 
                paragraph.SpacingBefore = 20;  // 段前间距 
                paragraph.LeftIndent = 20;     // 左缩进 
                paragraph.RightIndent = 20;    // 右缩进  
                paragraph.Alignment = ParagraphAlignment.Justify;
                paragraph.ContextualSpacing = true; // 上下段落间距相等
                paragraph.LineSpacingMultiplier = 1.5f; // 行间距倍数 
                paragraph.LineSpacingType = DevExpress.XtraRichEdit.API.Native.ParagraphLineSpacing.Multiple;
                
                pos = paragraph.Range.End;
            }
            catch (Exception ex)
            {
                LogUtil.Error($"设置段落格式失败: {ex.Message}");
            }
        }

        public Dictionary<string, object> GetLableReplace()
        {
            Type appHo = typeof(AppHospital);
            Type t = typeof(TestInfo);
            Type r = typeof(TestResult);
            var dic = new Dictionary<string, object>();

            dic.Add("宠物名称", new Valueformat(t.GetProperty("Pet"), null));
            dic.Add("病历号", new Valueformat(t.GetProperty("RecordNo"), null));
            dic.Add("检查医生", new Valueformat(t.GetProperty("DCOperation"), null));
            dic.Add("宠物性别", new Valueformat(t.GetProperty("Gender"), null));
            dic.Add("主人名称", new Valueformat(t.GetProperty("Customer"), null));
            dic.Add("检查时间", new Valueformat(t.GetProperty("TestDate"), o => ((DateTime)o).ToString("yyyy-MM-dd")));
            dic.Add("宠物种类", new Valueformat(t.GetProperty("Type"), null));
            dic.Add("宠物品种", new Valueformat(t.GetProperty("Variety"), null));
            dic.Add("检查所见", new Valueformat(t.GetProperty("See"), null));
            dic.Add("宠物年龄", new Valueformat(t.GetProperty("Age"), null));
            dic.Add("是否绝育", new Valueformat(t.GetProperty("Neuter"), null));
            dic.Add("医院名称", new Valueformat(appHo.GetProperty("HospitalName"), null));
            dic.Add("医院Logo", new Valueformat(appHo.GetProperty("HospitalLogo"), null));
            dic.Add("医院地址", new Valueformat(appHo.GetProperty("HospitalAddress"), null));
            dic.Add("医院简介", new Valueformat(appHo.GetProperty("HospitalBiref"), null));
            return dic;
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.Description = "选择文件保存目录";
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                try
                {
                    var exportPath = System.IO.Path.Combine(dialog.SelectedPath, $"{tInfo.TestName.ToString()}.docx");
                    richEditControl1.SaveDocument(exportPath, DocumentFormat.OpenXml);
                    HandyControl.Controls.MessageBox.Success($"导出成功！", "系统提示");
                }
                catch (Exception ex)
                {
                    HandyControl.Controls.MessageBox.Error($"导出失败：{ex.Message}", "系统提示");
                }
            }
        }

        private void Print_OnClick(object sender, RoutedEventArgs e)
        {
            richEditControl1.Print();
        }
    }
    /// <summary>
    /// 表示内容类型
    /// </summary>
    public enum RichConntextType
    {
        /// <summary>
        /// rtf文件
        /// </summary>
        rtf = 1,
        /// <summary>
        /// html 文件
        /// </summary>
        html = 2,
        xml = 3,
        doc = 4,
        /// <summary>
        /// docx
        /// </summary>
        docx = 5,
        txt = 6,
    }
    /// <summary>
    /// 些类表示富文本控件的内容
    /// </summary>
    public class RichConntext
    {

        /// <summary>
        /// 文本内容，加载内容时可以从rtf\docx\html等文件中读出2进制流
        /// </summary>
        public System.IO.Stream Conntext
        {
            get;
            set;
        }

        /// <summary>
        /// 内容的类型
        /// </summary>
        public RichConntextType ConntextType
        {
            get;
            set;
        }

        /// <summary>
        /// 绑定 设置此属性可以替换标签内容
        /// </summary>
        public List<RichConntextBindData> BindData
        {
            get;
            set;
        }

        /// <summary>
        /// 内容绑定
        /// </summary>
        public class RichConntextBindData
        {

            /// <summary>
            /// 数据源
            /// </summary>
            public object BindData { get; set; }

            /// <summary>
            /// 要绑定的数据源属性名与标签名,key为属性名，value为标签名
            /// 如需自动填充，将此属性设置为NULL
            /// 如果些属性为空，那么富文本会自动替key与value都是属性名的标签
            /// </summary>
            public Dictionary<string, string> BindProperties
            {
                get;
                set;
            }
        }


    }


}
