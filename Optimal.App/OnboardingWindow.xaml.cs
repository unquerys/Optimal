using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Optimal.Core.Detection;
using Optimal.Core.Manifest;

namespace Optimal.App;

public partial class OnboardingWindow : Window
{
	private readonly MachineProfile _profile;

	private readonly IReadOnlyList<TweakItemViewModel> _allTweaks;

	private int _step;

	public string? SelectedProfile { get; private set; }

	public bool ManualMode { get; private set; }

	public ObservableCollection<OnboardingChoice> TweakChoices { get; } = new ObservableCollection<OnboardingChoice>();

	public ObservableCollection<OnboardingChoice> DebloatChoices { get; } = new ObservableCollection<OnboardingChoice>();

	public ObservableCollection<OnboardingChoice> SoftwareChoices { get; } = new ObservableCollection<OnboardingChoice>();

	public IReadOnlyList<string> SelectedIds => (from choice in TweakChoices.Concat(DebloatChoices).Concat(SoftwareChoices)
		where choice.IsSelected
		select choice.Id).ToList();

	public OnboardingWindow(MachineProfile profile, IEnumerable<TweakItemViewModel> tweaks)
	{
		_profile = profile;
		_allTweaks = tweaks.ToList();
		InitializeComponent();
		base.DataContext = this;
		HardwareRecommendation hardwareRecommendation = HardwareAdvisor.Recommend(profile);
		HardwareCpuText.Text = $"{profile.CpuName}\n{profile.CpuPhysicalCores} cores · {profile.CpuLogicalCores} threads";
		HardwareGpuText.Text = $"{profile.GpuName}\n{profile.DeviceKind} · {profile.SystemDriveKind}";
		HardwareRecommendationTitle.Text = hardwareRecommendation.Name;
		HardwareRecommendationText.Text = hardwareRecommendation.Rationale;
		foreach (TweakItemViewModel item in _allTweaks.Where((TweakItemViewModel item) => item.Definition.Category == TweakCategory.Apps))
		{
			SoftwareChoices.Add(new OnboardingChoice(item.Id, item.Name, item.Description, item.BrandGlyph, item.LogoPath));
		}
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			"debloat.clipchamp.remove", "debloat.solitaire.remove", "debloat.news.remove", "debloat.weather.remove", "debloat.feedback-hub.remove", "debloat.get-help.remove", "debloat.office-hub.remove", "debloat.power-automate.remove", "debloat.dev-home.remove", "debloat.3d-viewer.remove",
			"debloat.cortana.remove", "debloat.messaging.remove", "privacy.diagnostic-data.required-only", "privacy.tailored-experiences.disable"
		};
		foreach (TweakItemViewModel item2 in _allTweaks.Where(delegate(TweakItemViewModel item)
		{
			bool flag = item.Definition.Category == TweakCategory.Debloat;
			if (!flag)
			{
				string id = item.Id;
				bool flag2 = ((id == "privacy.diagnostic-data.required-only" || id == "privacy.tailored-experiences.disable") ? true : false);
				flag = flag2;
			}
			return flag;
		}))
		{
			DebloatChoices.Add(new OnboardingChoice(item2.Id, item2.Name, item2.Description, item2.BrandGlyph, item2.LogoPath)
			{
				IsSelected = hashSet.Contains(item2.Id)
			});
		}
	}

	private void Profile_Click(object sender, RoutedEventArgs e)
	{
		if (sender is Button { Tag: string tag })
		{
			SelectProfile(tag, recommended: false);
		}
	}

	private void ProceedRecommended_Click(object sender, RoutedEventArgs e)
	{
		GpuVendor gpuVendor = _profile.GpuVendor;
		bool flag = (uint)(gpuVendor - 1) <= 1u;
		string profile = ((flag && !_profile.IsLaptop && _profile.CpuPhysicalCores >= 6) ? "gaming" : "balanced");
		SelectProfile(profile, recommended: true);
		_step = 1;
		RenderStep();
	}

	private void Manual_Click(object sender, RoutedEventArgs e)
	{
		ManualMode = true;
		base.DialogResult = true;
	}

	private void SelectProfile(string profile, bool recommended)
	{
		SelectedProfile = profile;
		ProfileSelectionText.Text = (recommended ? (profile.ToUpperInvariant() + " recommended from detected hardware") : (profile.ToUpperInvariant() + " profile selected"));
		ProfileSelectionText.Foreground = (Brush)FindResource("SuccessBrush");
		NextButton.IsEnabled = true;
		BuildTweakChoices(profile);
		Brush brush = (Brush)FindResource("SurfaceRaisedBrush");
		Brush brush2 = (Brush)FindResource("AccentDarkBrush");
		Button[] array = new Button[4] { GamingProfileButton, BalancedProfileButton, PrivacyProfileButton, MinimalProfileButton };
		foreach (Button obj in array)
		{
			obj.Background = ((obj.Tag as string == profile) ? brush2 : brush);
		}
	}

	private void BuildTweakChoices(string profile)
	{
		TweakChoices.Clear();
		HardwareRecommendation hardwareRecommendation = HardwareAdvisor.Recommend(_profile);
		foreach (TweakItemViewModel item in _allTweaks.Where(delegate(TweakItemViewModel item)
		{
			bool flag4 = !item.IsAdvanced;
			if (flag4)
			{
				TweakCategory category2 = item.Definition.Category;
				bool flag5 = ((category2 == TweakCategory.Debloat || category2 == TweakCategory.Apps) ? true : false);
				flag4 = !flag5;
			}
			return flag4;
		}))
		{
			bool flag;
			switch (profile)
			{
			case "gaming":
			{
				TweakCategory category = item.Definition.Category;
				bool flag2 = (uint)category <= 1u;
				flag = flag2;
				break;
			}
			case "privacy":
				flag = item.Definition.Category == TweakCategory.Privacy;
				break;
			case "minimal":
			{
				bool flag2 = item.Definition.Tier == TweakTier.Verified;
				if (flag2)
				{
					TweakCategory category = item.Definition.Category;
					bool flag3 = ((category == TweakCategory.System || (uint)(category - 5) <= 1u) ? true : false);
					flag2 = flag3;
				}
				flag = flag2;
				break;
			}
			default:
				flag = item.Definition.Tier == TweakTier.Verified && item.Definition.Category != TweakCategory.Power;
				break;
			}
			if (flag && (!item.Id.StartsWith("gaming.nvidia.", StringComparison.OrdinalIgnoreCase) || item.Id.Equals(hardwareRecommendation.ProfileId, StringComparison.OrdinalIgnoreCase)))
			{
				TweakChoices.Add(new OnboardingChoice(item.Id, item.Name, item.Description, item.BrandGlyph, item.LogoPath)
				{
					IsSelected = true
				});
			}
		}
	}

	private void Next_Click(object sender, RoutedEventArgs e)
	{
		if (_step == 6)
		{
			base.DialogResult = true;
			return;
		}
		_step++;
		RenderStep();
	}

	private void Back_Click(object sender, RoutedEventArgs e)
	{
		if (_step > 0)
		{
			_step--;
			RenderStep();
		}
	}

	private void RenderStep()
	{
		UIElement[] array = new UIElement[7] { ConsentStep, ProfileStep, HardwareStep, TweaksStep, DebloatStep, SoftwareStep, SafetyStep };
		for (int i = 0; i < array.Length; i++)
		{
			array[i].Visibility = ((i != _step) ? Visibility.Collapsed : Visibility.Visible);
		}
		string[] array2 = new string[7] { "Recommended setup", "Choose a profile", "Hardware match", "Review tweaks", "Debloat & privacy", "Choose software", "Safety check" };
		StepCaption.Text = array2[_step];
		StepCounter.Text = $"{_step + 1:00} / 07";
		BackButton.Visibility = ((_step == 0) ? Visibility.Collapsed : Visibility.Visible);
		NextButton.Visibility = ((_step == 0) ? Visibility.Collapsed : Visibility.Visible);
		NextButton.IsEnabled = _step != 1 || SelectedProfile != null;
		NextButton.Content = ((_step == 6) ? "Build my plan  →" : "Continue  →");
		Brush brush = (Brush)FindResource("AccentBrush");
		SolidColorBrush solidColorBrush = new SolidColorBrush(Color.FromRgb(52, 66, 88));
		Ellipse[] array3 = new Ellipse[6] { Dot1, Dot2, Dot3, Dot4, Dot5, Dot6 };
		for (int j = 0; j < array3.Length; j++)
		{
			array3[j].Fill = ((j <= Math.Min(_step, array3.Length - 1)) ? brush : solidColorBrush);
		}
		StepHost.Opacity = 0.0;
		StepHost.RenderTransform = new TranslateTransform(16.0, 0.0);
		StepHost.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(180L, 0L)));
		((TranslateTransform)StepHost.RenderTransform).BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(16.0, 0.0, TimeSpan.FromMilliseconds(240L, 0L))
		{
			EasingFunction = new CubicEase
			{
				EasingMode = EasingMode.EaseOut
			}
		});
	}
}