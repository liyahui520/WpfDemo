using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using Entity.Enum;
using Newtonsoft.Json;
using Tools.App;
using Tools.Extend;

namespace Entity.Entity
{

    /// <summary>
    /// 检查信息
    /// </summary> 
    public class TestInfo : ObservableObject
    {
        public string Id { get; set; }

        /// <summary>
        /// 病历号
        /// </summary>
        public string RecordNo { get; set; }

        /// <summary>
        /// 检查名称
        /// </summary>
        public string TestName { get; set; }

        /// <summary>
        /// 所见
        /// </summary>
        public string See { get; set; }

        /// <summary>
        /// 大小
        /// </summary>
        public string Size { get; set; }

        /// <summary>
        /// 数量
        /// </summary>
        public string Count { get; set; }

        /// <summary>
        /// 检查医生姓名
        /// </summary>
        public string DCOperation { get; set; }

        /// <summary>
        /// 宠物名
        /// </summary>
        public string Pet { get; set; }

        /// <summary>
        /// 性别
        /// </summary>
        public string Gender { get; set; }

        /// <summary>
        /// 宠主
        /// </summary>
        public string Customer { get; set; }

        /// <summary>
        /// 电话
        /// </summary>
        public string CustomerPhone { get; set; }

        /// <summary>
        /// 检查时间
        /// </summary>
        public DateTime TestDate { get; set; }

        /// <summary>
        /// 宠物种类
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// 宠物品种
        /// </summary>
        public string Variety { get; set; }


        /// <summary>
        /// 检查结果
        /// </summary>
        private TestResult result;


        public TestInfo()
        {
            TestDate = DateTime.Now;
            Customer = "测试用户";
            DCOperation = AppStatic.AppHospital.UserName;
            TestName = "镜检";
            Gender = GenderEnum.公.ToString();
            Id = Guid.NewGuid().ToString();
        }

        public TestResult Result
        {
            get => result;
            set => SetProperty(ref result, value, nameof(Result));
        }
    }

    public class TestResult : ObservableObject
    {
        /// <summary>
        /// 结果集
        /// </summary>
        public List<ResultItem> Datas { get; set; }


        /// <summary>
        /// 图片集
        /// </summary>
        public List<ImageItem> Images { get; set; }

        /// <summary>
        /// 视频集
        /// </summary>
        public List<MediaItem> Vedios { get; set; }

        /// <summary>
        /// 备注
        /// </summary>
        public string Remark { get; set; }

    }


    public class MediaItem : ObservableObject
    {
        public string Name { get; set; }

        /// <summary>
        /// 数据源
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// 是否已修改
        /// </summary>
        public bool IsEdit { get; set; }

        /// <summary>
        /// 是否选中
        /// </summary>
        private bool _IsSelected;

        /// <summary>
        /// 数据类型
        /// </summary>
        public MediaSourceType Type { get; set; }


        public bool IsSelected
        {
            get => _IsSelected;
            set => SetProperty(ref _IsSelected, value, nameof(IsSelected));
        }
    }

    public enum MediaSourceType
    {
        /// <summary>
        /// base64
        /// </summary>
        Base64 = 0,
        /// <summary>
        /// 网络地址
        /// </summary>
        Url = 1,
        /// <summary>
        /// 本机地址
        /// </summary>
        LocalPath = 2,
    }

    //图片 
    [Serializable]
    public class ImageItem : MediaItem
    {
        [XmlIgnore]
        [NonSerialized]
        private Bitmap bitmap;
        [XmlIgnore]
        [NonSerialized]
        private ImageSource imageSource;

        /// <summary>
        /// 像素间距
        /// </summary>
        public double PixelSpacing { get; set; }

        /// <summary>
        /// 像素间距单拉 毫米mm,纳米 pm
        /// </summary>
        public string PixelSpacingUnit { get; set; }

        [XmlIgnore]
        [JsonIgnore]
        public byte[] Bitmap
        {
            get
            {
                if (bitmap != null)
                    return bitmap.Bitmap2Byte();

                if (Type != MediaSourceType.LocalPath)
                    return null;
                if (File.Exists(Source))
                    bitmap = new Bitmap(Source);
                return bitmap.Bitmap2Byte();
            }
            set
            {
                bitmap = value.Byte2Bitmap();
            }
        }
        [XmlIgnore]
        [JsonIgnore]
        public ImageSource ImageSource
        {

            get
            {
                if (imageSource != null)
                    return imageSource;

                if (Bitmap != null)
                {
                    MemoryStream stream = new MemoryStream();
                    bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                    imageSource = (ImageSource)new ImageSourceConverter().ConvertFrom(stream);
                }

                return imageSource;
            }
            set
            {
                imageSource = value;
                MemoryStream ms = new MemoryStream();
                BmpBitmapEncoder encoder = new BmpBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create((BitmapSource)value));
                encoder.Save(ms);
                bitmap = new Bitmap(ms);
                ms.Close();
            }
        }

    }

    /// <summary>
    /// 检查结果明细
    /// </summary>
    public class ResultItem
    {
        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 单位
        /// </summary>
        public string Unit { get; set; }

        /// <summary>
        /// 结果
        /// </summary>
        public object Value { get; set; }

        /// <summary>
        /// 阴阳性 true 阳性 false 阴性
        /// </summary>
        public bool PN { get; set; }

        /// <summary>
        /// 备注
        /// </summary>
        public string Remarks { get; set; }

    }
}
