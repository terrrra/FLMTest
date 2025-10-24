using System;
using System.Globalization;
using System.Windows;
using FLMDesktop.Models;

namespace FLMDesktop.Views
{
    public partial class ProductEditDialog : Window
    {
        public Product Product { get; }

        public ProductEditDialog()
            : this(new Product { Name = "Preview", WeightedItem = false, SuggestedSellingPrice = 0m })
        { }

        public ProductEditDialog(Product product)
        {
            InitializeComponent();
            Product = product ?? throw new ArgumentNullException(nameof(product));

            TxtName.Text = Product.Name;
            ChkWeighted.IsChecked = Product.WeightedItem;
            TxtPrice.Text = Product.SuggestedSellingPrice.ToString("0.00", CultureInfo.InvariantCulture);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtName.Text))
            {
                MessageBox.Show("Name is required."); return;
            }

            if (!decimal.TryParse(TxtPrice.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var price) || price < 0)
            {
                MessageBox.Show("Enter a valid non-negative price, e.g. 9.99"); return;
            }

            Product.Name = TxtName.Text.Trim();
            Product.WeightedItem = ChkWeighted.IsChecked == true;
            Product.SuggestedSellingPrice = price;

            DialogResult = true;
        }
    }
}
