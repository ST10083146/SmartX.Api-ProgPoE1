using System.Collections.Generic;

namespace SmartX.Api.Models;

public enum DeploymentTier
{
    Facility,
    Zone,
    SubZone,
    Node
}

/// <summary>
/// A node in the physical deployment hierarchy, e.g.
/// Facility A -> Zone 1 -> Sub-Zone B -> Node (sensor).
/// Deliberately a plain tree (List of children) rather than a flat table,
/// because the validation requirement is explicitly about nested
/// configuration trees.
/// </summary>
public sealed class DeploymentNode
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public DeploymentTier Tier { get; init; }
    public List<DeploymentNode> Children { get; init; } = new();

    /// <summary>
    /// Only a Node tier may host a physical sensor.
    /// </summary>
    public bool IsLeaf => Tier == DeploymentTier.Node;
}

/// <summary>
/// Recursively walks a deployment tree and confirms every branch is a legal
/// nesting: Facility -> Zone -> Sub-Zone -> Node, in that order, with no
/// tier skipped and no tier repeated out of sequence. This is the
/// "recursive validation algorithm for nested device deployment trees"
/// requirement (e.g. Sub-Zone B -> Zone 1 -> Facility A must resolve
/// cleanly the other way round).
/// </summary>
public static class DeploymentValidator
{
    private static readonly DeploymentTier[] Order =
    {
        DeploymentTier.Facility,
        DeploymentTier.Zone,
        DeploymentTier.SubZone,
        DeploymentTier.Node
    };

    public static bool Validate(DeploymentNode root, out List<string> errors)
    {
        errors = new List<string>();
        ValidateRecursive(root, depth: 0, errors);
        return errors.Count == 0;
    }

    private static void ValidateRecursive(DeploymentNode node, int depth, List<string> errors)
    {
        // Base case: tier at this depth must match the expected tier for that depth.
        if (depth >= Order.Length)
        {
            errors.Add($"'{node.Name}' nests deeper than the allowed Facility->Zone->SubZone->Node chain.");
            return;
        }

        if (node.Tier != Order[depth])
        {
            errors.Add($"'{node.Name}' is a {node.Tier} at depth {depth}, expected {Order[depth]}.");
        }

        if (node.IsLeaf && node.Children.Count > 0)
        {
            errors.Add($"'{node.Name}' is a Node (leaf) but has {node.Children.Count} children attached.");
        }

        // Recursive case: validate every child the same way, one level deeper.
        foreach (var child in node.Children)
        {
            ValidateRecursive(child, depth + 1, errors);
        }
    }
}
