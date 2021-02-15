using System;
using System.IO;
using System.Security;
using System.Runtime.Remoting.Proxies;
using System.IO.Compression;

using WinConfig.Helpers;
using System.Security.Policy;

using ExternalCommandCallback = System.Func<string, string, string, WinConfig.StatusCode>;

namespace WinConfig
{
	static class FileSystem
	{

		public static StatusCode CopyFile(string source, string target, bool overwrite)
		{
			Logger.CallStart(source, target, overwrite);

			var sourceFull = source.FullPath();
			var targetFull = target.FullPath();
			Logger.Debug($"sourceFull=<{sourceFull}>, targetFull=<{targetFull}>");

			try
			{
				File.Copy(Path.GetFullPath(sourceFull), Path.GetFullPath(targetFull), overwrite);
				Logger.CallSucceeded();
				return StatusCode.Success;
			}
			catch (Exception e)
			{
				Logger.LogException(e);
				return StatusCode.Failure;
			}
		}

		public static StatusCode RemoveFile(string target)
		{
			Logger.CallStart(target);

			var targetFull = target.FullPath();
			Logger.Debug($"targetFull=<{targetFull}>");

			try
			{
				File.Delete(Path.GetFullPath(targetFull));
				Logger.CallSucceeded();
				return StatusCode.Success;
			}
			catch (Exception e)
			{
				Logger.LogException(e);
				return StatusCode.Failure;
			}
		}

		public static StatusCode MoveFile(string source, string target, bool overwrite)
		{
			Logger.CallStart(source,target,overwrite);

			var sourceFull = source.FullPath();
			var targetFull = target.FullPath();
			Logger.Debug($"sourceFull=<{sourceFull}>, targetFull=<{targetFull}>");

			var status = CopyFile(sourceFull, targetFull, overwrite);
			if (status.Succeeded())
				status = RemoveFile(sourceFull);
			if (status.Succeeded())
				Logger.CallSucceeded();
			else
				Logger.CallFailed("copy-remove operation failed");
			return status;
		}

		public static StatusCode MakeDirectory(string target)
		{
			Logger.CallStart(target);

			var targetFull = target.FullPath();

			try
			{
				Directory.CreateDirectory(targetFull);
			}
			catch (Exception e)
			{
				Logger.LogException(e);
				return StatusCode.Failure;
			}
			return StatusCode.Success;
		}

		public static StatusCode CopyDirectory(string source, string target, bool merge, bool overwrite)
		{
			Logger.CallStart(source,target,merge,overwrite);

			var sourceFull = source.FullPath();
			var targetFull = target.FullPath();
			Logger.Debug($"sourceFull=<{sourceFull}>, targetFull=<{targetFull}>");

			DirectoryInfo sourceDir;
			DirectoryInfo targetDir;
			try
			{
				sourceDir = new DirectoryInfo(sourceFull);
				targetDir = new DirectoryInfo(targetFull);

				targetDir.Create();

				if (Directory.GetFileSystemEntries(targetFull).Length != 0 && !merge)
				{
					Logger.CallFailed($"directory not empty; target=<{targetFull}>");
					return StatusCode.DirectoryNotEmptyError;
				}

				foreach (FileInfo fi in sourceDir.GetFiles())
				{
					Logger.Debug($"copying file; source=<{fi.FullName}>, target=<{targetDir.FullName}>");
					var result = CopyFile(fi.FullName, Path.Combine(targetDir.FullName, fi.Name), overwrite);
					if (result != StatusCode.Success)
					{
						Logger.CallFailed($"cannot copy file; source=<{fi.FullName}>, target=<{targetDir.FullName}>");
						return result;
					}
				}

				// Copy each subdirectory using recursion.
				foreach (DirectoryInfo diSourceSubDir in sourceDir.GetDirectories())
				{
					Logger.Debug($"copying directory; source=<{diSourceSubDir.FullName}>, target=<{targetDir.FullName}>");
					DirectoryInfo nextTargetSubDir = targetDir.CreateSubdirectory(diSourceSubDir.Name);
					var result = CopyDirectory(diSourceSubDir.FullName, nextTargetSubDir.FullName, merge, overwrite);
					if (result != StatusCode.Success)
					{
						Logger.Warning($"cannot copy directory; source=<{diSourceSubDir.FullName}>, target=<{targetDir.FullName}>");
						return result;
					}
				}
			}
			catch (Exception e)
			{
				Logger.LogException(e);
				return StatusCode.Failure;
			}

			Logger.CallSucceeded();
			return StatusCode.Success;
		}

