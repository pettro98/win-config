using NetFwTypeLib;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace WinConfig.Helpers
{
	class ProcessStdRedirector
	{
		private StringBuilder[] m_strings;

		public ProcessStdRedirector(params StringBuilder[] strings)
		{
			m_strings = strings.ShallowClone();
		}

		public void DataReceivedHandler(object o, DataReceivedEventArgs args) => Array.ForEach(m_strings, str => str.Append(args.Data));
	}

	public class ProcessWrapper : IDisposable
	{
		private Process m_process;
		private StringBuilder m_out = new StringBuilder();
		private StringBuilder m_error = new StringBuilder();
		private StringBuilder m_combined = new StringBuilder();

		private ProcessWrapper()
		{
		}

		public StringBuilder StdOut => m_out;
		public StringBuilder StdErr => m_error;
		public StringBuilder CombinedOutput => m_combined;
		public Process Process => m_process;

		public static ProcessWrapper Execute(string executable, string[] args = null, string pwd = null, IDictionary<string, string> envVars = null, string verb = "")
		{
			string stringArgs = null;
			if (args != null)
				stringArgs = string.Join(" ", args.Select(el => $"\"{el}\""));
			return Execute(executable, stringArgs, pwd, envVars, verb);
		}

		public static ProcessWrapper Execute(string executable, string args = null, string pwd = null, IDictionary<string, string> envVars = null, string verb = "")
		{
			var startInfo = new ProcessStartInfo(executable);
			startInfo.UseShellExecute = false;
			startInfo.WorkingDirectory = pwd ?? Environment.CurrentDirectory;
			startInfo.CreateNoWindow = true;
			startInfo.RedirectStandardOutput = true;
			startInfo.RedirectStandardError = true;
			startInfo.Verb = verb;

			if (args != null)
				startInfo.Arguments = args;

			if (envVars != null)
				startInfo.Environment.Expand(envVars);

			var temp = new ProcessWrapper();
			temp.m_process = Process.Start(startInfo);
			temp.m_process.OutputDataReceived += new ProcessStdRedirector(temp.m_out, temp.m_combined).DataReceivedHandler;
			temp.m_process.ErrorDataReceived += new ProcessStdRedirector(temp.m_error, temp.m_combined).DataReceivedHandler;
			temp.m_process.BeginOutputReadLine();
			temp.m_process.BeginErrorReadLine();

			return temp;
		}

		public void Dispose() => m_process.Dispose();
	}

	static class Extensions
	{
		public static string FullPath(this string path) => Path.GetFullPath(path);

		public static T OrIfNull<T>(this T ths, T obj) => (ths == null) ? obj : ths;

		public static IDictionary<T1, T2> Expand<T1, T2>(this IDictionary<T1, T2> dict, IDictionary<T1, T2> other)
		{
			foreach (var (key, value) in other)
				dict.Add(key, value);
			return dict;
		}

		public static void Deconstruct<T1, T2>(this KeyValuePair<T1, T2> pair, out T1 key, out T2 value)
		{
			key = pair.Key;
			value = pair.Value;
		}

		public static T[] ShallowClone<T>(this T[] arr)
		{
			return (T[])arr.Clone();
		}

		public static bool SearchKVArray<TKey, TVal>(this KeyValuePair<TKey, TVal>[] arr, TKey key, out TVal result)
		{
			foreach (var (k, v) in arr)
			{
				if (k.Equals(key))
				{
					result = v;
					return true;
				}
			}
			result = default(TVal);
			return false;
		}

		public static void Deconstruct<T>(this T[] arr, out T a0, out T a1)
		{
			a0 = arr[0];
			a1 = arr[1];
		}

		public static void Deconstruct<T>(this T[] arr, out T a0, out T a1, out T a2)
		{
			a0 = arr[0];
			a1 = arr[1];
			a2 = arr[2];
		}

		public static void Deconstruct<T>(this T[] arr, out T a0, out T a1, out T a2, out T a3)
		{
			a0 = arr[0];
			a1 = arr[1];
			a2 = arr[2];
			a3 = arr[3];
		}
	}

}
