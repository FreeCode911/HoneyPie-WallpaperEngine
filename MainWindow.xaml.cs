// HoneyPie - VLC-based Wallpaper Video Player
// Optimized for Windows 10 & 11 using LibVLCSharp

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Drawing;
using System.Windows.Threading;
using LibVLCSharp.Shared;
using LibVLCSharp.WPF;

namespace HoneyPie
{
    public partial class MainWindow : Window, IDisposable
    {
        private NotifyIcon trayIcon;
        private IntPtr workerW = IntPtr.Zero;
        private Window wallpaperWindow;
        private LibVLC _libVLC;
        private MediaPlayer _mediaPlayer;
        private Media _currentMedia;
        private VideoView videoView;
        private DispatcherTimer _pauseCheckTimer;
        private bool _isDisposed = false;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, StringBuilder pvParam, uint fWinIni);
        [DllImport("user32.dll")] private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        [DllImport("user32.dll")] private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);
        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        [DllImport("user32.dll")] private static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);
        [DllImport("user32.dll")] private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam, uint fuFlags, uint uTimeout, out IntPtr lpdwResult);
        [DllImport("user32.dll")] private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        
        // Auto-pause Win32 dependencies
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern IntPtr GetDesktopWindow();
        [DllImport("user32.dll")] private static extern IntPtr GetShellWindow();
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left; public int Top; public int Right; public int Bottom;
            public int Width => Right - Left;
            public int Height => Bottom - Top;
        }

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        private const int SPI_SETDESKWALLPAPER = 0x0014;
        private const int SPIF_UPDATEINIFILE = 0x01;
        private const int SPIF_SENDCHANGE = 0x02;
        private const uint SPI_GETDESKWALLPAPER = 0x0073;
        private const int SW_SHOW = 5;
        private const uint SWP_SHOWWINDOW = 0x0040;

        public MainWindow()
        {
            try
            {
                Core.Initialize();
                // Extremely aggressive low-end optimization flags
                _libVLC = new LibVLC(new[]
                {
                    "--hw-dec=auto",              // Force hardware decoding (DXVA2/D3D11)
                    "--drop-late-frames",         // Drop frames instead of eating RAM/CPU
                    "--skip-frames",              // Skip decoding frames if lagging
                    "--no-video-title-show",      // Remove UI overlay
                    "--no-sub-autodetect-file",   // No subtitles
                    "--no-osd",                   // Disable OSD
                    "--no-snapshot",              // Disable snapshot feature
                    "--no-stats"                  // Disable background stats
                });
                _mediaPlayer = new MediaPlayer(_libVLC);

                videoView = new VideoView { MediaPlayer = _mediaPlayer };
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to initialize VLC: {ex.Message}\nPlease ensure VLC media player is installed.", "HoneyPie Error", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Windows.Application.Current.Shutdown();
                return;
            }

            InitializeComponent();
            SetupTrayIcon();
            SetupWallpaperWindow();
            SetupPauseTimer();
            Loaded += (_, _) => Hide();
        }

        private void SetupTrayIcon()
        {
            trayIcon = new NotifyIcon
            {
                Icon = File.Exists("honeypie.ico") ? new System.Drawing.Icon("honeypie.ico") : SystemIcons.Application,
                Visible = true
            };

            var menu = new ContextMenuStrip();
            menu.Items.Add("Select Video", null, (_, _) => SelectVideo());
            menu.Items.Add("Play", null, (_, _) => _mediaPlayer?.Play());
            menu.Items.Add("Pause", null, (_, _) => _mediaPlayer?.Pause());
            menu.Items.Add("Mute/Unmute", null, (_, _) => _mediaPlayer.Mute = !_mediaPlayer.Mute);
            menu.Items.Add("Stop", null, (_, _) => _mediaPlayer?.Stop());
            menu.Items.Add("Exit", null, (_, _) => ExitApplication());

            trayIcon.ContextMenuStrip = menu;
            trayIcon.DoubleClick += (_, _) => { Show(); WindowState = WindowState.Normal; Activate(); };
        }

        private void SetupPauseTimer()
        {
            _pauseCheckTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _pauseCheckTimer.Tick += CheckForegroundWindow;
            _pauseCheckTimer.Start();
        }

        private void CheckForegroundWindow(object sender, EventArgs e)
        {
            if (_mediaPlayer == null || !_mediaPlayer.IsPlaying && _mediaPlayer.State != VLCState.Paused) return;

            IntPtr fgWindow = GetForegroundWindow();
            if (fgWindow == IntPtr.Zero || fgWindow == GetDesktopWindow() || fgWindow == GetShellWindow())
            {
                // Desktop is visible, ensure playback is rolling
                if (_mediaPlayer.State == VLCState.Paused) _mediaPlayer.Play();
                return;
            }

            if (GetWindowRect(fgWindow, out RECT rect))
            {
                int screenW = (int)SystemParameters.PrimaryScreenWidth;
                int screenH = (int)SystemParameters.PrimaryScreenHeight;

                // Maximize or overlapping detection
                if (rect.Width >= screenW && rect.Height >= screenH)
                {
                    if (_mediaPlayer.IsPlaying) _mediaPlayer.Pause();
                }
                else
                {
                    if (_mediaPlayer.State == VLCState.Paused) _mediaPlayer.Play();
                }
            }
        }

        private void ExitApplication()
        {
            Hide();
            Dispose();
            System.Windows.Application.Current.Shutdown();
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            _pauseCheckTimer?.Stop();

            if (trayIcon != null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
            }

            if (wallpaperWindow != null)
            {
                var helper = new WindowInteropHelper(wallpaperWindow);
                SetParent(helper.Handle, IntPtr.Zero); // Release from WorkerW
                wallpaperWindow.Close();
            }

            _currentMedia?.Dispose();
            _mediaPlayer?.Stop();
            _mediaPlayer?.Dispose();
            _libVLC?.Dispose();
            videoView?.Dispose();
            
            GC.SuppressFinalize(this);
        }

        private void SetupWallpaperWindow()
        {
            wallpaperWindow = new Window
            {
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
                AllowsTransparency = false,
                Background = System.Windows.Media.Brushes.Black,
                Focusable = false,
                IsHitTestVisible = false,
                Width = SystemParameters.PrimaryScreenWidth,
                Height = SystemParameters.PrimaryScreenHeight,
                Top = 0,
                Left = 0,
                Content = videoView,
                Title = "HoneyPie VLC"
            };
        }

        protected override async void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            await SetupWallpaperLayerAsync();
        }

        private async System.Threading.Tasks.Task SetupWallpaperLayerAsync()
        {
            StringBuilder logBuilder = new StringBuilder();
            logBuilder.AppendLine("\n--- Starting Window Enumeration ---");

            IntPtr progman = FindWindow("Progman", null);
            logBuilder.AppendLine($"Progman handle: {progman}");

            // Send 0x052C to Progman to create WorkerW
            IntPtr result = IntPtr.Zero;
            SendMessageTimeout(progman, 0x052C, IntPtr.Zero, IntPtr.Zero, 0, 1000, out result);
            logBuilder.AppendLine($"SendMessageTimeout result: {result}");

            // Allow time for Windows to process the message and create the WorkerW layer
            await System.Threading.Tasks.Task.Delay(500);

            // Enumerate windows to find WorkerW
            EnumWindows((topHandle, lParam) =>
            {
                logBuilder.AppendLine($"Top-level window handle: {topHandle}");
                IntPtr defView = FindWindowEx(topHandle, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (defView != IntPtr.Zero)
                {
                    logBuilder.AppendLine($"  Found SHELLDLL_DefView: {defView} under {topHandle}");
                    IntPtr worker = FindWindowEx(IntPtr.Zero, topHandle, "WorkerW", null);
                    if (worker != IntPtr.Zero)
                    {
                        logBuilder.AppendLine($"    Found WorkerW: {worker} under {topHandle}");
                        workerW = worker;
                        return false; // Stop enumeration
                    }
                }
                return true; // Continue enumeration
            }, IntPtr.Zero);

            logBuilder.AppendLine($"\nWorkerW handle after enumeration: {workerW}");
            logBuilder.AppendLine($"\nWorkerW handle before MessageBox: {workerW}");
            
            // Write the log once at the end
            try
            {
                File.AppendAllText("WindowEnumerationLog.txt", logBuilder.ToString());
            } 
            catch { /* Ignore log write failures */ }

            if (workerW == IntPtr.Zero)
            {
                System.Windows.MessageBox.Show("Unable to find desktop layer. HoneyPie may not work.");
                return;
            }

            var helper = new WindowInteropHelper(wallpaperWindow);
            helper.EnsureHandle();
            SetParent(helper.Handle, workerW);
            ShowWindow(helper.Handle, SW_SHOW);
            SetWindowPos(helper.Handle, IntPtr.Zero, 0, 0,
                (int)SystemParameters.PrimaryScreenWidth,
                (int)SystemParameters.PrimaryScreenHeight,
                SWP_SHOWWINDOW);
            wallpaperWindow.Show();
        }

        private void SelectVideo()
        {
            using var dialog = new OpenFileDialog { Filter = "Video files|*.mp4;*.mkv;*.avi|All files|*.*" };
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _currentMedia?.Dispose(); // Free the previous media to prevent unmanaged memory leaks
                _currentMedia = new Media(_libVLC, new Uri(dialog.FileName), ":input-repeat");
                _mediaPlayer.Play(_currentMedia);
            }
        }

        private void SelectVideo_Click(object sender, RoutedEventArgs e)
        {
            SelectVideo();
        }

        private void PauseVideo_Click(object sender, RoutedEventArgs e)
        {
            _mediaPlayer?.Pause();
        }

        private void MuteVideo_Click(object sender, RoutedEventArgs e)
        {
            _mediaPlayer.Mute = !_mediaPlayer.Mute;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }
    }
}
