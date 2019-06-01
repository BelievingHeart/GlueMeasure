using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Windows.Forms;
using Cognex.VisionPro;
using Cognex.VisionPro.Display;
using Cognex.VisionPro.ImageFile;
using Cognex.VisionPro.PMAlign;
using Cognex.VisionPro.ToolBlock;
using Lib_MeasurementUtilities;
using MainAPP.UI;

namespace MainAPP
{
    internal static class UVGlue
    {
        //通信部分
        private const ushort IN_TRIGER = 1;
        private const ushort OUT_OK = 1;
        private const ushort OUT_NG = 2;
        private const ushort OUT_NO_PRODUCT = 3;
        private static int triger1 = 1;
        public static string logFile = "UnhandledExceptions.txt";

        public static AutoResetEvent cond_ReadyToRun = new AutoResetEvent(false);

        //视觉部分
        //public static CogToolGroup tgCheck;
        public static CogToolBlock _block;

        private static readonly string vppDir =
            Directory.GetParent(Directory.GetCurrentDirectory()).Parent.Parent.Parent.FullName + "/VPP";

        public static string vppFilePath = Path.Combine(vppDir, "Template.vpp");


        //
        public static FormMain _formMain;

        public static bool applicationIsRunning;

        public static object mu_alive = new object();

        public static Thread threadListen;
        public static Thread threadExecute;

        private static readonly List<string> _blockOutputNames = new List<string>
        {
            "W5", "W4", "W3", "W2", "W1", "WZ401", "WZ402", "WZ403", "WZ11", "WZ31", "ZhaiShang", "ZhaiXia", "pixel401",
            "pixel402", "pixel403"
        };


        private static readonly string _logDir = AppDomain.CurrentDomain.BaseDirectory + "Log";

        private static readonly List<string> _logTitile = new List<string>
        {
            "时间", "W5", "W4", "W3", "W2", "W1", "WZ401", "WZ402", "WZ403", "WZ11", "WZ31", "ZhaiShang", "ZhaiXia", "结果",
            "序号", "", "pixel401", "pixel402", "pixel403"
        };

        private static readonly DataLogger _dataLogger = new DataLogger(_logTitile, _logDir);


        // image module
        public static object mu_recentNGImagePath = new object();
        public static string recentNGIMagePath = "";
        public static bool saveAsScreenShot = true;
        public static object mu_SaveAsScreenShot = new object();

        // rectifier section
        public static string csvFile;

        // section handle result
        public static string resultOK = "OK", resultNG = "NG", resultNoProduct = "无料", resultImageAcqFailed = "取像失败";
        public static List<string> resultsThatImagesSholdBeSaved;
        public static object mu_resultsThatImagesShouldBeSaved = new object();


        public static void LoadVPP()
        {
            _block = (CogToolBlock) CogSerializer.LoadObjectFromFile(vppFilePath);
        }

        public static void SaveVPP()
        {
            CogSerializer.SaveObjectToFile(_block, vppFilePath, typeof(BinaryFormatter), CogSerializationOptionsConstants.Minimum);
        }

        public static void CloseVPP()
        {
            Misc.CloseCognexCamera(_block);
        }


        public static void Listen()
        {
            var temp1 = 0;
            while (true)
            {
                lock (mu_alive)
                {
                    if (!applicationIsRunning) break;
                }

                Thread.Sleep(3);


                temp1 = IOC0640.ioc_read_inbit(0, IN_TRIGER);

                //工位1
                if (triger1 != temp1)
                {
                    triger1 = temp1;
                    if (triger1 == 0)
                        switch (triger1)
                        {
                            case 0:
                                _formMain.ShowAndSaveMsg_Invoke("收到触发信号");
                                cond_ReadyToRun.Set();
                                break;
                        }
                }
            }
        }

        public static void ListenPos1()
        {
            while (true)
            {
                cond_ReadyToRun.WaitOne();
                lock (mu_alive)
                {
                    if (!applicationIsRunning) break;
                }

                RunOnce();
            }
        }


        public static void RunOnce()
        {
            _block.Run();
            var ImageAcqTool = (CogAcqFifoTool) _block.Tools["CogAcqFifoTool1"];
            _formMain.Invoke((MethodInvoker) (() =>
            {
                _formMain.cogRecordDisplay1.Record =
                    _block.CreateLastRunRecord().SubRecords["CogIPOneImageTool1.OutputImage"];
            }));

            string runResult;
            if (CogToolResultConstants.Accept == _block.RunStatus.Result)
            {
                runResult = resultOK;
            }
            else if (ImageAcqTool.RunStatus.Result != CogToolResultConstants.Accept)
            {
                runResult = resultImageAcqFailed;
            }
            else
            {
                var pma = (CogPMAlignTool) _block.Tools["主定位"];
                runResult = pma.Results.Count == 0 ? resultNoProduct : resultNG;
            }

            // 反馈信号
            SubmitResult(runResult);
            // 将结果显示到屏幕
            DisplayResult_Invoke(runResult);
            // 根据结果判断是否保存图片
            saveImage(_formMain.cogRecordDisplay1, 0, runResult);

            var blockOutputs = DataLogger.ExtractOutputsFromToolBlock(ref _block, _blockOutputNames);
            SaveLog(0, runResult, blockOutputs);
        }


