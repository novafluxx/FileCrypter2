using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FileCrypter.App.ViewModels;

public sealed partial class NavigationItemViewModel : ObservableObject
{
    private readonly Action<NavigationItemViewModel> select;

    [ObservableProperty]
    private bool isSelected;

    public NavigationItemViewModel(string key, string title, Action<NavigationItemViewModel> select)
    {
        Key = key;
        Title = title;
        this.select = select;
    }

    public string Key { get; }

    public string Title { get; }

    [RelayCommand]
    private void Select()
    {
        select(this);
    }
}
