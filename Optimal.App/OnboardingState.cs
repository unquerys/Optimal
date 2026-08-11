using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Optimal.App;

internal sealed class OnboardingState
{
	private sealed class Settings
	{
		public string? OnboardingKey { get; init; }
	}

	private static readonly string FlowRevision = "stable-v1-onboarding-v2";

	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		WriteIndented = true
	};

	private readonly string _settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Optimal", "ui-settings.json");

	public static string CurrentVersion => Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";

	private static string CurrentInstallKey
	{
		get
		{
			string s = Path.GetFullPath(AppContext.BaseDirectory).ToUpperInvariant();
			byte[] inArray = SHA256.HashData(Encoding.UTF8.GetBytes(s));
			return $"{CurrentVersion}:{FlowRevision}:{Convert.ToHexString(inArray)}";
		}
	}

	public bool ShouldShow()
	{
		try
		{
			if (!File.Exists(_settingsPath))
			{
				return true;
			}
			return !string.Equals(JsonSerializer.Deserialize<Settings>(File.ReadAllText(_settingsPath))?.OnboardingKey, CurrentInstallKey, StringComparison.Ordinal);
		}
		catch
		{
			return true;
		}
	}

	public void MarkComplete()
	{
		Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath));
		string contents = JsonSerializer.Serialize(new Settings
		{
			OnboardingKey = CurrentInstallKey
		}, JsonOptions);
		File.WriteAllText(_settingsPath, contents);
	}
}
