using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace dir2tree.UI
{
    /// <summary>
    /// Presentational title bar replacement for the native window chrome.
    /// It only drives window state (drag / minimize / maximize / close is
    /// handled by WindowChrome + the three buttons below); it contains no
    /// application logic and is not referenced by MainWindow.xaml.cs.
    /// </summary>
    public partial class TitleBar : UserControl
    {
        private const string MaximizeGlyph = "\uE922";
        private const string RestoreGlyph = "\uE923";

        // DWMWA_WINDOW_CORNER_PREFERENCE (Windows 11)
        private const int DwmwaWindowCornerPreference = 33;
        private const int DwmWindowCornerRound = 2;

        public TitleBar()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private Window HostWindow
        {
            get { return Window.GetWindow(this); }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Window window = HostWindow;
            if (window == null)
            {
                return;
            }

            window.StateChanged -= OnWindowStateChanged;
            window.StateChanged += OnWindowStateChanged;

            UpdateMaximizeButton();
            ApplyRoundedWindowCorners(window);
        }

        private void OnWindowStateChanged(object sender, EventArgs e)
        {
            UpdateMaximizeButton();
        }

        private void UpdateMaximizeButton()
        {
            Window window = HostWindow;
            if (window == null)
            {
                return;
            }

            bool isMaximized = window.WindowState == WindowState.Maximized;
            BtnMaximize.Content = isMaximized ? RestoreGlyph : MaximizeGlyph;
            BtnMaximize.ToolTip = isMaximized ? "Restore Down" : "Maximize";
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            Window window = HostWindow;
            if (window != null)
            {
                window.WindowState = WindowState.Minimized;
            }
        }

        private void BtnMaximize_Click(object sender, RoutedEventArgs e)
        {
            Window window = HostWindow;
            if (window == null)
            {
                return;
            }

            window.WindowState = window.WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Window window = HostWindow;
            if (window != null)
            {
                window.Close();
            }
        }

        /// <summary>
        /// Asks DWM for the Windows 11 rounded window corners. On older
        /// systems the call simply fails and the window keeps square corners.
        /// </summary>
        private static void ApplyRoundedWindowCorners(Window window)
        {
            try
            {
                IntPtr handle = new WindowInteropHelper(window).Handle;
                if (handle == IntPtr.Zero)
                {
                    return;
                }

                int preference = DwmWindowCornerRound;
                DwmSetWindowAttribute(handle, DwmwaWindowCornerPreference, ref preference,
                    sizeof(int));
            }
            catch (DllNotFoundException)
            {
                // dwmapi.dll is missing - keep the default corners.
            }
            catch (EntryPointNotFoundException)
            {
                // Attribute not supported by this DWM version.
            }
        }

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute,
            ref int attributeValue, int attributeSize);
    }
}
