using System.Collections.Generic;
using System.Linq;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.CSharp.Core;
using AElf.Sdk.CSharp;
using AElf.Types;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace Schrodinger;

public partial class SchrodingerContract
{
    public override Empty Adopt(AdoptInput input)
    {
        ValidateAdoptInput(input);

        var tick = GetTickFromSymbol(input.Parent);
        var inscriptionInfo = State.InscriptionInfoMap[tick];
        Assert(inscriptionInfo != null, "Tick not deployed.");

        var adoptId = GenerateAdoptId(tick);
        Assert(State.AdoptInfoMap[adoptId] == null, "Adopt id already exists.");

        GetParentInfo(inscriptionInfo, input.Parent, out var parentGen, out var parentAttributes);

        Assert(parentGen < inscriptionInfo.MaxGen, "Exceeds max gen.");

        var adoptInfo = new AdoptInfo
        {
            AdoptId = adoptId,
            Parent = input.Parent,
            ParentGen = parentGen,
            ParentAttributes = parentAttributes,
            BlockHeight = Context.CurrentHeight,
            Adopter = Context.Sender,
            ImageCount = inscriptionInfo.ImageCount
        };

        State.AdoptInfoMap[adoptId] = adoptInfo;

        CalculateAmount(inscriptionInfo.LossRate, inscriptionInfo.CommissionRate, input.Amount, out var lossAmount,
            out var commissionAmount, out var outputAmount);

        var minOutputAmount = new BigIntValue(SchrodingerContractConstants.Ten).Pow(inscriptionInfo.Decimals);
        Assert(outputAmount >= minOutputAmount, "Input amount not enough.");

        adoptInfo.InputAmount = input.Amount;
        adoptInfo.OutputAmount = outputAmount;

        ProcessAdoptTransfer(input.Parent, input.Amount, lossAmount, commissionAmount, inscriptionInfo.Recipient,
            inscriptionInfo.Ancestor, parentGen);

        var randomHash = GetRandomHash(tick);
        adoptInfo.Gen = GenerateGen(inscriptionInfo, parentGen, randomHash, tick);
        adoptInfo.Attributes = GenerateAttributes(parentAttributes, tick, inscriptionInfo.AttributesPerGen,
            adoptInfo.Gen.Sub(adoptInfo.ParentGen), randomHash);
        adoptInfo.Symbol = GenerateSymbol(tick);
        adoptInfo.TokenName = GenerateTokenName(adoptInfo.Symbol, adoptInfo.Gen);

        JoinPointsContract(input.Domain);
        SettlePoints(nameof(Adopt), adoptInfo.InputAmount, inscriptionInfo.Decimals);

        Context.Fire(new Adopted
        {
            AdoptId = adoptId,
            Parent = input.Parent,
            ParentGen = parentGen,
            InputAmount = input.Amount,
            LossAmount = lossAmount,
            CommissionAmount = commissionAmount,
            OutputAmount = outputAmount,
            ImageCount = inscriptionInfo.ImageCount,
            Adopter = Context.Sender,
            BlockHeight = Context.CurrentHeight,
            Attributes = adoptInfo.Attributes,
            Gen = adoptInfo.Gen,
            Ancestor = inscriptionInfo.Ancestor,
            Symbol = adoptInfo.Symbol,
            TokenName = adoptInfo.TokenName
        });

        return new Empty();
    }

    private void ValidateAdoptInput(AdoptInput input)
    {
        Assert(input != null, "Invalid input.");
        Assert(IsSymbolValid(input.Parent), "Invalid input parent.");
        Assert(input.Amount > 0, "Invalid input amount.");
        Assert(IsStringValid(input.Domain), "Invalid input domain.");
    }

    private Hash GenerateAdoptId(string tick)
    {
        return HashHelper.ConcatAndCompute(HashHelper.ComputeFrom(tick),
            HashHelper.ComputeFrom(State.SymbolCountMap[tick]));
    }

    private void GetParentInfo(InscriptionInfo inscriptionInfo, string parentSymbol, out int parentGen,
        out Attributes parentAttributes)
    {
        // gen0
        if (parentSymbol == inscriptionInfo.Ancestor)
        {
            parentGen = 0;
            parentAttributes = new Attributes();
        }
        else
        {
            var adoptId = State.SymbolAdoptIdMap[parentSymbol];
            Assert(adoptId != null, $"{parentSymbol} not exists.");

            var parentInfo = State.AdoptInfoMap[adoptId];
            parentGen = parentInfo.Gen;
            parentAttributes = parentInfo.Attributes;
        }
    }

