using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PuppeteerSharp;
using PuppeteerSharp.Media;
using TrivoraHrisBackend.Models;

namespace TrivoraHrisBackend.Services
{
    /// <summary>
    /// Builds PDFs from HTML using headless Chrome (PuppeteerSharp).
    /// Used to generate payslip / EPF-ETF / attendance report PDF attachments for email.
    /// </summary>
    public static class PaySheetPdfBuilder
    {
        // ── One-time init state ──
        private static readonly SemaphoreSlim _initLock = new(1, 1);
        private static string? _cachedExecutablePath;

        // ── Browser reuse (optional) ──
        private static IBrowser? _browser;
        private static readonly SemaphoreSlim _browserLock = new(1, 1);

        // ============================================================
        // 1. Build from a PaySheetPrintDto — builds HTML internally
        // ============================================================
        public static async Task<byte[]> BuildAsync(PaySheetPrintDto d, string baseUrl)
        {
            var html = PaySheetEmailRenderer.BuildHtml(d, null);

            // Absolute-ise relative image paths so headless Chrome can fetch them
            if (!string.IsNullOrWhiteSpace(baseUrl))
            {
                html = html.Replace("src=\"/images/", $"src=\"{baseUrl}/images/");
                html = html.Replace("src='/images/", $"src='{baseUrl}/images/");
            }

            return await BuildFromHtmlAsync(html);
        }

        // ============================================================
        // 2. Build from a raw HTML string
        // ============================================================
        public static async Task<byte[]> BuildFromHtmlAsync(string html, bool landscape = false)
        {
            if (string.IsNullOrWhiteSpace(html))
                throw new ArgumentException("HTML is empty.", nameof(html));

            // Ensure a Chrome binary exists (download once, cache for life)
            var execPath = await EnsureBrowserAsync();

            await using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = true,
                ExecutablePath = execPath,
                Args = new[]
                {
                    "--no-sandbox",
                    "--disable-setuid-sandbox",
                    "--disable-dev-shm-usage",
                    "--disable-gpu"
                }
            });

            await using var page = await browser.NewPageAsync();

            await page.SetContentAsync(html, new NavigationOptions
            {
                WaitUntil = new[] { WaitUntilNavigation.Networkidle0 },
                Timeout = 30000
            });

            return await page.PdfDataAsync(new PdfOptions
            {
                Format          = PaperFormat.A4,
                Landscape       = landscape,
                PrintBackground = true,
                MarginOptions   = new MarginOptions
                {
                    Top    = "6mm",
                    Bottom = "12mm",
                    Left   = "4mm",
                    Right  = "4mm"
                }
            });
        }

        // ============================================================
        // 3. Ensure a Chrome executable is available
        //    → Try system Chrome first, else download once to ~/.trivora-chrome
        // ============================================================
        private static async Task<string> EnsureBrowserAsync()
        {
            if (!string.IsNullOrEmpty(_cachedExecutablePath) &&
                File.Exists(_cachedExecutablePath))
                return _cachedExecutablePath;

            await _initLock.WaitAsync();
            try
            {
                if (!string.IsNullOrEmpty(_cachedExecutablePath) &&
                    File.Exists(_cachedExecutablePath))
                    return _cachedExecutablePath;

                // ── A) Try the machine's installed Chrome / Chromium ──
                var sysChrome = FindSystemChrome();
                if (sysChrome != null)
                {
                    _cachedExecutablePath = sysChrome;
                    return sysChrome;
                }

                // ── B) No system Chrome → download once into home directory ──
                var cacheDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".trivora-chrome");

                Directory.CreateDirectory(cacheDir);

                var fetcher = new BrowserFetcher(new BrowserFetcherOptions
                {
                    Path = cacheDir
                });

                var installed = await fetcher.DownloadAsync();

                if (installed == null)
                    throw new InvalidOperationException(
                        "PuppeteerSharp failed to download Chrome (null result).");

                var exePath = installed.GetExecutablePath();

                if (!File.Exists(exePath))
                {
                    // macOS sometimes reports the .app wrapper path; dig into it
                    var macInner = Path.Combine(
                        Path.GetDirectoryName(exePath) ?? "",
                        "Google Chrome for Testing.app",
                        "Contents",
                        "MacOS",
                        "Google Chrome for Testing");

                    if (File.Exists(macInner))
                        exePath = macInner;
                    else
                        throw new FileNotFoundException(
                            "Downloaded Chrome executable not found at the expected path.",
                            exePath);
                }

                _cachedExecutablePath = exePath;
                return exePath;
            }
            finally
            {
                _initLock.Release();
            }
        }

        // ============================================================
        // 4. Locate a Chrome / Chromium binary already installed on the OS
        // ============================================================
        private static string? FindSystemChrome()
        {
            try
            {
                if (OperatingSystem.IsMacOS())
                {
                    var paths = new[]
                    {
                        "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome",
                        "/Applications/Google Chrome Canary.app/Contents/MacOS/Google Chrome Canary",
                        "/Applications/Chromium.app/Contents/MacOS/Chromium",
                        "/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge"
                    };
                    return paths.FirstOrDefault(File.Exists);
                }

                if (OperatingSystem.IsWindows())
                {
                    var paths = new[]
                    {
                        @"C:\Program Files\Google\Chrome\Application\chrome.exe",
                        @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
                        @"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
                        @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
                    };
                    return paths.FirstOrDefault(File.Exists);
                }

                if (OperatingSystem.IsLinux())
                {
                    var paths = new[]
                    {
                        "/usr/bin/google-chrome",
                        "/usr/bin/google-chrome-stable",
                        "/usr/bin/chromium",
                        "/usr/bin/chromium-browser",
                        "/snap/bin/chromium"
                    };
                    return paths.FirstOrDefault(File.Exists);
                }
            }
            catch
            {
                // swallow — just means "no system Chrome found"
            }
            return null;
        }

        // ============================================================
        // 5. Shutdown (only relevant if you switch to browser-reuse mode)
        // ============================================================
        public static async Task ShutdownAsync()
        {
            if (_browser != null)
            {
                await _browser.CloseAsync();
                _browser.Dispose();
                _browser = null;
            }
        }
    }
}