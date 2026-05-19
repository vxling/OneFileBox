namespace OneFileBox_new.ViewModels;

public class RepositoryItemViewModel : ViewModelBase
{
    private string _key = string.Empty;
    private string _name = string.Empty;
    private string _path = string.Empty;
    private string _svnUrl = string.Empty;
    private string _userName = string.Empty;
    private string _password = string.Empty;

    public string Key
    {
        get => _key;
        set => SetProperty(ref _key, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Path
    {
        get => _path;
        set => SetProperty(ref _path, value);
    }

    public string SvnUrl
    {
        get => _svnUrl;
        set => SetProperty(ref _svnUrl, value);
    }

    public string UserName
    {
        get => _userName;
        set => SetProperty(ref _userName, value);
    }

    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }
}
