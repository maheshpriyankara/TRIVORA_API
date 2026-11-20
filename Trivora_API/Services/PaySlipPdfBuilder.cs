using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PuppeteerSharp;
using PuppeteerSharp.Media;

namespace TrivoraHrisBackend.Services
{
    /// <summary>
    /// PDF builder for PAYSLIP-ONLY emails (portrait A4).
    /// Independent from PaySheetPdfBuilder (used by Attendance page).
    /// </summary>
    public static class PaySlipPdfBuilder
    {
        private static readonly SemaphoreSlim _initLock = new(1, 1);
        private static string? _cachedExecutablePath;

        public static async Task<byte[]> BuildAsync(string html, string? printedBy = null)
        {
            if (string.IsNullOrWhiteSpace(html))
                throw new ArgumentException("HTML is empty.", nameof(html));

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

            var generated     = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
            var printedByTxt  = string.IsNullOrWhiteSpace(printedBy) ? "User" : printedBy;

            var footerTemplate = $@"
<div style='width:100%; padding:0 8mm; font-family:Segoe UI, Arial, sans-serif;
            font-size:8pt; color:#64748b; display:flex;
            justify-content:space-between; align-items:center;
            -webkit-print-color-adjust: exact;'>
    <span style='font-style:italic; color:#94a3b8;'>
        Printed by {System.Net.WebUtility.HtmlEncode(printedByTxt)} &middot; Generated: {generated}
    </span>
    <span style='font-weight:600;'>
        Page <span class='pageNumber'></span> of <span class='totalPages'></span>
    </span>
</div>";

            return await page.PdfDataAsync(new PdfOptions
            {
                Format              = PaperFormat.A4,
                Landscape           = false,
                PrintBackground     = true,
                DisplayHeaderFooter = true,
                HeaderTemplate      = "<div></div>",
                FooterTemplate      = footerTemplate,
                MarginOptions       = new MarginOptions
                {
                    Top    = "6mm",
                    Bottom = "15mm",
                    Left   = "4mm",
                    Right  = "4mm"
                }
            });
        }

        private static async Task<string> EnsureBrowserAsync()
        {
            if (!string.IsNullOrEmpty(_cachedExecutablePath) && File.Exists(_cachedExecutablePath))
                return _cachedExecutablePath;

            await _initLock.WaitAsync();
            try
            {
                if (!string.IsNullOrEmpty(_cachedExecutablePath) && File.Exists(_cachedExecutablePath))
                    return _cachedExecutablePath;

                var sysChrome = FindSystemChrome();
                if (sysChrome != null) { _cachedExecutablePath = sysChrome; return sysChrome; }

                var cacheDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".trivora-chrome");
                Directory.CreateDirectory(cacheDir);

                var fetcher   = new BrowserFetcher(new BrowserFetcherOptions { Path = cacheDir });
                var installed = await fetcher.DownloadAsync();
                if (installed == null)
                    throw new InvalidOperationException("PuppeteerSharp failed to download Chrome (null result).");

                var exePath = installed.GetExecutablePath();
                if (!File.Exists(exePath))
                {
                    var macInner = Path.Combine(
                        Path.GetDirectoryName(exePath) ?? "",
                        "Google Chrome for Testing.app",
                        "Contents", "MacOS", "Google Chrome for Testing");
                    if (File.Exists(macInner)) exePath = macInner;
                    else throw new FileNotFoundException("Downloaded Chrome executable not found.", exePath);
                }
                _cachedExecutablePath = exePath;
                return exePath;
            }
            finally { _initLock.Release(); }
        }

        private static string? FindSystemChrome()
        {
            try
            {
                if (OperatingSystem.IsMacOS())
                    return new[]
                    {
                        "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome",
                        "/Applications/Google Chrome Canary.app/Contents/MacOS/Google Chrome Canary",
                        "/Applications/Chromium.app/Contents/MacOS/Chromium",
                        "/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge"
                    }.FirstOrDefault(File.Exists);

                if (OperatingSystem.IsWindows())
                    return new[]
                    {
                        @"C:\Program Files\Google\Chrome\Application\chrome.exe",
                        @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
                        @"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
                        @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
                    }.FirstOrDefault(File.Exists);

                if (OperatingSystem.IsLinux())
                    return new[]
                    {
                        "/usr/bin/google-chrome",
                        "/usr/bin/google-chrome-stable",
                        "/usr/bin/chromium",
                        "/usr/bin/chromium-browser",
                        "/snap/bin/chromium"
                    }.FirstOrDefault(File.Exists);
            }
            catch { }
            return null;
        }
    }
}