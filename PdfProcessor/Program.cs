using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PdfProcessor.Services;
using PdfProcessor.Services.Ocr;
using PdfProcessor.Options;
using System.Runtime.CompilerServices;
using DotNetEnv;

namespace PdfProcessor
{
    static class Program
    {
        [DllImport("kernel32.dll")]
        private static extern bool AllocConsole();

        [DllImport("kernel32.dll")]
        private static extern bool FreeConsole();

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

        [DllImport("user32.dll")]
        private static extern bool EnableMenuItem(IntPtr hMenu, uint uIDEnableItem, uint uEnable);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll")]
        private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;
        private const int SC_CLOSE = 0xF060;
        private const int MF_GRAYED = 0x1;
        private const int MF_BYCOMMAND = 0x0;
        private const int GWLP_WNDPROC = -4;
        private const int WM_CLOSE = 0x10;
        private const int STD_OUTPUT_HANDLE = -11;
        private const int STD_ERROR_HANDLE = -12;

        private static bool _consoleVisible = false;
        private static IntPtr _consoleWnd = IntPtr.Zero;
        private static IntPtr _oldWndProc = IntPtr.Zero;
        private static WndProcDelegate? _newWndProc;
        private static StreamWriter? _consoleWriter;
        private static TextWriter? _originalOut;
        private static TextWriter? _originalError;
        private static FolderWatcherService? _folderWatcherService;
        private static int _pollingIntervalSeconds;
        private static string _scheduleMode = "INTERVAL";
        private static string _specificTime = "00:00:00";
        private static IServiceProvider? _serviceProvider;

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Check if .env file exists (first run detection)
            var envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
            bool isFirstRun = !File.Exists(envPath);

            // Show settings dialog on first run
            if (isFirstRun)
            {
                using var settingsForm = new SettingsForm();
                if (settingsForm.ShowDialog() != DialogResult.OK)
                {
                    // User cancelled - exit application
                    return;
                }
            }

            // Load .env file for folder paths
            try
            {
                Env.Load();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not load .env file: {ex.Message}");
            }

            // Load configuration
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            // Set up dependency injection
            var services = new ServiceCollection();
            services.Configure<OcrToolOptions>(configuration.GetSection(OcrToolOptions.SectionName));
            services.AddSingleton<ICommandRunner, CommandRunner>();
            services.AddSingleton<IPdfTextExtractor, PdfTextExtractor>();
            services.AddSingleton<IPdfPageRenderer, PdfPageRenderer>();
            services.AddSingleton<IOcrEngine, TesseractOcrEngine>();
            services.AddSingleton<IKkParser, KkParser>();
            services.AddSingleton<IKkExtractionService, KkExtractionService>();
            _serviceProvider = services.BuildServiceProvider();

            // Get folder paths from .env file (fallback to JSON config or defaults)
            var inputFolderConfig = Env.GetString("PUBLIC_FOLDER_URL") 
                ?? configuration["FolderPaths:InputFolder"] 
                ?? "Input";
            var failedFolderConfig = Env.GetString("FAILED_FOLDER_URL") 
                ?? configuration["FolderPaths:FailedFolder"] 
                ?? "Failed";

            // Handle relative vs absolute paths
            var inputFolderPath = Path.IsPathRooted(inputFolderConfig) 
                ? inputFolderConfig 
                : Path.Combine(Directory.GetCurrentDirectory(), inputFolderConfig);
            var failedFolderPath = Path.IsPathRooted(failedFolderConfig) 
                ? failedFolderConfig 
                : Path.Combine(Directory.GetCurrentDirectory(), failedFolderConfig);

            // Get polling interval from .env file (default to 60 if not set)
            _pollingIntervalSeconds = int.TryParse(Env.GetString("POLLING_INTERVAL_SECONDS"), out var envInterval)
                ? envInterval
                : 60;

            // Get schedule mode and specific time from .env file
            _scheduleMode = Env.GetString("SCHEDULE_MODE") ?? "INTERVAL";
            _specificTime = Env.GetString("SPECIFIC_TIME") ?? "00:00:00";

