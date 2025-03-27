using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using DevExpress.Xpf.RichEdit;
using DevExpress.XtraRichEdit;
using DevExpress.XtraRichEdit.API.Native;
using Entity.Entity;
using Tools.App;
using Tools.Extend;
using SearchOptions = DevExpress.XtraRichEdit.API.Native.SearchOptions;

namespace WpfMain.Controlls
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
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UCPrintNotes_OnLoaded(object sender, RoutedEventArgs e)
        {
            Thread thread = new Thread(Init);
            thread.IsBackground = true;
            thread.Start(); 

        }

        public void Init()
        {
            RichConntext frtext = new RichConntext();
            frtext.ConntextType = RichConntextType.docx;
            frtext.BindData = new List<RichConntext.RichConntextBindData>();

            frtext.BindData.Add(new RichConntext.RichConntextBindData { BindData = AppStatic.AppHospital });
            frtext.BindData.Add(new RichConntext.RichConntextBindData { BindData = tInfo });
            frtext.BindData.Add(new RichConntext.RichConntextBindData { BindData = tInfo.Result });
            Dispatcher.Invoke(() =>
            {
                this.richEditControl1.Document.LoadDocument(tInfo.TestPath, ConvertToDevType(RichConntextType.docx));
            });

            foreach (var item in frtext.BindData)
                LoadBingDataValues(item);
            var a = tInfo.Result.Images;
            if (a.Any(o => o.IsSelected))
                a = tInfo.Result.Images.Where(o => o.IsSelected).ToList();
            string html = string.Empty;
            Dispatcher.Invoke(() =>
            {
                InsertImageAfterText(richEditControl1, "\\{<image \\S+>\\}", a);
                this.richEditControl1.Refresh();
            });

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

            if (BindData == null)
                return;
            if (BindData.BindData != null)
            {
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
        public void InsertImageAfterText(RichEditControl richEdit, string targetText, List<ImageItem> item)
        {
            richEdit.BeginUpdate();
            try
            {
                if (pos == null)
                {

                    // 查找目标文字范围 
                    DocumentRange searchRange = richEdit.Document.CreateRange(0, richEdit.Document.Range.End.ToInt());
                    //DocumentRange foundRange = richEdit.Document.FindAll(targetText,
                    //    DevExpress.XtraRichEdit.API.Native.SearchOptions.CaseSensitive, searchRange).FirstOrDefault();

                    DocumentRange foundRange = richEdit.Document.FindAll(new Regex(targetText), searchRange).FirstOrDefault();
                    string test = richEdit.Document.GetText(foundRange);
                    ParseImageSize(test);
                    richEdit.Document.Replace(foundRange, "");
                    if (foundRange == null) return;
                    pos = foundRange.End;
                }

                if (pos != null)
                {
                    // 移动光标到目标文字末尾 
                    richEdit.Document.CaretPosition = pos;

                    // 插入换行符 
                    var rh = richEdit.Document.InsertText(pos, "\n");
                    pos = rh.End;
                    item.ForEach(i =>
                    {
                        // 插入图片并设置布局 
                        DocumentImage image = richEdit.Document.Images.Insert(pos, i.BitBuffer.Byte2Bitmap());
                        image.Size = new SizeF(imgWidth, imgHeight);
                        pos = image.Range.End;

                        // 调整段落行距 
                        DevExpress.XtraRichEdit.API.Native.Paragraph paragraph = richEdit.Document.GetParagraph(pos);
                        paragraph.LineSpacingType = DevExpress.XtraRichEdit.API.Native.ParagraphLineSpacing.Single;
                        //paragraph.SpacingBefore = 100; // 段前5磅 
                        //paragraph.SpacingAfter = 100;  // 段后5磅 
                        //paragraph.LineSpacingType = DevExpress.XtraRichEdit.API.Native.ParagraphLineSpacing.Exactly;
                        //paragraph.LineSpacing = 3.0f;   // 行距12磅 
                        //paragraph.RightIndent = 100;
                    });
                    // 2. 获取当前段落并设置间距  
                    //paragraph.SpacingBefore = 100; // 段前5磅 
                    //paragraph.SpacingAfter = 100;  // 段后5磅 
                    //paragraph.LineSpacingType = DevExpress.XtraRichEdit.API.Native.ParagraphLineSpacing.Exactly;
                    //paragraph.LineSpacing = 620;   // 行距12磅 
                }

                richEdit.Refresh(); //.ActiveView.ReLayout();
            }
            finally
            {
                richEdit.EndUpdate();
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
                    HandyControl.Controls.MessageBox.Error($"导出失败：{ex.Message}","系统提示");
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
