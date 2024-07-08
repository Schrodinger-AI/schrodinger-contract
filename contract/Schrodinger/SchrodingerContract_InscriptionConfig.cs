using System.Linq;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.Sdk.CSharp;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace Schrodinger;

public partial class SchrodingerContract
{
    public override Empty SetFixedAttribute(SetAttributeInput input)
    {
        CheckParamsAndGetInscription(input);
        var inputTraitType = input.AttributeSet.TraitType;
        var inputTraitValues = input.AttributeSet.Values;

        var traitTypes = State.FixedTraitTypeMap[input.Tick] ?? new AttributeInfos();
        traitTypes = UpdateAttributeSet(input.Tick, traitTypes, inputTraitType, inputTraitValues, out var toRemove);
        var fixCount = CheckAndGetFixedAttributesCount<AttributeInfo>(traitTypes.Data.ToList());
        CheckTraitTypeCount(fixCount, State.RandomTraitTypeMap[input.Tick]?.Data.Count ?? 0);
        FireFixedAttributeSetLogEvent(toRemove, input.AttributeSet);
        return new Empty();
    }

    public override Empty SetRandomAttribute(SetAttributeInput input)
    {
        var inscription = CheckParamsAndGetInscription(input);
        var inputTraitType = input.AttributeSet.TraitType;
        var inputTraitValues = input.AttributeSet.Values;

        var traitTypes = State.RandomTraitTypeMap[input.Tick] ?? new AttributeInfos();
        traitTypes = UpdateAttributeSet(input.Tick, traitTypes, inputTraitType, inputTraitValues, out var toRemove);
        var list = traitTypes.Data.ToList();
        var randomCount = CheckAndGetRandomAttributesCount<AttributeInfo>(list);
        CheckRandomAttributeList(list, inscription.MaxGen, inscription.AttributesPerGen);
        CheckTraitTypeCount(State.FixedTraitTypeMap[input.Tick]?.Data.Count ?? 0, randomCount);
        FireRandomAttributeSetLogEvent(toRemove, input.AttributeSet);
        return new Empty();
    }

    private void FireRandomAttributeSetLogEvent(AttributeInfo toRemove, AttributeSet attributeSet)
    {
        var logEvent = new RandomAttributeSet();
        if (toRemove != null)
        {
            logEvent.RemovedAttribute = new AttributeSet
            {
                TraitType = toRemove
            };
        }
        else
        {
            logEvent.AddedAttribute = attributeSet;
        }

        Context.Fire(logEvent);
    }

    private void FireFixedAttributeSetLogEvent(AttributeInfo toRemove, AttributeSet attributeSet)
    {
        var logEvent = new FixedAttributeSet();
        if (toRemove != null)
        {
            logEvent.RemovedAttribute = new AttributeSet
            {
                TraitType = toRemove
            };
        }
        else
        {
            logEvent.AddedAttribute = attributeSet;
        }

        Context.Fire(logEvent);
    }

    public override Empty SetImageCount(SetImageCountInput input)
    {
        Assert(IsStringValid(input.Tick), "Invalid input.");
        var inscription = CheckInscriptionExistAndPermission(input.Tick);
        CheckImageCount(input.ImageCount);
        inscription.ImageCount = input.ImageCount;
        State.InscriptionInfoMap[input.Tick] = inscription;
        Context.Fire(new ImageCountSet
        {
            Tick = input.Tick,
            ImageCount = inscription.ImageCount
        });
        return new Empty();
    }

    public override Empty SetMaxGeneration(SetMaxGenerationInput input)
    {
        Assert(IsStringValid(input.Tick), "Invalid input.");
        var inscription = CheckInscriptionExistAndPermission(input.Tick);
        CheckGeneration(input.Gen);
        inscription.MaxGen = input.Gen;
        State.InscriptionInfoMap[input.Tick] = inscription;
        Context.Fire(new MaxGenerationSet
        {
            Tick = input.Tick,
            Gen = input.Gen
        });
        return new Empty();
    }

