using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using Optimal.Core.Manifest;

namespace Optimal.App;

public sealed class TweakItemViewModel : INotifyPropertyChanged
{
	private bool _isSelected;

	private string _state = "Not scanned";

	private Brush _stateBrush = Brushes.SlateGray;

	public TweakDefinition Definition { get; }

	public Action SelectionChanged { get; }

	public string Id => Definition.Id;

	public string Name => Definition.Name;

	public string Description => Definition.Description;

	public string Category => Definition.Category.ToString();

	public string Tier => Definition.Tier.ToString();

	public string Audience => Definition.Audience.ToString();

	public bool IsAdvanced => Definition.Audience == TweakAudience.Advanced;

	public string Impact
	{
		get
		{
			if (!string.IsNullOrWhiteSpace(Definition.Impact))
			{
				return Definition.Impact;
			}
			return "Measured system preference";
		}
	}

	public string Tradeoff => Definition.Tradeoff ?? string.Empty;

	public bool HasTradeoff => !string.IsNullOrWhiteSpace(Tradeoff);

	public string Source => Definition.Source;

	public bool Reboot => Definition.Reboot;

	public string BrandGlyph
	{
		get
		{
			string id = Definition.Id;
			if (id.Contains("brave"))
			{
				return "B";
			}
			if (id.Contains("firefox"))
			{
				return "F";
			}
			if (id.Contains("librewolf"))
			{
				return "LW";
			}
			if (id.Contains("powertoys"))
			{
				return "PT";
			}
			if (id.Contains("everything"))
			{
				return "E";
			}
			if (id.Contains("latencymon"))
			{
				return "LM";
			}
			if (id.Contains("gpuz"))
			{
				return "GZ";
			}
			if (id.Contains("nana"))
			{
				return "NZ";
			}
			if (id.Contains("nvidia"))
			{
				return "NV";
			}
			if (id.Contains("windhawk"))
			{
				return "W";
			}
			if (id.Contains("crystaldisk"))
			{
				return "CD";
			}
			if (id.Contains("hwmonitor"))
			{
				return "HW";
			}
			return Name.Substring(0, Math.Min(2, Name.Length)).ToUpperInvariant();
		}
	}

	public Brush BrandBrush
	{
		get
		{
			string id = Definition.Id;
			if (id.Contains("nvidia"))
			{
				return new SolidColorBrush(Color.FromRgb(118, 185, 0));
			}
			if (id.Contains("brave"))
			{
				return new SolidColorBrush(Color.FromRgb(251, 84, 43));
			}
			if (id.Contains("firefox"))
			{
				return new SolidColorBrush(Color.FromRgb(117, 70, 240));
			}
			if (id.Contains("librewolf"))
			{
				return new SolidColorBrush(Color.FromRgb(32, 113, 184));
			}
			if (id.Contains("powertoys"))
			{
				return new SolidColorBrush(Color.FromRgb(0, 120, 212));
			}
			return new SolidColorBrush(Color.FromRgb(80, 105, 190));
		}
	}

	public string? LogoPath
	{
		get
		{
			string id = Definition.Id;
			if (id.Contains("brave"))
			{
				return "Assets/Software/brave.png";
			}
			if (id.Contains("firefox"))
			{
				return "Assets/Software/firefox.png";
			}
			if (id.Contains("librewolf"))
			{
				return "Assets/Software/librewolf.png";
			}
			if (id.Contains("nvidia"))
			{
				return "Assets/Software/nvidia.png";
			}
			return null;
		}
	}

	public Brush TierBrush => Definition.Tier switch
	{
		TweakTier.Verified => new SolidColorBrush(Color.FromRgb(101, 214, 166)), 
		TweakTier.Situational => new SolidColorBrush(Color.FromRgb(244, 199, 107)), 
		_ => new SolidColorBrush(Color.FromRgb(byte.MaxValue, 125, 138)), 
	};

	public bool IsSelected
	{
		get
		{
			return _isSelected;
		}
		set
		{
			if (Set(ref _isSelected, value, "IsSelected"))
			{
				SelectionChanged();
			}
		}
	}

	public string State
	{
		get
		{
			return _state;
		}
		set
		{
			Set(ref _state, value, "State");
		}
	}

	public Brush StateBrush
	{
		get
		{
			return _stateBrush;
		}
		set
		{
			Set(ref _stateBrush, value, "StateBrush");
		}
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	public TweakItemViewModel(TweakDefinition definition, Action selectionChanged)
	{
		Definition = definition;
		_isSelected = false;
		SelectionChanged = selectionChanged;
	}

	public void SetState(TweakState state)
	{
		State = state switch
		{
			TweakState.Applied => "Already applied", 
			TweakState.NotApplied => "Ready", 
			TweakState.Partial => "Partially applied", 
			_ => "State unknown", 
		};
		StateBrush = state switch
		{
			TweakState.Applied => new SolidColorBrush(Color.FromRgb(101, 214, 166)), 
			TweakState.Partial => new SolidColorBrush(Color.FromRgb(244, 199, 107)), 
			TweakState.NotApplied => new SolidColorBrush(Color.FromRgb(99, 130, byte.MaxValue)), 
			_ => new SolidColorBrush(Color.FromRgb(147, 161, 181)), 
		};
	}

	private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
	{
		if (EqualityComparer<T>.Default.Equals(field, value))
		{
			return false;
		}
		field = value;
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		return true;
	}
}
