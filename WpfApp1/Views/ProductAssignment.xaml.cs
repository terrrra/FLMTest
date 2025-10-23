using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using FLMDesktop.Infrastructure;
using FLMDesktop.Models;

namespace FLMDesktop.Views
{
    public partial class ProductsAssignmentsPage : Page, IHasTitle
    {
        public string Title => "Products & Assignments";

        private ObservableCollection<Branch> _branches = new();
        private ObservableCollection<Product> _allProds = new();
        private ObservableCollection<Product> _assigned = new();

        public ProductsAssignmentsPage()
        {
            InitializeComponent();
            Loaded += async (_, __) => await LoadAllAsync();
        }

        private async Task LoadAllAsync()
        {
            try
            {
                // branches
                var b = await AppServices.Branches.GetAllAsync();
                _branches = new ObservableCollection<Branch>(b);
                BranchesGrid.ItemsSource = _branches;

                // all products
                var p = await AppServices.Products.GetAllAsync();
                _allProds = new ObservableCollection<Product>(p);
                AllProductsGrid.ItemsSource = _allProds;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Load failed: {ex.GetBaseException().Message}");
            }

            SelectedBranchText.Text = "";
            SelectedProductText.Text = "";
        }

        private async Task LoadAssignedAsync()
        {
            if (BranchesGrid.SelectedItem is not Branch br) { _assigned.Clear(); AssignedGrid.ItemsSource = _assigned; return; }

            try
            {
                var list = await AppServices.Assignments.GetProductsForBranchAsync(br.Id);
                _assigned = new ObservableCollection<Product>(list);
                AssignedGrid.ItemsSource = _assigned;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Load assignments failed: {ex.GetBaseException().Message}");
            }
        }

        // ==== Events ====

        private async void BranchesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (BranchesGrid.SelectedItem is Branch br)
                SelectedBranchText.Text = $"{br.Id} - {br.Name}";
            else
                SelectedBranchText.Text = "";

            await LoadAssignedAsync();
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is MainWindow main)
                main.MainFrame.Navigate(new StartPage(main.NavigateTo));
        }

        // Search
        private async void BranchSearch_Click(object sender, RoutedEventArgs e)
        {
            var q = (BranchSearch.Text ?? "").Trim().ToLowerInvariant();
            var all = await AppServices.Branches.GetAllAsync();
            var filtered = string.IsNullOrWhiteSpace(q) ? all :
                all.Where(b => (b.Name?.ToLowerInvariant().Contains(q) ?? false) ||
                               (b.TelephoneNumber?.ToLowerInvariant().Contains(q) ?? false))
                   .ToList();
            _branches = new ObservableCollection<Branch>(filtered);
            BranchesGrid.ItemsSource = _branches;
        }

        private async void ProductSearch_Click(object sender, RoutedEventArgs e)
        {
            var q = (ProductSearch.Text ?? "").Trim().ToLowerInvariant();
            var all = await AppServices.Products.GetAllAsync();
            var filtered = string.IsNullOrWhiteSpace(q) ? all :
                all.Where(p => (p.Name?.ToLowerInvariant().Contains(q) ?? false))
                   .ToList();
            _allProds = new ObservableCollection<Product>(filtered);
            AllProductsGrid.ItemsSource = _allProds;
        }

        // Assign/Unassign
        private async void Assign_Click(object sender, RoutedEventArgs e)
        {
            if (BranchesGrid.SelectedItem is not Branch br)
            {
                MessageBox.Show("Select a branch."); return;
            }
            if (AllProductsGrid.SelectedItem is not Product p)
            {
                MessageBox.Show("Select a product from All Products."); return;
            }

            try
            {
                await AppServices.Assignments.AddAssignmentAsync(br.Id, p.Id);
                await LoadAssignedAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Assign failed: {ex.GetBaseException().Message}");
            }
        }

        private async void Unassign_Click(object sender, RoutedEventArgs e)
        {
            if (BranchesGrid.SelectedItem is not Branch br)
            {
                MessageBox.Show("Select a branch."); return;
            }
            if (AssignedGrid.SelectedItem is not Product p)
            {
                MessageBox.Show("Select a product from Assigned."); return;
            }

            try
            {
                await AppServices.Assignments.RemoveAssignmentAsync(br.Id, p.Id);
                await LoadAssignedAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unassign failed: {ex.GetBaseException().Message}");
            }
        }

        // CRUD for single product
        private void UpdateSelectedProductLabel()
        {
            if (AllProductsGrid.SelectedItem is Product p)
                SelectedProductText.Text = $"{p.Id} - {p.Name}  |  {(p.WeightedItem ? "Weighted" : "Unit")}  |  {p.SuggestedSellingPrice:C}";
            else if (AssignedGrid.SelectedItem is Product ap)
                SelectedProductText.Text = $"{ap.Id} - {ap.Name}  |  {(ap.WeightedItem ? "Weighted" : "Unit")}  |  {ap.SuggestedSellingPrice:C}";
            else
                SelectedProductText.Text = "";
        }

        private async Task RefreshProductsAsync()
        {
            var p = await AppServices.Products.GetAllAsync();
            _allProds = new ObservableCollection<Product>(p);
            AllProductsGrid.ItemsSource = _allProds;
            UpdateSelectedProductLabel();
        }

        private async void ProdNew_Click(object sender, RoutedEventArgs e)
        {
            var draft = new Product
            {
                Name = "New Product",
                WeightedItem = false,
                SuggestedSellingPrice = 0m
            };
            var dlg = new ProductEditDialog(draft) { Owner = Window.GetWindow(this) };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    await AppServices.Products.CreateAsync(draft);
                    await RefreshProductsAsync();
                    await LoadAssignedAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Create product failed: {ex.GetBaseException().Message}");
                }
            }
        }

        private async void ProdEdit_Click(object sender, RoutedEventArgs e)
        {
            if (AllProductsGrid.SelectedItem is not Product p)
            {
                MessageBox.Show("Select a product to edit.");
                return;
            }

            // fetch fresh
            var fresh = await AppServices.Products.GetByIdAsync(p.Id);
            if (fresh is null)
            {
                MessageBox.Show("Product not found.");
                await RefreshProductsAsync();
                return;
            }

            var dlg = new ProductEditDialog(fresh) { Owner = Window.GetWindow(this) };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    await AppServices.Products.UpdateAsync(fresh);
                    await RefreshProductsAsync();
                    await LoadAssignedAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Update product failed: {ex.GetBaseException().Message}");
                }
            }
        }

        private async void ProdDelete_Click(object sender, RoutedEventArgs e)
        {
            if (AllProductsGrid.SelectedItem is not Product p)
            {
                MessageBox.Show("Select a product to delete.");
                return;
            }

            if (MessageBox.Show($"Delete product '{p.Name}'?", "Confirm",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                await AppServices.Products.DeleteAsync(p.Id);
                await RefreshProductsAsync();
                await LoadAssignedAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Delete product failed: {ex.GetBaseException().Message}");
            }
        }

        // Keep labels updated when selection changes
        private void AllProductsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateSelectedProductLabel();
        private void AssignedGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateSelectedProductLabel();
    }
}
