namespace mprCopyCropBox;

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Autodesk.Revit.DB;
using Models;
using ModPlusAPI;
using ModPlusAPI.IO;
using ModPlusAPI.Mvvm;

public class SelectViewsContext : ObservableObject
{
    /// <summary>
    /// Группы видов
    /// </summary>
    public ObservableCollection<BrowserGroup> SheetGroups { get; private set; } = new ();

    /// <summary>
    /// Копировать параметры видимости границ обрезки
    /// </summary>
    public bool CopyCropVisibilitySettings
    {
        get => bool.TryParse(UserConfigFile.GetValue(nameof(mprCopyCropBox), nameof(CopyCropVisibilitySettings)), out var b) && b;
        set => UserConfigFile.SetValue(nameof(mprCopyCropBox), nameof(CopyCropVisibilitySettings), value.ToString(), true);
    }

    /// <summary>
    /// Копировать границы 3D видов
    /// </summary>
    public bool Copy3DViewBox
    {
        get => bool.TryParse(UserConfigFile.GetValue(nameof(mprCopyCropBox), nameof(Copy3DViewBox)), out var b) && b;
        set => UserConfigFile.SetValue(nameof(mprCopyCropBox), nameof(Copy3DViewBox), value.ToString(), true);
    }

    public void BuildTree(Document doc, ICollection<View> viewsToAdd)
    {
        var browserOrganization = BrowserOrganization.GetCurrentBrowserOrganizationForViews(doc);
        var sortingOrder = browserOrganization.SortingOrder;
        var groupsByLevel = new Dictionary<int, List<BrowserGroup>>();

        // виды в браузере могут быть неорганизованны
        // сделаю коллекцию таких видов
        var notGroupingViews = new List<View>();

        foreach (var view in viewsToAdd)
        {
            var folderItems = browserOrganization.GetFolderItems(view.Id);
            if (folderItems.Any())
            {
                BrowserGroup parentGroup = null;
                for (var i = 0; i < folderItems.Count; i++)
                {
                    var folderItem = folderItems[i];
                    BrowserGroup viewGroup = null;
                    if (groupsByLevel.ContainsKey(i))
                        viewGroup = groupsByLevel[i].FirstOrDefault(g => g.Name == folderItem.Name && g.ParentGroup == parentGroup);

                    if (viewGroup == null)
                    {
                        viewGroup = new BrowserGroup(folderItem.Name, parentGroup, i);
                        if (groupsByLevel.ContainsKey(i))
                            groupsByLevel[i].Add(viewGroup);
                        else
                            groupsByLevel.Add(i, new List<BrowserGroup> { viewGroup });
                        parentGroup?.SubItems.Add(viewGroup);
                    }

                    parentGroup = viewGroup;

                    if (i != folderItems.Count - 1)
                        continue;

                    var browserView = new BrowserView(view, parentGroup);
                    parentGroup.SubItems.Add(browserView);
                }
            }
            else
            {
                notGroupingViews.Add(view);
            }
        }

        if (notGroupingViews.Any())
        {
            if (!groupsByLevel.ContainsKey(0))
                groupsByLevel.Add(0, new List<BrowserGroup>());

            var name = Language.GetItem("notOrganized");
            if (string.IsNullOrEmpty(name))
                name = "!Not organized";

            var browserGroup = new BrowserGroup(name, null, 0);
            foreach (var view in notGroupingViews)
            {
                var browserView = new BrowserView(view, browserGroup);
                browserGroup.SubItems.Add(browserView);
            }

            groupsByLevel[0].Add(browserGroup);
        }

        // Sort
        if (groupsByLevel.Any())
        {
            var ordinalStringComparer = new OrdinalStringComparer();
            if (sortingOrder == SortingOrder.Ascending)
            {
                var topGroups = groupsByLevel[0].OrderBy(g => g.Name, ordinalStringComparer).ToList();
                foreach (var sheetGroup in topGroups)
                    sheetGroup.SortViews(SortOrder.Ascending);

                SheetGroups = new ObservableCollection<BrowserGroup>(topGroups);
            }
            else
            {
                var topGroups = groupsByLevel[0].OrderByDescending(g => g.Name, ordinalStringComparer).ToList();
                foreach (var sheetGroup in topGroups)
                    sheetGroup.SortViews(SortOrder.Descending);

                SheetGroups = new ObservableCollection<BrowserGroup>(topGroups);
            }
        }

        if (SheetGroups.Count == 1)
            SheetGroups.First().IsExpanded = true;

        OnPropertyChanged(nameof(SheetGroups));
    }

    public IEnumerable<BrowserView> GetCheckedViews()
    {
        foreach (var browserSheetGroup in SheetGroups)
        {
            foreach (var browserSheet in GetCheckedViews(browserSheetGroup))
            {
                if (browserSheet.IsChecked && browserSheet.IsVisible)
                    yield return browserSheet;
            }
        }
    }

    private static IEnumerable<BrowserView> GetCheckedViews(BrowserGroup browserSheetGroup)
    {
        foreach (var browserItem in browserSheetGroup.SubItems)
        {
            if (browserItem is BrowserView browserView)
            {
                if (browserView.IsChecked && browserView.IsVisible)
                    yield return browserView;
            }

            if (browserItem is BrowserGroup subGroup)
            {
                foreach (var sheet in GetCheckedViews(subGroup))
                {
                    if (sheet.IsChecked && sheet.IsVisible)
                        yield return sheet;
                }
            }
        }
    }
}