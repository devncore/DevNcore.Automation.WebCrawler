using PuppeteerSharp;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DevNcore.Automation.Speech
{
    public class SpeechAPI
    {
        Browser browser { get; set; }
        Page page { get; set; }
        string baseURL { get; set; } = "https://www.bing.com";

        public SpeechAPI(bool ClearAllChromium = true)
        {            
            if(ClearAllChromium)
                ClearAllProcess();
        }


        /// <summary>
        /// �����ν��� �ϱ� ���� �ʱ�ȭ �۾� ����
        /// </summary>
        /// <returns></returns>
        public async Task Initialize()
        {
            // ũ�ι̿� ��ġ Ȯ��
            var browserFetcher = new BrowserFetcher();
            await browserFetcher.DownloadAsync();

            browser = await Puppeteer.LaunchAsync(
                new LaunchOptions {
                    // ������
                    Headless = true, 
                    // ����ũ ���
                    Args = new[] {  "--use-fake-ui-for-media-stream", },
                    // headless ��忡�� ���Ұ����� �ʰ�
                    IgnoredDefaultArgs = new[] { "--mute-audio" },
            });

            page = await browser.NewPageAsync();
            await page.GoToAsync(baseURL);
        }


        public async Task<string> Run()
        {
            string result = null;

            string oldTitle = "";
            string step = "";

            Stopwatch w = new Stopwatch();
            w.Start();

            while (true)
            {
                Thread.Sleep(100);

                // 10�� ��� ����
                if (w.Elapsed.TotalSeconds >= 10)
                {
                    break;
                }

                try
                {
                    if (step == "")
                    {
                        oldTitle = await page.EvaluateFunctionAsync<string>("()=> document.querySelector('head > title').text");
                        oldTitle = oldTitle.ToString().Replace(" - �˻�", "");

                        var el = await page.XPathAsync("//*[@id='sb_form']/div[1]/div");
                        if(el != null)
                        {
                            // �����ν� ����
                            await el[0].ClickAsync();
                            step = "wait";
                        }
                    }
                    else if (step == "wait")
                    {
                        string title = await page.EvaluateFunctionAsync<string>("()=> document.querySelector('head > title').text");
                        title = title.Replace(" - �˻�", "");

                        // �����ν� �Ϸ�� ��������
                        if (title != oldTitle)
                        {                            
                            result = title;
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                }
            }

            // �ʱ�ȭ �������� �̵��ص�
            await page.GoToAsync(baseURL);

            return result;

        }

        public async void Close()
        {
            await page.CloseAsync();
            await browser.CloseAsync();

            page = null;
            browser = null;
        }

        /// <summary>
        /// ��� ũ�δϿ� ���μ��� �ݱ�
        /// </summary>
        private void ClearAllProcess()
        {
            try
            {                
                var list = Process.GetProcessesByName("chrome");
                if (list != null)
                {
                    foreach (var row in list)
                    {
                        Process proc = row;
                        if(proc.MainModule != null)
                        {
                            string folder = Path.GetDirectoryName(proc.MainModule.FileName);
                            string checkFile = $"{folder}\\chrome_pwa_launcher.exe";
                            if(File.Exists(checkFile))
                            {
                                proc.Kill();
                            }
                        }                        
                    }
                }

                page = null;
                browser = null;
            }
            catch
            {
            }
        }






        ~SpeechAPI()
        {
            if(page != null)
                page.CloseAsync().ConfigureAwait(false);

            if(browser != null)
                browser.CloseAsync().ConfigureAwait(false);
        }
    }
}
