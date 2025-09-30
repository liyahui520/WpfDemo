@echo off
echo 正在启动 OpenCV 摄像头测试系统...
echo.

REM 切换到项目目录
cd /d "%~dp0"

REM 检查是否已编译
if not exist "bin\Debug\net461\OpenCv.exe" (
    echo 正在编译项目...
    dotnet build
    if errorlevel 1 (
        echo 编译失败！
        pause
        exit /b 1
    )
    echo 编译完成！
    echo.
)

REM 启动应用程序
echo 启动应用程序...
dotnet run

REM 如果应用程序退出，暂停以查看任何错误信息
if errorlevel 1 (
    echo.
    echo 应用程序异常退出！
    pause
)