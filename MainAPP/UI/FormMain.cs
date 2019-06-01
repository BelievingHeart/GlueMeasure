
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;
using Cognex.VisionPro;
using Cognex.VisionPro.ToolBlock;
using System.Runtime.InteropServices;
using Lib_MeasurementUtilities;

namespace MainAPP.UI
{
    public partial class FormMain : Form
    {
        FormModel frmModelUp;

        public void ShowAndSaveMsg_Invoke(string msg)
        {
            Action fp = () =>
            {
                    if (listBox1.Items.Count > 300)
                    {
                        listBox1.Items.RemoveAt(0);
                    }
                    string formatStr =  "HH:mm:ss";
                    string longMsg = string.Format("[{0}]", DateTime.Now.ToString(formatStr)) + msg;
                    listBox1.Items.Add(longMsg);
                    listBox1.SelectedIndex = listBox1.Items.Count - 1;

                    //Misc.WriteLog(longMsg);

                };
            if (this.InvokeRequired)
            {
                this.Invoke(fp);
            }
            else
            {
                fp();
            }
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
                    {
                        this.toolStripStatusLabel1.Text = "加密狗状态OK";
                    }
                    else
                    {
                        this.toolStripStatusLabel1.Text = "未识别到加密狗，请联系技术人员！";
                    }
                };
            if (this.InvokeRequired)
            {
                this.Invoke(fp);
            }
            else



            {
                fp();
            }
        }

        private void EnableControls(bool enabled)
        {
            this.menuIOCard.Enabled = enabled;
            this.stationsToolStripMenuItem.Enabled = enabled;
            this.btnRun.Enabled = enabled;
        }


        public FormMain()
        {
            InitializeComponent();

        }
        private void FormMain_Load(object sender, EventArgs e)
        {
            // Enable console
//            AllocConsole();
           // this.EnableControls(false);
            UVGlue.applicationIsRunning = true;
            UVGlue.resultsThatImagesSholdBeSaved = new List<string>(){UVGlue.resultNG, UVGlue.resultNoProduct, UVGlue.resultImageAcqFailed};
   
            int a = IOC0640.ioc_board_init();
            if (a > 0)
            {
                ShowAndSaveMsg_Invoke("IO卡打开成功");
            }
            else
            {
                ShowAndSaveMsg_Invoke("IO卡打开失败");
            }
           
            UVGlue.threadListen = new Thread(UVGlue.Listen);
            UVGlue.threadListen.Start();
            UVGlue.threadExecute = new Thread(UVGlue.ListenPos1);
            UVGlue.threadExecute.Start();

        }


        private void FormMain_FormClosing(object sender, FormClosingEventArgs e)
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


        private void menuLogin_Click(object sender, EventArgs e)
        {
            FormLogin fl = new FormLogin();
            if (DialogResult.OK == fl.ShowDialog())
            {
                EnableControls(true);
            }
        }
        private void menuLogout_Click(object sender, EventArgs e)
        {
            EnableControls(false);
        }
        private void menuWord_Click(object sender, EventArgs e)
        {
            string fileName = Application.StartupPath + @"\Sajet_ATE.doc";
            if (File.Exists(fileName))
            {
                Process word = Process.Start(fileName);
            }
        }
        private void menuAbout_Click(object sender, EventArgs e)
        {
            AboutBox1 ab = new AboutBox1();
            ab.ShowDialog();
        }
        private void btnClear_Click(object sender, EventArgs e)
        {
            this.listBox1.Items.Clear();
        }
        private void btnRun_Click(object sender, EventArgs e)
        {
         
            UVGlue.RunOnce();
        }
        private void 模板设置ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (frmModelUp == null || frmModelUp.IsDisposed)
            {
                frmModelUp = new FormModel();
            }
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
                if (File.Exists(UVGlue.recentNGIMagePath))
                {
                    Process.Start(UVGlue.recentNGIMagePath);
                }
            }
        }

        private void btnOpenImageDir_Click(object sender, EventArgs e)
        {
            string imageBaseDir = AppDomain.CurrentDomain.BaseDirectory + "Image";
            var imageDir_today = imageBaseDir + "\\" + DateTime.Now.ToString("MM-dd");
            if (!Directory.Exists(imageDir_today))
            {
                Directory.CreateDirectory(imageDir_today);
            }

            Process.Start(imageDir_today);

        }




        private void btnReset_Click(object sender, EventArgs e)
        {
            var me = (Button)sender;
            me.Enabled = false;
        }

        private void checkBox_saveOKImages_CheckedChanged(object sender, EventArgs e)
        {
            var me = (CheckBox) sender;
            if (me.Checked)
            {
                lock (UVGlue.mu_resultsThatImagesShouldBeSaved)
                {
                    if (!UVGlue.resultsThatImagesSholdBeSaved.Contains(UVGlue.resultOK))
                    {
                        UVGlue.resultsThatImagesSholdBeSaved.Add(UVGlue.resultOK);
                    }
                }
            }
            else
            {
                lock (UVGlue.mu_resultsThatImagesShouldBeSaved)
                {
                    if (UVGlue.resultsThatImagesSholdBeSaved.Contains(UVGlue.resultOK))
                    {
                        UVGlue.resultsThatImagesSholdBeSaved.Remove(UVGlue.resultOK);
                    }
                }
            }
        }
    }//class
}//namespace