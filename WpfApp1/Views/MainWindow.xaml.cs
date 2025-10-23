
using FLMDesktop.Views;
using System.Windows;


namespace FLMDesktop
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            MainFrame.Navigated += (_, e) => TitleBlock.Text = (e.Content as IHasTitle)?.Title ?? "Home";
            MainFrame.Navigate(new StartPage(NavigateTo));
        }

        public void NavigateTo(string target)
        {
            switch (target)
            {
                case "Branches":
                    MainFrame.Navigate(new BranchesPage());
                    break;
                case "Assignments":
                    MainFrame.Navigate(new ProductsAssignmentsPage());
                    break;
                default:
                    MainFrame.Navigate(new StartPage(NavigateTo));
                    break;
            }
        }
    }

    public interface IHasTitle { string Title { get; } }
}
