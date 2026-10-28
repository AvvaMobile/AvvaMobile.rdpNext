using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace RdpNext.Screenshots;

// RDP oturumunda görünen uydurma Windows Server masaüstü; BGRA32 olarak çizilir.
public static class FakeDesktop
{
    // Görüntü width x height birimlik, scale katı piksel olarak döner.
    public static (byte[] Pixels, int Width, int Height) Render(double width, double height, double scale)
    {
        var size = new PixelSize((int)Math.Round(width * scale), (int)Math.Round(height * scale));
        var desktop = new LayoutTransformControl
        {
            LayoutTransform = new ScaleTransform(scale, scale),
            Child = new Border { Width = size.Width / scale, Height = size.Height / scale, Child = Build() },
        };
        desktop.Measure(new Size(size.Width, size.Height));
        desktop.Arrange(new Rect(0, 0, size.Width, size.Height));
        using var bitmap = new RenderTargetBitmap(size);
        bitmap.Render(desktop);
        var pixels = new byte[size.Width * size.Height * 4];
        unsafe
        {
            fixed (byte* p = pixels)
                bitmap.CopyPixels(new PixelRect(size), (IntPtr)p, pixels.Length, size.Width * 4);
        }
        // Skia RGBA verir, oturum BGRA bekler.
        if (bitmap.Format == PixelFormat.Rgba8888)
            for (var i = 0; i < pixels.Length; i += 4)
                (pixels[i], pixels[i + 2]) = (pixels[i + 2], pixels[i]);
        return (pixels, size.Width, size.Height);
    }

    private static readonly FontFamily Segoe = new("Inter");

    private static Control Build()
    {
        var root = new Grid { RowDefinitions = new RowDefinitions("*,44") };
        root.Children.Add(Wallpaper());
        var window = ServerManager();
        root.Children.Add(window);
        var taskbar = Taskbar();
        Grid.SetRow(taskbar, 1);
        root.Children.Add(taskbar);
        return root;
    }

