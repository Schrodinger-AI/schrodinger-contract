using Google.Protobuf.WellKnownTypes;

namespace Schrodinger.Contracts.TestPointsContract;

public class TestPointsContract : TestPointsContractContainer.TestPointsContractBase
{
    public override Empty Join(JoinInput input)
    {
        return new Empty();
    }

    public override Empty Settle(SettleInput input)
    {
        return new Empty();
    }

    public override Empty AcceptReferral(AcceptReferralInput input)
    {
        return new Empty();
    }
}