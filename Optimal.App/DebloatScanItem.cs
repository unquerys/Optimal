using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace Optimal.App;

public sealed class DebloatScanItem : INotifyPropertyChanged
{
	private readonly TweakItemViewModel _tweak;

	public string Name => _tweak.Name;
	public string Description => _tweak.Description;
	public string PackageName { get; }
	public string RiskLabel => _tweak.IsAdvanced ? "EXPERT REVIEW" : "OPTIONAL APP";
	public Brush RiskBrush => _tweak.IsAdvanced ? Brushes.Orange : new SolidColorBrush(Color.FromRgb(101, 214, 166));

	public bool IsSelected
	{
		get => _tweak.IsSelected;
		set
		{
			if (_tweak.IsSelected == value)
				return;
			_tweak.IsSelected = value;
			OnPropertyChanged();
		}
	}

	public DebloatScanItem(TweakItemViewModel tweak, string packageName)
	{
		_tweak = tweak;
		PackageName = packageName;
		_tweak.PropertyChanged += (_, e) =>
		{
			if (e.PropertyName == nameof(TweakItemViewModel.IsSelected))
				OnPropertyChanged(nameof(IsSelected));
		};
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
