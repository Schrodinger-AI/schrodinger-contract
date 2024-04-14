using System.Collections.Generic;
using AElf.CSharp.Core;
using AElf.Sdk.CSharp;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;
using Points.Contracts.Point;

namespace Schrodinger;

public partial class SchrodingerContract
{
    public override Empty Join(JoinInput input)
    {
        Assert(input != null && IsStringValid(input.Domain), "Invalid input.");
        Assert(!State.JoinRecord[Context.Sender], "Already joined.");

        JoinPointsContract(input.Domain);

        return new Empty();
    }

    public override BoolValue GetJoinRecord(Address address)
    {
        return new BoolValue { Value = State.JoinRecord[address] };
    }

    private void JoinPointsContract(string domain, Address registrant = null)
    {
        registrant ??= Context.Sender;
        if (!IsHashValid(State.PointsContractDAppId.Value) || State.PointsContract.Value == null)
        {
            return;
        }

        if (State.JoinRecord[registrant]) return;

        domain ??= State.PointsContract.GetDappInformation.Call(new GetDappInformationInput
        {
            DappId = State.PointsContractDAppId.Value
        })?.DappInfo?.OfficialDomain;

        State.JoinRecord[registrant] = true;

        State.PointsContract.Join.Send(new Points.Contracts.Point.JoinInput
        {
            DappId = State.PointsContractDAppId.Value,
            Domain = domain,
            Registrant = registrant
        });

        Context.Fire(new Joined
        {
            Domain = domain,
            Registrant = registrant
        });
    }

    private void SettlePoints(string actionName, long amount, int inscriptionDecimal)
    {
        var proportion = GetProportion(actionName);

        var points = new BigIntValue(amount).Mul(new BigIntValue(proportion));
        var userPointsValue = new BigIntValue(points).Div(new BigIntValue(10).Pow(inscriptionDecimal));
        State.PointsContract.Settle.Send(new SettleInput
        {
            DappId = State.PointsContractDAppId.Value,
            ActionName = actionName,
            UserAddress = Context.Sender,
            UserPointsValue = userPointsValue
        });
    }

    private long GetProportion(string actionName)
    {
        var proportion = State.PointsProportion[actionName];
        proportion = actionName switch
        {
            nameof(Adopt) => proportion == 0 ? SchrodingerContractConstants.DefaultAdoptProportion : proportion,
            nameof(Reroll) => proportion == 0 ? SchrodingerContractConstants.DefaultRerollProportion : proportion,
            _ => proportion == 0 ? SchrodingerContractConstants.DefaultProportion : proportion
        };
        return proportion;
    }


    public override Empty BatchSettle(BatchSettleInput input)
    {
        CheckSettleAdminPermission();
        Assert(input.UserPointsList != null && input.UserPointsList.Count > 0, "Invalid input.");
        var userPointsList = new List<Points.Contracts.Point.UserPoints>();
        foreach (var userPoints in input.UserPointsList)
        {
            JoinPointsContract(null, userPoints.UserAddress);
            userPointsList.Add(new Points.Contracts.Point.UserPoints
            {
                UserAddress = userPoints.UserAddress,
                UserPointsValue = userPoints.UserPointsValue
            });
        }

        State.PointsContract.BatchSettle.Send(new Points.Contracts.Point.BatchSettleInput
        {
            ActionName = input.ActionName,
            DappId = State.PointsContractDAppId.Value,
            UserPointsList = { userPointsList }
        });
        return new Empty();
    }

    public override Empty SetPointsProportionList(SetPointsProportionListInput input)
    {
        CheckAdminPermission();
        Assert(input.Data.Count > 0 && input.Data.Count <= SchrodingerContractConstants.DefaultMaxProportionListCount,
            "Invalid input list count.");
        foreach (var pointsProportion in input.Data)
        {
            Assert(pointsProportion != null, "Invalid input.");
            var actionName = pointsProportion.ActionName;
            var proportion = pointsProportion.Proportion;
            Assert(IsStringValid(actionName) && proportion > 0, "Invalid action name and proportion.");
            State.PointsProportion[actionName] = proportion;
        }

        return new Empty();
    }

    public override Empty SetPointsSettleAdmin(Address input)
    {
        Assert(IsAddressValid(input), "Invalid input points settle admin.");
        CheckAdminPermission();
        State.PointsSettleAdmin.Value = input;
        return new Empty();
    }

    public override Int64Value GetPointsProportion(StringValue input)
    {
        return new Int64Value
        {
            Value = State.PointsProportion[input.Value]
        };
    }

    public override Address GetPointsSettleAdmin(Empty input)
    {
        return State.PointsSettleAdmin.Value;
    }

    public override Empty AcceptReferral(AcceptReferralInput input)
    {
        CheckInitialized();
        
        Assert(input != null, "Invalid input.");
        Assert(IsAddressValid(input.Referrer) && State.JoinRecord[input.Referrer], "Invalid referrer.");
        Assert(!State.JoinRecord[Context.Sender], "Already joined.");
        
        State.JoinRecord[Context.Sender] = true;
        
        State.PointsContract.AcceptReferral.Send(new Points.Contracts.Point.AcceptReferralInput
        {
            DappId = State.PointsContractDAppId.Value,
            Referrer = input.Referrer,
            Invitee = Context.Sender
        });
        
        Context.Fire(new ReferralAccepted
        {
            Invitee = Context.Sender,
            Referrer = input.Referrer
        });
        
        return new Empty();
    }
}