using System.Windows;

namespace VHDX_Manager.Views
{
    public partial class InputDialog : Window
    {
        public string InputText { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;

        public InputDialog(string message, string defaultValue = "")
        {
            InitializeComponent();
            Message = message;
            InputText = defaultValue;
            DataContext = this;
            InputTextBox.SelectAll();
            InputTextBox.Focus();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
