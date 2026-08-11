using System;
using Microsoft.Win32;
using Optimal.Core.Manifest;

namespace Optimal.Core.Operations;

public sealed record RegistryPath
{
	public string Hive { get; }

	public RegistryHive RegistryHive { get; }

	public string SubKey { get; }

	public RegistryView View { get; }

	public int ViewBits
	{
		get
		{
			if (View != RegistryView.Registry32)
			{
				return 64;
			}
			return 32;
		}
	}

	public string FullPath => Hive + "\\" + SubKey;

	private RegistryPath(string hive, RegistryHive registryHive, string subKey, RegistryView view)
	{
		Hive = hive;
		RegistryHive = registryHive;
		SubKey = subKey;
		View = view;
	}

	public override string ToString()
	{
		return FullPath;
	}

	public static RegistryPath Parse(string path, int viewBits = 64)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			throw new ManifestValidationException("Registry path is empty.");
		}
		RegistryView view = viewBits switch
		{
			32 => RegistryView.Registry32, 
			64 => RegistryView.Registry64, 
			_ => throw new ManifestValidationException($"Registry view must be 32 or 64, found {viewBits}."), 
		};
		string text = path.Trim().Replace('/', '\\');
		if (text.StartsWith("Registry::", StringComparison.OrdinalIgnoreCase))
		{
			string text2 = text;
			int length = "Registry::".Length;
			text = text2.Substring(length, text2.Length - length);
		}
		int num = text.IndexOf('\\');
		string text3 = ((num < 0) ? text : text.Substring(0, num));
		string text4;
		if (num >= 0)
		{
			string text2 = text;
			int length = num + 1;
			text4 = text2.Substring(length, text2.Length - length);
		}
		else
		{
			text4 = string.Empty;
		}
		text3 = text3.TrimEnd(':');
		(string Canonical, RegistryHive Hive) obj = NormalizeHive(text3) ?? throw new ManifestValidationException($"Unrecognised registry hive '{text3}' in path '{path}'.");
		string item = obj.Canonical;
		RegistryHive item2 = obj.Hive;
		string subKey = text4.Trim('\\');
		return new RegistryPath(item, item2, subKey, view);
	}

	public static RegistryPath FromBackup(RegistryValueBackup backup)
	{
		return Parse(backup.Hive + "\\" + backup.SubKey, backup.View);
	}

	private static (string Canonical, RegistryHive Hive)? NormalizeHive(string token)
	{
		switch (token.ToUpperInvariant())
		{
		case "HKLM":
		case "HKEY_LOCAL_MACHINE":
			return ("HKLM", RegistryHive.LocalMachine);
		case "HKCU":
		case "HKEY_CURRENT_USER":
			return ("HKCU", RegistryHive.CurrentUser);
		case "HKCR":
		case "HKEY_CLASSES_ROOT":
			return ("HKCR", RegistryHive.ClassesRoot);
		case "HKU":
		case "HKEY_USERS":
			return ("HKU", RegistryHive.Users);
		case "HKCC":
		case "HKEY_CURRENT_CONFIG":
			return ("HKCC", RegistryHive.CurrentConfig);
		default:
			return null;
		}
	}

	public RegistryKey OpenBaseKey()
	{
		return RegistryKey.OpenBaseKey(RegistryHive, View);
	}
}
