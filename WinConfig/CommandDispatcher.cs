using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Reflection;
using System.IO;

using WinConfig.Helpers;

using SectionDict = System.Collections.Generic.IDictionary<string, System.Collections.Generic.KeyValuePair<string, string>[]>;
using ExternalCommandCallback = System.Func<string, string, string, WinConfig.StatusCode>;
using CommandHandler = System.Func<string, string, System.Func<string, string, string, WinConfig.StatusCode>, WinConfig.Logger, WinConfig.StatusCode>;

namespace WinConfig
{
	class CommandDispatcher
	{
		private static int g_maxVersion = 1;
		private static int g_minVersion = 1;

		private Dictionary<string, CommandHandler> m_commandHandlers = new Dictionary<string, CommandHandler>();
		private SectionDict m_sections;
		private Stack<string> m_dirStack = new Stack<string>();

		public CommandDispatcher(SectionDict sections)
		{
			m_sections = sections;

			m_commandHandlers.Add("General", ExecuteGeneralCommand);
			m_commandHandlers.Add("FileSystem", FileSystem.CommandHandler);
			m_commandHandlers.Add("Launcher", Launcher.CommandHandler);
			m_commandHandlers.Add("Registry", Registry.CommandHandler);
		}

		public StatusCode DispatchCommands(string startSection = "Default")
		{
			Logger.CallStart(startSection);

			if (!m_sections.TryGetValue("Meta", out var metaSection))
			{
				Logger.Error("Cannot find 'Meta' section");
				return StatusCode.Failure;
			}
			if (!metaSection.SearchKVArray("Version", out var version) || int.Parse(version) > g_maxVersion || int.Parse(version) < g_minVersion)
			{
				Logger.Error("Unsupported version number");
				return StatusCode.Failure;
			}

			var status = ExecuteSection(startSection);
			if (status.Succeeded())
				Logger.CallSucceeded();
			else
				Logger.CallFailed($"could not execute starting section '{startSection}'");
			return status;
		}

		private StatusCode ExecCommand(string module, string command, string args)
		{
			Logger.CallStart(module, command, args);

			var status = FindCommandHandler(module, out var handler);
			if (status.Failed())
			{
				Logger.CallFailed($"cannot find module <{module}>");
				return status;
			}

			status = handler(command, args, ExecCommand, Logger.Global);
			if (status.Succeeded())
				Logger.CallSucceeded();
			else
				Logger.CallFailed($"command handler failed");

			return status;
		}

		private StatusCode ExecuteSection(string section)
		{
			Logger.CallStart(section);

			if (!m_sections.TryGetValue(section, out var sectionCommands))
			{
				Logger.CallFailed($"Cannot find section '{section}'");
				return StatusCode.Failure;
			}

			var status = StatusCode.Success;
			var shortSectionName = section;
			var moduleName = "";
			CommandHandler sectionHandler;

			if (section.StartsWith("X."))
				shortSectionName = section.Substring(2);

			if (section == "Default")
			{
				sectionHandler = m_commandHandlers["General"];
			}
			else
			{
				var tmp = shortSectionName.IndexOf('.');
				if (tmp == -1)
				{
					Logger.Error($"Invalid section name '{section}'");
					return StatusCode.Failure;
				}
				moduleName = shortSectionName.Substring(0, shortSectionName.IndexOf('.'));

				status = FindCommandHandler(moduleName, out sectionHandler);
				if (status.Failed())
				{
					Logger.CallFailed($"cannot find command handler for <{section}>");
					return status;
				}
			}

			foreach (var (command, args) in sectionCommands)
			{
				var expandedArgs = Environment.ExpandEnvironmentVariables(args);
				status = sectionHandler(command, expandedArgs, ExecCommand, Logger.Global);

				if (status.Failed())
					break;
			}

			if (status.Succeeded())
				Logger.CallSucceeded();
			else
				Logger.CallFailed($"could not execute section <{section}>");

			return status;
		}

		private StatusCode FindCommandHandler(string moduleName, out CommandHandler handler)
		{
			if (m_commandHandlers.ContainsKey(moduleName))
			{
				handler = m_commandHandlers[moduleName];
				return StatusCode.Success;
			}

			try
			{
				var modulePath = Path.Combine("./plugins", moduleName + ".dll").FullPath();
				var module = Assembly.LoadFrom(modulePath);
				handler = module.GetType(moduleName + "." + moduleName)
					.GetMethod("CommandHandler")
					.CreateDelegate(typeof(CommandHandler)) as CommandHandler;

				m_commandHandlers[moduleName] = handler;
				return StatusCode.Success;
			}
			catch (Exception e)
			{
				handler = null;
				Logger.LogException(e);
				return StatusCode.PluginNotFoundError;
			}
		}

		private StatusCode ExecuteGeneralCommand(string command, string args, ExternalCommandCallback _, Logger logger)
		{
			Logger.CallStart(command, args);

			if (command == "SetEnv")
			{
				var envName = args.Substring(0, args.IndexOf(","));
				var envValue = args.Substring(envName.Length + 1);

				Environment.SetEnvironmentVariable(envName, envValue);

				Logger.CallSucceeded();
				return StatusCode.Success;
			}
			else if (command == "SetPwd")
			{
				try
				{
					Environment.CurrentDirectory = args;
					Logger.CallSucceeded();
					return StatusCode.Success;
				}
				catch (Exception e)
				{
					Logger.LogException(e);
					return StatusCode.Failure;
				}
			}
			else if (command == "PushDir")
			{
				m_dirStack.Push(Environment.CurrentDirectory);
				try
				{
					Environment.CurrentDirectory = args;
					Logger.CallSucceeded();
					return StatusCode.Success;
				}
				catch (Exception e)
				{
					m_dirStack.Pop();
					Logger.LogException(e);
					return StatusCode.Failure;
				}
			}
			else if (command == "PopDir")
			{
				try
				{
					if (m_dirStack.Count != 0)
						Environment.CurrentDirectory = m_dirStack.Pop();
					Logger.CallSucceeded();
					return StatusCode.Success;
				}
				catch (Exception e)
				{
					Logger.LogException(e);
					return StatusCode.Failure;
				}
			}
			else if (command == "LaunchSections")
			{
				var sections = args.Split(',');
				var status = StatusCode.Success;
				foreach (var sec in sections)
				{
					status = ExecuteSection(sec);

					if (status.Failed())
						break;
				}

				if (status.Succeeded())
					Logger.CallSucceeded();
				else
					Logger.CallFailed($"cannot execute one of sections <{args}>");

				return status;
			}
			else if (command == "Echo")
			{
				Logger.Always($"ECHO: {args}");
				return StatusCode.Success;
			}

			return StatusCode.CommandNotFoundError;
		}
	}
}
