using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using Optimal.Core.Detection;
using Optimal.Core.Execution;
using Optimal.Core.Manifest;
using Optimal.Core.Operations;
using Optimal.Core.Safety;

namespace Optimal.App;

public partial class MainWindow : Window
{
	private const string DiscordInviteUrl = "https://discord.gg/U8KTvCDyuM";

	private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();

	private readonly OptimalPaths _paths = new OptimalPaths();

	private readonly IProcessRunner _process = new ProcessRunner();

	private readonly OperationRegistry _registry;

	private readonly RevertJournal _journal;

	private readonly CleanupService _cleanup = new CleanupService();

	private readonly SystemMetricsService _metrics = new SystemMetricsService();

	private readonly DispatcherTimer _metricsTimer = new DispatcherTimer
	{
		Interval = TimeSpan.FromSeconds(3L)
	};

	private TweakCatalog? _catalog;

	private MachineProfile _profile = MachineProfile.Unknown;

	private ExecutionPlan? _currentPlan;

	private bool _initialized;

	private bool _samplingMetrics;

	private HardwareRecommendation? _gamingRecommendation;

	private UIElement? _reviewOrigin;

	private bool _advancedMode;

	public ObservableCollection<TweakItemViewModel> Tweaks { get; } = new ObservableCollection<TweakItemViewModel>();

	public ObservableCollection<TweakItemViewModel> AppInstallTweaks { get; } = new ObservableCollection<TweakItemViewModel>();

	public ObservableCollection<TweakItemViewModel> DebloatTweaks { get; } = new ObservableCollection<TweakItemViewModel>();

	public ObservableCollection<TweakItemViewModel> GamingTweaks { get; } = new ObservableCollection<TweakItemViewModel>();

	public ObservableCollection<TweakItemViewModel> NetworkTweaks { get; } = new ObservableCollection<TweakItemViewModel>();

	public ICollectionView TweakView { get; }

	public ICollectionView GamingView { get; }

	public ICollectionView NetworkView { get; }

	public ObservableCollection<PlanRow> PlanRows { get; } = new ObservableCollection<PlanRow>();

	public ObservableCollection<HistoryRow> HistoryRows { get; } = new ObservableCollection<HistoryRow>();

	public MainWindow()
	{
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Expected O, but got Unknown
		InitializeComponent();
		_registry = OperationRegistry.CreateDefault(_process);
		_journal = new RevertJournal(_paths, NullLogger<RevertJournal>.Instance);
		TweakView = CollectionViewSource.GetDefaultView(Tweaks);
		TweakView.Filter = FilterTweak;
		GamingView = CollectionViewSource.GetDefaultView(GamingTweaks);
		GamingView.Filter = FilterGamingTweak;
		NetworkView = CollectionViewSource.GetDefaultView(NetworkTweaks);
		NetworkView.Filter = FilterGamingTweak;
		base.DataContext = this;
		_metricsTimer.Tick += async delegate
		{
			await RefreshMetricsAsync();
		};
		base.Closing += delegate
		{
			_metricsTimer.Stop();
			_lifetime.Cancel();
		};
	}

	private async void Window_Loaded(object sender, RoutedEventArgs e)
	{
		if (_initialized)
		{
			return;
		}
		_initialized = true;
		try
		{
			_paths.EnsureCreated();
			string directory = System.IO.Path.Combine(AppContext.BaseDirectory, "manifests");
			ManifestLoader manifestLoader = new ManifestLoader(_registry, NullLogger<ManifestLoader>.Instance);
			MachineProfiler machineProfiler = new MachineProfiler(NullLogger<MachineProfiler>.Instance);
			Progress<ProbeProgress> progress = new Progress<ProbeProgress>(delegate(ProbeProgress probeProgress)
			{
				LoadingText.Text = probeProgress.Stage;
				SidebarMachineText.Text = probeProgress.Stage;
			});
			Task<TweakCatalog> catalogTask = manifestLoader.LoadDirectoryAsync(directory, _lifetime.Token);
			Task<MachineProfile> profileTask = machineProfiler.ProfileAsync(progress, _lifetime.Token);
			await Task.WhenAll(catalogTask, profileTask);
			_catalog = await catalogTask;
			_profile = await profileTask;
			foreach (TweakDefinition tweak in _catalog.Tweaks)
			{
				TweakItemViewModel item = new TweakItemViewModel(tweak, UpdateSelection);
				Tweaks.Add(item);
				if (tweak.Category == TweakCategory.Apps)
				{
					AppInstallTweaks.Add(item);
				}
				if (tweak.Category == TweakCategory.Debloat)
				{
					DebloatTweaks.Add(item);
				}
				TweakCategory category = tweak.Category;
				if ((uint)category <= 1u)
				{
					GamingTweaks.Add(item);
				}
				if (tweak.Category == TweakCategory.Network)
				{
					NetworkTweaks.Add(item);
				}
			}
			CategoryFilter.Items.Add("All categories");
			foreach (string item2 in (from t in Tweaks.Where(delegate(TweakItemViewModel t)
				{
					bool flag;
					switch (t.Definition.Category)
					{
					case TweakCategory.Gaming:
					case TweakCategory.Power:
					case TweakCategory.Debloat:
					case TweakCategory.Apps:
					case TweakCategory.Network:
						flag = true;
						break;
					default:
						flag = false;
						break;
					}
					return !flag;
				})
				select t.Category).Distinct().Order<string>(StringComparer.OrdinalIgnoreCase))
			{
				CategoryFilter.Items.Add(item2);
			}
			CategoryFilter.SelectedIndex = 0;
			PopulateMachineProfile();
			PopulateDashboard();
			await RefreshMetricsAsync();
			_metricsTimer.Start();
			RefreshNetworkDiagnostics();
			UpdateSelection();
			await RefreshHistoryAsync();
			UpdateCatalogSummary();
			EngineDot.Fill = (Brush)FindResource("SuccessBrush");
			EngineStatusText.Text = "ENGINE READY";
			LoadingVeil.Visibility = Visibility.Collapsed;
			AnimateIn(HomePage);
			bool forceOnboarding = Environment.GetCommandLineArgs().Any(argument =>
				argument.Equals("--onboarding", StringComparison.OrdinalIgnoreCase));
			ShowOnboarding(forceOnboarding);
		}
		catch (OperationCanceledException)
		{
			Close();
		}
		catch (Exception ex2)
		{
			LoadingText.Text = "Optimal could not start";
			EngineDot.Fill = (Brush)FindResource("DangerBrush");
			EngineStatusText.Text = "STARTUP FAILED";
			MessageBox.Show("Optimal could not initialize.\n\n" + ex2.Message, "Startup failed", MessageBoxButton.OK, MessageBoxImage.Hand);
		}
	}

