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

    public static IEnumerable<(T1, T2)> ZipMatchingTreesDepthFirst<T1, T2>(this T1 node1, T2 node2)
        where T1 : ITreeNode<T1>
        where T2 : ITreeNode<T2>
    {
        ArgumentNullException.ThrowIfNull(node1);
        ArgumentNullException.ThrowIfNull(node2);

        if (node1.Children.Count != node2.Children.Count)
        {
            throw new InvalidOperationException("Tree hierarchy does not match.");
        }

        yield return (node1, node2);
        for (var i = 0; i < node1.Children.Count; ++i)
        {
            foreach (var child in ZipMatchingTreesDepthFirst(node1.Children[i], node2.Children[i]))
            {
                yield return child;
            }
        }
    }
}