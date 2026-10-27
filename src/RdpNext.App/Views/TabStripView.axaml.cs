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
    private int? _dropIndex;

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

    // Sürüklerken sekmeler yerinde kalır, bırakılacak yere çizgi çıkar: imlecin solunda ortası kalan sekme sayısı yeni sıradır.
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
        var others = vm.Sessions.Tabs.Where(t => t != _dragged)
            .Select(t => TabStrip.ContainerFromItem(t) is { } item && item.TranslatePoint(new Point(0, 0), TabStrip) is { } p
                ? new Rect(p, item.Bounds.Size) : (Rect?)null)
            .OfType<Rect>().ToList();
        _dropIndex = others.Count(r => r.Center.X < x);
        ShowLine(vm.Sessions.Tabs.IndexOf(_dragged), others);
    }

    // Çizgi iki sekme arasındaki boşluğun ortasına, en sona bırakırken son sekmenin sağına konur.
    private void ShowLine(int from, List<Rect> others)
    {
        TabDropLine.IsVisible = false;
        if (_dropIndex is not { } index || index == from || others.Count == 0)
            return;
        var gap = ((StackPanel?)TabStrip.ItemsPanelRoot)?.Spacing ?? 0;
        var anchor = index < others.Count ? others[index] : others[^1];
        var x = index < others.Count ? anchor.Left - gap / 2 : anchor.Right + gap / 2;
        Canvas.SetLeft(TabDropLine, x - TabDropLine.Width / 2);
        Canvas.SetTop(TabDropLine, anchor.Top);
        TabDropLine.Height = anchor.Height;
        TabDropLine.IsVisible = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_dragging)
        {
            e.Handled = true;
            if (_dragged is not null && _dropIndex is { } index && DataContext is MainWindowViewModel vm)
                vm.Sessions.MoveTab(_dragged, index);
        }
        e.Pointer.Capture(null);
        EndDrag();
    }

    private void EndDrag()
    {
        _dragged = null;
        _dragging = false;
        _dropIndex = null;
        TabDropLine.IsVisible = false;
    }
}
