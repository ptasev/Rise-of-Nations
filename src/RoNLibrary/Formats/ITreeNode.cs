namespace RoNLibrary.Formats;

public interface ITreeNode<T>
 where T : ITreeNode<T>
{
    public List<T> Children { get; set; }
}

public static class TreeNodeExtensions
{
    public static IEnumerable<T> TraverseDepthFirst<T>(this T node)
        where T : ITreeNode<T>
    {
        yield return node;
        foreach (var child in node.Children)
        {
            foreach (var childChild in TraverseDepthFirst(child))
            {
                yield return childChild;
            }
        }
    }

    public static IEnumerable<(T1, T2?)> ZipMatchingTreesDepthFirst<T1, T2>(this T1 node1, T2? node2)
        where T1 : class, ITreeNode<T1>
        where T2 : class, ITreeNode<T2>
    {
        ArgumentNullException.ThrowIfNull(node1);

        yield return (node1, node2);
        for (var i = 0; i < node1.Children.Count; ++i)
        {
            var n2C = node2 is null ? null : (i >= node2.Children.Count ? null : node2.Children[i]);
            
            foreach (var child in ZipMatchingTreesDepthFirst(node1.Children[i], n2C))
            {
                yield return child;
            }
        }
    }
}
