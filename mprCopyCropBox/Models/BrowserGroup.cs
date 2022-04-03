namespace mprCopyCropBox.Models;

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using JetBrains.Annotations;
using ModPlusAPI.IO;
using ModPlusAPI.Mvvm;

/// <summary>
/// Группа в браузере
/// </summary>
public class BrowserGroup : ObservableObject, IBrowserItem
{
    private bool _isVisible = true;
    private bool _isChecked;
    private bool _isExpanded;

    /// <summary>
    /// Конструктор
    /// </summary>
    /// <param name="name">Имя группы</param>
    /// <param name="parentGroup">Родительская группа</param>
    /// <param name="level">Уровень группы в дереве</param>
    public BrowserGroup(string name, [CanBeNull] BrowserGroup parentGroup, int level)
    {
        Name = name;
        ParentGroup = parentGroup;
        Level = level;
        SubItems = new ObservableCollection<IBrowserItem>();
        SubItems.CollectionChanged += SubItemsOnCollectionChanged;
    }

    /// <summary>
    /// Для реализации поиска
    /// </summary>
    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (value == _isVisible)
                return;
            _isVisible = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Имя группы
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Родительская группа
    /// </summary>
    [CanBeNull]
    public BrowserGroup ParentGroup { get; }

    /// <summary>
    /// Уровень группы в дереве
    /// </summary>
    public int Level { get; }

    /// <summary>
    /// Виды в группе
    /// </summary>
    public ObservableCollection<IBrowserItem> SubItems { get; private set; }

    /// <summary>
    /// Выбраны ли все листы в группе
    /// </summary>
    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (value == _isChecked)
                return;
            _isChecked = value;
            foreach (var browserItem in SubItems)
                browserItem.IsChecked = value;

            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Развернута ли группа
    /// </summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            _isExpanded = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Сортировать виды в группе
    /// </summary>
    /// <param name="sortOrder">Порядок сортировки</param>
    public void SortViews(SortOrder sortOrder)
    {
        if (SubItems.Any())
        {
            var ordinalStringComparer = new OrdinalStringComparer();
            if (SubItems.First() is BrowserView)
            {
                var sheets = SubItems.OfType<BrowserView>();
                SubItems = sortOrder == SortOrder.Ascending
                    ? new ObservableCollection<IBrowserItem>(sheets.OrderBy(s => s.Name, ordinalStringComparer))
                    : new ObservableCollection<IBrowserItem>(sheets.OrderByDescending(s => s.Name, ordinalStringComparer));
            }
            else
            {
                var groups = SubItems.Where(i => i is BrowserGroup).Cast<BrowserGroup>().ToList();
                groups.ForEach(g => g.SortViews(sortOrder));
                SubItems = sortOrder == SortOrder.Ascending
                    ? new ObservableCollection<IBrowserItem>(groups.OrderBy(g => g.Name, ordinalStringComparer))
                    : new ObservableCollection<IBrowserItem>(groups.OrderByDescending(g => g.Name, ordinalStringComparer));
            }
        }
    }

    private void SubItemsOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add)
        {
            foreach (var item in e.NewItems)
            {
                if (item is BrowserView browserView)
                    browserView.PropertyChanged += SubItemOnPropertyChanged;
                if (item is BrowserGroup browserGroup)
                    browserGroup.PropertyChanged += SubItemOnPropertyChanged;
            }
        }
    }

    private void SubItemOnPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IBrowserItem.IsVisible))
        {
            IsVisible = SubItems.Any(s => s.IsVisible);
        }
        else if (e.PropertyName == nameof(IBrowserItem.IsChecked))
        {
            _isChecked = SubItems.All(i => i.IsChecked);
            OnPropertyChanged(nameof(IsChecked));
        }
    }
}