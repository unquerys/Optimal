using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace Optimal.App;

public partial class App : Application
{
	private bool _handlingUnhandledException;

	protected override void OnStartup(StartupEventArgs e)
	{
		WriteStartupTrace("OnStartup entered");
		DispatcherUnhandledException += delegate(object _, DispatcherUnhandledExceptionEventArgs args)
		{
			args.Handled = true;
			HandleFatalStartupError(args.Exception);
		};
		AppDomain.CurrentDomain.UnhandledException += (_, args) =>
		{
			if (args.ExceptionObject is Exception exception)
				WriteStartupLog(exception);
		};
		base.OnStartup(e);
		WriteStartupTrace("WPF base startup completed");
		try
		{
			WriteStartupTrace("Creating MainWindow");
			MainWindow window = new();
			WriteStartupTrace("MainWindow created");
			MainWindow = window;
			WriteStartupTrace("Showing MainWindow");
			window.Show();
			WriteStartupTrace("MainWindow shown");
			window.Activate();
			WriteStartupTrace("MainWindow activated");
		}
		catch (Exception ex)
		{
			HandleFatalStartupError(ex);
		}
	}

	private void HandleFatalStartupError(Exception exception)
	{
		if (_handlingUnhandledException)
		{
			Shutdown(-1);
			return;
		}

		_handlingUnhandledException = true;
		string logPath = WriteStartupLog(exception);
		MessageBox.Show(
			"Optimal could not create its window and will close instead of remaining hidden.\n\n" +
			exception.Message + "\n\nDiagnostic log: " + logPath,
			"Optimal startup failed",
			MessageBoxButton.OK,
			MessageBoxImage.Error);
		Shutdown(-1);
	}

	private static string WriteStartupLog(Exception exception)
	{
		string directory = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
			"Optimal",
			"logs");
		string path = Path.Combine(directory, "startup.log");
		try
		{
			Directory.CreateDirectory(directory);
			File.AppendAllText(path, $"[{DateTimeOffset.Now:O}]\n{exception}\n\n");
		}
		catch
		{
			// Preserve the original startup failure even if diagnostics cannot be written.
		}
		return path;
	}

	[Conditional("DEBUG")]
	private static void WriteStartupTrace(string message)
	{
		string directory = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
			"Optimal",
			"logs");
		try
		{
			Directory.CreateDirectory(directory);
			File.AppendAllText(Path.Combine(directory, "startup-trace.log"), $"[{DateTimeOffset.Now:O}] {message}\n");
		}
		catch
		{
		}
	}
}