    private void CalculateAmount(long lossRate, long commissionRate, long inputAmount, out long lossAmount,
        out long commissionAmount, out long outputAmount)
    {
        // calculate amount
        lossAmount = inputAmount.Mul(lossRate).Div(SchrodingerContractConstants.Denominator);
        if (lossAmount == 0 && lossRate != 0) lossAmount = lossAmount.Add(1);

        outputAmount = inputAmount.Sub(lossAmount);

        commissionAmount = lossAmount.Mul(commissionRate).Div(SchrodingerContractConstants.Denominator);
        if (commissionAmount == 0 && commissionRate != 0) commissionAmount = commissionAmount.Add(1);

        lossAmount = lossAmount.Sub(commissionAmount);
    }

    private void ProcessAdoptTransfer(string symbol, long inputAmount, long lossAmount, long commissionAmount,
        Address recipient, string ancestor, int parentGen)
    {
        // transfer parent from sender
        State.TokenContract.TransferFrom.Send(new TransferFromInput
        {
            Amount = inputAmount,
            From = Context.Sender,
            To = Context.Self,
            Symbol = symbol
        });

        // burn non-gen0
        if (parentGen > 0)
        {
            // burn token
            State.TokenContract.Burn.Send(new BurnInput
            {
                Symbol = symbol,
                Amount = inputAmount
            });
        }

        // transfer ancestor to virtual address
        if (lossAmount > 0)
        {
            State.TokenContract.Transfer.Send(new TransferInput
            {
                Symbol = ancestor,
                Amount = lossAmount,
                To = GetReceivingAddress(GetTickFromSymbol(ancestor))
            });
        }

        // send commission to recipient
        State.TokenContract.Transfer.Send(new TransferInput
        {
            Amount = commissionAmount,
            To = recipient,
            Symbol = ancestor
        });
    }

    private void ProcessRerollTransfer(string symbol, long amount, string ancestor)
    {
        // transfer token from sender
        State.TokenContract.TransferFrom.Send(new TransferFromInput
        {
            Amount = amount,
            From = Context.Sender,
            To = Context.Self,
            Symbol = symbol
        });

        // burn token
        State.TokenContract.Burn.Send(new BurnInput
        {
            Symbol = symbol,
            Amount = amount
        });

        // send gen0 to sender
        State.TokenContract.Transfer.Send(new TransferInput
        {
            Amount = amount,
            To = Context.Sender,
            Symbol = ancestor
        });
    }

    private Hash GetRandomHash(string tick)
    {
        if (State.ConsensusContract.Value == null)
        {
            State.ConsensusContract.Value =
                Context.GetContractAddressByName(SmartContractConstants.ConsensusContractSystemName);
        }

        var randomHash = State.ConsensusContract.GetRandomHash.Call(new Int64Value
        {
            Value = Context.CurrentHeight
        });

        return HashHelper.ConcatAndCompute(HashHelper.ComputeFrom(State.SymbolCountMap[tick]), randomHash);
    }

    private int GenerateGen(InscriptionInfo inscriptionInfo, int parentGen, Hash randomHash, string tick)
    {
        var crossGenerationConfig = inscriptionInfo.CrossGenerationConfig;

        parentGen++;

        if (parentGen == inscriptionInfo.MaxGen || crossGenerationConfig.Gen == 0 ||
            !IsCrossGenerationHappened(crossGenerationConfig.CrossGenerationProbability, randomHash))
        {
            return parentGen;
        }

        if (crossGenerationConfig.CrossGenerationFixed)
        {
            var newGen = parentGen + crossGenerationConfig.Gen;
            return newGen >= inscriptionInfo.MaxGen ? inscriptionInfo.MaxGen : newGen;
        }

        var gens = new List<ItemWithWeight>();

        for (var i = 1; i <= crossGenerationConfig.Gen; i++)
        {
            gens.Add(new ItemWithWeight
            {
                Item = i.ToString(),
                Weight = crossGenerationConfig.Weights[i - 1]
            });
        }

        var selected =
            int.TryParse(
                GetRandomItems(randomHash, nameof(GenerateGen), gens, 1, 0)
                    .FirstOrDefault(), out var gen);

        var result = parentGen.Add(selected ? gen : 0);
        return result >= inscriptionInfo.MaxGen ? inscriptionInfo.MaxGen : result;
    }

