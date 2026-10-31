namespace SmartX.Api.Models;

public record MeshNodeDto(string Id, string NodeId, string Zone, string Category, bool IsGateway, bool Up, bool ManuallyFailed, string Status);

public record MeshLinkDto(string A, string B, double Cost, bool InMst);

public record MstDto(double TotalCost, double AllLinksCost, List<MeshLinkDto> Edges, List<string> Unreached);

public record TopologyDto(
    List<MeshNodeDto> Nodes,
    List<MeshLinkDto> Links,
    MstDto Mst,
    List<string> Reachable,
    List<string> Isolated,
    List<string> Down);

public record RouteHop(string Id, string NodeId);

public record RouteDto(
    string Id,
    string NodeId,
    bool Found,
    string Message,
    List<RouteHop> Hops,
    double TotalCost,
    int HopCount,
    double? DirectLinkCost,
    List<RouteHop> ShortestHops,
    double ShortestCost);

public record ConnectedNode(string Id, string NodeId, string Zone, int Hops);

public record ConnectedDto(string Mode, string Start, string? Zone, List<ConnectedNode> Order, List<string> Down, List<string> Isolated);

public record RerouteEntry(string Id, string NodeId, List<string> OldPath, List<string> NewPath, string NextHop, double OldCost, double NewCost, bool Isolated);

public record RerouteReport(string Action, string NodeId, string Summary, double MstCostBefore, double MstCostAfter, List<RerouteEntry> Changes);

public record LinkRequest(string A, string B, double Cost);

public record HistoryPoint(DateTimeOffset T, float Value, bool Anomalous);

public record HistoryResult(
    string Id,
    string NodeId,
    string Unit,
    int TotalInRange,
    int Returned,
    int NodesVisited,
    int TreeSize,
    int TreeHeight,
    double ElapsedMicroseconds,
    List<HistoryPoint> Readings);

public record BenchmarkResult(
    int Readings,
    int ResultsFound,
    int TreeHeight,
    bool TreeValid,
    int AvlNodesVisited,
    double AvlMicroseconds,
    double LinearMicroseconds,
    double Speedup);
