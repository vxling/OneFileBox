using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Interactivity;
using OneFileBox_new.ViewModels;

namespace OneFileBox_new.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
        AddHandler(DragDrop.DropEvent, OnDrop);

        // 注入 HostTopLevel，使 ViewModel 能访问 StorageProvider
        if (DataContext is MainWindowViewModel vm)
            vm.HostTopLevel = TopLevel.GetTopLevel(this);
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (DataTransferExtensions.Contains(e.DataTransfer, DataFormat.File))
            e.DragEffects = DragDropEffects.Copy;
        else
            e.DragEffects = DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (!DataTransferExtensions.Contains(e.DataTransfer, DataFormat.File)) return;

        var items = DataTransferExtensions.TryGetFiles(e.DataTransfer);
        if (items == null || items.Length == 0) return;

        var paths = new List<string>();
        foreach (var item in items)
            paths.Add(item.Path.LocalPath);

        if (DataContext is MainWindowViewModel vm && paths.Count > 0)
            await vm.HandleDroppedFiles(paths.ToArray());

        e.Handled = true;
    }
}