	private void ShowOnboarding(bool force)
	{
		OnboardingState onboardingState = new OnboardingState();
		if (!force && !onboardingState.ShouldShow())
		{
			return;
		}

		OnboardingWindow onboardingWindow = new OnboardingWindow(_profile, Tweaks)
		{
			Owner = this
		};
		if (onboardingWindow.ShowDialog() != true)
		{
			return;
		}

		if (!onboardingWindow.ManualMode)
		{
			foreach (TweakItemViewModel tweak in Tweaks)
			{
				tweak.IsSelected = false;
			}

			HashSet<string> selectedIds = onboardingWindow.SelectedIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
			foreach (TweakItemViewModel tweak in Tweaks)
			{
				tweak.IsSelected = selectedIds.Contains(tweak.Id);
			}

			UpdateSelection();
			ShowPage(onboardingWindow.SelectedProfile == "gaming" ? GamingPage : OptimizePage);
		}
		else
		{
			ShowPage(HomePage);
		}

		onboardingState.MarkComplete();
	}

	private void ReplayOnboarding_Click(object sender, RoutedEventArgs e)
	{
		ShowOnboarding(force: true);
	}

	private void PopulateMachineProfile()
	{
		HeroOsText.Text = $"{_profile.OsName} · build {_profile.Build}";
		HeroHardwareText.Text = $"{_profile.DeviceKind} · {_profile.RamGigabytes} GB · {_profile.SystemDriveKind}";
		SidebarMachineText.Text = $"{_profile.Edition} · {_profile.DeviceKind}\n{_profile.CpuLogicalCores} threads · {_profile.RamGigabytes} GB RAM";
		SystemOsText.Text = $"{_profile.OsName} {_profile.Edition}\nBuild {_profile.Build} · {_profile.DisplayVersion}";
		SystemCpuText.Text = $"{_profile.CpuName}\n{_profile.CpuPhysicalCores} cores · {_profile.CpuLogicalCores} threads";
		SystemGpuText.Text = $"{_profile.GpuName}\n{_profile.GpuVendor}";
		SystemMemoryText.Text = $"{_profile.RamGigabytes} GB RAM\n{_profile.SystemDriveKind} system drive";
	}

	private void PopulateDashboard()
	{
		OpportunityText.Text = ((_catalog == null) ? "Catalog unavailable" : $"{_catalog.Tweaks.Count} compatible controls ready to review");
		_gamingRecommendation = HardwareAdvisor.Recommend(_profile);
		HardwareProfileTitle.Text = (_gamingRecommendation.CanApplyAutomatically ? ("Recommended: " + _gamingRecommendation.Name) : _gamingRecommendation.Name);
		HardwareProfileText.Text = _gamingRecommendation.Rationale;
		GamingRecommendationTitle.Text = _gamingRecommendation.Name;
		GamingRecommendationConfidence.Text = _gamingRecommendation.Confidence;
		GamingRecommendationText.Text = _gamingRecommendation.Rationale;
		RecommendedGamingButton.IsEnabled = _gamingRecommendation.CanApplyAutomatically;
		RecommendedGamingButton.Content = (_gamingRecommendation.CanApplyAutomatically ? "Select recommendation" : "No NVIDIA profile");
		WindhawkButton.Content = ((FindWindhawkExecutable() == null) ? "Get Windhawk" : "Open Windhawk");
	}

