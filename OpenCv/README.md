# OpenCV 类库

这是一个基于 OpenCvSharp 的 .NET Framework 4.6.1 类库，提供了完整的摄像头管理、视频录制和图像处理功能。

## 主要功能

### 核心类库 (Core)
- **CameraManager**: 摄像头管理器，提供设备枚举、预览、录制等功能
- **DeviceManager**: 设备管理器，负责摄像头设备的发现和管理
- **VideoRecorder**: 视频录制器，支持多种格式的视频录制
- **PerformanceMonitor**: 性能监控器，监控帧率和系统性能
- **ErrorHandler**: 错误处理器，统一的异常处理机制

### WPF 控件 (Controls)
- **CameraPreviewControl**: 摄像头预览控件，提供完整的UI界面

### 工具类
- **CameraTest**: 摄像头测试工具，用于验证设备访问权限
- **InvertedBooleanToVisibilityConverter**: WPF转换器

## 使用方法

### 1. 引用类库
将生成的 `OpenCv.dll` 及其依赖项添加到您的项目中。

### 2. 基本使用示例

```csharp
using OpenCv.Core;
using OpenCv.Controls;

// 创建摄像头管理器
var cameraManager = new CameraManager();

// 初始化摄像头
bool initialized = await cameraManager.InitializeCaptureAsync(640, 480);
if (initialized)
{
    // 开始预览
    cameraManager.StartPreview();
    
    // 等待一段时间
    await Task.Delay(5000);
    
    // 停止预览
    cameraManager.StopPreview();
}

// 释放资源
cameraManager.Dispose();
```

### 3. 录像功能示例

```csharp
using OpenCv.Core;

var cameraManager = new CameraManager();

// 初始化摄像头
await cameraManager.InitializeCaptureAsync(640, 480);

// 开始预览
cameraManager.StartPreview();

// 开始录像
string outputPath = "recording.mp4";
bool recordingStarted = cameraManager.StartRecording(outputPath);

if (recordingStarted)
{
    // 录制5秒
    await Task.Delay(5000);
    
    // 停止录像
    cameraManager.StopRecording();
}

// 停止预览并释放资源
cameraManager.StopPreview();
cameraManager.Dispose();
```

### 4. 拍照功能示例

```csharp
using OpenCv.Core;

var cameraManager = new CameraManager();

// 初始化摄像头
await cameraManager.InitializeCaptureAsync(640, 480);

// 开始预览
cameraManager.StartPreview();

// 等待摄像头稳定
await Task.Delay(2000);

// 拍照
string snapshotPath = "snapshot.jpg";
bool snapshotTaken = cameraManager.TakeSnapshot(snapshotPath);

// 停止预览并释放资源
cameraManager.StopPreview();
cameraManager.Dispose();
```

### 5. 在WPF中使用预览控件

```csharp
using OpenCv.Controls;

// 在XAML中
<local:CameraPreviewControl x:Name="CameraPreview" />

// 在代码中
var previewControl = new CameraPreviewControl();
// 将控件添加到您的窗口中
```

### 6. 测试摄像头访问

```csharp
using OpenCv;

// 测试摄像头访问权限
var testResults = CameraTest.TestCameraAccess();
foreach (var result in testResults)
{
    Console.WriteLine(result);
}
```

## API 参考

### CameraManager 类

#### 主要属性
- `bool IsCapturing` - 是否正在捕获
- `bool IsRecording` - 是否正在录制
- `double CurrentFps` - 当前帧率
- `int FrameWidth` - 帧宽度
- `int FrameHeight` - 帧高度
- `List<CameraDevice> AvailableDevices` - 可用设备列表

#### 主要方法
- `Task<bool> InitializeCaptureAsync(int width, int height)` - 初始化摄像头捕获
- `void StartPreview()` - 开始预览
- `void StopPreview()` - 停止预览
- `bool StartRecording(string outputPath, FourCC codec, double fps)` - 开始录像
- `void StopRecording()` - 停止录像
- `bool TakeSnapshot(string savePath)` - 拍照
- `void SetCameraProperty(CameraProperty property, double value)` - 设置摄像头属性
- `double GetCameraProperty(CameraProperty property)` - 获取摄像头属性

#### 主要事件
- `EventHandler<FrameCapturedEventArgs> FrameCaptured` - 帧捕获事件
- `EventHandler<StatusChangedEventArgs> StatusChanged` - 状态变更事件
- `EventHandler<RecordingStatusChangedEventArgs> RecordingStatusChanged` - 录像状态变更事件
- `EventHandler<ErrorOccurredEventArgs> ErrorOccurred` - 错误发生事件

### CameraTest 类

#### 静态方法
- `List<string> TestCameraAccess()` - 测试摄像头访问权限

## 依赖项

- .NET Framework 4.6.1
- OpenCvSharp4 (4.5.1.20210208)
- OpenCvSharp4.runtime.win (4.5.1.20210208)
- OpenCvSharp4.WpfExtensions (4.5.1.20210208)
- System.Drawing
- System.Windows.Forms

## 构建

```bash
dotnet build
```

生成的类库文件位于 `bin\Debug\net461\OpenCv.dll`

## 注意事项

1. 确保目标系统已安装 .NET Framework 4.6.1 或更高版本
2. 摄像头访问需要相应的系统权限
3. 某些功能需要 WPF 环境支持
4. 建议在使用前先运行 CameraTest 验证设备兼容性

## 版本历史

- v1.0: 初始版本，支持基本的摄像头管理和预览功能
- v1.1: 修复了死锁问题，优化了线程安全性
- v1.2: 转换为类库项目，支持 .NET Framework 4.6.1