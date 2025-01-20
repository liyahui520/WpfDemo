using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace WpfMain.Entity
{
    public class ImageInfo
    {
        public string FilePath { get; set; }


        public Bitmap ClonedBitmap
        {
            get
            {
                return DicomImage;
            }
        }
        public Bitmap DicomImage { get; set; }

        public ImageSource Source { get; set; }

        /// <summary>
        /// 像素间距
        /// </summary>
        public double PixelSpacing { get; set; }

        /// <summary>
        /// 像素间距单拉
        /// </summary>
        public double PixelSpacingUnit { get; set; }
    }
}
