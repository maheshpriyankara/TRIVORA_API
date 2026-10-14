using System;
using System.Threading;
using System.Threading.Tasks;
using PuppeteerSharp;
using PuppeteerSharp.Media;
using TrivoraHrisBackend.Models;

namespace TrivoraHrisBackend.Services
{
    /// <summary>
    /// Builds PDFs from HTML using headless Chrome (PuppeteerSharp).
    /// Used to generate the payslip / EPF-ETF PDF attachments for email.
    /// </summary>
    public static class PaySheetPdfBuilder
    {
        private static IBrowser? _browser;
        private static readonly SemaphoreSlim _lock = new(1, 1);

        // ============================================================
        // 1. Build from a PaySheetPrintDto — builds HTML internally
        //    and renders it as PDF.
        // ============================================================
        public static async Task<byte[]> BuildAsync(PaySheetPrintDto d, string baseUrl)
        {
            var html = PaySheetEmailRenderer.BuildHtml(d, null);

            // Make relative image paths absolute so headless Chrome can fetch them
            if (!string.IsNullOrWhiteSpace(baseUrl))
            {
                html = html.Replace("src=\"/images/", $"src=\"{baseUrl}/images/");
                html = html.Replace("src='/images/", $"src='{baseUrl}/images/");
            }

            return await BuildFromHtmlAsync(html);
        }

        // ============================================================
        // 2. Build directly from an HTML string.
        //    This is the core method. All other overloads call this.
        // ============================================================
        public static async Task<byte[]> BuildFromHtmlAsync(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
                throw new ArgumentException("HTML content is required", nameof(html));

            await _lock.WaitAsync();
            try
            {
                // ---- Launch headless Chrome once ----
                if (_browser == null || !_browser.IsConnected)
                {
                    Console.WriteLine("[PaySheetPdfBuilder] Launching headless Chrome...");
                    await new BrowserFetcher().DownloadAsync();

                    _browser = await Puppeteer.LaunchAsync(new LaunchOptions
                    {
                        Headless = true,
                        Args = new[]
                        {
                            "--no-sandbox",
                            "--disable-setuid-sandbox",
                            "--disable-dev-shm-usage"
                        }
                    });
                }

                using var page = await _browser.NewPageAsync();

                // ---- Load the HTML ----
                await page.SetContentAsync(html, new NavigationOptions
                {
                    WaitUntil = new[] { WaitUntilNavigation.Load }
                });

                // ---- Wait for images to load ----
                await page.EvaluateExpressionAsync(
                    "Promise.all(Array.from(document.images).map(img => " +
                    "img.complete ? null : new Promise(r => { img.onload = r; img.onerror = r; })))");

                // ---- Generate PDF ----
                var pdfBytes = await page.PdfDataAsync(new PdfOptions
                {
                    Format = PaperFormat.A4,
                    Landscape = true,
                    PrintBackground = true,
                    MarginOptions = new MarginOptions
                    {
                        Top    = "5mm",
                        Bottom = "5mm",
                        Left   = "5mm",
                        Right  = "5mm"
                    }
                });

                Console.WriteLine($"[PaySheetPdfBuilder] PDF generated: {pdfBytes.Length} bytes");
                return pdfBytes;
            }
            finally
            {
                _lock.Release();
            }
        }

        // ============================================================
        // 3. Shutdown helper — call on app shutdown if needed
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