	private async Task RefreshMetricsAsync()
	{
		if (_samplingMetrics || _lifetime.IsCancellationRequested)
		{
			return;
		}
		_samplingMetrics = true;
		try
		{
			SystemMetricSample systemMetricSample = await _metrics.SampleAsync(_lifetime.Token);
			CpuGauge.Percentage = systemMetricSample.CpuPercent;
			CpuGauge.ValueText = $"{systemMetricSample.CpuPercent:0}%";
			CpuMetricDetail.Text = $"{_profile.CpuPhysicalCores} cores · {_profile.CpuLogicalCores} threads";
			MemoryGauge.Percentage = systemMetricSample.MemoryPercent;
			MemoryGauge.ValueText = $"{systemMetricSample.MemoryPercent:0}%";
			MemoryMetricDetail.Text = $"{_profile.RamGigabytes} GB installed";
			StorageGauge.Percentage = systemMetricSample.StoragePercent;
			StorageGauge.ValueText = $"{systemMetricSample.StoragePercent:0}%";
			StorageMetricDetail.Text = $"{_profile.SystemDriveKind} · {systemMetricSample.StorageText.Split('·')[0].Trim()}";
			GpuGauge.Percentage = systemMetricSample.GpuPercent.GetValueOrDefault();
			RadialMetric gpuGauge = GpuGauge;
			double? gpuPercent = systemMetricSample.GpuPercent;
			object valueText;
			if (gpuPercent.HasValue)
			{
				double valueOrDefault = gpuPercent.GetValueOrDefault();
				valueText = $"{valueOrDefault:0}%";
			}
			else
			{
				valueText = "READY";
			}
			gpuGauge.ValueText = (string)valueText;
			TextBlock gpuMetricDetail = GpuMetricDetail;
			gpuPercent = systemMetricSample.GpuTemperature;
			object text;
			if (gpuPercent.HasValue)
			{
				double valueOrDefault2 = gpuPercent.GetValueOrDefault();
				text = $"{valueOrDefault2:0}°C · {_profile.GpuName}";
			}
			else
			{
				text = _profile.GpuName;
			}
			gpuMetricDetail.Text = (string)text;
			TextBlock temperatureText = TemperatureText;
			gpuPercent = systemMetricSample.GpuTemperature;
			object text2;
			if (gpuPercent.HasValue)
			{
				double valueOrDefault3 = gpuPercent.GetValueOrDefault();
				text2 = $"GPU TEMPERATURE · {valueOrDefault3:0}°C   |   DISK ACTIVITY · {systemMetricSample.DiskActivityPercent:0}%";
			}
			else
			{
				text2 = $"GPU TEMPERATURE · sensor unavailable   |   DISK ACTIVITY · {systemMetricSample.DiskActivityPercent:0}%";
			}
			temperatureText.Text = (string)text2;
		}
		catch (OperationCanceledException)
		{
		}
		finally
		{
			_samplingMetrics = false;
		}
	}

	private async void RefreshNetworkDiagnostics()
	{
		_ = 1;
		try
		{
			NetworkInterface networkInterface = (from n in NetworkInterface.GetAllNetworkInterfaces()
				where n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback && n.GetIPProperties().GatewayAddresses.Count > 0
				orderby n.Speed descending
				select n).FirstOrDefault();
			if (networkInterface == null)
			{
				NetworkAdapterText.Text = "No active internet adapter";
				DnsServerText.Text = " - ";
				LinkSpeedText.Text = "Disconnected";
				return;
			}
			IPInterfaceProperties iPProperties = networkInterface.GetIPProperties();
			string[] dns = iPProperties.DnsAddresses.Select((IPAddress address) => address.ToString()).Take(3).ToArray();
			NetworkAdapterText.Text = $"{networkInterface.Name}\n{networkInterface.NetworkInterfaceType}";
			DnsServerText.Text = ((dns.Length == 0) ? "Automatic / unavailable" : string.Join("\n", dns));
			LinkSpeedText.Text = ((networkInterface.Speed > 0) ? $"{(double)networkInterface.Speed / 1000000.0:0} Mbps" : "Speed unavailable");
			UnicastIPAddressInformation unicastIPAddressInformation = iPProperties.UnicastAddresses.FirstOrDefault((UnicastIPAddressInformation address) => address.Address.AddressFamily == AddressFamily.InterNetwork);
			IPAddress iPAddress = iPProperties.GatewayAddresses.FirstOrDefault((GatewayIPAddressInformation address) => address.Address.AddressFamily == AddressFamily.InterNetwork)?.Address;
			IPv4InterfaceProperties iPv4Properties = iPProperties.GetIPv4Properties();
			IpAddressText.Text = unicastIPAddressInformation?.Address.ToString() ?? "IPv4 unavailable";
			GatewayText.Text = iPAddress?.ToString() ?? "Gateway unavailable";
			NetworkModeText.Text = $"{(iPv4Properties.IsDhcpEnabled ? "DHCP" : "Static")} · MTU {iPv4Properties.Mtu}";
			PingReply pingReply = ((iPAddress != null) ? (await MeasurePingAsync(iPAddress.ToString())) : null);
			PingReply gatewayResult = pingReply;
			PingReply reply = await MeasurePingAsync("1.1.1.1");
			NetworkHealthText.Text = $"Gateway: {FormatPing(gatewayResult)}   ·   Internet: {FormatPing(reply)}   ·   DNS servers: {dns.Length}";
		}
		catch (NetworkInformationException)
		{
			NetworkAdapterText.Text = "Diagnostics unavailable";
			DnsServerText.Text = " - ";
			LinkSpeedText.Text = " - ";
		}
	}

	private static async Task<PingReply?> MeasurePingAsync(string host)
	{
		try
		{
			using Ping ping = new Ping();
			return await ping.SendPingAsync(host, TimeSpan.FromSeconds(2L));
		}
		catch (PingException)
		{
			return null;
		}
	}

	private static string FormatPing(PingReply? reply)
	{
		if (reply == null || reply.Status != IPStatus.Success)
		{
			return "ICMP unavailable";
		}
		return $"{reply.RoundtripTime} ms";
	}

