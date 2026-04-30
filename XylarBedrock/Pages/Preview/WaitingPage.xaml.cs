using System;
using System.Windows;
using System.Windows.Controls;
using XylarBedrock.UI.Interfaces;

namespace XylarBedrock.Pages.Preview
{
    public partial class WaitingPage : Page, IDisposable
    {

        public IDialogHander Handler { get; private set; }


        public WaitingPage()
        {
            InitializeComponent();
        }

        public WaitingPage(IDialogHander _hander)
        {
            InitializeComponent();
            Handler = _hander;
        }

        private void ErrorScreenCloseButton_Click(object sender, RoutedEventArgs e)
        {
            Handler.SetDialogFrame(null);
        }

        private void ErrorScreenViewCrashButton_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("notepad.exe", $@"{Environment.CurrentDirectory}\Log.txt");
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {

        }
    }
}

