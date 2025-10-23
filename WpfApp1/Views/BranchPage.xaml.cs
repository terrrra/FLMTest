using FLMDesktop.Infrastructure;
using FLMDesktop.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using FLMDesktop.Services;

namespace FLMDesktop.Views
{
    /// <summary>
    /// Interaction logic for BranchPage.xaml
    /// </summary>
    public partial class BranchesPage : Page, IHasTitle
    {
        public string Title => "Branches";

        private ObservableCollection<Branch> _all = new();
        private ObservableCollection<Branch> _view = new();

        private ObservableCollection<Branch> _items = new();
        public BranchesPage()
        {
            InitializeComponent();
            Loaded += async (_, __) => await LoadAsync();
            //Seed(); I did this to test the BranchGrid to test populate task
            
        }
        private async Task LoadAsync()
        {
            try
            {
                var list = await AppServices.Branches.GetAllAsync();
                _items = new ObservableCollection<Branch>((System.Collections.Generic.IEnumerable<Branch>)list);
                BranchesGrid.ItemsSource = _items;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading branches: {ex.GetBaseException().Message}", "Load",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void Search_Click(object sender, RoutedEventArgs e)
        {
            var q = (SearchBox.Text ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(q))
                _view = new ObservableCollection<Branch>(_all);
            else
                _view = new ObservableCollection<Branch>(_all.Where(b =>
                    (b.Name?.ToLowerInvariant().Contains(q) ?? false) ||
                    (b.TelephoneNumber?.ToLowerInvariant().Contains(q) ?? false)));

            BranchesGrid.ItemsSource = _view;
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            // Navigate back to StartPage
            var mainWindow = (FLMDesktop.MainWindow)Application.Current.MainWindow;
            mainWindow?.MainFrame.Navigate(new StartPage(mainWindow.NavigateTo));
        }

        // --- Toolbar actions ---

        private async void New_Click(object sender, RoutedEventArgs e)
        {
            var draft = new Branch
            {
                Name = "New Branch",
                OpenDate = DateTime.Today
            };

            var dlg = new BranchEditDialog(draft) { Owner = Window.GetWindow(this) };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    await AppServices.Branches.CreateAsync(draft);
                    await LoadAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Save failed: {ex.GetBaseException().Message}", "Create",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void Edit_Click(object sender, RoutedEventArgs e)
        {
            if (BranchesGrid.SelectedItem is not Branch selected)
            {
                MessageBox.Show("Select a branch to edit."); return;
            }

            // fetch fresh copy from DB
            var entity = await AppServices.Branches.GetByIdAsync(selected.Id);
            if (entity is null) { MessageBox.Show("That branch no longer exists."); await LoadAsync(); return; }

            var dlg = new BranchEditDialog(entity) { Owner = Window.GetWindow(this) };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    await AppServices.Branches.UpdateAsync(entity);
                    await LoadAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Save failed: {ex.GetBaseException().Message}", "Update",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (BranchesGrid.SelectedItem is not Branch selected)
            {
                MessageBox.Show("Select a branch to delete."); return;
            }

            if (MessageBox.Show($"Delete '{selected.Name}'?", "Confirm delete",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                await AppServices.Branches.DeleteAsync(selected.Id);
                await LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Delete failed: {ex.GetBaseException().Message}", "Delete",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ImportBranches_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "All Supported|*.csv;*.json;*.xml|CSV|*.csv|JSON|*.json|XML|*.xml" };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var svc = new ImportExportService(AppServices.ConnectionString);
                var count = await svc.ImportBranchesAsync(dlg.FileName);      // upsert
                await LoadAsync();
                MessageBox.Show($"Imported {count} branches.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Import failed: {ex.GetBaseException().Message}");
            }
        }

        private async void ExportBranches_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog { Filter = "CSV|*.csv|JSON|*.json|XML|*.xml", FileName = "Branch" };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var svc = new ImportExportService(AppServices.ConnectionString);
                var count = await svc.ExportBranchesAsync(dlg.FileName);
                MessageBox.Show($"Exported {count} branches to {System.IO.Path.GetFileName(dlg.FileName)}.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.GetBaseException().Message}");
            }
        }
    }
}
