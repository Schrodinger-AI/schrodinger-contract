using AElf.Sdk.CSharp;
using Google.Protobuf.WellKnownTypes;

namespace Schrodinger;

public partial class SchrodingerContract
{
    public override Empty Deploy(DeployInput input)
    {
        CheckDeployParams(input);
        var tick = input.Tick;
        Assert(State.InscriptionInfoMap[tick] == null, "Already exist.");
        var ancestor = GetInscriptionSymbol(tick);
        var inscription = new InscriptionInfo
        {
            Ancestor = ancestor,
            Decimals = input.Decimals,
            MaxGen = input.MaxGeneration,
            LossRate = input.LossRate,
            CommissionRate = input.CommissionRate,
            Recipient = input.Recipient ?? Context.Sender,
            Admin = Context.Sender,
            CrossGenerationConfig = input.CrossGenerationConfig,
            IsWeightEnabled = input.IsWeightEnabled,
            ImageCount = input.ImageCount,
            AttributesPerGen = input.AttributesPerGen,
            MaxGenLossRate = input.MaxGenLossRate,
            RewardThreshold = input.RewardThreshold
        };
        State.InscriptionInfoMap[tick] = inscription;
        State.SignatoryMap[tick] = input.Signatory;
        
        var attributeList =
            SetAttributeList(tick, inscription.MaxGen, input.AttributeLists, inscription.AttributesPerGen);
        // Generate external info
        var externalInfo = GenerateExternalInfo(tick, input.Image, input.TotalSupply, input.ImageUri);
        CreateInscription(tick, inscription.Decimals, input.TotalSupply, externalInfo, input.Issuer);
        State.SymbolCountMap[tick] = SchrodingerContractConstants.DefaultSymbolIndexStart;
        JoinPointsContract(input.Domain);

        Context.Fire(new Deployed
        {
            Tick = tick,
            Ancestor = ancestor,
            MaxGeneration = inscription.MaxGen,
            TotalSupply = input.TotalSupply,
            Decimals = inscription.Decimals,
            AttributeLists = attributeList,
            ImageCount = inscription.ImageCount,
            Issuer = input.Issuer ?? Context.Sender,
            Owner = Context.Self,
            IssueChainId = Context.ChainId,
            Deployer = Context.Sender,
            TokenName = tick,
            ExternalInfos = new ExternalInfos
            {
                Value = { externalInfo.Value }
            },
            CrossGenerationConfig = inscription.CrossGenerationConfig,
            IsWeightEnabled = inscription.IsWeightEnabled,
            Admin = inscription.Admin,
            LossRate = inscription.LossRate,
            CommissionRate = inscription.CommissionRate,
            AttributesPerGen = inscription.AttributesPerGen,
            ImageUri = input.ImageUri,
            Signatory = input.Signatory,
            MaxGenLossRate = inscription.MaxGenLossRate,
            RewardThreshold = inscription.RewardThreshold
        });
        return new Empty();
    }
}