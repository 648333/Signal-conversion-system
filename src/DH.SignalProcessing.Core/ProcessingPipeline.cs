using System.Collections.ObjectModel;

namespace DH.SignalProcessing;

public sealed class ProcessingPipeline
{
    private readonly List<IPipelineNode> _nodes = new();
    private readonly Dictionary<Guid, IPipelineNode> _nodeMap = new();

    public IReadOnlyList<IPipelineNode> Nodes => _nodes;
    public ReadOnlyDictionary<Guid, IPipelineNode> NodeMap => new(_nodeMap);

    public IPipelineNode AddNode(IPipelineNode node)
    {
        _nodes.Add(node);
        _nodeMap[node.Id] = node;
        return node;
    }

    public bool RemoveNode(Guid nodeId)
    {
        if (_nodeMap.TryGetValue(nodeId, out var node))
        {
            _nodes.Remove(node);
            _nodeMap.Remove(nodeId);
            return true;
        }
        return false;
    }

    public void Connect(IPipelineNode source, IPipelineNode target)
    {
    }

    public void Clear()
    {
        _nodes.Clear();
        _nodeMap.Clear();
    }

    public float[] Execute(Guid outputNodeId)
    {
        if (_nodeMap.TryGetValue(outputNodeId, out var node))
            return node.Execute();
        return Array.Empty<float>();
    }
}
