namespace SmartX.Api.Services;

/// <summary>
/// A self-balancing binary search tree (AVL tree). I use it to store each sensor's telemetry
/// history, keyed by timestamp, so I can ask "give me every reading between 14:00 and 16:00"
/// without looping through the whole history.
///
/// How it works:
///  - It is a normal binary search tree (smaller keys left, bigger keys right).
///  - Every node remembers its height. The "balance factor" of a node is
///    height(left) - height(right). In an AVL tree it must always be -1, 0 or +1.
///  - If an insert makes a balance factor -2 or +2, the tree fixes itself with rotations.
///  - That keeps the height at about log2(n), so insert, find and range queries stay fast
///    even with hundreds of thousands of readings. A plain BST fed sorted timestamps would
///    degrade into a linked list (height n), which is the problem AVL solves.
///
/// <para>References (full list in README, IIE Harvard Anglia style):</para>
///   Adelson-Velsky, G.M. and Landis, E.M. (1962) 'An algorithm for the organization of information',
///     Doklady Akademii Nauk SSSR, 146(2), pp. 263-266.
///   GeeksforGeeks (n.d.) AVL Tree | Set 1 (Insertion).
///     Available at: https://www.geeksforgeeks.org/avl-tree-set-1-insertion/ (Accessed: 29 October 2026).
///   Cormen, T.H., Leiserson, C.E., Rivest, R.L. and Stein, C. (2022) Introduction to Algorithms. 4th edn.
///     Cambridge, MA: MIT Press.
/// The code is my own implementation of the standard algorithm. No code was copied from these sources.
/// </summary>
public class AvlTree<TKey, TValue> where TKey : IComparable<TKey>
{
    private class Node
    {
        public TKey Key;
        public TValue Value;
        public int Height = 1;   // a single node has height 1
        public Node? Left;
        public Node? Right;

        public Node(TKey key, TValue value)
        {
            Key = key;
            Value = value;
        }
    }

    private Node? _root;

    public int Count { get; private set; }

    /// <summary>Height of the whole tree (0 when empty).</summary>
    public int Height
    {
        get { return HeightOf(_root); }
    }

    // ---------------------------------------------------------------- insert

    public void Insert(TKey key, TValue value)
    {
        _root = Insert(_root, key, value);
    }

    private Node Insert(Node? node, TKey key, TValue value)
    {
        // normal BST insert first
        if (node == null)
        {
            Count++;
            return new Node(key, value);
        }

        var compare = key.CompareTo(node.Key);
        if (compare < 0)
        {
            node.Left = Insert(node.Left, key, value);
        }
        else if (compare > 0)
        {
            node.Right = Insert(node.Right, key, value);
        }
        else
        {
            node.Value = value; // same key: replace the value
            return node;
        }

        // then fix the heights and balance on the way back up
        UpdateHeight(node);
        return Rebalance(node);
    }

    private Node Rebalance(Node node)
    {
        var balance = BalanceFactor(node);

        if (balance > 1) // left side too heavy
        {
            // Left-Right case: turn it into a Left-Left case first
            if (BalanceFactor(node.Left!) < 0)
            {
                node.Left = RotateLeft(node.Left!);
            }
            return RotateRight(node);
        }

        if (balance < -1) // right side too heavy
        {
            // Right-Left case: turn it into a Right-Right case first
            if (BalanceFactor(node.Right!) > 0)
            {
                node.Right = RotateRight(node.Right!);
            }
            return RotateLeft(node);
        }

        return node; // already balanced
    }

    //        y                x
    //       / \              / \
    //      x   C    --->    A   y
    //     / \                  / \
    //    A   B                B   C
    private Node RotateRight(Node y)
    {
        var x = y.Left!;
        y.Left = x.Right;
        x.Right = y;
        UpdateHeight(y); // y is now lower, so update it first
        UpdateHeight(x);
        return x;
    }

    private Node RotateLeft(Node x)
    {
        var y = x.Right!;
        x.Right = y.Left;
        y.Left = x;
        UpdateHeight(x);
        UpdateHeight(y);
        return y;
    }

