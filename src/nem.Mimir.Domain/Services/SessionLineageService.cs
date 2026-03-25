namespace nem.Mimir.Domain.Services;

using nem.Mimir.Domain.Entities;

/// <summary>
/// Provides in-memory lineage graph traversal for conversation fork trees.
/// All methods are pure functions operating on a provided collection of conversations.
/// </summary>
public static class SessionLineageService
{
    /// <summary>
    /// Represents a node in the conversation lineage tree.
    /// </summary>
    public sealed class LineageNode
    {
        public Guid ConversationId { get; }
        public Guid? ParentConversationId { get; }
        public string? ForkReason { get; }
        public List<LineageNode> Children { get; } = [];

        public LineageNode(Guid conversationId, Guid? parentConversationId, string? forkReason)
        {
            ConversationId = conversationId;
            ParentConversationId = parentConversationId;
            ForkReason = forkReason;
        }
    }

    /// <summary>
    /// Builds a lineage tree from a collection of conversations.
    /// Returns all root nodes (conversations with no parent).
    /// Handles orphaned children (parent not in collection) by treating them as roots.
    /// Protects against circular references by tracking visited nodes.
    /// </summary>
    public static IReadOnlyList<LineageNode> BuildLineageTree(IEnumerable<Conversation> conversations)
    {
        ArgumentNullException.ThrowIfNull(conversations);

        var conversationList = conversations.ToList();
        var nodeMap = new Dictionary<Guid, LineageNode>(conversationList.Count);

        // Create nodes
        foreach (var conversation in conversationList)
        {
            nodeMap[conversation.Id] = new LineageNode(
                conversation.Id,
                conversation.ParentConversationId,
                conversation.ForkReason);
        }

        var roots = new List<LineageNode>();

        // Wire parent-child relationships
        foreach (var node in nodeMap.Values)
        {
            if (node.ParentConversationId.HasValue &&
                nodeMap.TryGetValue(node.ParentConversationId.Value, out var parent))
            {
                parent.Children.Add(node);
            }
            else
            {
                // Root node or orphan (parent not in collection)
                roots.Add(node);
            }
        }

        return roots;
    }

    /// <summary>
    /// Gets the ordered ancestor chain from a conversation back to its root.
    /// Returns [self, parent, grandparent, ...root].
    /// Protects against circular references with a visited set.
    /// </summary>
    public static IReadOnlyList<Guid> GetAncestors(Guid conversationId, IEnumerable<Conversation> conversations)
    {
        ArgumentNullException.ThrowIfNull(conversations);

        var lookup = conversations.ToDictionary(c => c.Id);
        var ancestors = new List<Guid>();
        var visited = new HashSet<Guid>();
        var current = conversationId;

        while (lookup.TryGetValue(current, out var conversation) && visited.Add(current))
        {
            ancestors.Add(current);

            if (!conversation.ParentConversationId.HasValue)
                break;

            current = conversation.ParentConversationId.Value;
        }

        return ancestors;
    }

    /// <summary>
    /// Gets all descendants of a conversation (children, grandchildren, etc.).
    /// Returns a flat list of descendant conversation IDs.
    /// Protects against circular references with a visited set.
    /// </summary>
    public static IReadOnlyList<Guid> GetDescendants(Guid conversationId, IEnumerable<Conversation> conversations)
    {
        ArgumentNullException.ThrowIfNull(conversations);

        var childrenLookup = conversations
            .Where(c => c.ParentConversationId.HasValue)
            .GroupBy(c => c.ParentConversationId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(c => c.Id).ToList());

        var descendants = new List<Guid>();
        var visited = new HashSet<Guid>();
        var queue = new Queue<Guid>();

        if (childrenLookup.TryGetValue(conversationId, out var directChildren))
        {
            foreach (var child in directChildren)
                queue.Enqueue(child);
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!visited.Add(current))
                continue;

            descendants.Add(current);

            if (childrenLookup.TryGetValue(current, out var children))
            {
                foreach (var child in children)
                    queue.Enqueue(child);
            }
        }

        return descendants;
    }
}
