using Avalonia.Controls;
using Avalonia.Interactivity;

namespace OneFileBox.Views;

public partial class InputDialog : Window
{
    public string InputText { get; set; } = "";
    public string LabelText { get; set; } = "";
    public new string Title { get; set; } = "Input";

    public InputDialog()
    {
        InitializeComponent();

        PromptText.Text = LabelText;
        InputTextBox.Text = InputText;
        Title = Title;
    }

    private void OK_Click(object? sender, RoutedEventArgs e)
    {
        Close(InputTextBox.Text);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }
}