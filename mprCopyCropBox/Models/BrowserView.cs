namespace mprCopyCropBox.Models;

using Autodesk.Revit.DB;
using JetBrains.Annotations;
using ModPlusAPI.Mvvm;

/// <summary>
/// Вид в браузере
/// </summary>
public class BrowserView : ObservableObject, IBrowserItem
{
    private bool _isChecked;
    private bool _isVisible = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="BrowserView"/> class.
    /// </summary>
    /// <param name="view"><see cref="View"/></param>
    /// <param name="parentGroup"><see cref="BrowserGroup"/></param>
    public BrowserView(View view, [CanBeNull] BrowserGroup parentGroup)
    {
        Name = view.Name;
        View = view;
        ParentGroup = parentGroup;
    }

    /// <summary>
    /// Родительская группа
    /// </summary>
    public BrowserGroup ParentGroup { get; }

    /// <summary>
    /// Вид
    /// </summary>
    public View View { get; }

    /// <summary>
    /// Исходное имя
    /// </summary>
    public string Name { get; }
    
    /// <inheritdoc/>
    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (_isChecked == value)
                return;
            _isChecked = value;
            OnPropertyChanged();
        }
    }

    /// <inheritdoc/>
    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible == value)
                return;
            _isVisible = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Развернуть предков
    /// </summary>
    public void ExpandParents()
    {
        var parent = ParentGroup;
        while (parent != null)
        {
            parent.IsExpanded = true;
            parent = parent.ParentGroup;
        }
    }
}