		public static StatusCode RemoveDirectory(string target, bool recursive)
		{
			Logger.CallStart(target,recursive);

			var targetFull = target.FullPath();
			Logger.Debug($"targetFull=<{targetFull}>");

			try
			{
				Directory.Delete(targetFull, recursive);
				Logger.CallSucceeded();
				return StatusCode.Success;
			}
			catch (Exception e)
			{
				Logger.LogException(e);
				return StatusCode.Failure;
			}
		}

		public static StatusCode MoveDirectory(string source, string target, bool merge, bool overwrite)
		{
			Logger.CallStart(source,target,merge,overwrite);

			var sourceFull = source.FullPath();
			var targetFull = target.FullPath();
			Logger.Debug($"sourceFull=<{sourceFull}>, targetFull=<{targetFull}>");

			var status = CopyDirectory(sourceFull, targetFull, merge, overwrite);
			if (status.Succeeded())
				status = RemoveDirectory(sourceFull, true);
			if (status.Succeeded())
				Logger.Debug("FileSystem.MoveDirectory call succeeded");
			else
				Logger.Debug("FileSystem.MoveDirectory call failed");
			return status;
		}

		public static StatusCode ExtractZip(string source, string target, bool merge, bool overwrite)
		{
			Logger.CallStart(source,target);

			var sourceFull = source.FullPath();
			var targetFull = target.FullPath();
			Logger.Debug($"sourceFull=<{sourceFull}>, targetFull=<{targetFull}>");

			var path = Path.Combine(Path.GetTempPath(), "WinConfig_" + DateTime.Now);
			try
			{
				ZipFile.ExtractToDirectory(sourceFull, path);
			}
			catch (Exception e)
			{
				Logger.LogException(e);
				return StatusCode.Failure;
			}

			var status = MoveDirectory(path, targetFull, merge, overwrite);
			if (status.Succeeded())
				Logger.CallSucceeded();
			else
				Logger.CallFailed("moving temp directory failed");
			return status;
		}

		public static StatusCode CommandHandler(string command, string args, ExternalCommandCallback cb, Logger log)
		{
			Logger.CallStart(command, args);

			var status = StatusCode.CommandNotFoundError;
			if (command == "CopyFile")
			{
				var (source, target, flag) = args.Split(',');
				var f = int.Parse(flag);
				if (f != 1 && f != 0)
				{
					Logger.CallFailed($"incorrect flag value: {f}");
					return StatusCode.Failure;
				}
				status = CopyFile(source, target, f == 1);
			}
			else if (command == "RemoveFile")
			{
				status = RemoveFile(args);
			}
			else if (command == "MoveFile")
			{
				var (source, target, flag) = args.Split(',');
				var f = int.Parse(flag);
				if (f != 1 && f != 0)
				{
					Logger.CallFailed($"incorrect flag value: {f}");
					return StatusCode.Failure;
				}
				status = MoveFile(source, target, f == 1);
			}
			else if (command == "CopyDir")
			{
				var (source, target, flag) = args.Split(',');
				var f = int.Parse(flag);
				if (f < 0 || f > 3)
				{
					Logger.CallFailed($"incorrect flag value: {f}");
					return StatusCode.Failure;
				}
				status = CopyDirectory(source, target, (f & 2) == 2, (f & 1) == 1);
			}
			else if (command == "MakeDir")
			{
				status = MakeDirectory(args);
			}
			else if (command == "RemoveDir")
			{
				var (target, flag) = args.Split(',');
				var f = int.Parse(flag);
				if (f != 1 && f != 0)
				{
					Logger.CallFailed($"incorrect flag value: {f}");
					return StatusCode.Failure;
				}
				status = RemoveDirectory(target, f == 1);
			}
			else if (command == "MoveDir")
			{
				var (source, target, flag) = args.Split(',');
				var f = int.Parse(flag);
				if (f != 1 && f != 0)
				{
					Logger.CallFailed($"incorrect flag value: {f}");
					return StatusCode.Failure;
				}
				status = MoveDirectory(source, target, (f & 2) == 2, (f & 1) == 1);
			}
			else if (command == "ExtractZip")
			{
				var (source, target, flag) = args.Split(',');
				var f = int.Parse(flag);
				if (f != 1 && f != 0)
				{
					Logger.CallFailed($"incorrect flag value: {f}");
					return StatusCode.Failure;
				}
				status = ExtractZip(source, target, (f & 2) == 2, (f & 1) == 1);
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
