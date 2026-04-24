using XylarBedrock.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows;

namespace XylarBedrock.Backend.Backporting
{
    public interface IBackwardsCommunication
    {
        public DependencyObject ProgressBarGrid { get; }
        public Task<System.Windows.Forms.DialogResult> ShowDialog_YesNo(string? title, string? content);
        public void errormsg(string dialogTitle, string dialogText, Exception ex2);
        public Task<bool> exceptionmsg(Exception ex);
        public void UpdateAnimatePageTransitions(bool value);
    }
}


