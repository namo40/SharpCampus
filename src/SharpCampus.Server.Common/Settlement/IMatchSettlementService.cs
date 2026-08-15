namespace SharpCampus.Server.Common.Settlement;

public interface IMatchSettlementService
{
    Task<MatchSettlement> SettleAsync(MatchSettlementRequest request);
}
