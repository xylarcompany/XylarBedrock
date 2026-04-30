using System.Windows;

namespace XylarBedrock.Pages.Toolbar
{
    public partial class Toolbar_AddonsButton : Toolbar_ButtonBase
    {
        public Toolbar_AddonsButton()
        {
            InitializeComponent();
        }

        private void SideBarButton_Click(object sender, RoutedEventArgs e)
        {
            ToolbarButtonBase_Click(this, e);
        }
    }
}
