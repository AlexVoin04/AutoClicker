using System;
using System.Timers;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AutoClicker.Enums;
using AutoClicker.Models;
using AutoClicker.Utils;
using Serilog;
using CheckBox = System.Windows.Controls.CheckBox;
using MouseAction = AutoClicker.Enums.MouseAction;
using MouseButton = AutoClicker.Enums.MouseButton;
using MouseCursor = System.Windows.Forms.Cursor;
using NotifyIcon = System.Windows.Forms.NotifyIcon;
using Point = System.Drawing.Point;
using Timer = System.Timers.Timer;

namespace AutoClicker.Views
{
    public partial class MainWindow : Window
    {
        public AutoClickerSettings AutoClickerSettings
        {
            get { return (AutoClickerSettings)GetValue(CurrentSettingsProperty); }
            set { SetValue(CurrentSettingsProperty, value); }
        }

        public static readonly DependencyProperty CurrentSettingsProperty =
           DependencyProperty.Register(nameof(AutoClickerSettings), typeof(AutoClickerSettings), typeof(MainWindow),
               new UIPropertyMetadata(SettingsUtils.CurrentSettings.AutoClickerSettings));

        public int timesRepeated = 0;
        public readonly Timer clickTimer;
        public readonly Uri runningIconUri =
            new Uri(Constants.RUNNING_ICON_RESOURCE_PATH, UriKind.Relative);

        public NotifyIcon systemTrayIcon;
        public SystemTrayMenu systemTrayMenu;
        public AboutWindow aboutWindow = null;
        public SettingsWindow settingsWindow = null;
        public CaptureMouseScreenCoordinatesWindow captureMouseCoordinatesWindow;

        public ImageSource _defaultIcon;
        public IntPtr _mainWindowHandle;
        public HwndSource _source;

        #region Life Cycle

        public MainWindow()
        {
            clickTimer = new Timer();
            clickTimer.Elapsed += OnClickTimerElapsed;

            DataContext = this;
            ResetTitle();
            InitializeComponent();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            _mainWindowHandle = new WindowInteropHelper(this).Handle;
            _source = HwndSource.FromHwnd(_mainWindowHandle);
            _source.AddHook(StartStopHooks);

            SettingsUtils.HotKeyChangedEvent += SettingsUtils_HotKeyChangedEvent;
            SettingsUtils_HotKeyChangedEvent(this, new HotkeyChangedEventArgs()
            {
                Hotkey = SettingsUtils.CurrentSettings.HotkeySettings.StartHotkey,
                Operation = Operation.Start
            });
            SettingsUtils_HotKeyChangedEvent(this, new HotkeyChangedEventArgs()
            {
                Hotkey = SettingsUtils.CurrentSettings.HotkeySettings.StopHotkey,
                Operation = Operation.Stop
            });
            SettingsUtils_HotKeyChangedEvent(this, new HotkeyChangedEventArgs()
            {
                Hotkey = SettingsUtils.CurrentSettings.HotkeySettings.ToggleHotkey,
                Operation = Operation.Toggle
            });

            _defaultIcon = Icon;

            RadioButtonSelectedLocationMode_CurrentLocation.Checked += RadioButtonSelectedLocationMode_CurrentLocationOnChecked;

            InitializeSystemTrayMenu();
        }

        protected override void OnClosed(EventArgs e)
        {
            _source.RemoveHook(StartStopHooks);

            SettingsUtils.HotKeyChangedEvent -= SettingsUtils_HotKeyChangedEvent;
            UnregisterHotkey(Constants.START_HOTKEY_ID);
            UnregisterHotkey(Constants.STOP_HOTKEY_ID);
            UnregisterHotkey(Constants.TOGGLE_HOTKEY_ID);

            systemTrayIcon.Click -= SystemTrayIcon_Click;
            systemTrayIcon.Dispose();

            systemTrayMenu.SystemTrayMenuActionEvent -= SystemTrayMenu_SystemTrayMenuActionEvent;
            systemTrayMenu.Dispose();

            Log.Information("Application closing");
            Log.Debug("==================================================");

            base.OnClosed(e);
        }

        #endregion Life Cycle

