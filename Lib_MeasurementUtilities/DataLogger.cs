using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Cognex.VisionPro.ToolBlock;

namespace Lib_MeasurementUtilities
{
    public class DataLogger
    {
        private List<string> argNames;
        private string title;
        private string _logDir;

        public DataLogger(List<string> argNames, string logDir)
        {
            this.argNames = argNames;
            this._logDir = logDir;
            title = string.Join(",", this.argNames);

        }

        public string WriteLine(string line)
        {
            string logFile = _logDir + "\\" + DateTime.Now.ToString("yyyy-MM-dd") + ".csv";

            if (!File.Exists(logFile))
            {
                using (var ss = new StreamWriter(logFile, true))
                {
                    ss.WriteLine(title);
                    ss.WriteLine(line);
                }
            }
            else
            {
                using (var ss = new StreamWriter(logFile, true))
                {
                    ss.WriteLine(line);
                }
            }

            return logFile;
        }

        public static List<double> ExtractOutputsFromToolBlock(ref CogToolBlock block, List<string> names)
        {
            var ret = new List<double>();
            foreach (var name in names)
            {
                var output = (double) block.Outputs[name].Value;
                ret.Add(output);
            }

            return ret;
        }

        public static string FormatOutputLine(string runResult, int itemIndex, List<double> blockOutputs, int afterWhichToInsertSpace)
        {
            var lineStrings = new List<string>() { DateTime.Now.ToString("HH:mm:ss") };
            for (int i = 0; i < blockOutputs.Count; i++)
            {
                lineStrings.Add(blockOutputs[i].ToString("f3"));
                if (i == afterWhichToInsertSpace)
                {
                    lineStrings.Add(runResult);
                    lineStrings.Add(itemIndex.ToString());
                    lineStrings.Add(string.Empty);
                }
            }

            var line = string.Join(",", lineStrings);

            return line;
        }
    }
}
