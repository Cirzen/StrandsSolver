namespace Solver;

public class TrieNode
{
    public readonly Dictionary<char, TrieNode> Children = new();
    public bool IsEndOfWord = false;
}