            // Create Failed folder if it doesn't exist
            if (!Directory.Exists(failedFolderPath))
            {
                Directory.CreateDirectory(failedFolderPath);
            }

            // Create services
            var httpClient = new HttpClient();
            var apiBaseUrl = configuration["ApiSettings:BaseUrl"] ?? "https://localhost:5000/api";
            var apiService = new ApiService(httpClient, apiBaseUrl, failedFolderPath);
            var kkExtractionService = _serviceProvider.GetRequiredService<IKkExtractionService>();
            var pdfProcessorService = new PdfProcessorService(apiService, kkExtractionService);
            _folderWatcherService = new FolderWatcherService(inputFolderPath, pdfProcessorService, apiService, _pollingIntervalSeconds, _scheduleMode, _specificTime);

            // Start the folder watcher
            _folderWatcherService.Start();

            // Create system tray icon
            var trayIcon = new NotifyIcon()
            {
                Text = "PDF Processor",
                Icon = new System.Drawing.Icon("icon.ico"),
                Visible = true
            };

            // Create context menu
            var contextMenu = new ContextMenuStrip();
            
            var toggleConsoleItem = new ToolStripMenuItem("Show Console");
            toggleConsoleItem.Click += (sender, e) => ToggleConsole();
            contextMenu.Items.Add(toggleConsoleItem);
            
            contextMenu.Items.Add(new ToolStripSeparator());
            
            var settingsItem = new ToolStripMenuItem("Settings");
            settingsItem.Click += (sender, e) => OpenSettings();
            contextMenu.Items.Add(settingsItem);
            
            contextMenu.Items.Add(new ToolStripSeparator());
            
            var exitItem = new ToolStripMenuItem("Exit");
            exitItem.Click += (sender, e) =>
            {
                _folderWatcherService?.Stop();
                trayIcon.Visible = false;
                Application.Exit();
            };
            contextMenu.Items.Add(exitItem);
            trayIcon.ContextMenuStrip = contextMenu;

            // Left-click to toggle console
            trayIcon.MouseClick += (sender, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    ToggleConsole();
                }
            };

            // Show balloon tip on startup
            trayIcon.ShowBalloonTip(3000, "PDF Processor", "Application is running in background. Click tray icon to show console.", ToolTipIcon.Info);

