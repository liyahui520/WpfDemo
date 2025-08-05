using DirectShowLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading.Tasks;
using WPFMediaKit.DirectShow.Controls;

namespace WPFMediaKit.DirectShow.MediaPlayers
{
    public class VideoRecorder : VideoCaptureElement
    {
        private ICaptureGraphBuilder2 _captureGraph;
        private IFileSinkFilter _fileSink;
        private bool _isRecording;

        // 重写InitializeMediaPlayer（来自MediaElementBase）
        protected override void InitializeMediaPlayer()
        {
            // 调用基类初始化（创建VideoCapturePlayer）
            base.InitializeMediaPlayer(); 
            // 初始化捕获图形构建器
            if (VideoCapturePlayer != null)
            {
                _captureGraph = (ICaptureGraphBuilder2)new CaptureGraphBuilder2();
                if (VideoCapturePlayer.m_graph != null)
                {
                    _captureGraph = (ICaptureGraphBuilder2)new CaptureGraphBuilder2();
                    int hr = _captureGraph.SetFiltergraph(VideoCapturePlayer.m_graph);
                    DsError.ThrowExceptionForHR(hr);
                    //// 关联到VideoCapturePlayer的滤镜图
                    //int hr = _captureGraph.SetFiltergraph(VideoCapturePlayer.m_graph);
                    //DsError.ThrowExceptionForHR(hr);
                }
            }
        }

        /// <summary>
        /// 开始录制（含压缩配置）
        /// </summary>
        public void StartRecording(string outputPath, Guid containerType, Guid videoSubType)
        {
            if (_isRecording || VideoCapturePlayer == null)
                return;

            try
            {
                var graphBuilder = (ICaptureGraphBuilder2)new CaptureGraphBuilder2();
                int hr = graphBuilder.SetFiltergraph(VideoCapturePlayer.m_graph);
                DsError.ThrowExceptionForHR(hr);

                IBaseFilter mux = null;
                // 使用ICaptureGraphBuilder2创建输出文件
                 hr = graphBuilder.SetOutputFileName(
                    //containerType,  // 容器格式（如MP4、ASF）
                    //outputPath,
                    MediaSubType.Avi, outputPath,
                    out mux,
                    out _fileSink);
                DsError.ThrowExceptionForHR(hr);

                // 配置压缩格式（如H.264）
                //ConfigureCompression(videoSubType);

                // 连接视频流到复用器
                hr = graphBuilder.RenderStream(
                    PinCategory.Capture,
                    MediaType.Video,
                    VideoCapturePlayer.m_captureDevice,  // 获取视频捕获滤镜
                    null,                // 压缩滤镜（自动选择）
                    mux);
                // 手动连接引脚（如果自动失败） 
                DsError.ThrowExceptionForHR(hr); 

                _isRecording = true;
            }
            catch (Exception ex)
            {
                InvokeMediaFailed(new MediaFailedEventArgs("录制失败", ex));
            }
        }

        /// <summary>
        /// 配置视频压缩参数
        /// </summary>
        private void ConfigureCompression(Guid videoSubType)
        {
            var captureFilter = GetCaptureFilter();
            if (captureFilter == null) return;

            // 获取流配置接口
            var streamConfig = DsFindPin.ByCategory(captureFilter, PinCategory.Capture, 0)
                as IAMStreamConfig;
            if (streamConfig == null) return;

            // 获取当前媒体类型
            streamConfig.GetFormat(out var mediaType);
            try
            {
                // 设置压缩格式（如H.264）
                mediaType.subType = videoSubType;
                // 应用配置
                streamConfig.SetFormat(mediaType);
            }
            finally
            {
                DsUtils.FreeAMMediaType(mediaType);
            }
        }

        private IBaseFilter GetCaptureFilter()
        {
            if (VideoCapturePlayer?.m_graph == null)
                return null;

            var videoCaptureClsids = GetVideoCaptureDeviceClsids();

            IEnumFilters enumFilters = null;
            IBaseFilter[] filters = new IBaseFilter[1];
            IntPtr fetched = IntPtr.Zero;

            try
            {
                int hr = VideoCapturePlayer.m_graph.EnumFilters(out enumFilters);
                DsError.ThrowExceptionForHR(hr);

                while (enumFilters.Next(filters.Length, filters, fetched) == 0)
                {
                    // 关键：使用as转换并检查是否为有效IBaseFilter
                    IBaseFilter filter = filters[0] as IBaseFilter;
                    if (filter == null)
                    {
                        // 释放无效对象
                        Marshal.ReleaseComObject(filters[0]);
                        continue;
                    }

                    // 检查是否为视频捕获滤镜
                    if (IsVideoCaptureFilter(filter, videoCaptureClsids))
                    {
                        return filter; // 返回找到的捕获滤镜
                    }

                    // 释放当前滤镜引用
                    Marshal.ReleaseComObject(filter);
                }
            }catch(Exception ex)
            {
                // 捕获异常并处理
                InvokeMediaFailed(new MediaFailedEventArgs("获取视频捕获滤镜失败", ex));
            }
            finally
            {
                if (enumFilters != null)
                    Marshal.ReleaseComObject(enumFilters);
            }
            return null;
        }