	private bool FilterTweak(object item)
	{
		if (!(item is TweakItemViewModel tweakItemViewModel))
		{
			return false;
		}
		bool flag;
		switch (tweakItemViewModel.Definition.Category)
		{
		case TweakCategory.Gaming:
		case TweakCategory.Power:
		case TweakCategory.Debloat:
		case TweakCategory.Apps:
		case TweakCategory.Network:
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (flag)
		{
			return false;
		}
		if (!_advancedMode && tweakItemViewModel.IsAdvanced)
		{
			return false;
		}
		string text = CategoryFilter?.SelectedItem as string;
		if (!string.IsNullOrWhiteSpace(text) && text != "All categories" && !tweakItemViewModel.Category.Equals(text, StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		string value = SearchBox?.Text?.Trim();
		if (!string.IsNullOrWhiteSpace(value) && !tweakItemViewModel.Name.Contains(value, StringComparison.OrdinalIgnoreCase) && !tweakItemViewModel.Description.Contains(value, StringComparison.OrdinalIgnoreCase))
		{
			return tweakItemViewModel.Category.Contains(value, StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	private bool FilterGamingTweak(object item)
	{
		if (item is TweakItemViewModel tweakItemViewModel)
		{
			if (!_advancedMode)
			{
				return !tweakItemViewModel.IsAdvanced;
			}
			return true;
		}
		return false;
	}

	private void CatalogMode_Click(object sender, RoutedEventArgs e)
	{
		if (sender is Button { Tag: string tag })
		{
			_advancedMode = tag.Equals("advanced", StringComparison.OrdinalIgnoreCase);
			SimpleModeButton.Style = (Style)FindResource(_advancedMode ? "GhostButton" : "SecondaryButton");
			AdvancedModeButton.Style = (Style)FindResource(_advancedMode ? "SecondaryButton" : "GhostButton");
			TweakView.Refresh();
			GamingView.Refresh();
			NetworkView.Refresh();
			UpdateCatalogSummary();
		}
	}

	private void UpdateCatalogSummary()
	{
		if (CatalogSummaryText != null)
		{
			int value = Tweaks.Count((TweakItemViewModel t) => _advancedMode || !t.IsAdvanced);
			int value2 = Tweaks.Count((TweakItemViewModel t) => t.IsAdvanced);
			CatalogSummaryText.Text = (_advancedMode ? $"Advanced catalog · {value} validated controls · full tradeoffs visible" : $"Simple catalog · {value} clear controls · {value2} expert controls hidden");
		}
	}

	private void UpdateSelection()
	{
		int num = Tweaks.Count((TweakItemViewModel t) => t.IsSelected);
		SelectionText.Text = ((num == 1) ? "1 tweak selected" : $"{num} tweaks selected");
		int num2 = GamingTweaks.Count((TweakItemViewModel t) => t.IsSelected);
		GamingSelectionText.Text = ((num2 == 1) ? "1 gaming control selected" : $"{num2} gaming controls selected");
		ReviewButton.IsEnabled = num > 0;
	}

	private async void Review_Click(object sender, RoutedEventArgs e)
	{
		if (_catalog == null)
		{
			return;
		}
		List<TweakDefinition> list = (from t in Tweaks
			where t.IsSelected
			select t.Definition).ToList();
		if (list.Count == 0)
		{
			return;
		}
		_reviewOrigin = ((GamingPage.Visibility == Visibility.Visible) ? GamingPage : ((AppsPage.Visibility == Visibility.Visible) ? AppsPage : OptimizePage));
		ReviewButton.IsEnabled = false;
		ReviewButton.Content = "Scanning current state…";
		try
		{
			OperationContext context = new OperationContext
			{
				Logger = NullLogger.Instance,
				Process = _process,
				DryRun = true
			};
			_currentPlan = await new ExecutionPlanner(_registry, NullLogger<ExecutionPlanner>.Instance).PlanAsync(list, _profile, context, _lifetime.Token);
			PlanRows.Clear();
			foreach (PlannedTweak planned in _currentPlan.Tweaks)
			{
				string status = (planned.AlreadyApplied ? "NO CHANGE" : planned.CurrentState.ToString().ToUpperInvariant());
				Brush statusBrush = (planned.AlreadyApplied ? ((Brush)FindResource("SuccessBrush")) : ((Brush)FindResource("AccentBrush")));
				string detail = string.Join("  ·  ", planned.Descriptions);
				PlanRows.Add(new PlanRow(planned.Tweak.Name, detail, status, statusBrush));
				Tweaks.First((TweakItemViewModel t) => t.Id.Equals(planned.Tweak.Id, StringComparison.OrdinalIgnoreCase)).SetState(planned.CurrentState);
			}
			foreach (ExcludedTweak item in _currentPlan.Excluded)
			{
				PlanRows.Add(new PlanRow(item.Tweak.Name, item.Explanation, "EXCLUDED", (Brush)FindResource("WarningBrush")));
			}
			ReviewSummaryText.Text = $"{_currentPlan.ChangeCount} changes ready · {_currentPlan.Excluded.Count} excluded · " + (_currentPlan.RebootRequired ? "restart required" : "no restart expected");
			ShowPage(ReviewPage);
		}
		catch (Exception ex) when (!(ex is OperationCanceledException))
		{
			MessageBox.Show(ex.Message, "Could not build plan", MessageBoxButton.OK, MessageBoxImage.Exclamation);
		}
		finally
		{
			ReviewButton.Content = "Review plan  →";
			ReviewButton.IsEnabled = Tweaks.Any((TweakItemViewModel t) => t.IsSelected);
		}
	}

	private void BackToTweaks_Click(object sender, RoutedEventArgs e)
	{
		ShowPage(_reviewOrigin ?? OptimizePage);
	}

	private async void DryRun_Click(object sender, RoutedEventArgs e)
	{
		await ExecuteCurrentPlanAsync(dryRun: true);
	}

	private async void Apply_Click(object sender, RoutedEventArgs e)
	{
		if ((object)_currentPlan == null || _currentPlan.ChangeCount == 0)
		{
			MessageBox.Show("The selected tweaks already match this PC.", "Nothing to apply");
		}
		else if (MessageBox.Show((_currentPlan.HasAggressiveTweaks ? "This plan includes aggressive tweaks with meaningful tradeoffs.\n\n" : string.Empty) + $"Apply {_currentPlan.ChangeCount} changes to this PC now?", "Confirm changes", MessageBoxButton.YesNo, MessageBoxImage.Exclamation, MessageBoxResult.No) == MessageBoxResult.Yes)
		{
			await ExecuteCurrentPlanAsync(dryRun: false);
		}
	}

	private async Task ExecuteCurrentPlanAsync(bool dryRun)
	{
		if ((object)_currentPlan == null)
		{
			return;
		}
		ShowPage(ExecutionPage);
		ExecutionActions.Visibility = Visibility.Collapsed;
		RestartNotice.Visibility = Visibility.Collapsed;
		ExecutionLog.Text = string.Empty;
		ExecutionProgressBar.Value = 0.0;
		ExecutionTitle.Text = (dryRun ? "Running a safe preview" : "Applying your plan");
		ExecutionMessage.Text = (dryRun ? "No system values will be written." : "Optimal is capturing state before every change.");
		Progress<ExecutionProgress> progress = new Progress<ExecutionProgress>(delegate(ExecutionProgress update)
		{
			ExecutionMessage.Text = update.Message ?? update.Phase.ToString();
			ExecutionProgressBar.Value = ((update.Total == 0) ? 4.0 : (update.Fraction * 100.0));
			if (!string.IsNullOrWhiteSpace(update.Message))
			{
				ExecutionLog.Text += $"{DateTime.Now:HH:mm:ss}  {update.Message}\n";
			}
		});
		try
		{
			RunRecord runRecord = await CreateRunner().ApplyAsync(_currentPlan, new RunOptions
			{
				DryRun = dryRun,
				BackupRegistry = (RegistryBackupCheck.IsChecked == true),
				PresetName = "Desktop selection"
			}, progress, _lifetime.Token);
			ExecutionProgressBar.Value = 100.0;
			ExecutionTitle.Text = ((runRecord.FailedCount != 0) ? "Completed with warnings" : (dryRun ? "Preview complete" : "Optimization complete"));
			ExecutionMessage.Text = $"{runRecord.AppliedCount} applied · {runRecord.SkippedCount} skipped · {runRecord.FailedCount} failed";
			ExecutionActions.Visibility = Visibility.Visible;
			RestartNotice.Visibility = ((dryRun || runRecord.FailedCount != 0 || !_currentPlan.RebootRequired) ? Visibility.Collapsed : Visibility.Visible);
			await RefreshHistoryAsync();
		}
		catch (Exception ex) when (!(ex is OperationCanceledException))
		{
			ExecutionTitle.Text = "Run stopped";
			ExecutionMessage.Text = ex.Message;
			ExecutionLog.Text += $"ERROR  {ex}\n";
			ExecutionActions.Visibility = Visibility.Visible;
		}
	}

	private void RestartNow_Click(object sender, RoutedEventArgs e)
	{
		if (MessageBox.Show("Save all open work before restarting. Restart Windows now?", "Restart required", MessageBoxButton.YesNo, MessageBoxImage.Exclamation, MessageBoxResult.No) == MessageBoxResult.Yes)
		{
			Process.Start(new ProcessStartInfo("shutdown.exe", "/r /t 0")
			{
				UseShellExecute = false
			});
		}
	}

	private ExecutionRunner CreateRunner()
	{
		return new ExecutionRunner(_registry, _journal, new RegistryBackupService(_process, NullLogger<RegistryBackupService>.Instance), new SystemRestorePointService(NullLogger<SystemRestorePointService>.Instance), _paths, _process, NullLogger<ExecutionRunner>.Instance);
	}

	private async Task RefreshHistoryAsync()
	{
		IReadOnlyList<RunRecord> obj = await _journal.ListAsync(50, _lifetime.Token);
		HistoryRows.Clear();
		foreach (RunRecord item in obj)
		{
			HistoryRows.Add(new HistoryRow(item));
		}
	}

	private async void UndoRun_Click(object sender, RoutedEventArgs e)
	{
		if (_catalog == null || !(sender is Button { Tag: RunRecord tag }) || MessageBox.Show("Restore the state captured before run " + tag.RunId + "?", "Undo run", MessageBoxButton.YesNo, MessageBoxImage.Exclamation, MessageBoxResult.No) != MessageBoxResult.Yes)
		{
			return;
		}
		ShowPage(ExecutionPage);
		ExecutionActions.Visibility = Visibility.Collapsed;
		ExecutionTitle.Text = "Undoing the selected run";
		ExecutionMessage.Text = "Restoring captured state in reverse order.";
		ExecutionLog.Text = string.Empty;
		Progress<ExecutionProgress> progress = new Progress<ExecutionProgress>(delegate(ExecutionProgress update)
		{
			ExecutionMessage.Text = update.Message ?? "Restoring…";
			ExecutionProgressBar.Value = ((update.Total == 0) ? 0.0 : (update.Fraction * 100.0));
			if (!string.IsNullOrWhiteSpace(update.Message))
			{
				ExecutionLog.Text += $"{DateTime.Now:HH:mm:ss}  {update.Message}\n";
			}
		});
		try
		{
			RunRecord runRecord = await CreateRunner().RevertAsync(tag, _catalog, progress, _lifetime.Token);
			ExecutionProgressBar.Value = 100.0;
			ExecutionTitle.Text = ((runRecord.FailedCount == 0) ? "Run successfully undone" : "Undo completed with warnings");
			ExecutionMessage.Text = $"{runRecord.AppliedCount} restored · {runRecord.FailedCount} failed";
			await RefreshHistoryAsync();
		}
		catch (Exception ex) when (!(ex is OperationCanceledException))
		{
			ExecutionTitle.Text = "Undo stopped";
			ExecutionMessage.Text = ex.Message;
		}
		ExecutionActions.Visibility = Visibility.Visible;
	}

	private void Recommended_Click(object sender, RoutedEventArgs e)
	{
		foreach (TweakItemViewModel tweak in Tweaks)
		{
			tweak.IsSelected = tweak.Definition.Tier == TweakTier.Verified && (_advancedMode || !tweak.IsAdvanced);
		}
	}

	private void Preset_Click(object sender, RoutedEventArgs e)
	{
		if (!(sender is Button { Tag: string tag }))
		{
			return;
		}
		foreach (TweakItemViewModel tweak in Tweaks)
		{
			tweak.IsSelected = false;
		}
		foreach (TweakItemViewModel tweak2 in Tweaks)
		{
			if (!_advancedMode && tweak2.IsAdvanced)
			{
				continue;
			}
			TweakItemViewModel tweakItemViewModel = tweak2;
			bool isSelected;
			switch (tag.ToLowerInvariant())
			{
			case "minimal":
			{
				bool flag = tweak2.Definition.Tier == TweakTier.Verified;
				if (flag)
				{
					TweakCategory category = tweak2.Definition.Category;
					bool flag2 = ((category == TweakCategory.Power || category == TweakCategory.Apps) ? true : false);
					flag = !flag2;
				}
				isSelected = flag;
				break;
			}
			case "standard":
			{
				bool flag = tweak2.Definition.Tier == TweakTier.Verified;
				if (!flag)
				{
					bool flag2;
					switch (tweak2.Id)
					{
					case "privacy.windows-suggestions.disable":
					case "explorer.search-highlights.disable":
					case "gaming.game-capture.disable":
						flag2 = true;
						break;
					default:
						flag2 = false;
						break;
					}
					flag = flag2;
				}
				isSelected = flag;
				break;
			}
			case "privacy":
				isSelected = tweak2.Definition.Category == TweakCategory.Privacy;
				break;
			case "performance":
			{
				TweakCategory category = tweak2.Definition.Category;
				bool flag = (uint)category <= 1u;
				isSelected = flag;
				break;
			}
			case "debloat":
				isSelected = tweak2.Definition.Category == TweakCategory.Debloat;
				break;
			case "apps":
			{
				bool flag;
				switch (tweak2.Id)
				{
				case "apps.brave.install":
				case "apps.librewolf.install":
				case "apps.latencymon.install":
				case "apps.gpuz.install":
					flag = true;
					break;
				default:
					flag = false;
					break;
				}
				isSelected = flag;
				break;
			}
			default:
				isSelected = false;
				break;
			}
			tweakItemViewModel.IsSelected = isSelected;
		}
		UpdateSelection();
	}

	private void SelectTweak_Click(object sender, RoutedEventArgs e)
	{
		if (!(sender is Button { Tag: var tag }))
		{
			return;
		}
		string id = tag as string;
		if (id == null)
		{
			return;
		}
		TweakItemViewModel tweakItemViewModel = Tweaks.FirstOrDefault((TweakItemViewModel item) => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
		if (tweakItemViewModel == null)
		{
			MessageBox.Show("This feature is not compatible with the detected hardware or Windows build.", "Not available", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			return;
		}
		if (id.StartsWith("gaming.nvidia.", StringComparison.OrdinalIgnoreCase))
		{
			TweakItemViewModel tweakItemViewModel2 = Tweaks.FirstOrDefault((TweakItemViewModel item) => item.Id == "apps.nvidia-profile-inspector.install");
			if (tweakItemViewModel2 != null)
			{
				tweakItemViewModel2.IsSelected = true;
			}
		}
		tweakItemViewModel.IsSelected = true;
		TweakCategory category = tweakItemViewModel.Definition.Category;
		if ((uint)category <= 1u)
		{
			ShowPage(GamingPage);
			return;
		}
		ShowPage(OptimizePage);
		SearchBox.Text = tweakItemViewModel.Name;
		TweakView.Refresh();
	}

	private void SelectRecommendedGaming_Click(object sender, RoutedEventArgs e)
	{
		HardwareRecommendation recommendation = _gamingRecommendation;
		if ((object)recommendation == null || !recommendation.CanApplyAutomatically)
		{
			return;
		}
		TweakItemViewModel tweakItemViewModel = Tweaks.FirstOrDefault((TweakItemViewModel item) => item.Id.Equals(recommendation.ProfileId, StringComparison.OrdinalIgnoreCase));
		TweakItemViewModel tweakItemViewModel2 = Tweaks.FirstOrDefault((TweakItemViewModel item) => item.Id == "apps.nvidia-profile-inspector.install");
		if (tweakItemViewModel == null)
		{
			MessageBox.Show("The detected profile is not compatible with this Windows or driver configuration.", "Profile unavailable", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			return;
		}
		tweakItemViewModel.IsSelected = true;
		if (tweakItemViewModel2 != null)
		{
			tweakItemViewModel2.IsSelected = true;
		}
		GamingRecommendationConfidence.Text = "SELECTED · REVIEW REQUIRED";
	}

	private void Clear_Click(object sender, RoutedEventArgs e)
	{
		foreach (TweakItemViewModel tweak in Tweaks)
		{
			tweak.IsSelected = false;
		}
	}

	private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
	{
		TweakView.Refresh();
	}

	private void CategoryFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		TweakView.Refresh();
	}

	private void OpenSource_Click(object sender, RoutedEventArgs e)
	{
		if (sender is Button { Tag: string tag } && Uri.TryCreate(tag, UriKind.Absolute, out Uri _))
		{
			Process.Start(new ProcessStartInfo(tag)
			{
				UseShellExecute = true
			});
		}
	}

	private void OpenWindowsTool_Click(object sender, RoutedEventArgs e)
	{
		if (!(sender is Button { Tag: string tag }) || string.IsNullOrWhiteSpace(tag))
		{
			return;
		}
		try
		{
			string[] array = tag.Split('|', 2);
			Process.Start(new ProcessStartInfo
			{
				FileName = array[0],
				Arguments = ((array.Length == 2) ? array[1] : string.Empty),
				UseShellExecute = true
			});
		}
		catch (Exception ex)
		{
			MessageBox.Show("Windows could not open that tool.\n\n" + ex.Message, "Could not open tool", MessageBoxButton.OK, MessageBoxImage.Asterisk);
		}
	}

	private void OpenWindhawk_Click(object sender, RoutedEventArgs e)
	{
		string fileName = FindWindhawkExecutable() ?? "https://windhawk.net/";
		try
		{
			Process.Start(new ProcessStartInfo(fileName)
			{
				UseShellExecute = true
			});
		}
		catch (Exception ex)
		{
			MessageBox.Show("Windhawk could not be opened.\n\n" + ex.Message, "Windhawk", MessageBoxButton.OK, MessageBoxImage.Asterisk);
		}
	}

	private void OpenDiscord_Click(object sender, RoutedEventArgs e)
	{
		Uri result;
		bool flag = !Uri.TryCreate("", UriKind.Absolute, out result);
		if (!flag)
		{
			string scheme = result.Scheme;
			bool flag2 = ((scheme == "https" || scheme == "http") ? true : false);
			flag = !flag2;
		}
		if (flag)
		{
			MessageBox.Show("The official Discord invite has not been added to this build yet. Check optimal.app for the published community link.", "Optimal community", MessageBoxButton.OK, MessageBoxImage.Asterisk);
			return;
		}
		Process.Start(new ProcessStartInfo(result.AbsoluteUri)
		{
			UseShellExecute = true
		});
	}

	private static string? FindWindhawkExecutable()
	{
		return new string[2]
		{
			System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Windhawk", "Windhawk.exe"),
			System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Windhawk", "Windhawk.exe")
		}.FirstOrDefault(File.Exists);
	}

	private void RefreshNetwork_Click(object sender, RoutedEventArgs e)
	{
		RefreshNetworkDiagnostics();
	}

	private IReadOnlyList<CleanupTarget> SelectedCleanupTargets()
	{
		List<CleanupTarget> list = new List<CleanupTarget>();
		if (UserTempCheck.IsChecked == true)
		{
			list.Add(_cleanup.Targets[0]);
		}
		if (WindowsTempCheck.IsChecked == true)
		{
			list.Add(_cleanup.Targets[1]);
		}
		if (ShaderCacheCheck.IsChecked == true)
		{
			list.Add(_cleanup.Targets[2]);
		}
		if (CrashDumpCheck.IsChecked == true)
		{
			list.Add(_cleanup.Targets[3]);
		}
		return list;
	}

	private async void ScanCleanup_Click(object sender, RoutedEventArgs e)
	{
		IReadOnlyList<CleanupTarget> readOnlyList = SelectedCleanupTargets();
		if (readOnlyList.Count == 0)
		{
			CleanupStatusText.Text = "Select one or more cache groups first";
			return;
		}
		CleanupStatusText.Text = "Scanning selected locations…";
		CleanupSummary cleanupSummary = await _cleanup.AnalyzeAsync(readOnlyList, _lifetime.Token);
		CleanupStatusText.Text = $"{FormatBytes(cleanupSummary.Bytes)} across {cleanupSummary.Files:N0} removable files";
	}

	private async void CleanSelected_Click(object sender, RoutedEventArgs e)
	{
		IReadOnlyList<CleanupTarget> targets = SelectedCleanupTargets();
		if (targets.Count == 0)
		{
			CleanupStatusText.Text = "Select one or more cache groups first";
			return;
		}
		CleanupSummary cleanupSummary = await _cleanup.AnalyzeAsync(targets, _lifetime.Token);
		if (cleanupSummary.Files == 0)
		{
			CleanupStatusText.Text = "Nothing eligible to remove";
		}
		else if (MessageBox.Show($"Permanently delete {cleanupSummary.Files:N0} temporary files ({FormatBytes(cleanupSummary.Bytes)})?\n\nThese cache files are not added to the undo journal.", "Confirm Smart Clean", MessageBoxButton.YesNo, MessageBoxImage.Exclamation, MessageBoxResult.No) == MessageBoxResult.Yes)
		{
			CleanupStatusText.Text = "Creating required restore point…";
			RestorePointResult restorePointResult = await new SystemRestorePointService(NullLogger<SystemRestorePointService>.Instance).CreateAsync("Optimal Smart Clean", _lifetime.Token);
			if (!restorePointResult.Created)
			{
				CleanupStatusText.Text = "Cleanup stopped: " + restorePointResult.Message;
				return;
			}
			CleanupStatusText.Text = "Cleaning selected cache groups…";
			CleanupSummary cleanupSummary2 = await _cleanup.CleanAsync(targets, _lifetime.Token);
			CleanupStatusText.Text = $"Removed {cleanupSummary2.Files:N0} files · reclaimed {FormatBytes(cleanupSummary2.Bytes)}";
		}
	}

	private static string FormatBytes(long bytes)
	{
		string[] array = new string[5] { "B", "KB", "MB", "GB", "TB" };
		double num = bytes;
		int num2 = 0;
		while (num >= 1024.0 && num2 < array.Length - 1)
		{
			num /= 1024.0;
			num2++;
		}
		return $"{num:0.#} {array[num2]}";
	}

	private void HomeNav_Click(object sender, RoutedEventArgs e)
	{
		ShowPage(HomePage);
	}

	private void OptimizeNav_Click(object sender, RoutedEventArgs e)
	{
		ShowPage(OptimizePage);
	}

	private void GamingNav_Click(object sender, RoutedEventArgs e)
	{
		ShowPage(GamingPage);
	}

	private void AppsNav_Click(object sender, RoutedEventArgs e)
	{
		ShowPage(AppsPage);
	}

	private void MaintenanceNav_Click(object sender, RoutedEventArgs e)
	{
		ShowPage(MaintenancePage);
	}

	private void NetworkNav_Click(object sender, RoutedEventArgs e)
	{
		RefreshNetworkDiagnostics();
		ShowPage(NetworkPage);
	}

	private void ModulesNav_Click(object sender, RoutedEventArgs e)
	{
		ShowPage(ModulesPage);
	}

	private async void RestoreNav_Click(object sender, RoutedEventArgs e)
	{
		await RefreshHistoryAsync();
		ShowPage(RestorePage);
	}

	private async void CreateRestorePoint_Click(object sender, RoutedEventArgs e)
	{
		string text = RestoreNameBox.Text.Trim();
		if (string.IsNullOrWhiteSpace(text))
		{
			RestoreStatusText.Text = "Enter a name for this checkpoint.";
			return;
		}
		RestoreStatusText.Text = "Creating a fresh Windows restore point…";
		string description = (string.IsNullOrWhiteSpace(RestoreDescriptionBox.Text) ? text : (text + " · " + RestoreDescriptionBox.Text.Trim()));
		RestorePointResult restorePointResult = await new SystemRestorePointService(NullLogger<SystemRestorePointService>.Instance).CreateAsync(description, _lifetime.Token);
		RestoreStatusText.Text = restorePointResult.Message;
		RestoreStatusText.Foreground = (Brush)FindResource(restorePointResult.Created ? "SuccessBrush" : "WarningBrush");
	}

	private async void HistoryNav_Click(object sender, RoutedEventArgs e)
	{
		await RefreshHistoryAsync();
		ShowPage(HistoryPage);
	}

	private void SystemNav_Click(object sender, RoutedEventArgs e)
	{
		ShowPage(SystemPage);
	}

	private void AboutNav_Click(object sender, RoutedEventArgs e)
	{
		ShowPage(AboutPage);
	}

	private void ShowPage(UIElement page)
	{
		Grid[] array = new Grid[13]
		{
			HomePage, OptimizePage, GamingPage, AppsPage, MaintenancePage, NetworkPage, ModulesPage, RestorePage, ReviewPage, ExecutionPage,
			HistoryPage, SystemPage, AboutPage
		};
		foreach (Grid obj in array)
		{
			obj.Visibility = ((obj != page) ? Visibility.Collapsed : Visibility.Visible);
		}
		Button[] array2 = new Button[11]
		{
			HomeNav, OptimizeNav, GamingNav, AppsNav, MaintenanceNav, NetworkNav, ModulesNav, RestoreNav, HistoryNav, SystemNav,
			AboutNav
		};
		foreach (Button obj2 in array2)
		{
			obj2.Background = Brushes.Transparent;
			obj2.Foreground = (Brush)FindResource("MutedBrush");
		}
		Button obj3 = ((page == HomePage) ? HomeNav : ((page != ReviewPage && page != ExecutionPage) ? ((page == OptimizePage) ? OptimizeNav : ((page == GamingPage) ? GamingNav : ((page == AppsPage) ? AppsNav : ((page == MaintenancePage) ? MaintenanceNav : ((page == NetworkPage) ? NetworkNav : ((page == ModulesPage) ? ModulesNav : ((page == RestorePage) ? RestoreNav : ((page == HistoryPage) ? HistoryNav : ((page == SystemPage) ? SystemNav : AboutNav))))))))) : ((_reviewOrigin == GamingPage) ? GamingNav : ((_reviewOrigin == AppsPage) ? AppsNav : OptimizeNav))));
		obj3.Background = new SolidColorBrush(Color.FromRgb(19, 28, 41));
		obj3.Foreground = (Brush)FindResource("TextBrush");
		AnimateIn(page);
	}

	private static void AnimateIn(UIElement element)
	{
		element.Opacity = 0.0;
		element.RenderTransform = new TranslateTransform(0.0, 10.0);
		element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(180L, 0L))
		{
			EasingFunction = new QuadraticEase
			{
				EasingMode = EasingMode.EaseOut
			}
		});
		((TranslateTransform)element.RenderTransform).BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(10.0, 0.0, TimeSpan.FromMilliseconds(220L, 0L))
		{
			EasingFunction = new CubicEase
			{
				EasingMode = EasingMode.EaseOut
			}
		});
	}

	private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		if (e.ClickCount == 2)
		{
			base.WindowState = ((base.WindowState != WindowState.Maximized) ? WindowState.Maximized : WindowState.Normal);
		}
		else
		{
			DragMove();
		}
	}

	private void Minimize_Click(object sender, RoutedEventArgs e)
	{
		base.WindowState = WindowState.Minimized;
	}

	private void Maximize_Click(object sender, RoutedEventArgs e)
	{
		base.WindowState = ((base.WindowState != WindowState.Maximized) ? WindowState.Maximized : WindowState.Normal);
	}

	private void Close_Click(object sender, RoutedEventArgs e)
	{
		Close();
	}
}
