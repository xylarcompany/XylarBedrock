using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using XylarBedrock.Enums;
using XylarBedrock.Handlers;
using XylarBedrock.Pages.Preview;
using XylarBedrock.Pages.Preview.Installation;
using XylarBedrock.ViewModels;

namespace XylarBedrock.Pages.Play.Installations
{
    public partial class InstallationsScreen : Page
    {

        public InstallationsScreen()
        {
            InitializeComponent();
            this.DataContext = MainDataModel.Default;
        }
        public void RefreshInstallations()
        {
            this.Dispatcher.Invoke(() =>
            {
                if (InstallationsList != null) FilterSortingHandler.Sort_InstallationList(InstallationsList.ItemsSource);
            });
        }
        private void PageHost_Loaded(object sender, RoutedEventArgs e)
        {
            this.RefreshInstallations();
        }

        private void InstallationsList_SourceUpdated(object sender, DataTransferEventArgs e)
        {
            this.RefreshInstallations();
        }

        private void CollectionViewSource_Filter(object sender, FilterEventArgs e)
        {
            e.Accepted = Handlers.FilterSortingHandler.Filter_InstallationList(e.Item);
        }
    }
}

