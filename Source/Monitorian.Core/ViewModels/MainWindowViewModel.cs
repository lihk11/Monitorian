using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;

using Monitorian.Core.Models;

namespace Monitorian.Core.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
	private readonly AppControllerCore _controller;
	public SettingsCore Settings => _controller.Settings;

	public MainWindowViewModel(AppControllerCore controller)
	{
		this._controller = controller ?? throw new ArgumentNullException(nameof(controller));
		this._controller.ScanningChanged += OnScanningChanged;
		Settings.PropertyChanged += OnSettingsChanged;
	}

	public ListCollectionView MonitorsView
	{
		get
		{
			if (_monitorsView is null)
			{
				_monitorsView = new ListCollectionView(_controller.Monitors);
				if (Settings.OrdersArrangement)
				{
					_monitorsView.SortDescriptions.Add(new SortDescription(nameof(MonitorViewModel.MonitorTopLeft), ListSortDirection.Ascending));
					_monitorsView.IsLiveSorting = true;
					_monitorsView.LiveSortingProperties.Add(nameof(MonitorViewModel.MonitorTopLeft));
				}
				_monitorsView.SortDescriptions.Add(new SortDescription(nameof(MonitorViewModel.DisplayIndex), ListSortDirection.Ascending));
				_monitorsView.SortDescriptions.Add(new SortDescription(nameof(MonitorViewModel.MonitorIndex), ListSortDirection.Ascending));
				_monitorsView.Filter = x => ((MonitorViewModel)x).IsTarget;
				_monitorsView.IsLiveFiltering = true;
				_monitorsView.LiveFilteringProperties.Add(nameof(MonitorViewModel.IsTarget));

				((INotifyCollectionChanged)_monitorsView).CollectionChanged += OnCollectionChanged;

				// The merged unison view in MainWindow.xaml binds its DataContext to
				// "{Binding MonitorsView/}" (the trailing slash means CurrentItem) and uses
				// ObjectToVisibilityConverter to collapse itself when CurrentItem is null.
				// When the underlying collection is already populated by the time MonitorsView
				// is first accessed (which is the typical case: the brightness hotkey path
				// triggers visual-tree construction after ScanAsync has already finished
				// adding monitors), no Add event will fire, so OnCollectionChanged has no
				// chance to set up a current item. Likewise, applying SortDescriptions/Filter
				// can leave CurrentItem at the "before first" position. Without an explicit
				// initialization here, the unison view stays Collapsed and MainWindow shows
				// up as an empty narrow/flat panel.
				EnsureCurrentItem();
			}
			return _monitorsView;
		}
	}
	private ListCollectionView _monitorsView;

	private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
	{
		switch (e.Action)
		{
			//case NotifyCollectionChangedAction.Reset:
			case NotifyCollectionChangedAction.Add:
			case NotifyCollectionChangedAction.Remove:
				OnPropertyChanged(nameof(IsMonitorsEmpty));

				EnsureCurrentItem();
				break;
		}
	}

	private void EnsureCurrentItem()
	{
		if (_monitorsView is null || _monitorsView.IsEmpty)
			return;

		// If a monitor is already current and is selected, nothing to do.
		if (_monitorsView.CurrentItem is MonitorViewModel { IsSelected: true })
			return;

		// Prefer the persisted selected monitor; otherwise fall back to whichever monitor is
		// already marked as IsSelected; otherwise use the first monitor in the filtered/
		// sorted view. Without this fallback, the unison view's CurrentItem stays null when
		// SelectedDeviceInstanceId does not match any enumerated monitor (e.g. on a fresh
		// install or after the user disconnected the previously selected display), and the
		// merged unison Grid in MainWindow.xaml gets collapsed.
		var monitor = _monitorsView.Cast<MonitorViewModel>()
				.FirstOrDefault(x => ReferenceEquals(x, _controller.SelectedMonitor))
			?? _monitorsView.Cast<MonitorViewModel>()
				.FirstOrDefault(x => x.IsSelected)
			?? _monitorsView.Cast<MonitorViewModel>().FirstOrDefault();
		if (monitor is null)
			return;

		// Move the view's current item explicitly: just setting IsSelected=true on the
		// MonitorViewModel does not propagate to ListCollectionView.CurrentItem when the
		// ListView in the visual tree is collapsed (which it is whenever EnablesUnison is on).
		_monitorsView.MoveCurrentTo(monitor);
		monitor.IsSelected = true;
	}

	public bool IsMonitorsEmpty => MonitorsView.IsEmpty;

	private void OnScanningChanged(object sender, bool e)
	{
		IsScanning = e;
		OnPropertyChanged(nameof(IsScanning));
	}

	public bool IsScanning { get; private set; }

	private void OnSettingsChanged(object sender, PropertyChangedEventArgs e)
	{
		switch (e.PropertyName)
		{
			case nameof(Settings.OrdersArrangement):
				var description = new SortDescription(nameof(MonitorViewModel.MonitorTopLeft), ListSortDirection.Ascending);
				int index = MonitorsView.SortDescriptions.IndexOf(description);

				switch (Settings.OrdersArrangement, index)
				{
					case (true, < 0):
						MonitorsView.SortDescriptions.Insert(0, description);
						MonitorsView.IsLiveSorting = true;
						MonitorsView.LiveSortingProperties.Add(description.PropertyName);
						break;

					case (false, >= 0):
						MonitorsView.SortDescriptions.RemoveAt(index);
						MonitorsView.IsLiveSorting = false;
						MonitorsView.LiveSortingProperties.Remove(description.PropertyName);
						break;
				}

				MonitorsView.Refresh();
				break;
		}
	}

	internal void Deactivate()
	{
		var monitor = MonitorsView.Cast<MonitorViewModel>().FirstOrDefault(x => x.IsSelectedByKey);
		if (monitor is not null)
			monitor.IsByKey = false;
	}
}