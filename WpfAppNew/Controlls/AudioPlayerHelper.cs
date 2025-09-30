using System;
using System.IO;
using System.Threading.Tasks;
using NAudio.MediaFoundation;
using NAudio.Wave;
using Tools.Extend;

namespace WpfAppNew.Controlls
{
    /// <summary>
    /// 音频播放辅助类
    /// 提供音频播放、暂停、停止和音量控制功能
    /// 支持多种音频格式，与OpenCV视频播放器配合使用
    /// </summary>
    public class AudioPlayerHelper : IDisposable
    {
        #region 私有字段

        /// <summary>
        /// 音频播放设备
        /// </summary>
        private WaveOutEvent _waveOutDevice;

        /// <summary>
        /// 音频文件读取器
        /// </summary>
        private AudioFileReader _audioFileReader;

        /// <summary>
        /// 音量控制
        /// </summary>
        private float _volume = 1.0f;

        /// <summary>
        /// 播放状态
        /// </summary>
        private bool _isLoaded;
        private bool _isPlaying;
        private bool _isPaused;
        private bool _isDisposed;

        /// <summary>
        /// 当前音频文件路径
        /// </summary>
        private string _currentAudioFile;

        /// <summary>
        /// 音频信息
        /// </summary>
        private TimeSpan _totalDuration;
        private long _totalSamples;

        #endregion

        #region 事件

        /// <summary>
        /// 播放完成事件
        /// </summary>
        public event EventHandler PlaybackCompleted;

        /// <summary>
        /// 播放位置改变事件
        /// </summary>
        /// <param name="position">当前播放位置（毫秒）</param>
        /// <param name="duration">总时长（毫秒）</param>
        public event Action<double, double> PositionChanged;

        #endregion

        #region 属性

        /// <summary>
        /// 音量（0.0 - 1.0）
        /// </summary>
        public float Volume
        {
            get => _volume;
            set
            {
                _volume = Math.Max(0.0f, Math.Min(1.0f, value));
                if (_audioFileReader != null)
                {
                    _audioFileReader.Volume = _volume;
                }
                LogUtil.Info($"音量设置为: {_volume * 100:F1}%");
            }
        }

        /// <summary>
        /// 是否正在播放
        /// </summary>
        public bool IsPlaying => _isPlaying && !_isPaused;

        /// <summary>
        /// 是否已暂停
        /// </summary>
        public bool IsPaused => _isPaused;

        /// <summary>
        /// 总时长（毫秒）
        /// </summary>
        public double TotalDurationMs => _totalDuration.TotalMilliseconds;

        /// <summary>
        /// 当前播放位置（毫秒）
        /// </summary>
        public double CurrentPositionMs
        {
            get
            {
                if (_audioFileReader == null)
                    return 0;
                return _audioFileReader.CurrentTime.TotalMilliseconds;
            }
            set
            {
                if (_audioFileReader != null)
                {
                    var timeSpan = TimeSpan.FromMilliseconds(value);
                    if (timeSpan <= _totalDuration)
                    {
                        _audioFileReader.CurrentTime = timeSpan;
                    }
                }
            }
        }

        #endregion

        #region 构造函数和初始化

        /// <summary>
        /// 初始化音频播放器辅助类
        /// </summary>
        public AudioPlayerHelper()
        {
            try
            {
                // 初始化MediaFoundation（支持更多音频格式）
                MediaFoundationApi.Startup();
                
                _isDisposed = false;
                _isPlaying = false;
                _isPaused = false;
                
                LogUtil.Info("音频播放器辅助类初始化完成");
            }
            catch (Exception ex)
            {
                LogUtil.Error($"音频播放器初始化失败: {ex.Message}");
            }
        }

        #endregion

        #region 音频加载和播放控制

