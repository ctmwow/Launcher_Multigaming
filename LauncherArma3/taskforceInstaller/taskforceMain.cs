using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Forms;
using MetroFramework;
using System.IO;
using System.Diagnostics;
using RestSharp;
using Newtonsoft.Json.Linq;
using System.Collections;
using System.Security.Cryptography;
using System.Net;
using System.Threading;

namespace taskforceInstaller
{
    public partial class taskforceMain : MetroFramework.Forms.MetroForm
    {

        /* VARIABLES GLOBAL */
        string apiUrl;
        string appdata;
        string vTaskForce;
        string launcherDest;
        long downloaded_bytes = 0;
        long need_to_download = 0;
        long oldBytes = 0;
        long downloaded;
        int oldTime;
        long oldBytesPerSeconds;
        int stat = 0;
        DateTime startTimeDownload;
        long bytesPerSecond;
        string serverID;
        string downloadPath;
        string teamspeakDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\TS3Client\\plugins\\";


        public taskforceMain(string _apiUrl, string _appdata, string _vTaskForce, string _launcherDest, string _serverID, string _downloadPath)
        {
            InitializeComponent();
            apiUrl = _apiUrl;
            appdata = _appdata;
            vTaskForce = _vTaskForce;
            launcherDest = _launcherDest;
            serverID = _serverID;
            downloadPath = _downloadPath;
        }

        private void taskforceMain_Load(object sender, EventArgs e)
        {
            
        }

 

        private async void installButton_Click(object sender, EventArgs e)
        {
            if (!Directory.Exists(teamspeakDirectory))
                MetroMessageBox.Show(this, "TeamSpeak3 not installed.", "TaskForce installer", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            dynamic res;
            Queue files = new Queue();

            teamspeakDestination.Text = "Download initialisation";

            // LIST ALL FILES
             
                var client = new RestClient(apiUrl);

                var request = new RestRequest("api/games/arma3/taskforce/list", Method.POST);

                request.AddParameter("id", serverID);

                IRestResponse response = client.Execute(request);
                var content = response.Content;

                res = JObject.Parse(content.ToString());
                if (res.status == 42)
                {
                    int total_files = res.total;
                    int i = 0;

                    string local_md5;
                    string remote_md5;
                    string file;
                    string directory;

                    while (i != total_files)
                    {              
                        directory = Path.GetDirectoryName(teamspeakDirectory + res.files[i].name);
                        file = teamspeakDirectory + res.files[i].name;
                        if (!Directory.Exists(directory))
                            Directory.CreateDirectory(directory);
                        local_md5 = getFileMd5(file).ToLower();
                        remote_md5 = res.files[i].md5;
                        if (remote_md5 != local_md5)
                        {
                            need_to_download += (long)res.files[i].size;
                            files.Enqueue(res.files[i].name);
                        }
                        i++;
                    }

                }
            

            //Download files

            int j = files.Count;
            string current;

            while (j > 0)
            {
                stat = 0;
                current = files.Dequeue().ToString();
                startDownload(apiUrl + "/" + downloadPath +"/taskforce/" + current, teamspeakDirectory + current);
                while (stat == 0)
                    await Task.Delay(1000);
                downloaded++;
                j--;
            }

            // END DOWNLOAD
            teamspeakDestination.Text = "Ready to download";
            if (File.Exists(appdata + "/vTaskForce"))
                File.Delete(appdata + "/vTaskForce");
            File.WriteAllText(appdata + "/vTaskForce", vTaskForce);
            MetroMessageBox.Show(this, "Now you need to restart TeamSpeak 3 and enable Taskforce Radio plugins", "TaskForce installer", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            if (!File.Exists(launcherDest))
            {
                MetroMessageBox.Show(this, "Launcher does't exits", "TaskForce installer", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            try
            {
                Process launcher = new Process();
                launcher.StartInfo.FileName = launcherDest;
                launcher.Start();
                this.Close();
            }
            catch
            {
                MetroMessageBox.Show(this, "Launching error", "TaskForce installer", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        protected string getFileMd5(string filePath)
        {
            if (!File.Exists(filePath))
                return ("errorMd5");
            try
            {
                using (var md5 = MD5.Create())
                {
                    using (var stream = File.OpenRead(filePath))
                    {
                        return BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", string.Empty);
                    }
                }
            }
            catch
            {
                return "errormd5";
            }
        }

        private async void startDownload(string remote, string local)
        {
            oldBytes = 0;
            oldTime = DateTime.Now.Second;
            oldBytesPerSeconds = 0;
            WebClient client = new WebClient();
            Thread thread = new Thread(() =>
            {
                client.DownloadProgressChanged += new DownloadProgressChangedEventHandler(client_DownloadProgressChanged);
                client.DownloadFileCompleted += new AsyncCompletedEventHandler(client_DownloadFileCompleted);
                startTimeDownload = DateTime.Now;
                client.DownloadFileAsync(new Uri(remote), local);
            });
            thread.Start();
            while (stat == 0)                           
                await Task.Delay(1000);            
        }

        public string FormatBytes(long bytes)
        {
            const int scale = 1024;
            string[] orders = new string[] { "GB", "MB", "KB", "Bytes" };
            long max = (long)Math.Pow(scale, orders.Length - 1);

            foreach (string order in orders)
            {
                if (bytes > max)
                    return string.Format("{0:##.##} {1}", decimal.Divide(bytes, max), order);

                max /= scale;
            }
            return "0 Bytes";
        }
        void client_DownloadProgressChanged(object sender, System.Net.DownloadProgressChangedEventArgs e)
        {
            this.BeginInvoke((MethodInvoker)delegate
            {
                downloaded_bytes += e.BytesReceived - oldBytes;
                oldBytes = e.BytesReceived;
                string received = FormatBytes(e.BytesReceived);
                string total = FormatBytes(e.TotalBytesToReceive);
                taskforceProgress.Maximum = (int)e.TotalBytesToReceive;
                taskforceProgress.Value = (int)e.BytesReceived;
                teamspeakDestination.Text = "Downloaded " + FormatBytes(downloaded_bytes) + " of " + FormatBytes(need_to_download);
            });
        }

        public static string FormatDurationSeconds(int seconds)
        {
            var duration = TimeSpan.FromSeconds(seconds);
            string result = "";

            if (duration.TotalHours >= 1)
                result += (int)duration.TotalHours + " Hours, ";

            result += String.Format("{0:%m} Minutes, {0:%s} Seconds", duration);
            return result;
        }


        void client_DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
        {
            this.BeginInvoke((MethodInvoker)delegate
            {
                stat = 1;
            });
        }
    }
}
