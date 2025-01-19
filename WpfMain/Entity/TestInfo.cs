using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Ink;

namespace WpfMain.Entity
{

    /// <summary>
    /// 检查信息
    /// </summary>
    public class TestInfo
    {
        /// <summary>
        /// 检查名称
        /// </summary>
        public string TestName { get; set; }

        /// <summary>
        /// 检查医生姓名
        /// </summary>
        public string DCOperation { get; set; }

        /// <summary>
        /// 宠物名
        /// </summary>
        public string Pet { get; set; }

        /// <summary>
        /// 宠主
        /// </summary>
        public string Customer { get; set; }
        /// <summary>
        /// 检查时间
        /// </summary>
        public DateTime TestDate { get; set; }

        /// <summary>
        /// 检查结果
        /// </summary>
        public  TestResult Result { get; set; }

    }



    public class TestResult
    {
        /// <summary>
        /// 结果集
        /// </summary>
        public List<ResultItem> Datas { get; set; }


        /// <summary>
        /// 图片集
        /// </summary>
        public List<MediaItem> Images { get; set; }

        /// <summary>
        /// 视频集
        /// </summary>
        public List<MediaItem> Vedios { get; set; }

        /// <summary>
        /// 备注
        /// </summary>
        public string Remark { get; set; }

    }


    public class MediaItem
    {
        public string Name { get; set; }

        /// <summary>
        /// 数据源
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// 数据类型
        /// </summary>
        public MediaSourceType Type { get; set; }
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
        /// 本机绝对路径
        /// </summary>
        LocalPath = 2,
    }

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
