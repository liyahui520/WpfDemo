using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WpfMain.Extend;

namespace WpfMain.Entity
{

    public class PropertyVideoModel : ObservableObject
    {
        private Exposure exposureModel;
        private List<VideoImage> images;

        public Exposure ExposureModel
        {
            get => exposureModel;
            set => SetProperty(ref exposureModel, value, nameof(ExposureModel));
        }

        public List<VideoImage> Images
        {
            get => images;
            set => SetProperty(ref images, value, nameof(Images));
        } 
    }

    public class Exposure : ObservableObject
    {
        public bool IsAuto { get; set; } = true;

        public bool IsEnable { get; set; } = true;

    }

    public class VideoImage : ObservableObject
    {
        public string Path { get; set; }

        public string Name { get; set; }
    }

    /// <summary>
    /// 视频类型
    /// </summary>
    public class VideoType
    {
        public string Name { get; set; }

        public string VideoString { get; set; }
    }
}
