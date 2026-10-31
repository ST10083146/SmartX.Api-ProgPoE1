namespace SmartX.Api.Services;

/// <summary>One wireless link going OUT of a node. The graph is stored as an adjacency list.</summary>
public record MeshEdge(string To, double Cost);

/// <summary>One link chosen for the minimum spanning tree.</summary>
public record MstEdge(string From, string To, double Cost);

public class MstResult
{
    public List<MstEdge> Edges { get; } = new List<MstEdge>();

    // node -> the next node on its way to the gateway (its "parent" in the tree)
    public Dictionary<string, string> Parent { get; } = new Dictionary<string, string>();

    public HashSet<string> Included { get; } = new HashSet<string>();
    public double TotalCost { get; set; }
}

/// <summary>
/// The graph algorithms for the mesh network. The graph is a Dictionary where each key is a node
/// and the value is the list of links leaving it (an adjacency list). Every algorithm takes an
/// "isUp" function so that nodes that are down are simply skipped, which is how the network
/// "heals" around a failed sensor.
///
/// <para>References (full list in README, IIE Harvard Anglia style):</para>
///   Cormen, T.H., Leiserson, C.E., Rivest, R.L. and Stein, C. (2022) Introduction to Algorithms. 4th edn.
///     Cambridge, MA: MIT Press. (graph representation, BFS, DFS and minimum spanning trees)
///   Prim, R.C. (1957) 'Shortest connection networks and some generalizations',
///     Bell System Technical Journal, 36(6), pp. 1389-1401.
///   GeeksforGeeks (n.d.) Prim's algorithm using priority_queue in STL.
///     Available at: https://www.geeksforgeeks.org/prims-algorithm-using-priority_queue-stl/ (Accessed: 29 October 2026).
/// My own C# implementation of these standard algorithms. No code was copied from the sources.
/// </summary>
public static class MeshAlgorithms
{
    // ------------------------------------------------------------ BFS

    /// <summary>
    /// Breadth-First Search. Visits the start node, then all its neighbours, then THEIR neighbours,
    /// and so on, using a Queue. Because it goes level by level, "hops" is the smallest number of
    /// wireless hops from the start to each node. Down nodes are never entered.
    /// </summary>
    public static List<(string Id, int Hops)> Bfs(Dictionary<string, List<MeshEdge>> graph, string start, Func<string, bool> isUp)
    {
        var order = new List<(string Id, int Hops)>();
        if (!graph.ContainsKey(start) || !isUp(start)) return order;

        var visited = new HashSet<string> { start };
        var queue = new Queue<(string Id, int Hops)>();
        queue.Enqueue((start, 0));

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            order.Add(current);

            foreach (var edge in graph[current.Id])
            {
                if (!isUp(edge.To)) continue;
                if (!visited.Add(edge.To)) continue; // Add returns false if it was already visited

                queue.Enqueue((edge.To, current.Hops + 1));
            }
        }