        private static void DisplayResult_Invoke(string result)
        {
            _formMain.ShowAndSaveMsg_Invoke(result);
            var outColor = result == resultOK ? CogColorConstants.Green : CogColorConstants.Red;
            ShowLabel_Invoke(_formMain.cogRecordDisplay1, 30, 1600, 100, outColor, result);
        }

        private static void SubmitResult(string result)
        {
            var outPort = result == resultOK ? OUT_OK :
                result == resultNoProduct ? OUT_NO_PRODUCT :
                OUT_NG;
            IOC0640.ioc_write_outbit(0, outPort, 0);
            Thread.Sleep(100);
            IOC0640.ioc_write_outbit(0, outPort, 1);
        }


        public static void ShowLabel_Invoke(CogRecordDisplay disp, float size, double posX, double posY,
            CogColorConstants color, string text)
        {
            var label = new CogGraphicLabel();
            label.SelectedSpaceName = "#";
            label.Alignment = CogGraphicLabelAlignmentConstants.TopLeft;
            label.Font = new Font("Arial", size);
            label.Color = color;
            label.SetXYText(posX, posY, text);
            _formMain.Invoke((MethodInvoker) (() => { disp.StaticGraphics.Add(label, "label"); }));
        }

        public static void saveImage(CogRecordDisplay image, int index, string result)
        {
            var imageBaseDir = AppDomain.CurrentDomain.BaseDirectory + "Image";
            if (!Directory.Exists(imageBaseDir)) Directory.CreateDirectory(imageBaseDir);


            var imageDir_today = imageBaseDir + "\\" + DateTime.Now.ToString("MM-dd");
            if (!Directory.Exists(imageDir_today)) Directory.CreateDirectory(imageDir_today);

            var imagePath = imageDir_today + "\\" + index + "_" + DateTime.Now.ToString("HHmmss") + ".jpg";
            if (result == resultNG)
                lock (mu_recentNGImagePath)
                {
                    recentNGIMagePath = imagePath;
                }

            if (ShouldImageBeSavedBasedOnResult(result))
                try
                {
                    using (var fileTool = new CogImageFileTool())
                    {
                        Image img;
                        lock (mu_SaveAsScreenShot)
                        {
                            img = image.CreateContentBitmap(saveAsScreenShot
                                ? CogDisplayContentBitmapConstants.Display
                                : CogDisplayContentBitmapConstants.Image);
                        }

                        var bmp = new Bitmap(img);
                        var cogImage = new CogImage24PlanarColor(bmp);
                        fileTool.InputImage = cogImage;
                        lock (mu_recentNGImagePath)
                        {
                            fileTool.Operator.Open(imagePath, CogImageFileModeConstants.Write);
                        }

                        fileTool.Run();
                    }
                }
                catch
                {
                }


            //删除过期文件夹
            removeOutdatedDirs(imageBaseDir);
        }

        private static bool ShouldImageBeSavedBasedOnResult(string result)
        {
            bool ret;
            lock (mu_resultsThatImagesShouldBeSaved)
            {
                ret = resultsThatImagesSholdBeSaved.Contains(result);
            }

            return ret;
        }

        private static void removeOutdatedDirs(string imageBaseDir)
        {
            var dirs = Directory.GetDirectories(imageBaseDir);
            for (var i = 0; i < dirs.Length; i++)
            {
                var dt = Directory.GetCreationTime(dirs[i]);
                var ts = DateTime.Now - dt;
                if (ts.Days > 30) Directory.Delete(dirs[i], true);
            }
        }

        public static void SaveLog(int itemIndex, string runResult, List<double> blockOutputValues)
        {
            //创建文件夹
            if (!Directory.Exists(_logDir)) Directory.CreateDirectory(_logDir);

            var line = DataLogger.FormatOutputLine(runResult, itemIndex, blockOutputValues,
                _blockOutputNames.IndexOf("ZhaiXia"));

            try
            {
                csvFile = _dataLogger.WriteLine(line);
            }
            catch
            {
                MessageBox.Show("请先关闭CCD软件产生的所有文档");
            }

            removeOutdatedFiles(_logDir);
        }

        private static void removeOutdatedFiles(string dir)
        {
            //删除过期文件
            var files = Directory.GetFiles(dir);
            for (var i = 0; i < files.Length; i++)
            {
                var dt = File.GetCreationTime(files[i]);
                var ts = DateTime.Now - dt;
                if (ts.Days > 30) File.Delete(files[i]);
            }
        }

        public static void JoinBackgroundThreads()
        {
            lock (mu_alive)
            {
                applicationIsRunning = false;
            }

            cond_ReadyToRun.Set();
            threadExecute.Join();
            threadListen.Join();
        }

        public static void cleanUp()
        {
            CloseVPP();
            IOC0640.ioc_board_close();
        }
    }
}