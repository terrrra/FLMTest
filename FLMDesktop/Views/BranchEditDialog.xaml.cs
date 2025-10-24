using System;
using System.Windows;
using FLMDesktop.Models;

namespace FLMDesktop.Views
{
    public partial class BranchEditDialog : Window
    {
        public Branch Branch { get; private set; }

        // Designer-friendly ctor
        public BranchEditDialog()
            : this(new Branch
            {
                Name = "Preview",
                TelephoneNumber = "",
                OpenDate = DateTime.Today
            })
        { }

        // Runtime ctor
        public BranchEditDialog(Branch branch)
        {
            InitializeComponent();

            Branch = branch ?? throw new ArgumentNullException(nameof(branch));
            TxtName.Text = Branch.Name;
            TxtTel.Text = Branch.TelephoneNumber ?? string.Empty;
            DpOpen.SelectedDate = Branch.OpenDate;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtName.Text))
            {
                MessageBox.Show("Name is required.");
                return;
            }

            Branch.Name = TxtName.Text.Trim();
            Branch.TelephoneNumber = string.IsNullOrWhiteSpace(TxtTel.Text) ? null : TxtTel.Text.Trim();
            Branch.OpenDate = DpOpen.SelectedDate;

            DialogResult = true;
        }
    }
}
