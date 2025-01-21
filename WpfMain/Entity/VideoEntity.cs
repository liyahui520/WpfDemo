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

    public class Exposure
    {
        public bool IsAuto { get; set; } = true;

        public bool IsEnable { get; set; } = true;

    }

    public class VideoImage
    {
        public string Path { get; set; }

        public string Name { get; set; }
    }
}
