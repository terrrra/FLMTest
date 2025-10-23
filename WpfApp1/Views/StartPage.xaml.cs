using System;
using System.Windows.Controls;

namespace FLMDesktop.Views
{
    /// <summary>
    /// Interaction logic for StartPage.xaml
    /// </summary>
    
    public partial class StartPage : Page, IHasTitle
    {
        private readonly Action<string> _nav;
        public string Title => "Home";

        public StartPage(Action<string> nav)
        {
            InitializeComponent();
            _nav = nav;
        }

        private void Branches_Click(object sender, System.Windows.RoutedEventArgs e) => _nav("Branches");
        private void Assignments_Click(object sender, System.Windows.RoutedEventArgs e) => _nav("Assignments");
    }
}
