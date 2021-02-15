using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ExternalCommandCallback = System.Func<string, string, string, WinConfig.StatusCode>;

namespace HelloWorldExt
{
    public class HelloWorldExt
    {
        public static WinConfig.StatusCode CommandHandler(string command, string args, ExternalCommandCallback cb, WinConfig.Logger log)
		{
            if (command == "CreateHW")
            {
                var sw = new StreamWriter(args);
                sw.WriteLine("HELLO WORLD!");
                sw.Flush();
                sw.Close();
                return WinConfig.StatusCode.Success;
            }
            else return WinConfig.StatusCode.CommandNotFoundError;
        }
    }
}
