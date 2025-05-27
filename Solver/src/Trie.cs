
namespace Solver;

public class Trie
{
    private TrieNode _root;

    public Trie()
    {
        _root = new();
    }

    /// <summary>
    /// Gets a value indicating whether the current data structure is empty.
    /// </summary>
    public bool IsEmpty => _root.Children.Count == 0 && !_root.IsEndOfWord;

    /// <summary>
    /// Clears all words from the Trie.
    /// </summary>
    public void Clear()
    {
        _root = new();
    }

    /// <summary>
    /// Inserts a word into the trie.
    /// </summary>
    /// <remarks>This method adds the specified word to the trie, creating any necessary nodes along the way.
    /// After insertion, the word can be searched or used in other trie operations.</remarks>
    /// <param name="word">The word to insert. Cannot be null or empty.</param>
    public void Insert(string word)
    {
        var node = _root;
        foreach (char c in word)
        {
            if (!node.Children.ContainsKey(c))
            {
                node.Children[c] = new();
            }
            node = node.Children[c];
        }
        node.IsEndOfWord = true;
    }

    /// <summary>
    /// Determines whether the specified word exists in the data structure.
    /// </summary>
    /// <param name="word">The word to search for. Cannot be null or empty.</param>
    /// <returns><see langword="true"/> if the word exists and is marked as a complete word; otherwise, <see langword="false"/>.</returns>
    public bool Search(string word)
    {
        var node = FindNode(word);
        return node is { IsEndOfWord: true };
    }

    /// <summary>
    /// Determines whether the current instance starts with the specified prefix.
    /// </summary>
    /// <param name="prefix">The string to compare against the start of the current instance.</param>
    /// <returns><see langword="true"/> if the current instance starts with the specified <paramref name="prefix"/>;  otherwise,
    /// <see langword="false"/>.</returns>
    public bool StartsWith(string prefix)
    {
        return FindNode(prefix) != null;
    }

    /// <summary>
    /// Helper function to find the node in the trie that corresponds to the specified prefix.
    /// </summary>
    /// <param name="prefix">The prefix to search for in the trie. Must not be <see langword="null"/>.</param>
    /// <returns>The <see cref="TrieNode"/> corresponding to the last character of the prefix,  or <see langword="null"/> if the
    /// prefix does not exist in the trie.</returns>
    private TrieNode FindNode(string prefix)
    {
        var node = _root;
        foreach (char c in prefix)
        {
            if (!node.Children.TryGetValue(c, out node))
            {
                return null;
            }
        }
        return node;
    }
}