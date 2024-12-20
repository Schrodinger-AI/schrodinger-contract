using System.Linq;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace Schrodinger;

public partial class SchrodingerContract
{
    public override Address GetAdmin(Empty input)
    {
        return State.Admin.Value;
    }

    public override Hash GetPointsContractDAppId(Empty input)
    {
        return State.PointsContractDAppId.Value;
    }

    public override Address GetPointsContract(Empty input)
    {
        return State.PointsContract.Value;
    }

    public override Config GetConfig(Empty input)
    {
        return State.Config.Value;
    }

    public override Address GetSignatory(StringValue input)
    {
        return State.SignatoryMap[input.Value];
    }

    public override Int64Value GetImageUriMaxSize(Empty input)
    {
        return new Int64Value { Value = State.Config.Value.ImageUriMaxSize };
    }

    #region inscription

    public override InscriptionInfo GetInscriptionInfo(StringValue input)
    {
        var result = new InscriptionInfo();
        if (input != null && IsStringValid(input.Value))
        {
            result = State.InscriptionInfoMap[input.Value] ?? new InscriptionInfo();
        }

        return result;
    }

    public override StringValue GetTick(StringValue input)
    {
        return new StringValue
        {
            Value = input.Value.Split(SchrodingerContractConstants.Separator).First()
        };
    }

    public override StringValue GetParent(StringValue input)
    {
        var adoptId = State.SymbolAdoptIdMap[input.Value];
        if (adoptId == null) return new StringValue();

        var adoptInfo = State.AdoptInfoMap[adoptId];

        return new StringValue
        {
            Value = adoptInfo?.Parent
        };
    }

    public override AttributeInfos GetAttributeTypes(StringValue input)
    {
        var result = new AttributeInfos();
        var tick = input.Value;
        if (string.IsNullOrEmpty(tick))
        {
            return result;
        }

        var fixedTraitTypeMap = State.FixedTraitTypeMap[tick] ?? new AttributeInfos();
        var randomTraitTypeMap = State.RandomTraitTypeMap[tick] ?? new AttributeInfos();
        result.Data.AddRange(fixedTraitTypeMap.Data);
        result.Data.AddRange(randomTraitTypeMap.Data);
        return result;
    }

    public override AttributeInfos GetAttributeValues(GetAttributeValuesInput input)
    {
        var result = new AttributeInfos();
        var tick = input.Tick;
        var traitType = input.TraitType;
        if (string.IsNullOrEmpty(tick) && string.IsNullOrEmpty(traitType))
        {
            return result;
        }

        var upperWeightSums = State.UpperWeightSumsMap[tick][traitType];

        if (upperWeightSums == null) return result;

        for (var i = 0; i < upperWeightSums.Data.Count; i++)
        {
            result.Data.AddRange(State.TraitValuesMap[tick][traitType][i].TraitValueList.Data);
        }

        return result;
    }

    public override AdoptInfo GetAdoptInfo(Hash input)
    {
        return State.AdoptInfoMap[input];
    }

    public override GetTokenInfoOutput GetTokenInfo(StringValue input)
    {
        var adoptId = State.SymbolAdoptIdMap[input.Value];
        if (adoptId == null) return new GetTokenInfoOutput();

        var adoptInfo = State.AdoptInfoMap[adoptId];
        if (adoptInfo == null) return new GetTokenInfoOutput();

        return new GetTokenInfoOutput
        {
            AdoptId = adoptId,
            Parent = adoptInfo.Parent,
            ParentGen = adoptInfo.ParentGen,
            ParentAttributes = adoptInfo.ParentAttributes,
            Attributes = adoptInfo.Attributes,
            Gen = adoptInfo.Gen
        };
    }

    #endregion

    public override Address GetReceivingAddress(StringValue input)
    {
        if (input == null || !IsStringValid(input.Value)) return new Address();

        return GetReceivingAddress(input.Value);
    }

    public override StringValue GetOfficialDomainAlias(Empty input)
    {
        return new StringValue
        {
            Value = State.OfficialDomainAlias.Value
        };
    }

    public override GetRewardConfigOutput GetRewardConfig(StringValue input)
    {
        var output = new GetRewardConfigOutput();

        if (input != null && IsStringValid(input.Value))
        {
            output = new GetRewardConfigOutput
            {
                List = State.RewardListMap[input.Value],
                Pool = GetSpinPoolAddress(input.Value)
            };
        }

        return output;
    }

    public override SpinInfo GetSpinInfo(Hash input)
    {
        return IsHashValid(input) ? State.SpinInfoMap[input] : new SpinInfo();
    }

    public override VoucherInfo GetVoucherInfo(Hash input)
    {
        return IsHashValid(input) ? State.VoucherInfoMap[input] : new VoucherInfo();
    }

    public override Int64Value GetAdoptionVoucherAmount(GetAdoptionVoucherAmountInput input)
    {
        var output = new Int64Value();

        if (input != null && IsStringValid(input.Tick) && IsAddressValid(input.Account))
        {
            output.Value = State.AdoptionVoucherMap[input.Tick][input.Account];
        }

        return output;
    }

    public override AddressList GetAirdropController(StringValue input)
    {
        return State.AirdropControllerMap[input.Value];
    }

    public override RerollConfig GetRerollConfig(StringValue input)
    {
        return input != null && IsStringValid(input.Value) ? State.RerollConfigMap[input.Value] : new RerollConfig();
    }

    public override GetMergeConfigOutput GetMergeConfig(StringValue input)
    {
        if (input == null || !IsStringValid(input.Value)) return new GetMergeConfigOutput();

        var output = new GetMergeConfigOutput
        {
            Tick = input.Value,
            MaximumLevel = State.MaximumLevelMap[input.Value],
            Config = State.MergeConfigMap[input.Value]
        };

        var mergeRates = new MergeRates();

        for (var i = 1; i <= output.MaximumLevel; i++)
        {
            mergeRates.Data.Add(new MergeRate
            {
                Level = i,
                Rate = State.MergeRatesMap[output.Tick][i]
            });
        }

        output.MergeRates = mergeRates;

        return output;
    }

    public override VoucherAdoptionConfig GetVoucherAdoptionConfig(StringValue input)
    {
        return input != null && IsStringValid(input.Value)
            ? State.VoucherAdoptionConfigMap[input.Value]
            : new VoucherAdoptionConfig();
    }

    public override Int64Value GetSymbolCount(StringValue input)
    {
        return new Int64Value
        {
            Value = State.SymbolCountMap[input.Value]
        };
    }

    public override BoolValue GetRedeemSwitchStatus(StringValue input)
    {
        return new BoolValue
        {
            Value = State.RedeemSwitch[input.Value]
        };
    }

    public override RebateConfig GetRebateConfig(StringValue input)
    {
        return State.RebateConfig[input.Value];
    }
}