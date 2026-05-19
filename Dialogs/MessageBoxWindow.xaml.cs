#nullable enable

namespace SVNFileBox.Windows;

public enum MessageBoxIconType { Info, Warning, Error, Question, Success }

public enum MessageBoxButtonType { OK, YesNo, OKCancel, YesNoCancel }

public enum MessageBoxResult { None, OK, Yes, No, Cancel }

public partial class MsgBox
{
    public MsgBox()
    {
    }

    public static MessageBoxResult Show(
        object? owner,
        string message,
        string title,
        MessageBoxButtonType buttons = MessageBoxButtonType.OK,
        MessageBoxIconType icon = MessageBoxIconType.Info)
    {
        return MessageBoxResult.None;
    }
}