    private static Control Wallpaper()
    {
        var panel = new Panel
        {
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Color.Parse("#0A1F3D"), 0), new GradientStop(Color.Parse("#0E4C8A"), 0.6), new GradientStop(Color.Parse("#1B78C8"), 1) },
            },
        };
        panel.Children.Add(new Border
        {
            Background = new RadialGradientBrush
            {
                Center = new RelativePoint(0.75, 0.35, RelativeUnit.Relative),
                GradientOrigin = new RelativePoint(0.75, 0.35, RelativeUnit.Relative),
                RadiusX = new RelativeScalar(0.6, RelativeUnit.Relative),
                RadiusY = new RelativeScalar(0.6, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Color.Parse("#5539A0FF"), 0), new GradientStop(Colors.Transparent, 1) },
            },
        });
        var icons = new StackPanel { Margin = new Thickness(18), Spacing = 22, HorizontalAlignment = HorizontalAlignment.Left };
        icons.Children.Add(DesktopIcon("Recycle Bin", "#8FB8E0"));
        icons.Children.Add(DesktopIcon("Logs", "#F2C14E"));
        panel.Children.Add(icons);
        return panel;
    }

    private static Control DesktopIcon(string name, string color)
    {
        var stack = new StackPanel { Spacing = 6, Width = 76 };
        stack.Children.Add(new Border { Width = 40, Height = 40, CornerRadius = new CornerRadius(6), Background = Brush(color), HorizontalAlignment = HorizontalAlignment.Center });
        stack.Children.Add(new TextBlock { Text = name, FontFamily = Segoe, FontSize = 12, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center });
        return stack;
    }

    private static Control ServerManager()
    {
        var window = new Border
        {
            Margin = new Thickness(130, 46, 110, 40),
            Background = Brush("#F3F3F3"),
            BoxShadow = BoxShadows.Parse("0 12 40 0 #66000000"),
            ClipToBounds = true,
        };
        var dock = new DockPanel();
        window.Child = dock;

        var title = new Border { Height = 34, Background = Brushes.White, Padding = new Thickness(14, 0) };
        var titleGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        titleGrid.Children.Add(Text("Server Manager", 13, "#1F1F1F", vertical: true));
        var buttons = new TextBlock { Text = "−     ☐     ✕", FontSize = 12, Foreground = Brush("#444444"), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(buttons, 1);
        titleGrid.Children.Add(buttons);
        title.Child = titleGrid;
        DockPanel.SetDock(title, Dock.Top);
        dock.Children.Add(title);

        var header = new Border { Height = 52, Background = Brush("#FFFFFF"), BorderBrush = Brush("#E0E0E0"), BorderThickness = new Thickness(0, 1, 0, 1), Padding = new Thickness(20, 0) };
        header.Child = Text("Server Manager  ›  Dashboard", 20, "#1F1F1F", vertical: true, light: true);
        DockPanel.SetDock(header, Dock.Top);
        dock.Children.Add(header);

        var nav = new StackPanel { Width = 190, Background = Brush("#FFFFFF") };
        nav.Children.Add(NavItem("Dashboard", selected: true));
        foreach (var item in new[] { "Local Server", "All Servers", "AD DS", "DNS", "File and Storage Services", "IIS" })
            nav.Children.Add(NavItem(item, selected: false));
        DockPanel.SetDock(nav, Dock.Left);
        dock.Children.Add(nav);

        var content = new StackPanel { Margin = new Thickness(28, 22), Spacing = 18 };
        content.Children.Add(Text("WELCOME TO SERVER MANAGER", 12, "#5A5A5A", bold: true));
        var welcome = new Border { Background = Brushes.White, Padding = new Thickness(20), BorderBrush = Brush("#E3E3E3"), BorderThickness = new Thickness(1) };
        var steps = new StackPanel { Spacing = 12 };
        steps.Children.Add(Step("1", "Configure this local server", "#1A73C8"));
        steps.Children.Add(Step("2", "Add roles and features", "#1A73C8"));
        steps.Children.Add(Step("3", "Add other servers to manage", "#1A73C8"));
        steps.Children.Add(Step("4", "Create a server group", "#1A73C8"));
        welcome.Child = steps;
        content.Children.Add(welcome);
        content.Children.Add(Text("ROLES AND SERVER GROUPS", 12, "#5A5A5A", bold: true));
        var tiles = new UniformGrid { Columns = 4 };
        tiles.Children.Add(Tile("AD DS", "1", "#2E7D32"));
        tiles.Children.Add(Tile("DNS", "1", "#2E7D32"));
        tiles.Children.Add(Tile("IIS", "1", "#C62828"));
        tiles.Children.Add(Tile("Storage", "2", "#2E7D32"));
        content.Children.Add(tiles);
        dock.Children.Add(content);
        return window;
    }

    private static Control NavItem(string text, bool selected) => new Border
    {
        Height = 36,
        Padding = new Thickness(16, 0),
        Background = selected ? Brush("#DCEBFA") : Brushes.Transparent,
        Child = Text(text, 13, selected ? "#0B5CAD" : "#333333", vertical: true),
    };

    private static Control Step(string number, string text, string color)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        row.Children.Add(new Border
        {
            Width = 24,
            Height = 24,
            CornerRadius = new CornerRadius(12),
            Background = Brush(color),
            Child = new TextBlock { Text = number, FontFamily = Segoe, FontSize = 12, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
        });
        row.Children.Add(Text(text, 15, "#1A73C8", vertical: true));
        return row;
    }

    private static Control Tile(string name, string count, string status)
    {
        var tile = new Border { Margin = new Thickness(0, 0, 12, 0), Background = Brushes.White, BorderBrush = Brush("#E3E3E3"), BorderThickness = new Thickness(1) };
        var stack = new StackPanel();
        var head = new Border { Background = Brush("#1F5FA8"), Padding = new Thickness(12, 9) };
        var headGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        headGrid.Children.Add(Text(name, 13, "#FFFFFF", bold: true));
        var countText = Text(count, 13, "#FFFFFF");
        Grid.SetColumn(countText, 1);
        headGrid.Children.Add(countText);
        head.Child = headGrid;
        stack.Children.Add(head);
        var body = new StackPanel { Margin = new Thickness(12, 10), Spacing = 8 };
        foreach (var line in new[] { "Manageability", "Events", "Services", "Performance", "BPA results" })
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            row.Children.Add(new Rectangle { Width = 4, Height = 14, Fill = Brush(line == "Services" ? status : "#2E7D32") });
            row.Children.Add(Text(line, 12, "#333333"));
            body.Children.Add(row);
        }
        stack.Children.Add(body);
        tile.Child = stack;
        return tile;
    }

    private static Control Taskbar()
    {
        var bar = new Border { Background = Brush("#E61C1C1C"), Padding = new Thickness(10, 0) };
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto") };
        var start = new UniformGrid { Columns = 2, Width = 18, Height = 18, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 16, 0) };
        for (var i = 0; i < 4; i++)
            start.Children.Add(new Border { Background = Brush("#4CC2FF"), Margin = new Thickness(1) });
        grid.Children.Add(start);
        var search = new Border
        {
            Width = 220,
            Height = 30,
            CornerRadius = new CornerRadius(4),
            Background = Brush("#2E2E2E"),
            Padding = new Thickness(10, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = Text("Type here to search", 12, "#A0A0A0", vertical: true),
        };
        Grid.SetColumn(search, 1);
        grid.Children.Add(search);
        var apps = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14, Margin = new Thickness(18, 0), VerticalAlignment = VerticalAlignment.Center };
        foreach (var color in new[] { "#F2C14E", "#1F5FA8", "#3A96DD", "#5C5C5C" })
            apps.Children.Add(new Border { Width = 22, Height = 22, CornerRadius = new CornerRadius(4), Background = Brush(color) });
        Grid.SetColumn(apps, 2);
        grid.Children.Add(apps);
        var clock = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
        clock.Children.Add(new TextBlock { Text = "10:42 AM", FontFamily = Segoe, FontSize = 11, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Right });
        clock.Children.Add(new TextBlock { Text = "10/28/2026", FontFamily = Segoe, FontSize = 11, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Right });
        Grid.SetColumn(clock, 3);
        grid.Children.Add(clock);
        bar.Child = grid;
        return bar;
    }

    private static TextBlock Text(string text, double size, string color, bool vertical = false, bool bold = false, bool light = false) => new()
    {
        Text = text,
        FontFamily = Segoe,
        FontSize = size,
        Foreground = Brush(color),
        FontWeight = bold ? FontWeight.SemiBold : light ? FontWeight.Light : FontWeight.Normal,
        VerticalAlignment = vertical ? VerticalAlignment.Center : VerticalAlignment.Top,
    };

    private static IBrush Brush(string color) => new SolidColorBrush(Color.Parse(color));
}