            // Run the application
            Application.Run();
        }

        private static void ToggleConsole()
        {
            if (!_consoleVisible)
            {
                AllocConsole();
                _consoleVisible = true;
                
                // Redirect console output to the new console window
                var stdHandle = GetStdHandle(STD_OUTPUT_HANDLE);
                var stdErrorHandle = GetStdHandle(STD_ERROR_HANDLE);
                
                _consoleWriter = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
                _originalOut = Console.Out;
                _originalError = Console.Error;
                
                Console.SetOut(_consoleWriter);
                Console.SetError(_consoleWriter);
                
                Console.WriteLine("Console window opened. Monitoring for PDF files...");
                Console.WriteLine("Close this window to hide it (application will continue running in system tray)");
                
                // Get console window handle and subclass it to intercept close messages
                _consoleWnd = GetConsoleWindow();
                if (_consoleWnd != IntPtr.Zero)
                {
                    _newWndProc = new WndProcDelegate(ConsoleWndProc);
                    _oldWndProc = SetWindowLongPtr(_consoleWnd, GWLP_WNDPROC, Marshal.GetFunctionPointerForDelegate(_newWndProc));
                    
                    // Disable the close button in the system menu
                    var hMenu = GetSystemMenu(_consoleWnd, false);
                    EnableMenuItem(hMenu, SC_CLOSE, MF_GRAYED | MF_BYCOMMAND);
                }
            }
            else
            {
                // Restore original window procedure before freeing console
                if (_consoleWnd != IntPtr.Zero && _oldWndProc != IntPtr.Zero)
                {
                    SetWindowLongPtr(_consoleWnd, GWLP_WNDPROC, _oldWndProc);
                }
                
                // Restore original console output
                if (_originalOut != null)
                {
                    Console.SetOut(_originalOut);
                }
                if (_originalError != null)
                {
                    Console.SetError(_originalError);
                }
                _consoleWriter?.Dispose();
                
                FreeConsole();
                _consoleVisible = false;
                _consoleWnd = IntPtr.Zero;
            }
        }

        private static IntPtr ConsoleWndProc(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam)
        {
            if (Msg == WM_CLOSE)
            {
                // Hide console instead of closing application
                ToggleConsole();
                return IntPtr.Zero;
            }
            return CallWindowProc(_oldWndProc, hWnd, Msg, wParam, lParam);
        }

        private static void OpenSettings()
        {
            using var settingsForm = new SettingsForm();
            if (settingsForm.ShowDialog() == DialogResult.OK)
            {
                // Reload configuration
                var configuration = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .Build();

                // Rebuild service provider
                var services = new ServiceCollection();
                services.Configure<OcrToolOptions>(configuration.GetSection(OcrToolOptions.SectionName));
                services.AddSingleton<ICommandRunner, CommandRunner>();
                services.AddSingleton<IPdfTextExtractor, PdfTextExtractor>();
                services.AddSingleton<IPdfPageRenderer, PdfPageRenderer>();
                services.AddSingleton<IOcrEngine, TesseractOcrEngine>();
                services.AddSingleton<IKkParser, KkParser>();
                services.AddSingleton<IKkExtractionService, KkExtractionService>();
                _serviceProvider = services.BuildServiceProvider();

                // Reload .env file
                try
                {
                    Env.Load();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Could not load .env file: {ex.Message}");
                }

                // Get new folder paths
                var inputFolderConfig = Env.GetString("PUBLIC_FOLDER_URL")
                    ?? configuration["FolderPaths:InputFolder"]
                    ?? "Input";
                var failedFolderConfig = Env.GetString("FAILED_FOLDER_URL")
                    ?? configuration["FolderPaths:FailedFolder"]
                    ?? "Failed";

                // Get new polling interval from .env file (default to 60 if not set)
                _pollingIntervalSeconds = int.TryParse(Env.GetString("POLLING_INTERVAL_SECONDS"), out var newInterval)
                    ? newInterval
                    : 60;

                // Get new schedule mode and specific time from .env file
                _scheduleMode = Env.GetString("SCHEDULE_MODE") ?? "INTERVAL";
                _specificTime = Env.GetString("SPECIFIC_TIME") ?? "00:00:00";

                // Handle relative vs absolute paths
                var inputFolderPath = Path.IsPathRooted(inputFolderConfig)
                    ? inputFolderConfig
                    : Path.Combine(Directory.GetCurrentDirectory(), inputFolderConfig);
                var failedFolderPath = Path.IsPathRooted(failedFolderConfig)
                    ? failedFolderConfig
                    : Path.Combine(Directory.GetCurrentDirectory(), failedFolderConfig);

                // Create Failed folder if it doesn't exist
                if (!Directory.Exists(failedFolderPath))
                {
                    Directory.CreateDirectory(failedFolderPath);
                }

                // Restart folder watcher with new settings
                if (_folderWatcherService != null)
                {
                    _folderWatcherService.Stop();

                    var httpClient = new HttpClient();
                    var apiBaseUrl = configuration["ApiSettings:BaseUrl"] ?? "https://localhost:5000/api";
                    var apiService = new ApiService(httpClient, apiBaseUrl, failedFolderPath);
                    var kkExtractionService = _serviceProvider.GetRequiredService<IKkExtractionService>();
                    var pdfProcessorService = new PdfProcessorService(apiService, kkExtractionService);

                    _folderWatcherService = new FolderWatcherService(inputFolderPath, pdfProcessorService, apiService, _pollingIntervalSeconds, _scheduleMode, _specificTime);
                    _folderWatcherService.Start();

                    if (_consoleVisible)
                    {
                        Console.WriteLine("Settings updated. Folder watcher restarted with new paths.");
                    }
                }
            }
        }
    }
}
