using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace WinConfig
{
	// assume there are no IOExceptions while writing logs
	public sealed class Logger
	{
		private static Logger g_logger = new Logger(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs"), LogLevel.Info);

		public enum LogLevel
		{
			Debug,
			Info,
			Warning,
			Error,
			Always
		}

		private LogLevel m_level;
		private StreamWriter m_file;

		private Logger(string directory, LogLevel logLevel)
		{
			m_level = logLevel;
			var dt = DateTime.Now;
			Directory.CreateDirectory(directory);
			var logFilePath = Path.Combine(directory, $"WinConfig_{dt.Day}.{dt.Month}.{dt.Year}_{dt.Hour}-{dt.Minute}-{dt.Second}_pid" + Process.GetCurrentProcess().Id.ToString("X") + ".log");
			try
			{
				m_file = File.CreateText(logFilePath);
			}
			catch (Exception e)
			{
				m_file = null;
				Console.WriteLine($"Couldn't open log file for writing; logFilePath='{logFilePath}'\n\texception: {e}");
			}
			if (m_file != null)
			{
				m_file.WriteLine("WinConfig log file " + DateTime.Now);
				m_file.WriteLine("Logger configuration: logLevel: {0}", Enum.GetName(typeof(LogLevel), logLevel));
				m_file.WriteLine();
				m_file.AutoFlush = true;
			}
		}

		public static Logger Global => g_logger;

		public static void LogException(Exception ex, int skipFrames = 0)
		{
			Warning($"Exception: {ex.GetType().Name}, message='{ex.Message}', stackTrace:\n{ex.StackTrace}", skipFrames + 1);
		}

		public static void CallStart(params object[] args)
		{
			Debug("call - " + MapCallerArgs(1, args), 1);
		}

		public static void CallSucceeded()
		{
			Debug("call succeeded", 1);
		}

		public static void CallFailed(string message)
		{
			Warning("call failed - " + message, 1);
		}

		public static void Debug(string message, int skipFrames = 0)
		{
			if (g_logger.m_level > LogLevel.Debug) return;
			WriteMessageEx(skipFrames + 1, "DBG", message);
		}

		public static void Info(string message, int skipFrames = 0)
		{
			if (g_logger.m_level > LogLevel.Info) return;
			WriteMessageEx(skipFrames + 1, "INF", message);
		}

		public static void Warning(string message, int skipFrames = 0)
		{
			if (g_logger.m_level > LogLevel.Warning) return;
			WriteMessageEx(skipFrames + 1, "WRN", message);
		}

		public static void Error(string message, int skipFrames = 0)
		{
			if (g_logger.m_level > LogLevel.Error) return;
			WriteMessageEx(skipFrames + 1, "ERR", message);
		}

		public static void Always(string message, int skipFrames = 0)
		{
			if (g_logger.m_level > LogLevel.Error) return;
			WriteMessageEx(skipFrames + 1, "ALW", message);
		}

		//CAUTION: must pass all arguments except out
		//CAUTION: args must be passed in the same order as params in method
		private static string MapCallerArgs(int skipFrames, params object[] args)
		{
			//skip frames above caller of this method
			var method = new StackFrame(skipFrames + 1).GetMethod();
			var parameters = method.GetParameters()
				.Where(parameter => !parameter.IsOut).ToList();
			var par = args.Select((value, i) => $"{parameters[i].Name}=<{value}>");
			return string.Join(", ", par);
		}

		private static void WriteMessageEx(int skipFrames, string severity, string message)
		{
			// skip frames above caller of this method
			var method = new StackFrame(skipFrames + 1).GetMethod();
			var fullMethodName = method.DeclaringType.FullName + "." + method.Name + "()";

			string str = $"[{DateTime.Now}] {severity} : {fullMethodName} : " + message;

			g_logger.m_file?.WriteLine(str);
			Console.WriteLine(str);
		}
	}
}