    private Hash CalculateRandomHash(Hash randomHash, string seed)
    {
        return HashHelper.ConcatAndCompute(randomHash, HashHelper.ComputeFrom(seed));
    }

    private bool IsCrossGenerationHappened(long probability, Hash randomHash)
    {
        var random = Context.ConvertHashToInt64(CalculateRandomHash(randomHash, nameof(IsCrossGenerationHappened)), 1,
            SchrodingerContractConstants.Denominator + 1);
        return random <= probability;
    }

    private List<string> GetRandomItems(Hash randomHash, string seed, List<ItemWithWeight> items, int count,
        long totalWeights)
    {
        var selectedItems = new List<string>();

        if (totalWeights == 0)
        {
            foreach (var item in items)
            {
                totalWeights = totalWeights.Add(item.Weight);
            }
        }

        var hash = CalculateRandomHash(randomHash, seed);

        while (selectedItems.Count < count && items.Count > 0)
        {
            hash = CalculateRandomHash(hash, selectedItems.Count.ToString());
            var random = Context.ConvertHashToInt64(hash, 1, totalWeights + 1);
            var sum = 0L;
            for (var i = 0; i < items.Count; i++)
            {
                sum = sum.Add(items[i].Weight);

                if (random > sum && i < items.Count - 1) continue;

                selectedItems.Add(items[i].Item);
                totalWeights = totalWeights.Sub(items[i].Weight);
                items.RemoveAt(i);
                break;
            }
        }

        return selectedItems;
    }

    private Attributes GenerateAttributes(Attributes parentAttributes, string tick, int attributesPerGen, int amount,
        Hash randomHash)
    {
        var attributes = new Attributes();

        // gen0 -> gen1
        if (parentAttributes.Data.Count == 0)
        {
            attributes.Data.AddRange(State.FixedTraitTypeMap[tick].Data.Select(t => new Attribute
            {
                TraitType = t.Name,
                Value = GetRandomItems(randomHash, t.Name,
                    GenerateItemsWithWeight(State.TraitValueMap[tick][t.Name].Data), 1,
                    State.TraitValueTotalWeightsMap[tick][t.Name]).FirstOrDefault()
            }));

            amount = amount.Sub(1);
        }
        else
        {
            attributes.Data.AddRange(parentAttributes.Data);
        }

        // get non-selected trait types
        var traitTypes = new List<AttributeInfo>();
        var existTypes = new List<string>();
        foreach (var attribute in attributes.Data)
        {
            existTypes.Add(attribute.TraitType);
        }

        foreach (var info in State.RandomTraitTypeMap[tick].Data)
        {
            if (!existTypes.Contains(info.Name)) traitTypes.Add(info);
        }

        // select trait types randomly
        var randomTraitTypes = GetRandomItems(randomHash, nameof(GenerateAttributes),
            GenerateItemsWithWeight(traitTypes), amount, 0);

        // select trait values randomly
        attributes.Data.AddRange(randomTraitTypes.Select(t => new Attribute
        {
            TraitType = t,
            Value = GetRandomItems(randomHash, t, GenerateItemsWithWeight(State.TraitValueMap[tick][t].Data), 1,
                State.TraitValueTotalWeightsMap[tick][t]).FirstOrDefault()
        }));

        return attributes;
    }

    private List<ItemWithWeight> GenerateItemsWithWeight(IEnumerable<AttributeInfo> attributeInfos)
    {
        var result = new List<ItemWithWeight>();

        foreach (var attributeInfo in attributeInfos)
        {
            result.Add(new ItemWithWeight
            {
                Item = attributeInfo.Name,
                Weight = attributeInfo.Weight
            });
        }

        return result;
    }

