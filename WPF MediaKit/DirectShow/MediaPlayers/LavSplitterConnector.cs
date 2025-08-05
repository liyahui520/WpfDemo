using DirectShowLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace WPFMediaKit.DirectShow.MediaPlayers
{
    public class LavSplitterConnector
    {
        // LAV Splitter的CLSID（来自LAV Filters）
        private static readonly Guid CLSID_LAV_Splitter = new Guid("171252A0-8820-4AFE-9DF8-5C92B2D66B04");

        // 标准视频格式GUID
        private static readonly Guid MEDIASUBTYPE_RGB24 = new Guid("E436EB7E-524F-11CE-9F53-0020AF0BA770");
        private static readonly Guid MEDIASUBTYPE_YUY2 = new Guid("32595559-0000-0010-8000-00AA00389B71");
        private static readonly Guid MEDIASUBTYPE_MJPG = new Guid("4D4A5047-0000-0010-8000-00AA00389B71");

        private readonly IGraphBuilder _graph;

        public LavSplitterConnector(IGraphBuilder graph)
        {
            _graph = graph ?? throw new ArgumentNullException(nameof(graph));
        }

        /// <summary>
        /// 连接捕获设备到LAV Splitter，自动处理格式转换
        /// </summary>
        public IBaseFilter ConnectCaptureToLavSplitter(IBaseFilter captureDevice)
        {
            if (captureDevice == null)
                throw new ArgumentNullException(nameof(captureDevice));

            // 创建LAV Splitter实例
            var lavSplitter = (IBaseFilter)Activator.CreateInstance(
                Type.GetTypeFromCLSID(CLSID_LAV_Splitter));

            int hr = _graph.AddFilter(lavSplitter, "LAV Splitter");
            DsError.ThrowExceptionForHR(hr);

            // 获取捕获设备的输出引脚
            IPin captureOutPin = GetCaptureOutputPin(captureDevice);
            if (captureOutPin == null)
                throw new Exception("无法获取捕获设备的输出引脚");

            // 获取LAV Splitter的输入引脚
            IPin splitterInPin = DsFindPin.ByDirection(lavSplitter, PinDirection.Input, 0);
            if (splitterInPin == null)
            {
                Marshal.ReleaseComObject(captureOutPin);
                throw new Exception("无法获取LAV Splitter的输入引脚");
            }

            try
            {
                // 尝试直接连接（适用于格式兼容的情况）
                hr = _graph.Connect(captureOutPin, splitterInPin);
                if (hr == 0)
                    return lavSplitter; // 直接连接成功

                // 直接连接失败，尝试通过格式转换滤镜连接
                if (!TryConnectWithConverter(captureOutPin, splitterInPin))
                {
                    // 最后尝试强制指定支持的媒体类型
                    if (!TryConnectWithMediaType(captureOutPin, splitterInPin))
                    {
                        throw new Exception("无法建立连接，请确保LAV Filters已正确安装");
                    }
                }

                return lavSplitter;
            }
            finally
            {
                Marshal.ReleaseComObject(captureOutPin);
                Marshal.ReleaseComObject(splitterInPin);
            }
        }

        /// <summary>
        /// 获取捕获设备的输出引脚（优先视频流）
        /// </summary>
        private IPin GetCaptureOutputPin(IBaseFilter filter)
        {
            IPin pin = DsFindPin.ByDirection(filter, PinDirection.Output, int.MaxValue);
            if (pin!=null && IsVideoPin(pin))
            {
                Marshal.ReleaseComObject(pin);
                return pin;
            }
            else
            {
                // 如果没有找到视频引脚，返回第一个输出引脚
                IEnumPins enumPins;
                int hr = filter.EnumPins(out enumPins);
                DsError.ThrowExceptionForHR(hr);
                IPin[] pins = new IPin[1];
                while (enumPins.Next(1, pins, IntPtr.Zero) == 0)
                {
                    if (IsVideoPin(pins[0]))
                    {
                        return pins[0]; // 返回第一个视频引脚
                    }
                    Marshal.ReleaseComObject(pins[0]);
                }
            }
            return null;
        }

        /// <summary>
        /// 判断引脚是否为视频引脚
        /// </summary>
        private bool IsVideoPin(IPin pin)
        {
            var mediaType = new AMMediaType();
            try
            {
                pin.ConnectionMediaType(mediaType);
                return mediaType.majorType == MediaType.Video;
            }
            catch
            {
                return false;
            }
            finally
            {
                DsUtils.FreeAMMediaType(mediaType);
            }
        }

        /// <summary>
        /// 尝试通过格式转换滤镜连接（使用系统自带的转换滤镜）
        /// </summary>
        private bool TryConnectWithConverter(IPin capturePin, IPin splitterPin)
        {
            try
            {
                // 创建格式转换滤镜（系统自带）
                var converter = (IBaseFilter)Activator.CreateInstance(
                    Type.GetTypeFromCLSID(CLSID_VideoConverter));

                _graph.AddFilter(converter, "Video Converter");

                // 先连接捕获设备到转换滤镜
                int hr = _graph.Connect(capturePin, DsFindPin.ByDirection(converter, PinDirection.Input, 0));
                if (hr != 0)
                {
                    _graph.RemoveFilter(converter);
                    Marshal.ReleaseComObject(converter);
                    return false;
                }

                // 再连接转换滤镜到LAV Splitter
                hr = _graph.Connect(
                    DsFindPin.ByDirection(converter, PinDirection.Output, 0),
                    splitterPin);

                if (hr != 0)
                {
                    _graph.RemoveFilter(converter);
                    Marshal.ReleaseComObject(converter);
                    return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 尝试强制指定支持的媒体类型进行连接
        /// </summary>
        private bool TryConnectWithMediaType(IPin capturePin, IPin splitterPin)
        {
            // LAV Splitter支持的常见视频格式列表
            var supportedTypes = new[] { MediaSubType.YUY2, MediaSubType.RGB24, MediaSubType.MJPG };

            foreach (var mediaSubType in supportedTypes)
            {
                var mediaType = new AMMediaType
                {
                    majorType = MediaType.Video,
                    subType = mediaSubType,
                    formatType = FormatType.VideoInfo,
                    fixedSizeSamples = true,
                    sampleSize = 1
                };

                try
                {
                    // 尝试使用指定的媒体类型连接
                    int hr = _graph.ConnectDirect(capturePin, splitterPin, mediaType);
                    if (hr == 0)
                        return true;
                }
                finally
                {
                    DsUtils.FreeAMMediaType(mediaType);
                }
            }

            return false;
        }

        // 系统视频转换滤镜的CLSID
        private static readonly Guid CLSID_VideoConverter = new Guid("04FE9017-F873-11d0-A18C-00A0C9118956");
    }
}
