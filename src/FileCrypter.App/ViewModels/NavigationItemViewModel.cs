using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FileCrypter.App.ViewModels;

public sealed partial class NavigationItemViewModel : ObservableObject
{
    private readonly Action<NavigationItemViewModel> select;

    [ObservableProperty]
    private bool isSelected;

    public NavigationItemViewModel(string key, string title, string iconPathData, Action<NavigationItemViewModel> select)
    {
        Key = key;
        Title = title;
        IconPathData = iconPathData;
        this.select = select;
    }

    public string Key { get; }

    public string Title { get; }

    public string IconPathData { get; }

    [RelayCommand]
    private void Select()
    {
        select(this);
    }
}
