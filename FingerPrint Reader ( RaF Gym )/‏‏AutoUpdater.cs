using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;

public class AutoUpdater
{
    private readonly string _pastebinUrl;      // URL of the Pastebin raw data
    private readonly string _currentVersion; // Current application version

    public AutoUpdater(string pastebinUrl, string currentVersion)
    {
        _pastebinUrl = pastebinUrl;
        _currentVersion = currentVersion;
    }

    public async Task CheckAndUpdateAsync()
    {
        MessageBox.Show("Checking for updates...", "Update Check", MessageBoxButtons.OK, MessageBoxIcon.Information);

        try
        {
            using (HttpClient client = new HttpClient())
            {
                MessageBox.Show($"Fetching metadata from: {_pastebinUrl}", "Debug", MessageBoxButtons.OK, MessageBoxIcon.Information);

                var response = await client.GetStringAsync(_pastebinUrl);
                var metadata = Newtonsoft.Json.JsonConvert.DeserializeObject<UpdateMetadata>(response);

                if (metadata == null)
                {
                    MessageBox.Show("Failed to parse update metadata.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                MessageBox.Show($"Latest Version: {metadata.LatestVersion}\nDownload URL: {metadata.DownloadUrl}", "Debug", MessageBoxButtons.OK, MessageBoxIcon.Information);

                if (IsUpdateAvailable(metadata.LatestVersion))
                {
                    MessageBox.Show($"Update available: {metadata.LatestVersion}", "Update Available", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    await DownloadAndReplaceExeAsync(metadata.DownloadUrl);
                }
                else
                {
                    MessageBox.Show("You're up to date!", "No Updates", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error during update check: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private bool IsUpdateAvailable(string latestVersion)
    {
        return string.Compare(latestVersion, _currentVersion, StringComparison.Ordinal) > 0;
    }

    private async Task DownloadAndReplaceExeAsync(string downloadUrl)
    {
        string tempPath = Path.Combine(Path.GetTempPath(), "UpdatedApp.exe");
        string currentExePath = Process.GetCurrentProcess().MainModule.FileName;

        MessageBox.Show("Downloading new version...", "Downloading", MessageBoxButtons.OK, MessageBoxIcon.Information);

        try
        {
            using (HttpClient client = new HttpClient())
            {
                var response = await client.GetAsync(downloadUrl);
                response.EnsureSuccessStatusCode();

                using (FileStream fs = new FileStream(tempPath, FileMode.Create))
                {
                    await response.Content.CopyToAsync(fs);
                }
            }

            MessageBox.Show("Download complete. Restarting to apply update...", "Download Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);

            Process.Start(new ProcessStartInfo
            {
                FileName = tempPath,
                Arguments = $"--replace \"{currentExePath}\"",
                UseShellExecute = false,
            });

            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error during download: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}

public class UpdateMetadata
{
    public string LatestVersion { get; set; }
    public string DownloadUrl { get; set; }
}
