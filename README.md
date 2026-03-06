<div align="center">
  <img src="honeypie.ico" width="128" height="128" alt="HoneyPie Logo">
  <h1>🍯 HoneyPie Wallpaper Engine</h1>
  <p><b>An ultra-lightweight, zero-bloat, hardware-accelerated video wallpaper engine for Windows.</b></p>
  
  [![Platform](https://img.shields.io/badge/Platform-Windows_10%20%7C%2011-blue.svg)](#)
  [![Framework](https://img.shields.io/badge/.NET-6.0-purple.svg)](#)
  [![License](https://img.shields.io/badge/License-MIT-green.svg)](#)
</div>

<br/>

HoneyPie is designed specifically for **low-end PCs and laptops**. Unlike heavy Electron-based or web-rendered wallpaper engines, HoneyPie acts purely as a tray application that directly hooks into the Windows `WorkerW` desktop layer, decoding videos entirely on your GPU.

## ✨ Key Features

*   🚀 **True Hardware Acceleration**: Built on `LibVLCSharp` with aggressive flags (`--hw-dec=auto`) to force 100% GPU video decoding.
*   🛑 **Smart Auto-Pause**: Automatically detects when you maximize a window (e.g., playing a video game or watching a movie) and **pauses the wallpaper, dropping resource usage to 0%**.
*   🧹 **Zero Memory Leaks**: Strictly implements the `IDisposable` pattern and COM cleanup, ensuring flat RAM usage no matter how long it runs.
*   🪶 **No UI Bloat**: HoneyPie runs entirely from your System Tray. There are no rendered graphical interfaces wasting your system's layout engine.

## 💻 System Requirements

*   **OS:** Windows 10 or Windows 11
*   **Runtime:** [.NET 6.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/6.0)
*   **VLC:** [VLC Media Player](https://www.videolan.org/vlc/) (Must be installed on the system)

## 🛠️ Installation & Build

If you want to compile HoneyPie from source:

1. Clone the repository:
   ```bash
   git clone https://github.com/chenura999/HoneyPie-WallpaperEngine.git
   cd HoneyPie-WallpaperEngine
   ```
2. Build the executable using the .NET CLI:
   ```bash
   dotnet build -c Release
   ```
3. Run the compiled `.exe` located in `bin/Release/net6.0-windows10.0.19041.0/`.

## 🎮 Usage

1. Launch **HoneyPie**. It will immediately hide into your System Tray (bottom right of your taskbar).
2. Right-click the 🍯 HoneyPie icon.
3. Click **Select Video** and choose an `.mp4`, `.mkv`, or `.avi` file.
4. The video will seamlessly loop on your desktop!
   * *Tip: For the absolute best performance on old PCs, use `1080p H.264 (.mp4)` files!*

## 🧪 Testing & Debugging

Because HoneyPie heavily relies on Windows-specific COM Interop (`user32.dll`) and VLC native libraries, automated testing is handled through manual validation workflows. 

### How to Test Changes
If you contribute to HoneyPie, please verify the following before submitting a Pull Request:
1. **Memory Leak Test:** Open Task Manager. Change the video 10 times via the tray icon. Ensure the "Private Working Set" RAM usage does not permanently increment.
2. **Auto-Pause Test:** With a video playing on the desktop, open Google Chrome and press `F11` to maximize it. Check Task Manager to verify that HoneyPie's GPU usage drops to exactly `0%`.
3. **Graceful Exit:** Right-click the tray icon and press `Exit`. Verify the wallpaper returns to normal, the icon disappears, and the `HoneyPie.exe` process fully terminates.

## 🤝 Contributing

Contributions are heavily encouraged! If you have an idea for optimization or a new feature (like multi-monitor support), please open an Issue or submit a Pull Request.

## 📝 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.