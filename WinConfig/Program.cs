using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

using WinConfig.Helpers;

namespace WinConfig
{
	class Program
	{
		public static bool IsAdministrator()
		{
			WindowsIdentity identity = WindowsIdentity.GetCurrent();

			if (identity != null)
			{
				WindowsPrincipal principal = new WindowsPrincipal(identity);
				return principal.IsInRole(WindowsBuiltInRole.Administrator);
			}

			return false;
		}

		static int Main(string[] args)
		{
			StatusCode status;

			if (args.Length < 1)
			{
				Console.WriteLine("usage: WinConfig.exe <config file> [<config section>]");
				Console.ReadKey();
				return 1;
			}

			//if (!IsAdministrator())
			//{
			//	var process = ProcessWrapper.Execute(System.Reflection.Assembly.GetExecutingAssembly().Location, args, verb: "runas");
			//	process.Process.OutputDataReceived += ProcessStdRedirector.StdOutRedirector.DataReceivedHandler;
			//	process.Process.ErrorDataReceived += ProcessStdRedirector.StdErrRedirector.DataReceivedHandler;
			//	process.Process.WaitForExit();
			//	return process.Process.ExitCode;
			//}

			status = ConfigParser.Parse(args[0], out var config);
			if (status.Failed())
			{
				Logger.Error($"Could not parse config file '{args[0]}'");
			}
			else
			{
				status = new CommandDispatcher(config).DispatchCommands();
				if (status.Failed())
					Logger.Error($"could not apply some commands from file '{args[0]}'");
			}

			Console.ReadKey();
			return status.Succeeded() ? 0 : 1;
		}
	}
}