    private static int HeightOf(Node? node)
    {
        return node == null ? 0 : node.Height;
    }

    private static void UpdateHeight(Node node)
    {
        node.Height = 1 + Math.Max(HeightOf(node.Left), HeightOf(node.Right));
    }

    private static int BalanceFactor(Node node)
    {
        return HeightOf(node.Left) - HeightOf(node.Right);
    }

    // ---------------------------------------------------------------- find

    public bool TryGet(TKey key, out TValue? value)
    {
        var node = _root;
        while (node != null)
        {
            var compare = key.CompareTo(node.Key);
            if (compare == 0)
            {
                value = node.Value;
                return true;
            }
            node = compare < 0 ? node.Left : node.Right;
        }

        value = default;
        return false;
    }

    // ---------------------------------------------------------------- range query

    /// <summary>
    /// All values whose key is between low and high (inclusive), in key order.
    /// The tree skips whole branches that cannot contain a match, so the work is about
    /// O(log n + k) where k is the number of results. nodesVisited shows how little of the tree was touched.
    /// </summary>
    public List<TValue> RangeQuery(TKey low, TKey high, out int nodesVisited)
    {
        var results = new List<TValue>();
        var visited = 0;
        CollectRange(_root, low, high, results, ref visited);
        nodesVisited = visited;
        return results;
    }

    private void CollectRange(Node? node, TKey low, TKey high, List<TValue> results, ref int visited)
    {
        if (node == null) return;
        visited++;

        var compareLow = low.CompareTo(node.Key);   // < 0 means low is smaller than this key
        var compareHigh = high.CompareTo(node.Key); // > 0 means high is bigger than this key

        // Only go left if there could be keys smaller than this one that are still >= low.
        if (compareLow < 0)
        {
            CollectRange(node.Left, low, high, results, ref visited);
        }

        if (compareLow <= 0 && compareHigh >= 0)
        {
            results.Add(node.Value);
        }

        // Only go right if there could be keys bigger than this one that are still <= high.
        if (compareHigh > 0)
        {
            CollectRange(node.Right, low, high, results, ref visited);
        }
    }

    // ---------------------------------------------------------------- newest first

    /// <summary>The newest N values (biggest keys), newest first. Walks the tree right to left and stops early.</summary>
    public List<TValue> GetLatest(int count, out int nodesVisited)
    {
        var results = new List<TValue>();
        var visited = 0;
        CollectLatest(_root, count, results, ref visited);
        nodesVisited = visited;
        return results;
    }

    private void CollectLatest(Node? node, int count, List<TValue> results, ref int visited)
    {
        if (node == null || results.Count >= count) return;
        visited++;

        CollectLatest(node.Right, count, results, ref visited);
        if (results.Count < count)
        {
            results.Add(node.Value);
        }
        CollectLatest(node.Left, count, results, ref visited);
    }

    // ---------------------------------------------------------------- self check

    /// <summary>
    /// Checks the tree really is a valid AVL tree: keys in order, every height correct and every
    /// balance factor between -1 and +1. Used by the benchmark to prove the balancing works.
    /// </summary>
    public bool IsValid()
    {
        return CheckNode(_root, default, false, default, false) >= 0;
    }

    // returns the node's height, or -1 if something is wrong
    private int CheckNode(Node? node, TKey? min, bool hasMin, TKey? max, bool hasMax)
    {
        if (node == null) return 0;

        if (hasMin && node.Key.CompareTo(min!) <= 0) return -1;
        if (hasMax && node.Key.CompareTo(max!) >= 0) return -1;

        var left = CheckNode(node.Left, min, hasMin, node.Key, true);
        var right = CheckNode(node.Right, node.Key, true, max, hasMax);
        if (left < 0 || right < 0) return -1;

        if (Math.Abs(left - right) > 1) return -1;
        if (node.Height != 1 + Math.Max(left, right)) return -1;

        return node.Height;
    }
}
