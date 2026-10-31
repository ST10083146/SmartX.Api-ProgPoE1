namespace SmartX.Api.Services;

/// <summary>
/// A binary min-heap that I wrote myself. The smallest priority is always at the top.
/// It is stored in a plain List (an "array-based" heap): the children of the item at index i
/// are at 2i+1 and 2i+2, and its parent is at (i-1)/2.
///
/// Push and Pop both take O(log n) because an item only moves up or down one path of the tree.
/// Prim's algorithm uses it to always pick the cheapest wireless link next.
///
/// <para>References (full list in README, IIE Harvard Anglia style):</para>
///   GeeksforGeeks (n.d.) Types of Heap Data Structure.
///     Available at: https://www.geeksforgeeks.org/types-of-heap-data-structure (Accessed: 29 October 2026).
///   Cormen, T.H., Leiserson, C.E., Rivest, R.L. and Stein, C. (2022) Introduction to Algorithms. 4th edn.
///     Cambridge, MA: MIT Press.
/// My own implementation. .NET also has a built-in PriorityQueue (used in Part 2), but the brief names Heaps
/// as a topic, so here I build the heap by hand to show how it works.
/// </summary>
public class MinHeap<T>
{
    private readonly List<(T Item, double Priority)> _data = new List<(T Item, double Priority)>();

    public int Count
    {
        get { return _data.Count; }
    }

    public void Push(T item, double priority)
    {
        _data.Add((item, priority));
        SiftUp(_data.Count - 1);
    }

    /// <summary>Removes and returns the item with the smallest priority.</summary>
    public (T Item, double Priority) Pop()
    {
        if (_data.Count == 0)
        {
            throw new InvalidOperationException("The heap is empty.");
        }

        var top = _data[0];
        var last = _data[_data.Count - 1];
        _data.RemoveAt(_data.Count - 1);

        if (_data.Count > 0)
        {
            _data[0] = last;   // move the last item to the top...
            SiftDown(0);       // ...and let it sink to where it belongs
        }

        return top;
    }

    private void SiftUp(int index)
    {
        while (index > 0)
        {
            var parent = (index - 1) / 2;
            if (_data[index].Priority >= _data[parent].Priority) break;

            Swap(index, parent);
            index = parent;
        }
    }

    private void SiftDown(int index)
    {
        var count = _data.Count;

        while (true)
        {
            var left = 2 * index + 1;
            var right = left + 1;
            var smallest = index;

            if (left < count && _data[left].Priority < _data[smallest].Priority) smallest = left;
            if (right < count && _data[right].Priority < _data[smallest].Priority) smallest = right;

            if (smallest == index) break;

            Swap(index, smallest);
            index = smallest;
        }
    }

    private void Swap(int a, int b)
    {
        var temp = _data[a];
        _data[a] = _data[b];
        _data[b] = temp;
    }
}
