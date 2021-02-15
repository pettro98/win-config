using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using WinConfig.Helpers;

using ExternalCommandCallback = System.Func<string, string, string, WinConfig.StatusCode>;

namespace WinConfig
{
	static class Launcher
	{
		[DllImport("advpack.dll", EntryPoint = "LaunchINFSection", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
		private static extern void LaunchINFSection(
			[In] IntPtr hwnd,
			[In] IntPtr ModuleHandle,
			[In, MarshalAs(UnmanagedType.LPWStr)] string CmdLineBuffer,
			int nCmdShow);

		public static StatusCode InstallInf(string path, int flags)
		{
			Logger.CallStart(path, flags);

			var fullPath = path.FullPath();
			Logger.Debug($"fullPath=<{fullPath}>");

			LaunchINFSection(IntPtr.Zero, IntPtr.Zero, $"{fullPath},DefaultInstall,{flags}", 0);
			var err = Marshal.GetLastWin32Error();
			if (err == 0)
			{
				Logger.CallSucceeded();
				return StatusCode.Success;
			}
			else
			{
				Logger.CallFailed($"win32error=<{err}>");
				return StatusCode.Failure;
			}
		}

		public static StatusCode LaunchScript(string path, string pwd)
		{
			Logger.CallStart(path, pwd);

			var fullPath = path.FullPath();
			Logger.Debug($"fullPath=<{fullPath}>");

			var ext = Path.GetExtension(path);
			StatusCode status;
			if (ext == ".cmd" || ext == ".bat")
				status = LaunchProcess("cmd.exe", new[] { "/c", fullPath }, pwd);
			else if (ext == ".ps1")
				status = LaunchProcess("powershell.exe", new[] { "-File", fullPath }, pwd);
			else if (ext == ".js" || ext == ".wsh" || ext == ".vbs")
				status = LaunchProcess("CScript.exe", new[] { "//B", fullPath }, pwd);
			else
				status = StatusCode.UnknownExtensionError;


			if (status.Succeeded())
				Logger.CallSucceeded();
			else
				Logger.CallFailed("launching script file failed");
			return status;
		}

		public static StatusCode LaunchProcess(string executable, string[] args, string pwd)
		{
			Logger.CallStart(executable, args, pwd);
			try
			{
				using (var process = ProcessWrapper.Execute(executable, args, pwd))
				{
					process.Process.WaitForExit();
					if (process.Process.ExitCode != 0)
					{
						Logger.CallFailed($"{process.Process.StartInfo.FileName} failed with exitCode={process.Process.ExitCode};" +
							" combined output:\n" + process.CombinedOutput);
						return StatusCode.ChildProcessFailedError;
					}
					else
					{
						Logger.CallSucceeded();
						return StatusCode.Success;
					}
				}
			}
			catch (Exception e)
			{
				Logger.LogException(e);
				return StatusCode.Failure;
			}
		}

		public static StatusCode LaunchProcess(string executable, string args, string pwd)
		{
			Logger.CallStart(executable, args, pwd);
			try
			{
				using (var process = ProcessWrapper.Execute(executable, args, pwd))
				{
					process.Process.WaitForExit();
					if (process.Process.ExitCode != 0)
					{
						Logger.CallFailed($"{process.Process.StartInfo.FileName} failed with exitCode={process.Process.ExitCode};" +
							" combined output:\n" + process.CombinedOutput);
						return StatusCode.ChildProcessFailedError;
					}
					else
					{
						Logger.Info("output:\n" + process.CombinedOutput);
						Logger.CallSucceeded();
						return StatusCode.Success;
					}
				}
			}
			catch (Exception e)
			{
				Logger.LogException(e);
				return StatusCode.Failure;
			}
		}

		public static StatusCode CommandHandler(string command, string args, ExternalCommandCallback cb, Logger log)
		{
			Logger.CallStart(command, args);

			var status = StatusCode.CommandNotFoundError;
			if (command == "InstallInf")
			{
				var (target, flag) = args.Split(',');
				var f = int.Parse(flag);
				if (!new int[] { 0, 1, 2, 3, 4, 128, 129, 130, 131, 132 }.Contains(f))
				{
					Logger.CallFailed($"incorrect flag value: {f}");
					return StatusCode.Failure;
				}
				status = InstallInf(target, f);
			}
			else if (command == "LaunchScript")
			{
				status = LaunchScript(args, "");
			}
			else if (command == "LaunchProgram")
			{
				var executable = args;
				var arguments = "";
				if (args.IndexOf(",") != -1)
				{
					executable = args.Substring(0, args.IndexOf(","));
					arguments = args.Substring(args.IndexOf(",") + 1);
				}

				status = LaunchProcess(executable, arguments, "");

				if (status.Succeeded())
					Logger.CallSucceeded();
				else
					Logger.CallFailed($"child process <{executable}> failed");
			}
			else
				status = cb("General", command, args);

			if (status.Succeeded())
				Logger.CallSucceeded();
			else
				Logger.CallFailed("Could not perform launcher operation");
			return status;
		}
	}

}
