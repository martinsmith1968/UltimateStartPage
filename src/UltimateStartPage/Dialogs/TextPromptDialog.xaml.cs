using System.Windows;

namespace UltimateStartPage.Dialogs
{
    public partial class TextPromptDialog
    {
        public TextPromptDialog(string title, string prompt, string initialValue)
        {
            InitializeComponent();
            Title = title;
            PromptText.Text = prompt;
            InputBox.Text = initialValue;
            InputBox.SelectAll();
        }

        public string Value => InputBox.Text;

        private void OnOk(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(InputBox.Text))
            {
                InputBox.Focus();
                return;
            }

            DialogResult = true;
        }
    }
}
