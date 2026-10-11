namespace HanClassicBuyWheel.Domain;

public sealed class WheelNavigation(MenuNode root)
{
    public const int PageSize = 12;
    private readonly Stack<(MenuNode Node, int Page)> _parents = [];
    public MenuNode Root { get; } = root;
    public MenuNode Current { get; private set; } = root;
    public int Page { get; private set; }
    public int Depth => _parents.Count;
    public int LastPage => Math.Max(0, (Current.Children.Length - 1) / PageSize);
    public MenuNode[] Visible => Current.Children.Skip(Page * PageSize).Take(PageSize).ToArray();
    public BuyItem? Activate(int index)
    {
        int targetIndex = Page * PageSize + index;
        if (index < 0 || index >= PageSize || targetIndex >= Current.Children.Length) return null;
        var node = BuyCatalog.Collapse(Current.Children[targetIndex]);
        if (node.Item is { } item) return item;
        if (!BuyCatalog.HasItems(node)) return null;
        _parents.Push((Current, Page));
        Current = node; Page = 0;
        return null;
    }
    public bool Back()
    {
        if (!_parents.TryPop(out var previous)) return false;
        Current = previous.Node; Page = previous.Page;
        return true;
    }
    public void Home() { _parents.Clear(); Current = Root; Page = 0; }
    public void ChangePage(int offset) => Page = Math.Clamp(Page + offset, 0, LastPage);
}
