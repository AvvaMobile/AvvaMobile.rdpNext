using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using RdpNext.App.ViewModels;

namespace RdpNext.App.Views;

public partial class SidebarView : UserControl
{
    private const double DragThreshold = 4;
    private const double ScrollEdge = 24;

    private TreeNodeViewModel? _dragged;
    private Point _pressPoint;
    private bool _dragging;
    private int _pressClickCount;
    private (TreeNodeViewModel Target, DropPlace Place)? _drop;

    public SidebarView()
    {
        InitializeComponent();
        // TreeView basışı seçim için kendisi işliyor; sürüklemeyi yine de görmek için handledEventsToo.
        Tree.AddHandler(PointerPressedEvent, OnTreePointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        Tree.AddHandler(PointerMovedEvent, OnTreePointerMoved, handledEventsToo: true);
        Tree.AddHandler(PointerReleasedEvent, OnTreePointerReleased, handledEventsToo: true);
        Tree.AddHandler(PointerCaptureLostEvent, (_, _) => EndDrag(), handledEventsToo: true);
    }

    private void OnTreeDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is Control { DataContext: ServerNodeViewModel server } && DataContext is MainWindowViewModel vm)
            vm.ConnectCommand.Execute(server);
    }

    private void OnTreePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(Tree).Properties.IsLeftButtonPressed || e.Source is not Visual source
            || source.FindAncestorOfType<ToggleButton>(includeSelf: true) is not null)
            return;
        _dragged = source.FindAncestorOfType<TreeViewItem>(includeSelf: true)?.DataContext as TreeNodeViewModel;
        _pressPoint = e.GetPosition(Tree);
        _pressClickCount = e.ClickCount;
    }

    private void OnTreePointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragged is null || !e.GetCurrentPoint(Tree).Properties.IsLeftButtonPressed)
            return;
        var point = e.GetPosition(Tree);
        if (!_dragging)
        {
            var delta = point - _pressPoint;
            if (Math.Abs(delta.X) < DragThreshold && Math.Abs(delta.Y) < DragThreshold)
                return;
            _dragging = true;
            e.Pointer.Capture(Tree);
        }
        ScrollNearEdge(point.Y);
        UpdateDrop(point);
    }

    private void OnTreePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_dragging)
        {
            if (_drop is { } drop && _dragged is { } dragged && DataContext is MainWindowViewModel vm)
                vm.Drop(dragged, drop.Target, drop.Place);
            e.Handled = true;
        }
        else if (_dragged is GroupNodeViewModel folder && e.InitialPressMouseButton == MouseButton.Left && _pressClickCount == 1
                 && (e.Source as Visual)?.FindAncestorOfType<TreeViewItem>(includeSelf: true)?.DataContext == folder)
        {
            // Klasör satırına tık açar ya da kapatır. Ok düğmesine basış ve çift tıktaki ikinci basış
            // (TreeViewItem kendisi açıp kapatıyor) burada sayılmaz; böylece her tık bir kez açıp kapatır.
            folder.IsExpanded = !folder.IsExpanded;
        }
        e.Pointer.Capture(null);
        EndDrag();
    }

    private void EndDrag()
    {
        _dragged = null;
        _dragging = false;
        _drop = null;
        DropLine.IsVisible = false;
        DropHighlight.IsVisible = false;
    }

    // İmlecin altındaki satıra göre bırakma yerini bulur ve göstergeyi oraya koyar.
    private void UpdateDrop(Point point)
    {
        _drop = null;
        DropLine.IsVisible = false;
        DropHighlight.IsVisible = false;
        if (_dragged is null || DataContext is not MainWindowViewModel vm
            || (Tree.InputHitTest(point) as Visual)?.FindAncestorOfType<TreeViewItem>(includeSelf: true) is not { DataContext: TreeNodeViewModel target } item
            || HeaderBounds(item) is not { } header)
            return;

        var ratio = (point.Y - header.Top) / header.Height;
        var place = _dragged is ServerNodeViewModel && target is GroupNodeViewModel
            ? ratio < 0.25 ? DropPlace.Above : ratio > 0.75 ? DropPlace.Below : DropPlace.Into
            : ratio < 0.5 ? DropPlace.Above : DropPlace.Below;
        if (vm.Tree.ResolveDrop(_dragged, target, place) is not { } drop)
            return;
        _drop = (target, place);

        if (drop.Into)
        {
            Canvas.SetLeft(DropHighlight, header.Left);
            Canvas.SetTop(DropHighlight, header.Top);
            DropHighlight.Width = header.Width;
            DropHighlight.Height = header.Height;
            DropHighlight.IsVisible = true;
            return;
        }
        // Önüne giriyorsa satırın üstü; açık klasörün ilk sırasıysa başlığın altı; yoksa satırın (içeriğiyle) altı.
        var y = drop.Before == target ? header.Top
            : target is GroupNodeViewModel { Group: var group } && drop.Parent == group ? header.Bottom
            : item.TranslatePoint(new Point(0, item.Bounds.Height), Tree)?.Y ?? header.Bottom;
        // Çizgi satırın içeriğinden başlar; iç içe sunucularda girintiyle hizalı.
        var contentLeft = HeaderPresenter(item)?.TranslatePoint(new Point(0, 0), Tree)?.X ?? header.Left;
        Canvas.SetLeft(DropLine, contentLeft);
        Canvas.SetTop(DropLine, y - 1);
        DropLine.Width = Math.Max(0, Tree.Bounds.Width - contentLeft - 8);
        DropLine.IsVisible = true;
    }

    private static Control? HeaderPresenter(TreeViewItem item) => item.GetVisualDescendants().OfType<Control>()
        .FirstOrDefault(c => c.Name == "PART_HeaderPresenter" && c.TemplatedParent == item);

    // Satırın yalnız kendi başlık şeridi (açık klasörün çocukları hariç), ağaca göre.
    private Rect? HeaderBounds(TreeViewItem item)
    {
        var header = HeaderPresenter(item);
        if (header?.TranslatePoint(new Point(0, 0), Tree) is not { } topLeft)
            return null;
        var left = item.TranslatePoint(new Point(0, 0), Tree)?.X ?? topLeft.X;
        return new Rect(left, topLeft.Y, Tree.Bounds.Width - left, header.Bounds.Height);
    }

    // Uzun listede kenara gelince kaydır.
    private void ScrollNearEdge(double y)
    {
        if (Tree.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault() is not { } scroll)
            return;
        if (y < ScrollEdge)
            scroll.Offset = scroll.Offset.WithY(Math.Max(0, scroll.Offset.Y - 12));
        else if (y > Tree.Bounds.Height - ScrollEdge)
            scroll.Offset = scroll.Offset.WithY(scroll.Offset.Y + 12);
    }
}
