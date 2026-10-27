using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using RdpNext.App.ViewModels;

namespace RdpNext.App.Views;

public partial class TabStripView : UserControl
{
    private const double DragThreshold = 4;

    private TabViewModel? _dragged;
    private Point _pressPoint;
    private bool _dragging;

    public TabStripView()
    {
        InitializeComponent();
        // ListBox basışı seçim için kendisi işliyor; sürüklemeyi yine de görmek için handledEventsToo.
        TabStrip.AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        TabStrip.AddHandler(PointerMovedEvent, OnPointerMoved, handledEventsToo: true);
        TabStrip.AddHandler(PointerReleasedEvent, OnPointerReleased, handledEventsToo: true);
        TabStrip.AddHandler(PointerCaptureLostEvent, (_, _) => EndDrag(), handledEventsToo: true);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(TabStrip).Properties.IsLeftButtonPressed || e.Source is not Visual source
            || source.FindAncestorOfType<Button>(includeSelf: true) is not null)
            return;
        _dragged = source.FindAncestorOfType<ListBoxItem>(includeSelf: true)?.DataContext as TabViewModel;
        _pressPoint = e.GetPosition(TabStrip);
    }

    // Sürüklerken sekme anında yerine kayar: imlecin solunda ortası kalan sekme sayısı yeni sıradır.
    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragged is null || DataContext is not MainWindowViewModel vm || !e.GetCurrentPoint(TabStrip).Properties.IsLeftButtonPressed)
            return;
        var x = e.GetPosition(TabStrip).X;
        if (!_dragging)
        {
            if (Math.Abs(x - _pressPoint.X) < DragThreshold)
                return;
            _dragging = true;
            e.Pointer.Capture(TabStrip);
        }
        var index = vm.Sessions.Tabs.Where(t => t != _dragged)
            .Count(t => TabStrip.ContainerFromItem(t) is { } item
                        && item.TranslatePoint(new Point(item.Bounds.Width / 2, 0), TabStrip)?.X < x);
        vm.Sessions.MoveTab(_dragged, index);
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_dragging)
            e.Handled = true;
        e.Pointer.Capture(null);
        EndDrag();
    }

    private void EndDrag()
    {
        _dragged = null;
        _dragging = false;
    }
}
