using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace RelicWriter.Controls;

/// <summary>A border with its bottom-right corner cut off at an angle, like the game's cards and panels. It takes the same Background, BorderBrush, BorderThickness and Padding as a Border.</summary>
public class NotchedBorder : Decorator
{
    public static readonly StyledProperty<IBrush?> BackgroundProperty = Border.BackgroundProperty.AddOwner<NotchedBorder>();
    public static readonly StyledProperty<IBrush?> BorderBrushProperty = Border.BorderBrushProperty.AddOwner<NotchedBorder>();
    public static readonly StyledProperty<Thickness> BorderThicknessProperty = Border.BorderThicknessProperty.AddOwner<NotchedBorder>();
    public static readonly StyledProperty<Thickness> PaddingProperty = Decorator.PaddingProperty.AddOwner<NotchedBorder>();
    public static readonly StyledProperty<double> NotchProperty = AvaloniaProperty.Register<NotchedBorder, double>(nameof(Notch), 16);

    static NotchedBorder()
    {
        AffectsRender<NotchedBorder>(BackgroundProperty, BorderBrushProperty, NotchProperty);
        AffectsMeasure<NotchedBorder>(BorderThicknessProperty, PaddingProperty);
    }

    public IBrush? Background { get => GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }
    public IBrush? BorderBrush { get => GetValue(BorderBrushProperty); set => SetValue(BorderBrushProperty, value); }
    public Thickness BorderThickness { get => GetValue(BorderThicknessProperty); set => SetValue(BorderThicknessProperty, value); }
    public Thickness Padding { get => GetValue(PaddingProperty); set => SetValue(PaddingProperty, value); }
    public double Notch { get => GetValue(NotchProperty); set => SetValue(NotchProperty, value); }

    StreamGeometry? Outline(Size size, double inset)
    {
        if (size.Width <= 0 || size.Height <= 0) return null;
        var n = Math.Min(Notch, Math.Min(size.Width, size.Height) / 2);
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(new Point(inset, inset), true);
            c.LineTo(new Point(size.Width - inset, inset));
            c.LineTo(new Point(size.Width - inset, size.Height - n));
            c.LineTo(new Point(size.Width - n, size.Height - inset));
            c.LineTo(new Point(inset, size.Height - inset));
            c.EndFigure(true);
        }
        return g;
    }

    protected override Size MeasureOverride(Size availableSize) => LayoutHelper.MeasureChild(Child, availableSize, Padding, BorderThickness);

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = LayoutHelper.ArrangeChild(Child, finalSize, Padding, BorderThickness);
        Clip = Outline(finalSize, 0);   // nothing inside shows in the cut corner
        return size;
    }

    public override void Render(DrawingContext context)
    {
        var line = Math.Max(BorderThickness.Top, BorderThickness.Right);
        if (Outline(Bounds.Size, line / 2) is not { } outline) return;
        context.DrawGeometry(Background, BorderBrush is { } brush && line > 0 ? new Pen(brush, line) : null, outline);
        // A card's accent edge is a thicker left side: drawn over the even line.
        if (BorderBrush is { } edge && BorderThickness.Left > line) context.DrawRectangle(edge, null, new Rect(0, 0, BorderThickness.Left, Bounds.Height));
    }
}
