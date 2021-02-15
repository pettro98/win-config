using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using StringDict = System.Collections.Generic.Dictionary<string, string>;

namespace WinConfig
{
	class ConfigParser
	{
		public static StatusCode Parse(string file, out IDictionary<string, KeyValuePair<string, string>[]> config)
		{
			config = null;

			var fileStream = new StreamReader(file);
			var fileContents = fileStream.ReadToEnd().Split('\n').Select(str => str.Trim()).Where(str => str.Length != 0).ToArray();

			var matchOptions = RegexOptions.Compiled | RegexOptions.ECMAScript;
			var sectionRegex = new Regex(@"^\[(?<sectionName>.*)\]$", matchOptions);
			var commandRegex = new Regex(@"^(?<command>.+?)=(?<value>.*)$", matchOptions);

			var result = new Dictionary<string, KeyValuePair<string, string>[]>(5);

			string section = null;
			List<KeyValuePair<string, string>> sectionDict = null;
			foreach (var str in fileContents)
			{
				if (str.StartsWith("#")) continue;

				var match = sectionRegex.Match(str);
				if (match.Success)
				{
					if (section != null)
						result.Add(section, sectionDict.ToArray());

					section = match.Groups["sectionName"].Value;
					if (result.ContainsKey(section))
					{
						Logger.Error("section '{}' already exists. Section names must be unique");
						return StatusCode.Failure;
					}

					sectionDict = new List<KeyValuePair<string, string>>();
					continue;
				}
				else if ((match = commandRegex.Match(str)).Success)
				{
					if (section == null)
					{
						Logger.Error("commands before beginning of any section are not allowed");
						return StatusCode.Failure;
					}

					sectionDict.Add(new KeyValuePair<string, string>(match.Groups["command"].Value, match.Groups["value"].Value));
				}
				else
				{
					Logger.Error($"line '{str}' is not a valid configuration string");
					return StatusCode.Failure;
				}
			}

			if (sectionDict.Count != 0)
				result.Add(section, sectionDict.ToArray());

			config = result;

			fileStream.Dispose();

			return StatusCode.Success;
		}
	}
}
