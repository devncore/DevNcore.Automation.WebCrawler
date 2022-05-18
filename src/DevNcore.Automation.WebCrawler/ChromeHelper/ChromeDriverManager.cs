using Ressy;
using Ressy.HighLevel.Versions;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;

namespace DevNcore.Automation.WebCrawler
{
    public class ChromeDriverManager
    {
        private const string LatestReleaseVersionUrl = "https://chromedriver.storage.googleapis.com/LATEST_RELEASE";

        /// <summary>
        /// chromedriver.exe ������ �ֽŹ����� ������ Ȯ���ϰ� �ƴϸ� �ֽŹ����� �ٿ�ε� �޴´�.
        /// Make sure that the chromedriver.exe file is the same as the latest version, or download the latest version.
        /// </summary>
        /// <param name="currentChromedriverPath">���� ����̹� ���</param>
        /// <returns></returns>
        public bool SetUp(string currentDriverPath)
        {
            bool result = false;
            string lastVersion = GetLatestVersion();
            string fileVersion = "";

            if (File.Exists(currentDriverPath))
            {
                fileVersion = FileVersionInfo.GetVersionInfo(currentDriverPath).FileVersion;
            }

            // ���Ϲ����� �ٸ��� �ֽŹ����� �ٿ�޴´�.
            // If the file version is different, download the latest version
            if (lastVersion != fileVersion)
            {
                string url = $"https://chromedriver.storage.googleapis.com/{lastVersion}/chromedriver_win32.zip";
                string tempZipPath = GetZipDestination(url);
                if (InstallDriver(url, tempZipPath, currentDriverPath))
                {
                    // chromedriver.exe ���Ͽ� ����̹� ������ �Է��Ѵ�.
                    // ���⼭ ������ �Է����� ������ �Ź� �ٿ�������� �ϴ� ������ �ֱ� ������
                    // Type the driver version in the chromedriver.exe file.
                    // If you don't enter the version here, there's a problem that you always try to download
                    var portableExecutable = new PortableExecutable(currentDriverPath);
                    portableExecutable.SetVersionInfo(v => v.SetFileVersion(new Version(lastVersion)));
                    result = true;
                }
            }
            else
            {
                result = true;
            }

            return result;
        }


        /// <summary>
        /// ũ�ҵ���̹� �ֽŹ��������� ��´�.
        /// Get the latest version information of the chrome driver
        /// </summary>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        private string GetLatestVersion()
        {
            var uri = new Uri(LatestReleaseVersionUrl);
            var webRequest = WebRequest.Create(LatestReleaseVersionUrl);
            using (var response = webRequest.GetResponse())
            {
                using (var content = response.GetResponseStream())
                {
                    if (content == null)
                        throw new ArgumentNullException($"Can't get content from URL: {uri}");

                    using (var reader = new StreamReader(content))
                    {
                        var version = reader.ReadToEnd().Trim();
                        return version;
                    }
                }
            }
        }


        #region InstallDriver
        /// <summary>
        /// �ֽ� ����̹��� �ٿ�ް� ������ Ǭ �� ����ο� �����Ѵ�.
        /// Download and extract the latest driver and copy it to the destination path
        /// </summary>
        /// <param name="url">Download Url</param>
        /// <param name="zipPath">Download ZipPath</param>
        /// <param name="binaryPath">Unzip exePath</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private bool InstallDriver(string url, string zipPath, string binaryPath)
        {
            // ������ �̹� �ִ°�� ���� ������ �����ϰ� �޴´�.
            // If the file already exists, delete the existing file and receive it
            if (File.Exists(binaryPath))
            {
                try
                {
                    File.Delete(binaryPath);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[{MethodBase.GetCurrentMethod().Name}] ���ܹ߻� => {ex.Message}");
                    return false;
                }
            }

            var zipDir = Path.GetDirectoryName(zipPath);
            var binaryName = Path.GetFileName(binaryPath);

            // ����̹��� �ٿ�ε� �޴´�.
            // Download the driver
            Directory.CreateDirectory(zipDir);
            zipPath = DownloadZip(url, zipPath);

            // ������ ��ο� ������ Ǭ��.
            // Extract to the set path
            var stagingDir = Path.Combine(zipDir, "staging");
            var stagingPath = Path.Combine(stagingDir, binaryName);

            Directory.CreateDirectory(stagingDir);

            if (zipPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(zipPath, stagingPath);
            }
            else if (zipPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                UnZip(zipPath, stagingPath, binaryName);
            }

            var binaryDir = Path.GetDirectoryName(binaryPath);

            // ��� ���͸��� ���� ��� ����
            // Create the destination directory if it doesn't exist
            if (!Directory.Exists(binaryDir))
            {
                Directory.CreateDirectory(binaryDir);
            }


            Exception renameException = null;
            try
            {
                string[] files = Directory.GetFiles(stagingDir);

                // ������ �����ϰ� ��� ������ �̹� �ִ� ��� �����
                // Copy the files and overwrite destination files if they already exist.
                foreach (string file in files)
                {
                    // ���� ��� ����� ����Ͽ� ��ο��� ���� �̸��� ����
                    // Use static Path methods to extract only the file name from the path.
                    var fileName = Path.GetFileName(file);
                    var destFile = Path.Combine(binaryDir, fileName);
                    File.Copy(file, destFile, true);
                }
            }
            catch (Exception ex)
            {
                renameException = ex;
            }

            // �۾��� �Ϸ�Ǹ� �ʿ���� ������ �����Ѵ�
            // Delete unnecessary files when the operation is complete            
            try
            {
                if (Directory.Exists(stagingDir))
                    Directory.Delete(stagingDir, true);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
            }
            try
            {
                RemoveZip(zipPath);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
            }

            // ����� ������ �������� ������ ����ġ ���� ������� �̸� ������ ���������� �ǹ�
            // If the destination still doesn't exist, it means the rename failed in an unexpected way
            if (!Directory.Exists(binaryDir))
            {
                throw new Exception($"Error writing {binaryDir}", renameException);
            }

            return true;
        }


        private string GetZipDestination(string url)
        {
            var tempDirectory = Path.GetTempPath();
            var guid = Guid.NewGuid().ToString();
            var zipName = Path.GetFileName(url);
            if (zipName == null) throw new ArgumentNullException($"Can't get zip name from URL: {url}");
            return Path.Combine(tempDirectory, guid, zipName);
        }

        private string DownloadZip(string url, string destination)
        {
            if (File.Exists(destination)) return destination;
            using (var webClient = new WebClient())
            {
                webClient.DownloadFile(new Uri(url), destination);
            }

            return destination;
        }

        private string UnZip(string path, string destination, string name)
        {
            var zipName = Path.GetFileName(path);
            if (zipName != null && zipName.Equals(name, StringComparison.CurrentCultureIgnoreCase))
            {
                File.Copy(path, destination);
                return destination;
            }

            using (var zip = ZipFile.Open(path, ZipArchiveMode.Read))
            {
                foreach (var entry in zip.Entries)
                {
                    if (entry.Name == name)
                    {
                        entry.ExtractToFile(destination, true);
                    }
                }
            }

            return destination;
        }

        private void RemoveZip(string path)
        {
            File.Delete(path);
        }

        #endregion

    }
}
