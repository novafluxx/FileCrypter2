namespace FileCrypter.App.ViewModels;

public sealed class HelpTopicViewModel
{
    public HelpTopicViewModel(string title, string description)
    {
        Title = title;
        Description = description;
    }

    public string Title { get; }

    public string Description { get; }
}
