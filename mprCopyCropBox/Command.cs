namespace mprCopyCropBox;

using System;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ModPlus_Revit;
using ModPlus_Revit.Utils;
using ModPlusAPI;
using ModPlusAPI.Windows;

[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class Command : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
#if !DEBUG
        ModPlusAPI.Statistic.SendCommandStarting(ModPlusConnector.Instance);
#endif
        try
        {
            var sourceView = commandData.Application.ActiveUIDocument.ActiveGraphicalView;
            ValidateActiveView(sourceView);

            var doc = commandData.Application.ActiveUIDocument.Document;

            var allowableViews = sourceView.ViewType == ViewType.ThreeD
                ? new FilteredElementCollector(doc)
                    .OfClass(typeof(View))
                    .OfType<View>()
                    .Where(v => v.Category != null &&
                                v.Id.IntegerValue != sourceView.Id.IntegerValue &&
                                !v.IsTemplate &&
                                v.ViewType == ViewType.ThreeD)
                    .ToList()
                : new FilteredElementCollector(doc)
                    .OfClass(typeof(View))
                    .OfType<View>()
                    .Where(v => v.Category != null &&
                                v.Id.IntegerValue != sourceView.Id.IntegerValue &&
                                !v.IsTemplate &&
                                IsAllowableViewType(v) &&
                                v.ViewDirection.IsParallelTo(sourceView.ViewDirection))
                    .ToList();

            // Не найдено видов, подходящих для копирования границ обрезки
            if (allowableViews.Count == 0)
                throw new OperationCanceledException(Language.GetItem("h5"));

            var context = new SelectViewsContext();
            var win = new SelectViewsWindow
            {
                DataContext = context
            };

            win.ContentRendered += (_, _) => context.BuildTree(doc, allowableViews);
            if (ModPlus.ShowModal(win) != true)
                return Result.Cancelled;

            var checkedViews = context.GetCheckedViews().ToList();

            if (!checkedViews.Any())
                return Result.Cancelled;

            using (var tr = new TransactionGroup(doc, Language.GetPluginLocalName(ModPlusConnector.Instance)))
            {
                tr.Start();

                foreach (var checkedView in checkedViews)
                {
                    if (context.Copy3DViewBox && sourceView is View3D sourceView3D && checkedView.View is View3D targetView3D)
                    {
                        targetView3D.SetSectionBox(sourceView3D.GetSectionBox());
                    }

                    CopyCropBox(sourceView, checkedView.View, context.CopyCropVisibilitySettings);
                }

                tr.Assimilate();
            }

            return Result.Succeeded;
        }
        catch (OperationCanceledException exception)
        {
            MessageBox.Show(exception.Message, MessageBoxIcon.Close);
            return Result.Cancelled;
        }
        catch (Exception exception)
        {
            exception.ShowInExceptionBox();
            return Result.Failed;
        }
    }

    private void ValidateActiveView(View view)
    {
        // Активный вид не имеет границ подрезки
        if (!IsAllowableViewType(view))
            throw new OperationCanceledException(Language.GetItem("h4"));
    }

    private bool IsAllowableViewType(View view)
    {
        return view.ViewType is not (
            ViewType.Legend or
            ViewType.Schedule or
            ViewType.ColumnSchedule or
            ViewType.PanelSchedule or
            ViewType.DrawingSheet or
            ViewType.Rendering);
    }

    private void CopyCropBox(View sourceView, View targetView, bool copyCropVisibilitySettings)
    {
        var doc = sourceView.Document;

        var cropBoxVisible = targetView.CropBoxVisible;
        var cropBoxActive = targetView.CropBoxActive;
        var annotationCropActiveParameter = targetView.get_Parameter(BuiltInParameter.VIEWER_ANNOTATION_CROP_ACTIVE);
        var annotationCropActive = annotationCropActiveParameter.AsInteger();

        using (var tr = new Transaction(doc, "Modify target view crop box"))
        {
            tr.Start();
            targetView.CropBoxVisible = true;
            targetView.CropBoxActive = true;
            annotationCropActiveParameter.Set(1);
            tr.Commit();
        }

        if (sourceView.ViewType.ToString().Contains("Plan"))
        {
            var angle = sourceView.RightDirection.AngleTo(XYZ.BasisX);

            if (angle != 0.0)
            {
                var targetCropBoxElement = GetCropBoxElement(targetView);

                using (var tr = new Transaction(doc, "Rotate crop box element"))
                {
                    var bBox = targetView.CropBox;
                    var center = 0.5 * (bBox.Max + bBox.Min);
                    var axis = Line.CreateBound(center, center + XYZ.BasisZ);

                    tr.Start();
                    ElementTransformUtils.RotateElement(doc, targetCropBoxElement.Id, axis, angle);
                    tr.Commit();
                }
            }
        }

        using (var tr = new Transaction(doc, "Copy extents"))
        {
            tr.Start();
            if (sourceView.ViewType == ViewType.ThreeD && targetView.ViewType == ViewType.ThreeD)
            {
                targetView.CropBox = sourceView.CropBox;
            }
            else
            {
                var sourceCropRegionShapeManager = sourceView.GetCropRegionShapeManager();
                var targetCropRegionShapeManager = targetView.GetCropRegionShapeManager();

                if (sourceCropRegionShapeManager.CanHaveAnnotationCrop && targetCropRegionShapeManager.CanHaveAnnotationCrop)
                {
                    targetCropRegionShapeManager.BottomAnnotationCropOffset = sourceCropRegionShapeManager.BottomAnnotationCropOffset;
                    targetCropRegionShapeManager.LeftAnnotationCropOffset = sourceCropRegionShapeManager.LeftAnnotationCropOffset;
                    targetCropRegionShapeManager.TopAnnotationCropOffset = sourceCropRegionShapeManager.TopAnnotationCropOffset;
                    targetCropRegionShapeManager.RightAnnotationCropOffset = sourceCropRegionShapeManager.RightAnnotationCropOffset;
                }
                
                var cropShape = sourceCropRegionShapeManager.GetCropShape().FirstOrDefault();
                if (cropShape != null)
                    targetCropRegionShapeManager.SetCropShape(cropShape);
            }

            if (copyCropVisibilitySettings)
            {
                targetView.CropBoxVisible = sourceView.CropBoxVisible;
                targetView.CropBoxActive = sourceView.CropBoxActive;
                annotationCropActiveParameter.Set(sourceView.get_Parameter(BuiltInParameter.VIEWER_ANNOTATION_CROP_ACTIVE).AsInteger());
            }
            else
            {
                targetView.CropBoxVisible = cropBoxVisible;
                targetView.CropBoxActive = cropBoxActive;
                annotationCropActiveParameter.Set(annotationCropActive);
            }

            tr.Commit();
        }
    }

    private Element GetCropBoxElement(View view)
    {
        var doc = view.Document;
        Element cropBoxElement;
        using (var trGroup = new TransactionGroup(doc, "Temp to find crop box element"))
        {
            trGroup.Start();

            using (var tr = new Transaction(doc, "Temp to find crop box element"))
            {
                tr.Start();
                view.CropBoxVisible = false;
                tr.Commit();

                var collector = new FilteredElementCollector(doc, view.Id);

                var shownElems = collector.ToElementIds();

                tr.Start();
                view.CropBoxVisible = true;
                tr.Commit();

                collector = new FilteredElementCollector(doc, view.Id);
                collector.Excluding(shownElems);
                cropBoxElement = collector.FirstElement();
            }

            trGroup.RollBack();
        }

        return cropBoxElement;
    }
}