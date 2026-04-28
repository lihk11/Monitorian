using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Monitorian.Core.Views.Input;

public class BrightnessHotKeyTracker : IDisposable
{
	#region Win32

	[DllImport("User32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool RegisterHotKey(
		IntPtr hWnd,
		int id,
		uint fsModifiers,
		uint vk);

	[DllImport("User32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool UnregisterHotKey(
		IntPtr hWnd,
		int id);

	private const int WM_HOTKEY = 0x0312;
	private const uint MOD_NOREPEAT = 0x4000;
	private const uint VK_F23 = 0x86;
	private const uint VK_F24 = 0x87;

	#endregion

	private const int DecrementId = 1;
	private const int IncrementId = 2;

	public BrightnessHotKeyTracker(Window window)
	{
		this._window = window ?? throw new ArgumentNullException(nameof(window));
		this._window.Closed += OnClosed;

		Register();
	}

	private readonly Window _window;
	private HwndSource _source;
	private IntPtr _handle;
	private bool _isDecrementRegistered;
	private bool _isIncrementRegistered;

	private void Register()
	{
		_handle = new WindowInteropHelper(_window).EnsureHandle();
		_source = HwndSource.FromHwnd(_handle);
		_source?.AddHook(WndProc);

		_isDecrementRegistered = RegisterHotKey(_handle, DecrementId, MOD_NOREPEAT, VK_F23);
		_isIncrementRegistered = RegisterHotKey(_handle, IncrementId, MOD_NOREPEAT, VK_F24);
	}

	private void Unregister()
	{
		if (_isDecrementRegistered)
			UnregisterHotKey(_handle, DecrementId);
		if (_isIncrementRegistered)
			UnregisterHotKey(_handle, IncrementId);

		_source?.RemoveHook(WndProc);
		_isDecrementRegistered = false;
		_isIncrementRegistered = false;
	}

	private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
	{
		if (msg is not WM_HOTKEY)
			return IntPtr.Zero;

		switch (wParam.ToInt32())
		{
			case DecrementId:
				handled = true;
				DecrementPressed?.Invoke(this, EventArgs.Empty);
				break;

			case IncrementId:
				handled = true;
				IncrementPressed?.Invoke(this, EventArgs.Empty);
				break;
		}
		return IntPtr.Zero;
	}

	public event EventHandler DecrementPressed;
	public event EventHandler IncrementPressed;

	private void OnClosed(object sender, EventArgs e)
	{
		Dispose();
	}

	public void Dispose()
	{
		_window.Closed -= OnClosed;
		Unregister();
	}
}
