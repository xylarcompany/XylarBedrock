using System;
using System.Linq;
using System.Windows.Controls;
using XylarBedrock.Classes;
using XylarBedrock.Pages.Preview.Profile;
using XylarBedrock.ViewModels;

namespace XylarBedrock.Pages.Welcome
{
    public partial class WelcomePageThree : Page
    {
        public WelcomePagesSwitcher pageSwitcher = new WelcomePagesSwitcher();
        private Component_AddProfileContainer profileControl;

        public WelcomePageThree()
        {
            InitializeComponent();
            BuildProfileControl();
        }

        private void BuildProfileControl()
        {
            BLProfile editableProfile =
                MainDataModel.Default.Config?.CurrentProfile ??
                MainDataModel.Default.Config?.profiles?.Values?.FirstOrDefault();

            profileControl = editableProfile != null
                ? new Component_AddProfileContainer(editableProfile)
                : new Component_AddProfileContainer();

            profileControl.GoBack += ProfileControl_GoBack;
            profileControl.Confirm += ProfileControl_Confirm;
            ProfileControlHost.Children.Clear();
            ProfileControlHost.Children.Add(profileControl);
        }

        private void ProfileControl_GoBack(object sender, EventArgs e)
        {
            pageSwitcher.MoveToPage(2);
        }

        private void ProfileControl_Confirm(object sender, EventArgs e)
        {
            pageSwitcher.MoveToPage(5);
        }
    }
}
