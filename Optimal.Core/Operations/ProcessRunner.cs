using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Optimal.Core.Operations;

public sealed class ProcessRunner : IProcessRunner
{
	private readonly TimeSpan _timeout;

	public ProcessRunner(TimeSpan? timeout = null)
	{
		_timeout = timeout ?? TimeSpan.FromMinutes(5L);
	}

	public async Task<ProcessResult> RunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
	{
		ProcessStartInfo processStartInfo = new ProcessStartInfo
		{
			FileName = fileName,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true,
			StandardOutputEncoding = Encoding.UTF8,
			StandardErrorEncoding = Encoding.UTF8
		};
		foreach (string argument in arguments)
		{
			processStartInfo.ArgumentList.Add(argument);
		}
		using Process process = new Process
		{
			StartInfo = processStartInfo
		};
		StringBuilder stdout = new StringBuilder();
		StringBuilder stderr = new StringBuilder();
		process.OutputDataReceived += delegate(object _, DataReceivedEventArgs e)
		{
			if (e.Data != null)
			{
				stdout.AppendLine(e.Data);
			}
		};
		process.ErrorDataReceived += delegate(object _, DataReceivedEventArgs e)
		{
			if (e.Data != null)
			{
				stderr.AppendLine(e.Data);
			}
		};
		if (!process.Start())
		{
			throw new InvalidOperationException("Failed to start process '" + fileName + "'.");
		}
		process.BeginOutputReadLine();
		process.BeginErrorReadLine();
		using CancellationTokenSource timeoutSource = new CancellationTokenSource(_timeout);
		using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);
		try
		{
			await process.WaitForExitAsync(linked.Token).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (OperationCanceledException)
		{
			TryKill(process);
			throw;
		}
		return new ProcessResult(process.ExitCode, stdout.ToString(), stderr.ToString());
	}

	private static void TryKill(Process process)
	{
		try
		{
			if (!process.HasExited)
			{
				process.Kill(entireProcessTree: true);
			}
		}
		catch (Exception)
		{
		}
	}
}
