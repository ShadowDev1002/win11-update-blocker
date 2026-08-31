namespace Win11UpdateBlocker.ViewModels;

public sealed class UpdateCategoryViewModel : ViewModelBase
{
    private bool _isAllowed = true;

    public UpdateCategoryViewModel(string key, string title, string description, bool isAllowed = true)
    {
        Key = key;
        Title = title;
        Description = description;
        _isAllowed = isAllowed;
    }

    public string Key { get; }

    public string Title { get; }

    public string Description { get; }

    public bool IsAllowed
    {
        get => _isAllowed;
        set => SetProperty(ref _isAllowed, value);
    }
}
