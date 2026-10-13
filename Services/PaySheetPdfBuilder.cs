using System;
using System.Threading;
using System.Threading.Tasks;
using PuppeteerSharp;
using PuppeteerSharp.Media;
using TrivoraHrisBackend.Models;

namespace TrivoraHrisBackend.Services
{
    public static class PaySheetPdfBuilder
    {
        private static IBrowser? _browser;
        private static readonly SemaphoreSlim _lock = new(1, 1);

        public static async Task<byte[]> BuildAsync(PaySheetPrintDto d, string baseUrl)
        {
            // 1. Build the same HTML used for email body
            var html = PaySheetEmailRenderer.BuildHtml(d, null);

            // 2. Force relative image paths to absolute for the headless browser
            html = html.Replace("src='/images/", $"src='{baseUrl}/images/");
            html = html.Replace("src=\"/images/", $"src=\"{baseUrl}/images/");

            await _lock.WaitAsync();
            try
            {
                if (_browser == null || !_browser.IsConnected)
                {
                    Console.WriteLine("[PaySheetPdfBuilder] Launching headless Chrome...");
                    await new BrowserFetcher().DownloadAsync();
                    _browser = await Puppeteer.LaunchAsync(new LaunchOptions
                    {
                        Headless = true,
                        Args = new[] { "--no-sandbox", "--disable-setuid-sandbox" }
                    });
                }

                using var page = await _browser.NewPageAsync();

                await page.SetContentAsync(html, new NavigationOptions
                {
                    WaitUntil = new[] { WaitUntilNavigation.Networkidle0 }
                });

                // Wait for images
                await page.EvaluateExpressionAsync(
                    "Promise.all(Array.from(document.images).map(img => img.complete ? null : new Promise(r => { img.onload = r; img.onerror = r; })))");

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
    }
}