        return order;
    }

    // ------------------------------------------------------------ DFS

    /// <summary>
    /// Depth-First Search. Goes as deep as possible down one branch before backing up, using a Stack
    /// (instead of recursion, so a very long chain of nodes cannot overflow the call stack).
    /// </summary>
    public static List<(string Id, int Hops)> Dfs(Dictionary<string, List<MeshEdge>> graph, string start, Func<string, bool> isUp)
    {
        var order = new List<(string Id, int Hops)>();
        if (!graph.ContainsKey(start) || !isUp(start)) return order;

        var visited = new HashSet<string>();
        var stack = new Stack<(string Id, int Depth)>();
        stack.Push((start, 0));

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!visited.Add(current.Id)) continue;

            order.Add((current.Id, current.Depth));

            // push in reverse so the first neighbour is explored first
            var neighbours = graph[current.Id];
            for (var i = neighbours.Count - 1; i >= 0; i--)
            {
                var next = neighbours[i].To;
                if (isUp(next) && !visited.Contains(next))
                {
                    stack.Push((next, current.Depth + 1));
                }
            }
        }

        return order;
    }

    // ------------------------------------------------------------ Prim's MST

    /// <summary>
    /// Prim's algorithm. Starts at the gateway and grows a tree by repeatedly adding the CHEAPEST
    /// link that connects a node already in the tree to a node that is not. A min-heap hands me
    /// the cheapest link each time. When it finishes, the tree connects every reachable node to
    /// the gateway using the smallest possible total power cost.
    /// If some nodes cannot be reached (their links are down), they are simply left out.
    /// </summary>
    public static MstResult Prim(Dictionary<string, List<MeshEdge>> graph, string root, Func<string, bool> isUp)
    {
        var result = new MstResult();
        if (!graph.ContainsKey(root) || !isUp(root)) return result;

        var heap = new MinHeap<MstEdge>();
        result.Included.Add(root);

        foreach (var edge in graph[root])
        {
            if (isUp(edge.To))
            {
                heap.Push(new MstEdge(root, edge.To, edge.Cost), edge.Cost);
            }
        }

        while (heap.Count > 0)
        {
            var cheapest = heap.Pop().Item;

            // the heap can still hold old links to nodes we have since reached another way
            if (result.Included.Contains(cheapest.To)) continue;

            result.Included.Add(cheapest.To);
            result.Edges.Add(cheapest);
            result.Parent[cheapest.To] = cheapest.From;
            result.TotalCost += cheapest.Cost;

            foreach (var edge in graph[cheapest.To])
            {
                if (isUp(edge.To) && !result.Included.Contains(edge.To))
                {
                    heap.Push(new MstEdge(cheapest.To, edge.To, edge.Cost), edge.Cost);
                }
            }
        }

        return result;
    }

    // ------------------------------------------------------------ Dijkstra

    /// <summary>
    /// Dijkstra's shortest path from the root to every node. Unlike the MST (which minimises the cost of the
    /// WHOLE network), this finds the cheapest route for EACH node on its own. It uses the same min-heap.
    /// I show both on the dashboard because they answer different questions:
    ///   MST      = the cheapest backbone to keep switched on for everybody.
    ///   Dijkstra = the cheapest single path for one particular node's packets.
    ///
    /// <para>Reference: Dijkstra, E.W. (1959) 'A note on two problems in connexion with graphs',
    /// Numerische Mathematik, 1(1), pp. 269-271.</para>
    /// </summary>
    public static Dictionary<string, string> Dijkstra(Dictionary<string, List<MeshEdge>> graph, string root, Func<string, bool> isUp)
    {
        var parent = new Dictionary<string, string>();
        if (!graph.ContainsKey(root) || !isUp(root)) return parent;

        var best = new Dictionary<string, double> { [root] = 0 };
        var done = new HashSet<string>();
        var heap = new MinHeap<string>();
        heap.Push(root, 0);

        while (heap.Count > 0)
        {
            var current = heap.Pop();
            if (!done.Add(current.Item)) continue; // already settled with a cheaper cost

            foreach (var edge in graph[current.Item])
            {
                if (!isUp(edge.To) || done.Contains(edge.To)) continue;

                var newCost = current.Priority + edge.Cost;
                if (!best.TryGetValue(edge.To, out var oldCost) || newCost < oldCost)
                {
                    best[edge.To] = newCost;
                    parent[edge.To] = current.Item;
                    heap.Push(edge.To, newCost);
                }
            }
        }

        return parent;
    }

    /// <summary>
    /// Follows the tree from a node up to the root. Returns the list of nodes (node first, root last),
    /// or null if the node is not connected to the root.
    /// </summary>
    public static List<string>? PathToRoot(Dictionary<string, string> parent, string node, string root)
    {
        var path = new List<string> { node };
        var current = node;

        while (current != root)
        {
            if (!parent.TryGetValue(current, out var next)) return null;
            path.Add(next);
            current = next;
        }

        return path;
    }

    /// <summary>Cost of the link between two directly connected nodes (or null if they are not connected).</summary>
    public static double? LinkCost(Dictionary<string, List<MeshEdge>> graph, string a, string b)
    {
        if (!graph.TryGetValue(a, out var edges)) return null;

        foreach (var edge in edges)
        {
            if (edge.To == b) return edge.Cost;
        }
        return null;
    }
}
