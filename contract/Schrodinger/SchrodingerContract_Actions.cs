using AElf;
using AElf.Sdk.CSharp;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace Schrodinger;

public partial class SchrodingerContract : SchrodingerContractContainer.SchrodingerContractBase
{
    public override Empty Initialize(InitializeInput input)
    {
        Assert(!State.Initialized.Value, "Already initialized.");
        Assert(input != null, "Invalid input.");

        State.GenesisContract.Value = Context.GetZeroSmartContractAddress();
        Assert(State.GenesisContract.GetContractInfo.Call(Context.Self).Deployer == Context.Sender, "No permission.");

        ProcessInitializeInput(input);

        State.Initialized.Value = true;
        return new Empty();
    }

    private void ProcessInitializeInput(InitializeInput input)
    {
        Assert(input.Admin == null || !input.Admin.Value.IsNullOrEmpty(), "Invalid input admin.");
        State.Admin.Value = input.Admin ?? Context.Sender;

        Assert(input.PointsContract == null || IsAddressValid(input.PointsContract), "Invalid input points contract.");
        State.PointsContract.Value = input.PointsContract;

        Assert(input.PointsContractDappId == null || IsHashValid(input.PointsContractDappId),
            "Invalid input points contract dapp id");
        State.PointsContractDAppId.Value = input.PointsContractDappId;

        Assert(input.MaxGen > 0, "Invalid input max gen.");
        Assert(input.ImageMaxSize > 0, "Invalid input image max size.");
        Assert(input.ImageMaxCount > 0, "Invalid input image max count.");
        Assert(input.TraitTypeMaxCount > 0, "Invalid input trait type max count.");
        Assert(input.TraitValueMaxCount > 0, "Invalid input trait value max count.");
        Assert(input.AttributeMaxLength > 0, "Invalid input attribute max length.");
        Assert(input.MaxAttributesPerGen > 0, "Invalid input max attributes per gen.");
        Assert(input.FixedTraitTypeMaxCount > 0, "Invalid input fixed trait type max count.");
        Assert(input.ImageUriMaxSize > 0, "Invalid input image uri max size.");

        State.Config.Value = new Config
        {
            MaxGen = input.MaxGen,
            ImageMaxSize = input.ImageMaxSize,
            ImageMaxCount = input.ImageMaxCount,
            TraitTypeMaxCount = input.TraitTypeMaxCount,
            TraitValueMaxCount = input.TraitValueMaxCount,
            AttributeMaxLength = input.AttributeMaxLength,
            MaxAttributesPerGen = input.MaxAttributesPerGen,
            FixedTraitTypeMaxCount = input.FixedTraitTypeMaxCount,
            ImageUriMaxSize = input.ImageUriMaxSize
        };
    }

    public override Empty SetPointsContractDAppId(Hash input)
    {
        CheckAdminPermission();

        Assert(IsHashValid(input), "Invalid input.");

        State.PointsContractDAppId.Value = input;

        return new Empty();
    }

    public override Empty SetPointsContract(Address input)
    {
        CheckAdminPermission();

        Assert(IsAddressValid(input), "Invalid input.");

        State.PointsContract.Value = input;

        return new Empty();
    }

    public override Empty SetOfficialDomainAlias(SetOfficialDomainAliasInput input)
    {
        Assert(input != null && IsStringValid(input.Alias), "Invalid input.");
        CheckAdminPermission();

        if (State.OfficialDomainAlias.Value == input!.Alias)
        {
            return new Empty();
        }

        State.OfficialDomainAlias.Value = input.Alias;
        
        Context.Fire(new OfficialDomainAliasSet
        {
            Alias = input.Alias
        });
        return new Empty();
    }
}