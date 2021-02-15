using System;
using System.Collections.Generic;
using System.Linq;
namespace WinConfig
{
	public enum StatusCode : uint
	{
		Success = 0x00000000,
		Failure = 0x80000000,

		FileSystemErrorsFacility = 0x80010000,
		DirectoryNotEmptyError,

		SystemErrorsFacility = 0x80020000,
		ChildProcessFailedError,
		PluginNotFoundError,
		UnknownExtensionError,

		RegistryErrorsFacility = 0x80030000,
		KeyInvalidError,
		HiveUnknownError,

		CommandErrorsFacility = 0x80040000,
		CommandNotFoundError,
	}

	public static class StatusCodeExtensions
	{
		public static bool Succeeded(this StatusCode code) => ((uint)code & 0x80000000) == 0;
		public static bool Failed(this StatusCode code) => !code.Succeeded();
	}

}