    public override Empty SetRates(SetRatesInput input)
    {
        Assert(IsStringValid(input.Tick), "Invalid input tick.");
        CheckRate(input.LossRate, input.CommissionRate, input.MaxGenLossRate);
        var inscription = CheckInscriptionExistAndPermission(input.Tick);
        inscription.CommissionRate = input.CommissionRate;
        inscription.LossRate = input.LossRate;
        inscription.MaxGenLossRate = input.MaxGenLossRate;
        State.InscriptionInfoMap[input.Tick] = inscription;
        Context.Fire(new RatesSet
        {
            Tick = input.Tick,
            CommissionRate = input.CommissionRate,
            LossRate = input.LossRate,
            MaxGenLossRate = input.MaxGenLossRate
        });
        return new Empty();
    }

    public override Empty SetRecipient(SetRecipientInput input)
    {
        Assert(IsStringValid(input.Tick), "Invalid input tick.");
        Assert(IsAddressValid(input.Recipient), "Invalid recipient address.");
        var inscription = CheckInscriptionExistAndPermission(input.Tick);
        inscription.Recipient = input.Recipient;
        State.InscriptionInfoMap[input.Tick] = inscription;
        Context.Fire(new RecipientSet
        {
            Tick = input.Tick,
            Recipient = input.Recipient
        });
        return new Empty();
    }

    public override Empty SetInscriptionAdmin(SetInscriptionAdminInput input)
    {
        Assert(IsStringValid(input.Tick), "Invalid input tick.");
        Assert(IsAddressValid(input.Admin), "Invalid admin address.");
        var inscription = CheckInscriptionExistAndPermission(input.Tick);
        inscription.Admin = input.Admin;
        State.InscriptionInfoMap[input.Tick] = inscription;
        Context.Fire(new InscriptionAdminSet
        {
            Tick = input.Tick,
            Admin = input.Admin
        });
        return new Empty();
    }

    public override Empty SetCrossGenerationConfig(SetCrossGenerationConfigInput input)
    {
        Assert(IsStringValid(input.Tick), "Invalid input.");
        var inscription = CheckInscriptionExistAndPermission(input.Tick);
        var crossGenerationConfig = input.Config;
        CheckAndSetCrossGenerationConfig(input.Tick, crossGenerationConfig, inscription.MaxGen);
        inscription.CrossGenerationConfig = crossGenerationConfig;
        State.InscriptionInfoMap[input.Tick] = inscription;
        Context.Fire(new CrossGenerationConfigSet
        {
            Tick = input.Tick,
            CrossGenerationConfig = crossGenerationConfig
        });
        return new Empty();
    }

    public override Empty SetAttributesPerGen(SetAttributesPerGenInput input)
    {
        Assert(IsStringValid(input.Tick), "Invalid input.");
        var inscription = CheckInscriptionExistAndPermission(input.Tick);
        CheckAttributePerGen(input.AttributesPerGen, inscription.MaxGen);
        inscription.AttributesPerGen = input.AttributesPerGen;
        State.InscriptionInfoMap[input.Tick] = inscription;
        Context.Fire(new AttributesPerGenerationSet
        {
            Tick = input.Tick,
            AttributesPerGen = input.AttributesPerGen
        });
        return new Empty();
    }

    public override Empty SetSignatory(SetSignatoryInput input)
    {
        Assert(input != null, "Invalid input.");
        Assert(IsStringValid(input.Tick), "Invalid input tick.");
        Assert(IsAddressValid(input.Signatory), "Invalid signatory address.");

        CheckInscriptionExistAndPermission(input.Tick);

        if (State.SignatoryMap[input.Tick] == input.Signatory) return new Empty();

        State.SignatoryMap[input.Tick] = input.Signatory;

        Context.Fire(new SignatorySet
        {
            Tick = input.Tick,
            Signatory = input.Signatory
        });

        return new Empty();
    }

    public override Empty TransferFromReceivingAddress(TransferFromReceivingAddressInput input)
    {
        Assert(input != null, "Invalid input.");
        Assert(IsStringValid(input.Tick), "Invalid input tick.");

        var inscriptionInfo = CheckInscriptionExistAndPermission(input.Tick);

        Assert(IsAddressValid(input.Recipient), "Invalid recipient address.");
        Assert(input.Amount > 0, "Invalid amount.");

        Context.SendVirtualInline(HashHelper.ComputeFrom(input.Tick), State.TokenContract.Value,
            nameof(State.TokenContract.Transfer), new TransferInput
            {
                Amount = input.Amount,
                Symbol = inscriptionInfo.Ancestor,
                To = input.Recipient
            });

        return new Empty();
    }
}