using Avalonia.Controls;
using Avalonia.Input;
using FileCrypter.App.Services;
using FileCrypter.App.ViewModels;

namespace FileCrypter.App.Views;

public partial class DecryptView : UserControl
{
    public DecryptView()
    {
        InitializeComponent();
        DragDrop.SetAllowDrop(SourceDropZone, true);
        DragDrop.AddDragOverHandler(SourceDropZone, OnSourceDragOver);
        DragDrop.AddDropHandler(SourceDropZone, OnSourceDrop);
    }

    private void OnSourceDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = CanAcceptDrop() && FileDropDataHelper.GetLocalFilePaths(e.DataTransfer).Count > 0
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnSourceDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is DecryptViewModel viewModel)
        {
            viewModel.ApplyDroppedSourcePaths(FileDropDataHelper.GetLocalFilePaths(e.DataTransfer));
        }

        e.Handled = true;
    }

    private bool CanAcceptDrop()
    {
        return DataContext is DecryptViewModel { IsRunning: false };
    }
}
