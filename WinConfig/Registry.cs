using System;
using System.Diagnostics;
using System.Linq;

using Microsoft.Win32;

using WinConfig.Helpers;

using W32Reg = Microsoft.Win32.Registry;
using ExternalCommandCallback = System.Func<string, string, string, WinConfig.StatusCode>;

namespace WinConfig
{
	static class Registry
	{
		// empty string in 'valueName' means "default" value stored in key
		public static StatusCode SetValue(string keyName, string valueName, object value, RegistryValueKind valueType = RegistryValueKind.Unknown)
		{
			Logger.CallStart(keyName, valueName, value, Enum.GetName(typeof(RegistryValueKind), valueType));

			keyName = keyName.Replace("HKCU", "HKEY_CURRENT_USER")
				.Replace("HKLM", "HKEY_LOCAL_MACHINE")
				.Replace("HKCR", "HKEY_CLASSES_ROOT")
				.Replace("HKU", "HKEY_USERS")
				.Replace("HKCC", "HKEY_CURRENT_CONFIG")
				.Replace("HKPD", "HKEY_PERFORMANCE_DATA");


			try
			{
				W32Reg.SetValue(keyName, valueName, value, valueType);
				Logger.CallSucceeded();
				return StatusCode.Success;
			}
			catch (Exception e)
			{
				Logger.LogException(e);
				return StatusCode.Failure;
			}
		}

		public static StatusCode DeleteKey(string keyName, bool recursive)
		{
			Logger.CallStart(keyName, recursive);

			var status = getParentKey(keyName, out var regKey);
			if (status.Failed()) return status;
			var subKeyName = keyName.Substring(keyName.LastIndexOf('\\') + 1);

			try
			{
				using (regKey)
				{
					if (recursive)
						regKey.DeleteSubKeyTree(subKeyName, false);
					else
						regKey.DeleteSubKey(subKeyName, false);
				}

				Logger.CallSucceeded();
				return StatusCode.Success;
			}
			catch (Exception e)
			{
				Logger.LogException(e);
				return StatusCode.Failure;
			}
		}

		public static StatusCode DeleteValue(string keyName, string valueName)
		{
			Logger.CallStart(keyName, valueName);

			var status = getKey(keyName, out var regKey);
			if (status.Failed()) return status;

			try
			{
				using (regKey)
				{
					regKey.DeleteValue(valueName, false);
				}

				Logger.CallSucceeded();
				return StatusCode.Success;
			}
			catch (Exception e)
			{
				Logger.LogException(e);
				return StatusCode.Failure;
			}
		}

		public static StatusCode ApplyRegFile(string path)
		{
			Logger.CallStart(path);

			var fullPath = path.FullPath();
			Logger.Debug($"fullPath=<{fullPath}>'");

			int exitCode;
			string stderr;
			try
			{
				ProcessStartInfo startInfo = new ProcessStartInfo("reg.exe", "/s \"" + fullPath + "\"");
				using (Process regProcess = Process.Start(startInfo))
				{
					regProcess.WaitForExit();
					exitCode = regProcess.ExitCode;
					stderr = regProcess.StandardError.ReadToEnd();
				}
			}
			catch (Exception e)
			{
				Logger.LogException(e);
				return StatusCode.Failure;
			}

			if (exitCode != 0)
			{
				Logger.CallFailed($"reg.exe failed with exitCode={exitCode}; stderr:\n{stderr}");
				return StatusCode.ChildProcessFailedError;
			}

			Logger.CallSucceeded();
			return StatusCode.Success;
		}

		private static StatusCode getKey(string path, out RegistryKey key)
		{
			key = null;
			var pathArray = path.Split('\\').ToList();
			if (pathArray.Count < 1) return StatusCode.KeyInvalidError;

			var name = pathArray[0];
			RegistryKey hive;
			if (name == "HKCU" || name == "HKEY_CURRENT_USER")
				hive = W32Reg.CurrentUser;
			else if (name == "HKLM" || name == "HKEY_LOCAL_MACHINE")
				hive = W32Reg.LocalMachine;
			else if (name == "HKCR" || name == "HKEY_CLASSES_ROOT")
				hive = W32Reg.ClassesRoot;
			else if (name == "HKU" || name == "HKEY_USERS")
				hive = W32Reg.Users;
			else if (name == "HKCC" || name == "HKEY_CURRENT_CONFIG")
				hive = W32Reg.CurrentConfig;
			else if (name == "HKPD" || name == "HKEY_PERFORMANCE_DATA")
				hive = W32Reg.PerformanceData;
			else
				return StatusCode.HiveUnknownError;

			pathArray.RemoveAt(0);
			key = hive.OpenSubKey(string.Join("\\", pathArray), true);
			return StatusCode.Success;
		}

		private static StatusCode getParentKey(string path, out RegistryKey key)
		{
			key = null;
			var pathList = path.Split('\\').ToList();
			if (pathList.Count < 2) return StatusCode.KeyInvalidError;

			pathList.RemoveAt(pathList.Count - 1);
			return getKey(string.Join("\\", pathList), out key);
		}

		public static StatusCode CreateBackup(string key, string file)
		{
			Logger.CallStart(key, file);
			var status = Launcher.LaunchProcess("reg.exe", new string[] { "save", key, file }, Environment.CurrentDirectory);
			if (status.Failed())
				Logger.CallFailed("launch of reg.exe failed");
			else
				Logger.CallSucceeded();
			return status;
		}

		public static StatusCode CommandHandler(string command, string args, ExternalCommandCallback cb, Logger log)
		{
			Logger.CallStart(command, args);

			var status = StatusCode.CommandNotFoundError;
			if (command == "SetValue")
			{
				var (key, name, type, valueStr) = args.Split(',');
				object value;
				bool succ = true;
				if (type == "DWORD")
				{
					int outVal;
					succ = int.TryParse(valueStr, out outVal);
					value = outVal;
				}
				if (type == "QWORD")
				{
					long outVal;
					succ = long.TryParse(valueStr, out outVal);
					value = outVal;
				}
				if (type == "SZ")
					value = valueStr;
				else
				{
					Logger.CallFailed($"unknown reg type: {type}");
					return StatusCode.Failure;
				}
				status = SetValue(key, name, value);
			}
			else if (command == "DeleteKey")
			{
				var (key, flag) = args.Split(',');
				if (flag != "1" && flag != "0")
				{
					Logger.CallFailed($"incorrect flag: {flag}");
					return StatusCode.Failure;
				}
				status = DeleteKey(key, flag == "1");
			}
			else if (command == "DeleteValue")
			{
				var (key, name) = args.Split(',');
				status = DeleteValue(key, name);
			}
			else if (command == "ApplyRegFile")
			{
				status = ApplyRegFile(args);
			}
			else if (command == "CreateBackup")
			{
				var (key, file) = args.Split(',');
				status = CreateBackup(key, file);
			}
			else
				status = cb("General", command, args);

			if (status.Succeeded())
				Logger.CallSucceeded();
			else
				Logger.CallFailed("Could not perform filesystem operation");
			return status;
		}
	}
}
