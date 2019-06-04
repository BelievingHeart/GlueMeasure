using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace MainAPP.UI
{
    public partial class FormMain : Form
    {
        private FormModel frmModelUp;


        public FormMain()
        {
            InitializeComponent();
        }

        public void ShowAndSaveMsg_Invoke(string msg)
        {
            Action fp = () =>
            {
                if (listBox1.Items.Count > 300) listBox1.Items.RemoveAt(0);
                var formatStr = "HH:mm:ss";
                var longMsg = string.Format("[{0}]", DateTime.Now.ToString(formatStr)) + msg;
                listBox1.Items.Add(longMsg);
                listBox1.SelectedIndex = listBox1.Items.Count - 1;

                //Misc.WriteLog(longMsg);
            };
            if (InvokeRequired)
                Invoke(fp);
            else
                fp();
        }

        // using console
//        [DllImport("kernel32.dll", SetLastError = true)]
//        [return: MarshalAs(UnmanagedType.Bool)]
//        static extern bool AllocConsole();

        public void ShowDog(bool isOK)
        {
            Action fp = () =>
            {
                if (isOK)
                    toolStripStatusLabel1.Text = "加密狗状态OK";
                else
                    toolStripStatusLabel1.Text = "未识别到加密狗，请联系技术人员！";
            };
            if (InvokeRequired)
                Invoke(fp);
            else
                fp();
        }

        private void EnableControls(bool enabled)
        {
            menuIOCard.Enabled = enabled;
            stationsToolStripMenuItem.Enabled = enabled;
            btnRun.Enabled = enabled;
        }

        private void FormMain_Load(object sender, EventArgs e)
        {
            // Enable console
//            AllocConsole();
            // this.EnableControls(false);
            UVGlue.applicationIsRunning = true;
            UVGlue.resultsThatImagesSholdBeSaved = new List<string>
                {UVGlue.resultNG, UVGlue.resultNoProduct, UVGlue.resultImageAcqFailed};

            var a = IOC0640.ioc_board_init();
            if (a > 0)
                ShowAndSaveMsg_Invoke("IO卡打开成功");
            else
                ShowAndSaveMsg_Invoke("IO卡打开失败");

            UVGlue.threadListen = new Thread(UVGlue.Listen);
            UVGlue.threadListen.Start();
            UVGlue.threadExecute = new Thread(UVGlue.ListenPos1);
            UVGlue.threadExecute.Start();
        }


        private void FormMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                var closeDecision = MessageBox.Show("是否退出", "正在退出", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (closeDecision == DialogResult.Yes)
                {
                    UVGlue.JoinBackgroundThreads();
                    UVGlue.cleanUp();
                }
                else
                {
                    e.Cancel = true;
                }
            }
            else
            {
                UVGlue.JoinBackgroundThreads();
                UVGlue.cleanUp();
            }
        }


        private void btnClear_Click(object sender, EventArgs e)
        {
            listBox1.Items.Clear();
        }

        private void btnRun_Click(object sender, EventArgs e)
        {
            UVGlue.RunOnce();
        }

        private void 模板设置ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (frmModelUp == null || frmModelUp.IsDisposed) frmModelUp = new FormModel();
            frmModelUp.Hide();
            frmModelUp.Show(this);
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            ShowDog(true);
        }


        private void checkBox_saveRawImages_CheckedChanged(object sender, EventArgs e)
        {
            var me = (CheckBox) sender;
            lock (UVGlue.mu_SaveAsScreenShot)
            {
                UVGlue.saveAsScreenShot = !me.Checked;
            }
        }

        private void btnLastImage_Click(object sender, EventArgs e)
        {
            lock (UVGlue.mu_recentNGImagePath)
            {
                if (File.Exists(UVGlue.recentNGIMagePath)) Process.Start(UVGlue.recentNGIMagePath);
            }
        }

        private void btnOpenImageDir_Click(object sender, EventArgs e)
        {
            var imageBaseDir = AppDomain.CurrentDomain.BaseDirectory + "Image";
            var imageDir_today = imageBaseDir + "\\" + DateTime.Now.ToString("MM-dd");
            if (!Directory.Exists(imageDir_today)) Directory.CreateDirectory(imageDir_today);

            Process.Start(imageDir_today);
        }


        private void checkBox_saveOKImages_CheckedChanged(object sender, EventArgs e)
        {
            var me = (CheckBox) sender;
            if (me.Checked)
                lock (UVGlue.mu_resultsThatImagesShouldBeSaved)
                {
                    if (!UVGlue.resultsThatImagesSholdBeSaved.Contains(UVGlue.resultOK))
                        UVGlue.resultsThatImagesSholdBeSaved.Add(UVGlue.resultOK);
                }
            else
                lock (UVGlue.mu_resultsThatImagesShouldBeSaved)
                {
                    if (UVGlue.resultsThatImagesSholdBeSaved.Contains(UVGlue.resultOK))
                        UVGlue.resultsThatImagesSholdBeSaved.Remove(UVGlue.resultOK);
                }
        }
    } //class
} //namespace