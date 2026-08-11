using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;

namespace Optimal.App;

public partial class App : Application
{
	private bool _handlingUnhandledException;

	protected override void OnStartup(StartupEventArgs e)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Expected O, but got Unknown
		base.DispatcherUnhandledException += (DispatcherUnhandledExceptionEventHandler)delegate(object _, DispatcherUnhandledExceptionEventArgs args)
		{
			if (_handlingUnhandledException)
			{
				Shutdown(-1);
			}
			else
			{
				_handlingUnhandledException = true;
				MessageBox.Show("Optimal encountered an unexpected error.\n\n" + args.Exception.Message, "Optimal", MessageBoxButton.OK, MessageBoxImage.Hand);
				args.Handled = true;
				_handlingUnhandledException = false;
			}
		};
		base.OnStartup(e);
	}
}