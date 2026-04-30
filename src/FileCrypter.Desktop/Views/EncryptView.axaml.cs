using Avalonia.Controls;
using Avalonia.Input;
using FileCrypter.Desktop.Services;
using FileCrypter.Desktop.ViewModels;

namespace FileCrypter.Desktop.Views;

public partial class EncryptView : UserControl
{
    public EncryptView()
    {
        InitializeComponent();
        DragDrop.SetAllowDrop(SourceDropZone, true);
        DragDrop.AddDragOverHandler(SourceDropZone, OnSourceDragOver);
        DragDrop.AddDropHandler(SourceDropZone, OnSourceDrop);
        SourceDropZone.PointerReleased += OnSourcePointerReleased;
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
        if (DataContext is EncryptViewModel viewModel)
        {
            viewModel.ApplyDroppedSourcePaths(FileDropDataHelper.GetLocalFilePaths(e.DataTransfer));
        }

        e.Handled = true;
    }

    private void OnSourcePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Left || IsInteractiveChildClick(e.Source))
        {
            return;
        }

        if (DataContext is EncryptViewModel viewModel && viewModel.BrowseSourceCommand.CanExecute(null))
        {
            _ = viewModel.BrowseSourceCommand.ExecuteAsync(null);
            e.Handled = true;
        }
    }

    private bool CanAcceptDrop()
    {
        return DataContext is EncryptViewModel { IsRunning: false };
    }

    private static bool IsInteractiveChildClick(object? source)
    {
        for (Control? current = source as Control; current is not null; current = current.Parent as Control)
        {
            if (current is Button)
            {
                return true;
            }
        }

        return false;
    }
}