    public override Empty Confirm(ConfirmInput input)
    {
        Assert(input != null, "Invalid input.");
        Assert(input.AdoptId != null, "Invalid input adopt id.");
        Assert(IsStringValid(input.ImageUri), "Invalid input image uri.");
        Assert(IsByteStringValid(input.Signature), "Invalid input signature.");

        CheckImageSize(input.Image, input.ImageUri);

        var adoptInfo = State.AdoptInfoMap[input.AdoptId];
        Assert(adoptInfo != null, "Adopt id not exists.");
        Assert(adoptInfo.Adopter == Context.Sender, "No permission.");

        Assert(!adoptInfo.IsConfirmed, "Adopt id already confirmed.");

        adoptInfo.IsConfirmed = true;
        State.SymbolAdoptIdMap[adoptInfo.Symbol] = adoptInfo.AdoptId;

        var tick = GetTickFromSymbol(adoptInfo.Parent);

        Assert(RecoverAddressFromSignature(input) == (State.SignatoryMap[tick]), "Not authorized.");

        var inscriptionInfo = State.InscriptionInfoMap[tick];

        var externalInfo = GenerateAdoptExternalInfo(tick, input.Image, adoptInfo.OutputAmount, adoptInfo.Gen,
            adoptInfo.Attributes, input.ImageUri);

        CreateInscriptionAndIssue(adoptInfo.Symbol, adoptInfo.TokenName, inscriptionInfo.Decimals,
            adoptInfo.OutputAmount, externalInfo, Context.Self, Context.Self);

        Context.Fire(new Confirmed
        {
            AdoptId = input.AdoptId,
            Parent = adoptInfo.Parent,
            Symbol = adoptInfo.Symbol,
            TotalSupply = adoptInfo.OutputAmount,
            Attributes = adoptInfo.Attributes,
            Decimals = inscriptionInfo.Decimals,
            Deployer = Context.Sender,
            Gen = adoptInfo.Gen,
            Issuer = Context.Self,
            Owner = Context.Self,
            IssueChainId = Context.ChainId,
            TokenName = adoptInfo.TokenName,
            ExternalInfos = new ExternalInfos
            {
                Value = { externalInfo.Value }
            },
            ImageUri = input.ImageUri
        });

        return new Empty();
    }

    private Address RecoverAddressFromSignature(ConfirmInput input)
    {
        var hash = ComputeConfirmInputHash(input);
        var publicKey = Context.RecoverPublicKey(input.Signature.ToByteArray(), hash.ToByteArray());
        Assert(publicKey != null, "Invalid signature.");

        return Address.FromPublicKey(publicKey);
    }

    private Hash ComputeConfirmInputHash(ConfirmInput input)
    {
        return HashHelper.ComputeFrom(new ConfirmInput
        {
            AdoptId = input.AdoptId,
            Image = input.Image,
            ImageUri = input.ImageUri
        }.ToByteArray());
    }

    private string GenerateSymbol(string tick)
    {
        return $"{tick}{SchrodingerContractConstants.Separator}{State.SymbolCountMap[tick]++}";
    }

    private string GenerateTokenName(string symbol, int gen)
    {
        return $"{symbol}{SchrodingerContractConstants.TokenNameSuffix}{gen.ToString()}";
    }

    private ExternalInfo GenerateAdoptExternalInfo(string tick, string image, long totalSupply, int gen,
        Attributes attributes, string imageUri)
    {
        var externalInfo = new ExternalInfo();
        var dic = new Dictionary<string, string>
        {
            [SchrodingerContractConstants.InscriptionImageKey] = image,
            [SchrodingerContractConstants.InscriptionImageUriKey] = imageUri
        };

        var info = new AdoptInscriptionInfo
        {
            P = SchrodingerContractConstants.InscriptionType,
            Op = SchrodingerContractConstants.AdoptOp,
            Tick = tick,
            Amt = SchrodingerContractConstants.Amt,
            Gen = gen.ToString()
        };
        dic[SchrodingerContractConstants.InscriptionAdoptKey] = info.ToString();

        dic[SchrodingerContractConstants.AttributesKey] = attributes.Data.ToString();

        externalInfo.Value.Add(dic);
        return externalInfo;
    }

    private void CreateInscriptionAndIssue(string symbol, string tokenName, int decimals, long totalSupply,
        ExternalInfo externalInfo, Address issuer, Address owner)
    {
        State.TokenContract.Create.Send(new CreateInput
        {
            Symbol = symbol,
            TokenName = tokenName,
            Decimals = decimals,
            IsBurnable = true,
            Issuer = issuer,
            TotalSupply = totalSupply,
            Owner = owner,
            IssueChainId = Context.ChainId,
            ExternalInfo = externalInfo
        });

        State.TokenContract.Issue.Send(new IssueInput
        {
            To = Context.Sender,
            Symbol = symbol,
            Amount = totalSupply
        });
    }

