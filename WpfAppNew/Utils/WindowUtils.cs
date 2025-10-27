using System;
using System.Windows;
using System.Windows.Forms;

namespace WpfAppNew.Utils
{
    /// <summary>
    /// 窗口工具类，提供窗口操作的公共方法
    /// </summary>
    public static class WindowUtils
    {
        /// <summary>
        /// 设置窗口全屏显示但保留任务栏，支持多显示屏
        /// </summary>
        /// <param name="window">要设置全屏的窗口</param>
        public static void SetFullScreenWithTaskbar(Window window)
        {
            if (window == null)
                throw new ArgumentNullException(nameof(window));

            try
            {
                // 获取当前窗口所在的屏幕
                var windowHandle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
                var screen = Screen.FromHandle(windowHandle);
                
                // 如果句柄还未创建，使用主屏幕
                if (screen == null || windowHandle == IntPtr.Zero)
                {
                    screen = Screen.PrimaryScreen;
                }
                
                // 强制设置窗口为最大化状态，确保完全填满工作区域
                window.WindowState = WindowState.Normal;
                
                // 设置窗口位置和大小为工作区域（排除任务栏）
                // 使用屏幕的完整边界而不是工作区域，以确保完全填满
                window.Left = screen.Bounds.Left;
                window.Top = screen.WorkingArea.Top; // 保持顶部为工作区域以避免覆盖任务栏
                window.Width = screen.Bounds.Width;  // 使用完整宽度
                window.Height = screen.WorkingArea.Height;
                
                // 强制刷新窗口布局
                window.UpdateLayout();
                
                // 确保窗口激活
                window.Topmost = false;
                window.Activate();
            }
            catch (Exception ex)
            {
                // 如果出现异常，使用备用方案
                window.WindowState = WindowState.Normal;
                window.Left = 0;
                window.Top = 0;
                window.Width = SystemParameters.PrimaryScreenWidth;  // 使用完整屏幕宽度
                window.Height = SystemParameters.WorkArea.Height;
                window.UpdateLayout();
            }
        }
    }
}