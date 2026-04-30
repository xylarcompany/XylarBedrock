using XylarBedrock.UI.Interfaces;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace XylarBedrock.UI.Pages.Common
{
    public partial class ErrorScreen : Page
    {
        public IDialogHander Handler { get; private set; }

        public ErrorScreen()
        {
            InitializeComponent();
        }

        public ErrorScreen(IDialogHander _hander)
        {
            InitializeComponent();
            Handler = _hander;
        }

        private void ErrorScreenCloseButton_Click(object sender, RoutedEventArgs e)
        {
            Handler?.SetDialogFrame(null);
        }
    }

    public static class ErrorScreenShow
    {
        public static IDialogHander Handler { get; private set; }

        public static void SetHandler(IDialogHander _hander)
        {
            Handler = _hander;
        }

        public static Task<bool> exceptionmsg(string title, Exception error = null)
        {
            SuppressDialog(title, error?.Message, error);
            return Task.FromResult(true);
        }

        public static Task<bool> exceptionmsg(Exception error = null)
        {
            SuppressDialog(error?.HResult.ToString() ?? "ERROR", error?.Message, error);
            return Task.FromResult(true);
        }

        public static void errormsg(string title, string message, Exception e = null)
        {
            SuppressDialog(title, message, e);
        }

        private static void SuppressDialog(string title, string message, Exception error)
        {
            try
            {
                Trace.WriteLine($"Suppressed launcher error dialog: {title} - {message}");
                if (error != null)
                {
                    Trace.WriteLine(error.ToString());
                }

                Application.Current?.Dispatcher.Invoke(() => Handler?.SetDialogFrame(null));
            }
            catch
            {
            }
        }
    }
}