        #region Commands

        public void StartCommand_Execute(object sender, ExecutedRoutedEventArgs e)
        {
            int interval = CalculateInterval();
            Log.Information("Starting operation, interval={Interval}ms", interval);

            timesRepeated = 0;
            clickTimer.Interval = interval;
            clickTimer.Start();

            Icon = new BitmapImage(runningIconUri);
            Title += Constants.MAIN_WINDOW_TITLE_RUNNING;
            systemTrayIcon.Text += Constants.MAIN_WINDOW_TITLE_RUNNING;
        }

        public void StartCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = CanStartOperation();
        }

        public void StopCommand_Execute(object sender, ExecutedRoutedEventArgs e)
        {
            Log.Information("Stopping operation");
            clickTimer.Stop();

            ResetTitle();
            Icon = _defaultIcon;
        }

        public void StopCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = clickTimer.Enabled;
        }

        public void ToggleCommand_Execute(object sender, ExecutedRoutedEventArgs e)
        {
            if (clickTimer.Enabled)
                StopCommand_Execute(sender, e);
            else
                StartCommand_Execute(sender, e);
        }

        public void ToggleCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = CanStartOperation() | clickTimer.Enabled;
        }

        public void SaveSettingsCommand_Execute(object sender, ExecutedRoutedEventArgs e)
        {
            Log.Information("Saving Settings");
            SettingsUtils.SetApplicationSettings(AutoClickerSettings);
        }

        public void HotkeySettingsCommand_Execute(object sender, ExecutedRoutedEventArgs e)
        {
            if (settingsWindow == null)
            {
                settingsWindow = new SettingsWindow();
                settingsWindow.Closed += (o, args) => settingsWindow = null;
            }

            settingsWindow.Show();
        }

        public void ExitCommand_Execute(object sender, ExecutedRoutedEventArgs e)
        {
            Exit();
        }

        public void Exit()
        {
            Application.Current.Shutdown();
        }

        public void AboutCommand_Execute(object sender, ExecutedRoutedEventArgs e)
        {
            if (aboutWindow == null)
            {
                aboutWindow = new AboutWindow();
                aboutWindow.Closed += (o, args) => aboutWindow = null;
            }

            aboutWindow.Show();
        }

        public void CaptureMouseScreenCoordinatesCommand_Execute(
            object sender,
            ExecutedRoutedEventArgs e
        )
        {
            if (captureMouseCoordinatesWindow == null)
            {
                captureMouseCoordinatesWindow = new CaptureMouseScreenCoordinatesWindow();
                captureMouseCoordinatesWindow.Closed += (o, args) => captureMouseCoordinatesWindow = null;
                captureMouseCoordinatesWindow.OnCoordinatesCaptured += (o, point) =>
                {
                    TextBoxPickedXValue.Text = point.X.ToString();
                    TextBoxPickedYValue.Text = point.Y.ToString();
                    RadioButtonSelectedLocationMode_PickedLocation.IsChecked = true;
                };
            }

            captureMouseCoordinatesWindow.Show();
        }

        #endregion Commands

        #region Helper Methods

        public int CalculateInterval()
        {
            return AutoClickerSettings.Milliseconds
                + (AutoClickerSettings.Seconds * 1000)
                + (AutoClickerSettings.Minutes * 60 * 1000)
                + (AutoClickerSettings.Hours * 60 * 60 * 1000);
        }

        public bool IsIntervalValid()
        {
            return CalculateInterval() > 0;
        }

        public bool CanStartOperation()
        {
            return !clickTimer.Enabled && IsRepeatModeValid() && IsIntervalValid();
        }

        public int GetTimesToRepeat()
        {
            return AutoClickerSettings.SelectedRepeatMode == RepeatMode.Count ? AutoClickerSettings.SelectedTimesToRepeat : -1;
        }

        public Point GetSelectedPosition()
        {
            return AutoClickerSettings.SelectedLocationMode == LocationMode.CurrentLocation ?
                MouseCursor.Position : new Point(AutoClickerSettings.PickedXValue, AutoClickerSettings.PickedYValue);
        }

        public int GetSelectedXPosition()
        {
            return GetSelectedPosition().X;
        }

        public int GetSelectedYPosition()
        {
            return GetSelectedPosition().Y;
        }

        public int GetNumberOfMouseActions()
        {
            return AutoClickerSettings.SelectedMouseAction == MouseAction.Single ? 1 : 2;
        }

        public bool IsRepeatModeValid()
        {
            return AutoClickerSettings.SelectedRepeatMode == RepeatMode.Infinite
                || (AutoClickerSettings.SelectedRepeatMode == RepeatMode.Count && AutoClickerSettings.SelectedTimesToRepeat > 0);
        }

        public void ResetTitle()
        {
            Title = Constants.MAIN_WINDOW_TITLE_DEFAULT;
            if (systemTrayIcon != null)
            {
                systemTrayIcon.Text = Constants.MAIN_WINDOW_TITLE_DEFAULT;
            }
        }

        public void InitializeSystemTrayMenu()
        {
            systemTrayIcon = new NotifyIcon
            {
                Visible = true,
                Icon = AssemblyUtils.GetApplicationIcon()
            };

            systemTrayIcon.Click += SystemTrayIcon_Click;
            systemTrayIcon.Text = Constants.MAIN_WINDOW_TITLE_DEFAULT;
            systemTrayMenu = new SystemTrayMenu();
            systemTrayMenu.SystemTrayMenuActionEvent += SystemTrayMenu_SystemTrayMenuActionEvent;
        }

        public void ReRegisterHotkey(int hotkeyId, KeyMapping hotkey)
        {
            UnregisterHotkey(hotkeyId);
            RegisterHotkey(hotkeyId, hotkey);
        }

        public void RegisterHotkey(int hotkeyId, KeyMapping hotkey)
        {
            Log.Information("RegisterHotkey with hotkeyId {HotkeyId} and hotkey {Hotkey}", hotkeyId, hotkey.DisplayName);
            User32ApiUtils.RegisterHotKey(_mainWindowHandle, hotkeyId, Constants.MOD_NONE, hotkey.VirtualKeyCode);
        }

        public void UnregisterHotkey(int hotkeyId)
        {
            Log.Information("UnregisterHotkey with hotkeyId {HotkeyId}", hotkeyId);
            if (User32ApiUtils.UnregisterHotKey(_mainWindowHandle, hotkeyId))
                return;
            Log.Warning("No hotkey registered on {HotkeyId}", hotkeyId);
        }

        #endregion Helper Methods

        #region Event Handlers

        public void OnClickTimerElapsed(object sender, ElapsedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                InitMouseClick();
                timesRepeated++;

                if (timesRepeated == GetTimesToRepeat())
                {
                    clickTimer.Stop();
                    ResetTitle();
                }
            });
        }

        public void InitMouseClick()
        {
            Dispatcher.Invoke(() =>
            {
                switch (AutoClickerSettings.SelectedMouseButton)
                {
                    case MouseButton.Left:
                        PerformMouseClick(Constants.MOUSEEVENTF_LEFTDOWN, Constants.MOUSEEVENTF_LEFTUP, GetSelectedXPosition(), GetSelectedYPosition());
                        break;
                    case MouseButton.Right:
                        PerformMouseClick(Constants.MOUSEEVENTF_RIGHTDOWN, Constants.MOUSEEVENTF_RIGHTUP, GetSelectedXPosition(), GetSelectedYPosition());
                        break;
                    case MouseButton.Middle:
                        PerformMouseClick(Constants.MOUSEEVENTF_MIDDLEDOWN, Constants.MOUSEEVENTF_MIDDLEUP, GetSelectedXPosition(), GetSelectedYPosition());
                        break;
                }
            });
        }

        public void PerformMouseClick(int mouseDownAction, int mouseUpAction, int xPos, int yPos)
        {
            for (int i = 0; i < GetNumberOfMouseActions(); ++i)
            {
                var setCursorPos = User32ApiUtils.SetCursorPosition(xPos, yPos);
                if (!setCursorPos)
                {
                    Log.Error($"Could not set the mouse cursor.");
                }

                User32ApiUtils.ExecuteMouseEvent(mouseDownAction | mouseUpAction, xPos, yPos, 0, 0);
            }
        }

        public IntPtr StartStopHooks(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            int hotkeyId = wParam.ToInt32();
            if (msg == Constants.WM_HOTKEY && hotkeyId == Constants.START_HOTKEY_ID || hotkeyId == Constants.STOP_HOTKEY_ID || hotkeyId == Constants.TOGGLE_HOTKEY_ID)
            {
                int virtualKey = ((int)lParam >> 16) & 0xFFFF;
                if (virtualKey == SettingsUtils.CurrentSettings.HotkeySettings.StartHotkey.VirtualKeyCode && CanStartOperation())
                {
                    StartCommand_Execute(null, null);
                }
                if (virtualKey == SettingsUtils.CurrentSettings.HotkeySettings.StopHotkey.VirtualKeyCode && clickTimer.Enabled)
                {
                    StopCommand_Execute(null, null);
                }
                if (virtualKey == SettingsUtils.CurrentSettings.HotkeySettings.ToggleHotkey.VirtualKeyCode && CanStartOperation() | clickTimer.Enabled)
                {
                    ToggleCommand_Execute(null, null);
                }
                handled = true;
            }
            return IntPtr.Zero;
        }

        public void SettingsUtils_HotKeyChangedEvent(object sender, HotkeyChangedEventArgs e)
        {
            Log.Information("HotKeyChangedEvent with operation {Operation} and hotkey {Hotkey}", e.Operation, e.Hotkey.DisplayName);
            switch (e.Operation)
            {
                case Operation.Start:
                    ReRegisterHotkey(Constants.START_HOTKEY_ID, e.Hotkey);
                    startButton.Content = $"{Constants.MAIN_WINDOW_START_BUTTON_CONTENT} ({e.Hotkey.DisplayName})";
                    break;
                case Operation.Stop:
                    ReRegisterHotkey(Constants.STOP_HOTKEY_ID, e.Hotkey);
                    stopButton.Content = $"{Constants.MAIN_WINDOW_STOP_BUTTON_CONTENT} ({e.Hotkey.DisplayName})";
                    break;
                case Operation.Toggle:
                    ReRegisterHotkey(Constants.TOGGLE_HOTKEY_ID, e.Hotkey);
                    toggleButton.Content = $"{Constants.MAIN_WINDOW_TOGGLE_BUTTON_CONTENT} ({e.Hotkey.DisplayName})";
                    break;
                default:
                    Log.Warning("Operation {Operation} not supported!", e.Operation);
                    throw new NotSupportedException($"Operation {e.Operation} not supported!");
            }
        }

        public void SystemTrayIcon_Click(object sender, EventArgs e)
        {
            systemTrayMenu.IsOpen = true;
            systemTrayMenu.Focus();
        }

        public void SystemTrayMenu_SystemTrayMenuActionEvent(object sender, SystemTrayMenuActionEventArgs e)
        {
            switch (e.Action)
            {
                case SystemTrayMenuAction.Show:
                    Show();
                    break;
                case SystemTrayMenuAction.Hide:
                    Hide();
                    break;
                case SystemTrayMenuAction.Exit:
                    Exit();
                    break;
                default:
                    Log.Warning("Action {Action} not supported!", e.Action);
                    throw new NotSupportedException($"Action {e.Action} not supported!");
            }
        }

        public void AboutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (aboutWindow == null)
            {
                aboutWindow = new AboutWindow();
                aboutWindow.Closed += (o, args) => aboutWindow = null;
            }

            aboutWindow.Show();
        }

        public void MinimizeMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Hide();
            systemTrayMenu.ToggleMenuItemsVisibility(true);
        }

        public void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Exit();
        }

        public void RadioButtonSelectedLocationMode_CurrentLocationOnChecked(
            object sender,
            RoutedEventArgs e
        )
        {
            TextBoxPickedXValue.Text = string.Empty;
            TextBoxPickedYValue.Text = string.Empty;
        }

        #endregion Event Handlers

        public void TopMostCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            CheckBox checkbox = (CheckBox)sender;
            Topmost = checkbox.IsChecked.Value;
        }
    }
}