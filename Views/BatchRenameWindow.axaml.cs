using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace OneFileBox.Views;

public class RenamePreviewItem
{
    public string OldName { get; set; } = "";
    public string NewName { get; set; } = "";
}

public partial class BatchRenameWindow : Window
{
    public string ResultBaseName { get; private set; } = "";
    public ObservableCollection<RenamePreviewItem> Previews { get; } = new();

    private readonly List<string> _oldPaths;
    private readonly string _extension;

    public BatchRenameWindow(List<string> selectedPaths)
    {
        InitializeComponent();
        PreviewList.ItemsSource = Previews;

        _oldPaths = selectedPaths;
        _extension = Path.GetExtension(selectedPaths.FirstOrDefault() ?? "");

        // Default placeholder in textbox
        BaseNameTextBox.Text = "";
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        var baseName = BaseNameTextBox?.Text?.Trim() ?? "";
        Previews.Clear();

        if (string.IsNullOrEmpty(baseName)) return;

        for (int i = 0; i < _oldPaths.Count; i++)
        {
            var oldName = Path.GetFileName(_oldPaths[i]);
            var newName = $"{baseName} ({i + 1}){_extension}";
            Previews.Add(new RenamePreviewItem { OldName = oldName, NewName = newName });
        }
    }

    private async void OK_Click(object? sender, RoutedEventArgs e)
    {
        var baseName = BaseNameTextBox?.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(baseName))
        {
            Close(null);
            return;
        }
        ResultBaseName = baseName;
        Close(baseName);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(null);

    private void OnTextChanged(object? sender, Avalonia.Controls.TextChangedEventArgs e) => UpdatePreview();
}