        /// <summary>
        /// 获取所有视频捕获设备的CLSID
        /// </summary>
        private HashSet<Guid> GetVideoCaptureDeviceClsids()
        {
            var clsids = new HashSet<Guid>();
            // 遍历系统中注册的所有视频输入设备
            foreach (Filter filter in Filters.VideoInputDevices)
            {
                if (filter.CLSID != Guid.Empty)
                {
                    clsids.Add(filter.CLSID);
                }
            }
            return clsids;
        }

        /// <summary>
        /// 判断滤镜是否为视频捕获设备（通过CLSID比对）
        /// </summary>
        private bool IsVideoCaptureFilter(IBaseFilter filter, HashSet<Guid> captureClsids)
        {
            // 获取当前滤镜的CLSID
            var filterClsid = GetFilterClsid(filter);
            return filterClsid.HasValue && captureClsids.Contains(filterClsid.Value);
        }

        /// <summary>
        /// 获取滤镜的CLSID
        /// </summary>
        private Guid? GetFilterClsid(IBaseFilter filter)
        {
            IPropertyBag propertyBag = null;
            try
            {
                // 通过IMoniker获取滤镜属性（参考Filters.cs中Filter类的实现）
                IMoniker moniker = GetMonikerFromFilter(filter);
                if (moniker == null)
                    return null;

                // 绑定到存储以获取属性包
                Guid bagId = typeof(IPropertyBag).GUID;
                object bagObj;
                moniker.BindToStorage(null, null, ref bagId, out bagObj);
                propertyBag = (IPropertyBag)bagObj;

                // 读取CLSID属性
                object clsidObj = null;
                int hr = propertyBag.Read("CLSID", out clsidObj, null);
                if (hr == 0 && clsidObj != null)
                {
                    return new Guid(clsidObj.ToString());
                }
                return null;
            }
            catch
            {
                return null;
            }
            finally
            {
                if (propertyBag != null)
                    Marshal.ReleaseComObject(propertyBag);
            }
        }

        /// <summary>
        /// 从滤镜获取IMoniker（用于读取属性）
        /// </summary>
        private IMoniker GetMonikerFromFilter(IBaseFilter filter)
        {
            // 枚举系统设备，通过滤镜名称匹配对应的Moniker
            foreach (Filter videoFilter in Filters.VideoInputDevices)
            {
                // 尝试创建临时滤镜实例进行名称比对
                IBaseFilter tempFilter = videoFilter.CreateFilter();
                if (tempFilter == null)
                    continue;

                try
                {
                    FilterInfo tempInfo, targetInfo;
                    tempFilter.QueryFilterInfo(out tempInfo);
                    filter.QueryFilterInfo(out targetInfo);

                    // 释放不必要的引用
                    Marshal.ReleaseComObject(tempInfo.pGraph);
                    Marshal.ReleaseComObject(targetInfo.pGraph);

                    // 名称匹配则返回对应的Moniker
                    if (string.Equals(tempInfo.achName, targetInfo.achName, StringComparison.OrdinalIgnoreCase))
                    {
                        return videoFilter.GetMoniker();
                    }
                }
                finally
                {
                    Marshal.ReleaseComObject(tempFilter);
                }
            }
            return null;
        }
        /// <summary>
        /// 停止录制
        /// </summary>
        public void StopRecording()
        {
            if (!_isRecording) return;

            // 停止图形并释放资源
            VideoCapturePlayer.Stop();
            _fileSink?.SetFileName(null, null);
            Marshal.ReleaseComObject(_fileSink);
            _isRecording = false;

            // 重启预览
            VideoCapturePlayer.Play();
        }

        // 释放资源
        protected override void OnUnloadedOverride()
        {
            base.OnUnloadedOverride();
            if (_captureGraph != null)
            {
                Marshal.ReleaseComObject(_captureGraph);
                _captureGraph = null;
            }
        }
    }
}
