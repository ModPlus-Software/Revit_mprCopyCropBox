namespace mprCopyCropBox.Models;

/// <summary>
/// Элемент дерева
/// </summary>
public interface IBrowserItem 
{
    /// <summary>
    /// Элемент отмечен
    /// </summary>
    bool IsChecked { get; set; }

    /// <summary>
    /// Видимость элемента
    /// </summary>
    bool IsVisible { get; set; }
}