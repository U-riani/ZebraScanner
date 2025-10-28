namespace ZebraSCannerTest1.UI.Helpers;

public static class ViewExtensions
{
    public static Point GetAbsoluteLocation(this VisualElement view)
    {
        double x = view.X;
        double y = view.Y;
        var parent = view.Parent as VisualElement;

        while (parent != null)
        {
            x += parent.X;
            y += parent.Y;
            parent = parent.Parent as VisualElement;
        }

        return new Point(x, y);
    }
}
