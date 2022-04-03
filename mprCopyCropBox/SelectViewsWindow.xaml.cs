namespace mprCopyCropBox;

using System.Windows;

/// <summary>
/// Логика взаимодействия для SelectViewsWindow.xaml
/// </summary>
public partial class SelectViewsWindow
{
    public SelectViewsWindow()
    {
        InitializeComponent();
        Title = ModPlusAPI.Language.GetPluginLocalName(ModPlusConnector.Instance);
    }

    private void AcceptButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}