        /// <summary>
        /// 加载音频文件
        /// </summary>
        /// <param name="audioPath">音频文件路径</param>
        /// <returns>是否加载成功</returns>
        public async Task<bool> LoadAudio(string audioPath)
        {
            try
            {
                await Task.Run(() =>
                {
                    // 释放之前的资源
                    DisposeAudioResources();

                    if (!File.Exists(audioPath))
                    {
                        LogUtil.Error($"音频文件不存在: {audioPath}");
                        return;
                    }

                    // 创建音频文件读取器
                    _audioFileReader = new AudioFileReader(audioPath);
                    
                    // 创建音频输出设备
                    _waveOutDevice = new WaveOutEvent();
                    _waveOutDevice.Init(_audioFileReader);

                    // 注册播放完成事件
                    _waveOutDevice.PlaybackStopped += OnPlaybackStopped;

                    _isLoaded = true;
                    LogUtil.Info($"音频文件加载成功: {Path.GetFileName(audioPath)}");
                });

                return _isLoaded;
            }
            catch (Exception ex)
            {
                LogUtil.Error($"加载音频文件失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 从视频文件中提取音频路径
        /// </summary>
        /// <param name="videoFilePath">视频文件路径</param>
        /// <returns>对应的音频文件路径（如果存在）</returns>
        public string GetAudioPathFromVideo(string videoFilePath)
        {
            if (string.IsNullOrEmpty(videoFilePath))
                return null;

            try
            {
                var directory = Path.GetDirectoryName(videoFilePath);
                var fileNameWithoutExt = Path.GetFileNameWithoutExtension(videoFilePath);

                // 常见音频格式
                string[] audioExtensions = { ".mp3", ".wav", ".aac", ".m4a", ".ogg", ".flac" };

                foreach (var ext in audioExtensions)
                {
                    var audioPath = Path.Combine(directory, fileNameWithoutExt + ext);
                    if (File.Exists(audioPath))
                    {
                        return audioPath;
                    }
                }

                // 检查同名文件夹中的音频文件
                var audioFolder = Path.Combine(directory, fileNameWithoutExt);
                if (Directory.Exists(audioFolder))
                {
                    foreach (var ext in audioExtensions)
                    {
                        var audioPath = Path.Combine(audioFolder, fileNameWithoutExt + ext);
                        if (File.Exists(audioPath))
                        {
                            return audioPath;
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                LogUtil.Error($"获取音频路径失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 开始播放音频
        /// </summary>
        public async Task PlayAsync()
        {
            if (_waveOutDevice == null || _audioFileReader == null || _isPlaying)
                return;

            try
            {
                _isPlaying = true;
                _isPaused = false;

                _waveOutDevice.Play();

                // 启动位置更新任务
                _ = Task.Run(UpdatePositionLoop);

                LogUtil.Info("音频播放开始");
            }
            catch (Exception ex)
            {
                LogUtil.Error($"音频播放失败: {ex.Message}");
                _isPlaying = false;
            }
        }

        /// <summary>
        /// 暂停/继续播放
        /// </summary>
        public void Pause()
        {
            if (_waveOutDevice == null || !_isPlaying)
                return;

            try
            {
                if (_isPaused)
                {
                    _waveOutDevice.Play();
                    _isPaused = false;
                    LogUtil.Info("音频播放继续");
                }
                else
                {
                    _waveOutDevice.Pause();
                    _isPaused = true;
                    LogUtil.Info("音频播放暂停");
                }
            }
            catch (Exception ex)
            {
                LogUtil.Error($"音频暂停/继续失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 停止播放
        /// </summary>
        public async Task StopAsync()
        {
            try
            {
                _isPlaying = false;
                _isPaused = false;

                if (_waveOutDevice != null)
                {
                    _waveOutDevice.Stop();
                }

                if (_audioFileReader != null)
                {
                    _audioFileReader.Position = 0; // 重置到开始位置
                }

                LogUtil.Info("音频播放停止");
            }
            catch (Exception ex)
            {
                LogUtil.Error($"音频停止失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 设置播放位置
        /// </summary>
        /// <param name="positionMs">位置（毫秒）</param>
        public void SetPosition(double positionMs)
        {
            if (_audioFileReader == null)
                return;

            try
            {
                var timeSpan = TimeSpan.FromMilliseconds(positionMs);
                if (timeSpan <= _totalDuration)
                {
                    _audioFileReader.CurrentTime = timeSpan;
                }
            }
            catch (Exception ex)
            {
                LogUtil.Error($"设置音频位置失败: {ex.Message}");
            }
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 播放停止事件处理
        /// </summary>
        private void OnPlaybackStopped(object sender, StoppedEventArgs e)
        {
            _isPlaying = false;
            _isPaused = false;

            if (e.Exception != null)
            {
                LogUtil.Error($"音频播放异常停止: {e.Exception.Message}");
            }
            else
            {
                LogUtil.Info("音频播放完成");
                PlaybackCompleted?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// 位置更新循环
        /// </summary>
        private async Task UpdatePositionLoop()
        {
            while (_isPlaying && !_isDisposed)
            {
                try
                {
                    if (!_isPaused && _audioFileReader != null)
                    {
                        var currentMs = _audioFileReader.CurrentTime.TotalMilliseconds;
                        var totalMs = _totalDuration.TotalMilliseconds;
                        
                        PositionChanged?.Invoke(currentMs, totalMs);
                    }

                    await Task.Delay(100); // 每100ms更新一次
                }
                catch (Exception ex)
                {
                    LogUtil.Error($"位置更新异常: {ex.Message}");
                    break;
                }
            }
        }

        /// <summary>
        /// 释放音频资源
        /// </summary>
        private void DisposeAudioResources()
        {
            try
            {
                if (_waveOutDevice != null)
                {
                    _waveOutDevice.PlaybackStopped -= OnPlaybackStopped;
                    _waveOutDevice.Stop();
                    _waveOutDevice.Dispose();
                    _waveOutDevice = null;
                }

                if (_audioFileReader != null)
                {
                    _audioFileReader.Dispose();
                    _audioFileReader = null;
                }
            }
            catch (Exception ex)
            {
                LogUtil.Error($"释放音频资源异常: {ex.Message}");
            }
        }

        #endregion

        #region IDisposable实现

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            try
            {
                // 停止播放
                StopAsync().Wait(1000);

                // 释放音频资源
                DisposeAudioResources();



                LogUtil.Info("音频播放器辅助类资源已释放");
            }
            catch (Exception ex)
            {
                LogUtil.Error($"音频播放器资源释放异常: {ex.Message}");
            }
        }

        #endregion
    }
}