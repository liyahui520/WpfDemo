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

        private static int no = 1000;

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
        public DateTime TestDate { get; set; } = DateTime.Now;

        /// <summary>
        /// 宠物种类
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// 宠物品种
        /// </summary>
        public string Variety { get; set; }


        /// <summary>
        /// 年龄
        /// </summary>
        public string Age { get; set; }


        /// <summary>
        /// 绝育
        /// </summary>
        public string Neuter { get; set; }

        /// <summary>
        /// 报告地址
        /// </summary>
        public string TestPath { get; set; }


        /// <summary>
        /// 检查结果
        /// </summary>
        private TestResult result;


        public TestInfo()
        {
            TestDate = DateTime.Now;
            Customer = "测试用户";
            DCOperation = AppStatic.AppHospital.DcName;
            TestName = "分泌物";
            Gender = GenderEnum.公.ToString();
            Id = Guid.NewGuid().ToString();
            Neuter = NeuterEnum.未绝育.ToString();
            Type = "犬";
            Age = "1年3月";
            Pet = "暧暧";
            RecordNo = (no++).ToString();
        }

        public TestResult Result
        {
            get => result;
            set => SetProperty(ref result, value, nameof(Result));
        }
    }

    public class TestResult : ObservableObject
    {
        private List<ImageItem> _images;

        /// <summary>
        /// 结果集
        /// </summary>
        public List<ResultItem> Datas { get; set; }


        /// <summary>
        /// 图片集
        /// </summary>
        public List<ImageItem> Images
        {
            get => _images;
            set => SetProperty(ref _images, value, nameof(Images));
        }

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
        [XmlIgnore]
        [NonSerialized]
        private byte[] buffer;

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
        public byte[] BitBuffer
        {
            get
            {
                if (buffer != null)
                    return buffer;

                if (bitmap != null)
                    buffer= bitmap.Bitmap2Byte();

                if (!string.IsNullOrEmpty(Source))
                {
                    switch (Type)
                    {
                        case MediaSourceType.Base64:
                            buffer = Convert.FromBase64String(Source);
                            break;
                        case MediaSourceType.Url:
                            //暂时不会有网上下载
                            break;
                        case MediaSourceType.LocalPath:
                            if(File.Exists(Source))
                                buffer = File.ReadAllBytes(Source);
                            break;
                    }
                }

                return buffer;
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

                if (BitBuffer != null)
                {
                    MemoryStream stream = new MemoryStream();
                    BitBuffer.Byte2Bitmap().Save(stream, System.Drawing.Imaging.ImageFormat.Png);
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
                SetProperty(ref imageSource, value, nameof(ImageSource));
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