    public override Empty Reroll(RerollInput input)
    {
        Assert(input != null, "Invalid input.");
        Assert(IsSymbolValid(input.Symbol), "Invalid input symbol.");
        Assert(input.Amount > 0, "Invalid input amount.");
        Assert(IsStringValid(input.Domain), "Invalid input domain.");

        var tick = GetTickFromSymbol(input.Symbol);
        var inscriptionInfo = State.InscriptionInfoMap[tick];

        Assert(inscriptionInfo != null, "Tick not deployed.");
        Assert(inscriptionInfo.Ancestor != input.Symbol, "Can not reroll gen0.");

        ProcessRerollTransfer(input.Symbol, input.Amount, inscriptionInfo.Ancestor);

        JoinPointsContract(input.Domain);
        SettlePoints(nameof(Reroll), input.Amount, inscriptionInfo.Decimals);

        Context.Fire(new Rerolled
        {
            Symbol = input.Symbol,
            Ancestor = inscriptionInfo.Ancestor,
            Amount = input.Amount,
            Recipient = Context.Sender
        });

        return new Empty();
    }

    public override Empty AdoptMaxGen(AdoptMaxGenInput input)
    {
        ValidateAdoptMaxGenInput(input);
        
        var inscriptionInfo = State.InscriptionInfoMap[input.Tick];
        Assert(inscriptionInfo != null, "Tick not deployed.");

        var adoptId = GenerateAdoptId(input.Tick);
        Assert(State.AdoptInfoMap[adoptId] == null, "Adopt id already exists.");

        var parent = GetInscriptionSymbol(input.Tick);

        var adoptInfo = new AdoptInfo
        {
            AdoptId = adoptId,
            Parent = parent,
            ParentGen = 0,
            ParentAttributes = new Attributes(),
            BlockHeight = Context.CurrentHeight,
            Adopter = Context.Sender,
            ImageCount = inscriptionInfo.ImageCount,
            Gen = inscriptionInfo.MaxGen
        };

        State.AdoptInfoMap[adoptId] = adoptInfo;

        CalculateAmount(inscriptionInfo.MaxGenLossRate, inscriptionInfo.CommissionRate, input.Amount,
            out var lossAmount, out var commissionAmount, out var outputAmount);

        var minOutputAmount = new BigIntValue(SchrodingerContractConstants.Ten).Pow(inscriptionInfo.Decimals);
        Assert(outputAmount >= minOutputAmount, "Input amount not enough.");

        adoptInfo.InputAmount = input.Amount;
        adoptInfo.OutputAmount = outputAmount;

        ProcessAdoptTransfer(parent, input.Amount, lossAmount, commissionAmount, inscriptionInfo.Recipient,
            inscriptionInfo.Ancestor, 0);

        var randomHash = GetRandomHash(input.Tick);
        adoptInfo.Attributes = GenerateAttributes(new Attributes(), input.Tick, inscriptionInfo.AttributesPerGen,
            adoptInfo.Gen, randomHash);
        adoptInfo.Symbol = GenerateSymbol(input.Tick);
        adoptInfo.TokenName = GenerateTokenName(adoptInfo.Symbol, adoptInfo.Gen);

        JoinPointsContract(input.Domain);
        SettlePoints(nameof(Adopt), adoptInfo.InputAmount, inscriptionInfo.Decimals);

        Context.Fire(new Adopted
        {
            AdoptId = adoptId,
            Parent = parent,
            ParentGen = 0,
            InputAmount = input.Amount,
            LossAmount = lossAmount,
            CommissionAmount = commissionAmount,
            OutputAmount = outputAmount,
            ImageCount = inscriptionInfo.ImageCount,
            Adopter = Context.Sender,
            BlockHeight = Context.CurrentHeight,
            Attributes = adoptInfo.Attributes,
            Gen = adoptInfo.Gen,
            Ancestor = inscriptionInfo.Ancestor,
            Symbol = adoptInfo.Symbol,
            TokenName = adoptInfo.TokenName
        });
        
        return new Empty();
    }
    
    private void ValidateAdoptMaxGenInput(AdoptMaxGenInput input)
    {
        Assert(input != null, "Invalid input.");
        Assert(IsStringValid(input.Tick), "Invalid tick.");
        Assert(input.Amount > 0, "Invalid amount.");
        Assert(IsStringValid(input.Domain), "Invalid domain.");
    }
}