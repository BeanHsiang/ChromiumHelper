using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.Threading.Tasks;

namespace ChromiumHelper
{
    enum HttpMethod
    {
        Post,
        Get
    }

    public partial class Form1 : Form
    {
        const string USER_AGENT = "Mozilla/5.0 (Windows NT 6.1) AppleWebKit/537.2 (KHTML, like Gecko) Chrome/22.0.1212.0 Safari/537.2";
        const string ACCEPT = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";
        const string ACCEPT_CHARSET = "GBK,utf-8;q=0.7,*;q=0.3";
        const string ACCEPT_ENCODING = "gzip,deflate,sdch";
        const string ACCEPT_LANGUAGE = "zh-CN,zh;q=0.8";
        const string CONTENT_TYPE = "application/x-www-form-urlencoded";
        const string CHROME_VERSION = "http://commondatastorage.googleapis.com/chromium-browser-snapshots/Win/LAST_CHANGE";
        const string CHROME_DIRECT = "http://commondatastorage.googleapis.com/chromium-browser-snapshots/index.html?path=Win/{0}/";
        const string CHROME_DOWNLOAD = "http://commondatastorage.googleapis.com/chromium-browser-snapshots/Win/{0}/chrome-win32.zip";
        const string ZIP_FILE = "chrome-win32.zip";
        long total = 0;//文件总长度
        int taskCount = 3;//线程数
        long globalCurrent = 0;//累积下载长度

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Shown(object sender, EventArgs e)
        {
            //取得最新的版本号
            HttpWebResponse res = Requst(CHROME_VERSION, string.Empty);
            string version = string.Empty;
            using (var sw = new StreamReader(res.GetResponseStream()))
            {
                version = sw.ReadToEnd();
            }
            res.Close();

            //请求下载文件，初始化界面内容
            res = Requst(string.Format(CHROME_DOWNLOAD, version), string.Empty);
            total = res.ContentLength;
            res.Close();

            DownLoadProgress.Maximum = (int)total;
            DownLoadProgress.Minimum = 0;

            Total.Text = string.Format("{0}M", (total / 1024.00 / 1024.00).ToString("##0.00"));

            IList<Range> list = InitRange((int)total, taskCount);
            IList<Task> tasks = new List<Task>();

            File.Delete(ZIP_FILE);

            foreach (var item in list)
            {
                var task = new Task((o) =>
               {
                   var item2 = (Range)o;
                   HttpWebResponse subRes = Requst(string.Format(CHROME_DOWNLOAD, version), string.Empty, item2.from, item2.to);
                   using (var sw = subRes.GetResponseStream())
                   {
                       using (var fs = new FileStream(ZIP_FILE, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite))
                       {
                           fs.Seek(item2.from, SeekOrigin.Begin);
                           int length = 0;
                           byte[] byt = new byte[1000];
                           while ((length = sw.Read(byt, 0, byt.Length)) > 0)
                           {
                               fs.Write(byt, 0, length);
                               Interlocked.Add(ref globalCurrent, length);
                               //globalCurrent += length;
                               Current.Text = string.Format("{0}M", (globalCurrent / 1024.00 / 1024.00).ToString("##0.00"));
                               DownLoadProgress.Value = (int)globalCurrent;
                           }
                       }
                   }
                   subRes.Close();
               }, item);
                tasks.Add(task);
                task.Start();
            }

            //另起一个任务，等待下载任务结束后弹出提示并主动关闭应用
            var taskClose = new Task((o) =>
            {
                var t = (IList<Task>)o;
                Task.WaitAll(t.ToArray());
                if (globalCurrent == total)
                {
                    MessageBox.Show("Download finished !", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                this.Close();
            }, tasks);
            taskClose.Start();
        }

        //private void Update(object current)
        //{
        //    int c = (int)current;
        //    Current.Text = ((int)current).ToString();
        //    DownLoadProgress.Value = c;
        //}

        private IList<Range> InitRange(long total, int part)
        {
            IList<Range> list = new List<Range>();
            long max = total;
            while (max % part > 1000000) { max++; }
            long division = max / part;
            long last = -1;
            for (int i = 0; i < part; i++)
            {
                Range range = new Range { from = last + 1, to = (i + 1) * division };
                list.Add(range);
                last = range.to;
            }
            Range r = list.Last<Range>();
            r.to = total;
            list.RemoveAt(list.Count - 1);
            list.Add(r);
            return list;
        }

        private HttpWebResponse Requst(string url, string param, long from, long to, HttpMethod method = HttpMethod.Get)
        {
            HttpWebRequest req = WebRequest.Create(url) as HttpWebRequest;
            req.UserAgent = USER_AGENT;
            req.Accept = ACCEPT;
            req.ContentType = CONTENT_TYPE;
            req.KeepAlive = true;
            req.Method = method.ToString();
            req.AllowAutoRedirect = true;
            req.AddRange(from, to);
            //req.Proxy = new WebProxy("127.0.0.1", 8888)
            return req.GetResponse() as HttpWebResponse;
        }

        private HttpWebResponse Requst(string url, string param, HttpMethod method = HttpMethod.Get)
        {
            HttpWebRequest req = WebRequest.Create(url) as HttpWebRequest;
            req.UserAgent = USER_AGENT;
            req.Accept = ACCEPT;
            req.ContentType = CONTENT_TYPE;
            req.KeepAlive = true;
            req.Method = method.ToString();
            req.AllowAutoRedirect = true;
            //req.Proxy = new WebProxy("127.0.0.1", 8888)
            return req.GetResponse() as HttpWebResponse;
        }

    }

    struct Range
    {
        public long from;
        public long to;
    }
}
