using System.Collections.Generic;
using System.Linq;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.CSharp.Core;
using AElf.Sdk.CSharp;
using AElf.Types;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;

namespace Schrodinger;

public partial class SchrodingerContract
{
    // public override Empty Adopt(AdoptInput input)
    // {
    //     ValidateAdoptInput(input);
    //
    //     var tick = GetTickFromSymbol(input.Parent);
    //     var inscriptionInfo = GetInscriptionInfo(tick);
    //
    //     var symbolCount = State.SymbolCountMap[tick];
    //     var adoptId = GenerateAdoptId(tick, symbolCount);
    //     Assert(State.AdoptInfoMap[adoptId] == null, "Adopt id already exists.");
    //
    //     GetParentInfo(inscriptionInfo, input.Parent, out var parentGen, out var parentAttributes);
    //
    //     Assert(parentGen < inscriptionInfo!.MaxGen, "Exceeds max gen.");
    //
    //     var adoptInfo = new AdoptInfo
    //     {
    //         AdoptId = adoptId,
    //         Parent = input.Parent,
    //         ParentGen = parentGen,
    //         ParentAttributes = parentAttributes,
    //         BlockHeight = Context.CurrentHeight,
    //         Adopter = Context.Sender,
    //         ImageCount = inscriptionInfo.ImageCount
    //     };
    //
    //     State.AdoptInfoMap[adoptId] = adoptInfo;
    //     
    //     var rerollRate = State.RerollConfigMap[tick]?.Rate ?? 0;
    //     CalculateAmount(inscriptionInfo.LossRate, inscriptionInfo.CommissionRate, rerollRate, input.Amount, out var lossAmount,
    //         out var commissionAmount, out var outputAmount);
    //
    //     var minOutputAmount = new BigIntValue(SchrodingerContractConstants.Ten).Pow(inscriptionInfo.Decimals);
    //     Assert(outputAmount >= minOutputAmount, "Input amount not enough.");
    //
    //     adoptInfo.InputAmount = input.Amount;
    //     adoptInfo.OutputAmount = outputAmount;
    //
    //     ProcessAdoptTransfer(input.Parent, input.Amount, lossAmount, commissionAmount, inscriptionInfo.Recipient,
    //         inscriptionInfo.Ancestor, parentGen);
    //
    //     var randomHash = GetRandomHash(symbolCount);
    //     adoptInfo.Gen = GenerateGen(inscriptionInfo, parentGen, randomHash);
    //     adoptInfo.Attributes =
    //         GenerateAttributes(parentAttributes, tick, adoptInfo.Gen.Sub(adoptInfo.ParentGen), randomHash);
    //     adoptInfo.Symbol = GenerateSymbol(tick, symbolCount);
    //     adoptInfo.TokenName = GenerateTokenName(adoptInfo.Symbol, adoptInfo.Gen);
    //
    //     State.SymbolCountMap[tick] = symbolCount.Add(1);
    //
    //     JoinPointsContract(input.Domain);
    //     SettlePoints(nameof(Adopt), adoptInfo.InputAmount, inscriptionInfo.Decimals, nameof(Adopt));
    //
    //     Context.Fire(new Adopted
    //     {
    //         AdoptId = adoptId,
    //         Parent = input.Parent,
    //         ParentGen = parentGen,
    //         InputAmount = input.Amount,
    //         LossAmount = lossAmount,
    //         CommissionAmount = commissionAmount,
    //         OutputAmount = outputAmount,
    //         ImageCount = inscriptionInfo.ImageCount,
    //         Adopter = Context.Sender,
    //         BlockHeight = Context.CurrentHeight,
    //         Attributes = adoptInfo.Attributes,
    //         Gen = adoptInfo.Gen,
    //         Ancestor = inscriptionInfo.Ancestor,
    //         Symbol = adoptInfo.Symbol,
    //         TokenName = adoptInfo.TokenName
    //     });
    //
    //     return new Empty();
    // }

    public override Empty Confirm(ConfirmInput input)
    {
        Assert(input != null, "Invalid input.");
        Assert(input!.AdoptId != null, "Invalid input adopt id.");
        Assert(IsStringValid(input.ImageUri), "Invalid input image uri.");
        Assert(IsByteStringValid(input.Signature), "Invalid input signature.");

        CheckImageSize(input.Image, input.ImageUri);

        var adoptInfo = GetAndValidateAdoptInfo(input.AdoptId);

        adoptInfo.IsConfirmed = true;
        State.SymbolAdoptIdMap[adoptInfo.Symbol] = adoptInfo.AdoptId;

        var tick = GetTickFromSymbol(adoptInfo.Symbol);

        Assert(
            RecoverAddressFromSignature(ComputeConfirmInputHash(input), input.Signature) == State.SignatoryMap[tick],
            "Not authorized.");

        var inscriptionInfo = GetInscriptionInfo(tick);

        long.TryParse(new BigIntValue(SchrodingerContractConstants.Ten).Pow(inscriptionInfo.Decimals).Value,
            out var outputAmount);
        Assert(adoptInfo.OutputAmount >= outputAmount, "Amount not enough.");

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

    public override Empty Reroll(RerollInput input)
    {
        Assert(input != null, "Invalid input.");
        Assert(IsSymbolValid(input!.Symbol), "Invalid input symbol.");
        Assert(input.Amount > 0, "Invalid input amount.");
        Assert(IsStringValid(input.Domain), "Invalid input domain.");

        var tick = GetTickFromSymbol(input.Symbol);
        var inscriptionInfo = GetInscriptionInfo(tick);

        Assert(inscriptionInfo!.Ancestor != input.Symbol, "Can not reroll gen0.");

        ProcessRerollTransfer(input.Symbol, input.Amount, inscriptionInfo.Ancestor,
            CalculateRerollOutputAmount(tick, GetNumberFromSymbol(input.Symbol), input.Amount));

        JoinPointsContract(input.Domain);
        SettlePoints(nameof(Reroll), input.Amount, inscriptionInfo.Decimals, nameof(Reroll));

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

        var inscriptionInfo = GetInscriptionInfo(input.Tick);

        var symbolCount = State.SymbolCountMap[input.Tick];
        var adoptId = GenerateAdoptId(input.Tick, symbolCount);
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
            ImageCount = inscriptionInfo!.ImageCount,
            Gen = inscriptionInfo.MaxGen
        };

        State.AdoptInfoMap[adoptId] = adoptInfo;

        var randomHash = GetRandomHash(symbolCount);
        var minOutputAmount = new BigIntValue(SchrodingerContractConstants.Ten).Pow(inscriptionInfo.Decimals);
        long.TryParse(minOutputAmount.Value, out var outputAmount);

        CalculateAmount(input.Tick, outputAmount, inscriptionInfo.CommissionRate, randomHash, out var inputAmount,
            out var lossAmount, out var commissionAmount, out var rebateAmount, out var subsidyAmount);
        
        Assert(input.Amount >= inputAmount, "Input amount not enough.");

        adoptInfo.InputAmount = inputAmount;
        adoptInfo.OutputAmount = outputAmount;

        ProcessAdoptTransfer(inputAmount, lossAmount, commissionAmount, rebateAmount, subsidyAmount,
            inscriptionInfo.Recipient, inscriptionInfo.Ancestor, input.Tick);

        adoptInfo.Attributes = GenerateMaxAttributes(input.Tick, adoptInfo.Gen.Sub(1), randomHash);
        adoptInfo.Symbol = GenerateSymbol(input.Tick, symbolCount);
        adoptInfo.TokenName = GenerateTokenName(adoptInfo.Symbol, adoptInfo.Gen);

        State.SymbolCountMap[input.Tick] = symbolCount.Add(1);

        AddVoucher(input.Tick);

        JoinPointsContract(input.Domain);
        // AdoptMaxGen has the same type of point with Adopt
        SettlePoints("Adopt", adoptInfo.InputAmount, inscriptionInfo.Decimals, nameof(AdoptMaxGen));

        Context.Fire(new Adopted
        {
            AdoptId = adoptId,
            Parent = parent,
            ParentGen = 0,
            InputAmount = inputAmount,
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
            TokenName = adoptInfo.TokenName,
            RebateAmount = rebateAmount,
            SubsidyAmount = subsidyAmount
        });

        return new Empty();
    }

    public override Empty RerollAdoption(Hash input)
    {
        Assert(IsHashValid(input), "Invalid input.");

        var adoptInfo = GetAndValidateAdoptInfo(input);

        var tick = GetTickFromSymbol(adoptInfo.Symbol);
        var inscriptionInfo = GetInscriptionInfo(tick);

        State.TokenContract.Transfer.Send(new TransferInput
        {
            Amount = CalculateRerollOutputAmount(tick, GetNumberFromSymbol(adoptInfo.Symbol), adoptInfo.OutputAmount),
            To = Context.Sender,
            Symbol = inscriptionInfo.Ancestor
        });

        adoptInfo.IsRerolled = true;

        SettlePoints(nameof(Reroll), adoptInfo.OutputAmount, inscriptionInfo.Decimals, nameof(Reroll));

        Context.Fire(new AdoptionRerolled
        {
            AdoptId = input,
            Amount = adoptInfo.OutputAmount,
            Symbol = inscriptionInfo.Ancestor,
            Account = Context.Sender
        });

        return new Empty();
    }

    // public override Empty UpdateAdoption(Hash input)
    // {
    //     Assert(IsHashValid(input), "Invalid Input.");
    //
    //     var adoptInfo = GetAndValidateAdoptInfo(input);
    //     adoptInfo.IsUpdated = true;
    //
    //     var parent = adoptInfo.Symbol;
    //     var parentGen = adoptInfo.Gen;
    //     var parentAttributes = adoptInfo.Attributes;
    //     var inputAmount = adoptInfo.OutputAmount;
    //
    //     var tick = GetTickFromSymbol(parent);
    //     var inscriptionInfo = GetInscriptionInfo(tick);
    //
    //     Assert(parentGen < inscriptionInfo!.MaxGen, "Exceeds max gen.");
    //
    //     var symbolCount = State.SymbolCountMap[tick];
    //     var adoptId = GenerateAdoptId(tick, symbolCount);
    //     Assert(State.AdoptInfoMap[adoptId] == null, "Adopt id already exists.");
    //
    //     var newAdoptInfo = new AdoptInfo
    //     {
    //         AdoptId = adoptId,
    //         Parent = parent,
    //         ParentGen = parentGen,
    //         ParentAttributes = parentAttributes,
    //         BlockHeight = Context.CurrentHeight,
    //         Adopter = Context.Sender,
    //         ImageCount = inscriptionInfo.ImageCount
    //     };
    //
    //     State.AdoptInfoMap[adoptId] = newAdoptInfo;
    //
    //     var rerollRate = State.RerollConfigMap[tick]?.Rate ?? 0;
    //     CalculateAmount(inscriptionInfo.LossRate, inscriptionInfo.CommissionRate, rerollRate, inputAmount, out var lossAmount,
    //         out var commissionAmount, out var outputAmount);
    //
    //     var minOutputAmount = new BigIntValue(SchrodingerContractConstants.Ten).Pow(inscriptionInfo.Decimals);
    //     Assert(outputAmount >= minOutputAmount, "Input amount not enough.");
    //
    //     newAdoptInfo.InputAmount = inputAmount;
    //     newAdoptInfo.OutputAmount = outputAmount;
    //
    //     ProcessAdoptTransfer(string.Empty, 0, lossAmount, commissionAmount, inscriptionInfo.Recipient,
    //         inscriptionInfo.Ancestor, 0);
    //
    //     var randomHash = GetRandomHash(symbolCount);
    //     newAdoptInfo.Gen = GenerateGen(inscriptionInfo, parentGen, randomHash);
    //     newAdoptInfo.Attributes =
    //         GenerateAttributes(parentAttributes, tick, newAdoptInfo.Gen.Sub(newAdoptInfo.ParentGen), randomHash);
    //     newAdoptInfo.Symbol = GenerateSymbol(tick, symbolCount);
    //     newAdoptInfo.TokenName = GenerateTokenName(newAdoptInfo.Symbol, newAdoptInfo.Gen);
    //
    //     State.SymbolCountMap[tick] = symbolCount.Add(1);
    //
    //     // JoinPointsContract(input.Domain);
    //     SettlePoints(nameof(Adopt), newAdoptInfo.InputAmount, inscriptionInfo.Decimals, nameof(Adopt));
    //
    //     Context.Fire(new AdoptionUpdated
    //     {
    //         AdoptId = adoptId,
    //         Parent = parent,
    //         ParentGen = parentGen,
    //         InputAmount = inputAmount,
    //         LossAmount = lossAmount,
    //         CommissionAmount = commissionAmount,
    //         OutputAmount = outputAmount,
    //         ImageCount = inscriptionInfo.ImageCount,
    //         Adopter = Context.Sender,
    //         BlockHeight = Context.CurrentHeight,
    //         Attributes = newAdoptInfo.Attributes,
    //         Gen = newAdoptInfo.Gen,
    //         Ancestor = inscriptionInfo.Ancestor,
    //         Symbol = newAdoptInfo.Symbol,
    //         TokenName = newAdoptInfo.TokenName
    //     });
    //
    //     return new Empty();
    // }

    private void ValidateAdoptInput(AdoptInput input)
    {
        Assert(input != null, "Invalid input.");
        Assert(IsSymbolValid(input!.Parent), "Invalid input parent.");
        Assert(input.Amount > 0, "Invalid input amount.");
        Assert(IsStringValid(input.Domain), "Invalid input domain.");
    }

    private Hash GenerateAdoptId(string tick, long symbolCount)
    {
        return HashHelper.ConcatAndCompute(HashHelper.ComputeFrom(tick), HashHelper.ComputeFrom(symbolCount));
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

    // private void CalculateAmount(long lossRate, long commissionRate, long rerollRate, long inputAmount,
    //     out long lossAmount, out long commissionAmount, out long outputAmount)
    // {
    //     // calculate amount
    //     lossAmount = inputAmount.Mul(lossRate).Div(SchrodingerContractConstants.Denominator);
    //     if (lossAmount == 0 && lossRate != 0) lossAmount = 1;
    //
    //     outputAmount = inputAmount.Sub(lossAmount);
    //     if (rerollRate > 0) outputAmount = outputAmount.Div(rerollRate).Mul(SchrodingerContractConstants.Denominator);
    //
    //     commissionAmount = lossAmount.Mul(commissionRate).Div(SchrodingerContractConstants.Denominator);
    //     if (commissionAmount == 0 && commissionRate != 0) commissionAmount = 1;
    //
    //     lossAmount = lossAmount.Sub(commissionAmount);
    // }

    private void CalculateAmount(string tick, long outputAmount, long commissionRate, Hash randomHash,
        out long inputAmount, out long lossAmount, out long commissionAmount, out long rebateAmount,
        out long subsidyAmount)
    {
        var rebateConfig = State.RebateConfig[tick];
        var rerollRate = State.RerollConfigMap[tick]?.Rate ?? 0;
        var actualValue = outputAmount.Sub(outputAmount.Mul(rerollRate).Div(SchrodingerContractConstants.Denominator));

        if (rebateConfig == null)
        {
            inputAmount = SchrodingerContractConstants.DefaultInputAmount;
            rebateAmount = 0;
            lossAmount = inputAmount.Sub(actualValue);
        }
        /* transfer from user: 1.6 SGR
         * rebate: 0 ~ 1.3 SGR
         * user actual paid: 0.3 ~ 1.6 SGR
         * output: 1 cat
         * reroll(actural value): 0.5 SGR
         * then user may pay less than 0.5 SGR
         * any more than 0.5 SGR is considered income
         * any less than 0.5 SGR needs subsidy
         */
        else
        {
            inputAmount = rebateConfig.InputAmount;
            rebateAmount = GenerateRebateAmount(rebateConfig, randomHash);

            var payAmount = inputAmount.Sub(rebateAmount);

            if (payAmount <= actualValue)
            {
                lossAmount = 0;
                commissionAmount = 0;
                subsidyAmount = actualValue.Sub(payAmount);

                return;
            }

            lossAmount = payAmount.Sub(actualValue);
        }

        subsidyAmount = 0;
        commissionAmount = lossAmount.Mul(commissionRate).Div(SchrodingerContractConstants.Denominator);
        lossAmount = lossAmount.Sub(commissionAmount);
    }

    private void ProcessAdoptTransfer(long inputAmount, long lossAmount, long commissionAmount, long rebateAmount,
        long subsidyAmount, Address recipient, string ancestor, string tick)
    {
        // transfer ancestor from sender
        State.TokenContract.TransferFrom.Send(new TransferFromInput
        {
            Amount = inputAmount,
            From = Context.Sender,
            To = Context.Self,
            Symbol = ancestor
        });

        // send subsidy to contract
        if (subsidyAmount > 0)
        {
            State.TokenContract.Transfer.VirtualSend(GetSpinPoolHash(tick), new TransferInput
            {
                Symbol = ancestor,
                Amount = subsidyAmount,
                To = Context.Self
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
        if (commissionAmount > 0)
        {
            State.TokenContract.Transfer.Send(new TransferInput
            {
                Amount = commissionAmount,
                To = recipient,
                Symbol = ancestor
            });
        }

        // send rebate to send
        if (rebateAmount > 0)
        {
            State.TokenContract.Transfer.Send(new TransferInput
            {
                Amount = rebateAmount,
                To = Context.Sender,
                Symbol = ancestor
            });
        }
    }

    private void ProcessAdoptTransfer(string symbol, long inputAmount, long lossAmount, long commissionAmount,
        Address recipient, string ancestor, int parentGen)
    {
        // transfer parent from sender
        if (inputAmount > 0)
        {
            State.TokenContract.TransferFrom.Send(new TransferFromInput
            {
                Amount = inputAmount,
                From = Context.Sender,
                To = Context.Self,
                Symbol = symbol
            });
        }

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
        if (commissionAmount > 0)
        {
            State.TokenContract.Transfer.Send(new TransferInput
            {
                Amount = commissionAmount,
                To = recipient,
                Symbol = ancestor
            });
        }
    }

    private void ProcessRerollTransfer(string symbol, long amount, string ancestor, long outputAmount)
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
            Amount = outputAmount,
            To = Context.Sender,
            Symbol = ancestor
        });
    }

    private Hash GetRandomHash()
    {
        if (State.ConsensusContract.Value == null)
        {
            State.ConsensusContract.Value =
                Context.GetContractAddressByName(SmartContractConstants.ConsensusContractSystemName);
        }

        return State.ConsensusContract.GetRandomHash.Call(new Int64Value
        {
            Value = Context.CurrentHeight
        });
    }

    private Hash GetRandomHash(long symbolCount)
    {
        return HashHelper.ConcatAndCompute(HashHelper.ComputeFrom(symbolCount), GetRandomHash());
    }

    private int GenerateGen(InscriptionInfo inscriptionInfo, int parentGen, Hash randomHash)
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

        var gens = new RepeatedField<AttributeInfo>();

        for (var i = 1; i <= crossGenerationConfig.Gen; i++)
        {
            gens.Add(new AttributeInfo
            {
                Name = i.ToString(),
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

    private bool IsCrossGenerationHappened(long probability, Hash randomHash)
    {
        var random = Context.ConvertHashToInt64(
            HashHelper.ConcatAndCompute(randomHash, HashHelper.ComputeFrom(nameof(IsCrossGenerationHappened))), 1,
            SchrodingerContractConstants.Denominator + 1);
        return random <= probability;
    }

    private List<string> GetRandomItems(Hash randomHash, string seed, RepeatedField<AttributeInfo> attributeInfos,
        int count, long totalWeights)
    {
        var selectedItems = new List<string>();

        if (totalWeights == 0)
        {
            foreach (var attributeInfo in attributeInfos)
            {
                totalWeights = totalWeights.Add(attributeInfo.Weight);
            }
        }

        var hash = HashHelper.ConcatAndCompute(randomHash, HashHelper.ComputeFrom(seed));

        while (selectedItems.Count < count && attributeInfos.Count > 0)
        {
            hash = HashHelper.ConcatAndCompute(hash, HashHelper.ComputeFrom(selectedItems.Count.ToString()));
            var random = Context.ConvertHashToInt64(hash, 1, totalWeights + 1);
            var sum = 0L;
            for (var i = 0; i < attributeInfos.Count; i++)
            {
                var attributeInfo = attributeInfos[i];
                sum = sum.Add(attributeInfo.Weight);

                if (random > sum && i < attributeInfos.Count - 1) continue;

                selectedItems.Add(attributeInfo.Name);
                totalWeights = totalWeights.Sub(attributeInfo.Weight);
                attributeInfos.RemoveAt(i);
                break;
            }
        }

        return selectedItems;
    }

    private Attributes GenerateAttributes(Attributes parentAttributes, string tick, int amount, Hash randomHash)
    {
        var attributes = new Attributes();

        // gen0 -> gen1
        if (parentAttributes.Data.Count == 0)
        {
            foreach (var attributeInfo in State.FixedTraitTypeMap[tick].Data)
            {
                attributes.Data.Add(new Attribute
                {
                    TraitType = attributeInfo.Name,
                    Value = GetRandomItems(randomHash, attributeInfo.Name, tick)
                });
            }

            amount = amount.Sub(1);
        }
        else
        {
            attributes.Data.AddRange(parentAttributes.Data);
        }

        // get non-selected trait types
        var traitTypes = new RepeatedField<AttributeInfo>();
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
        var randomTraitTypes = GetRandomItems(randomHash, nameof(GenerateAttributes), traitTypes, amount, 0);

        // select trait values randomly
        foreach (var traitType in randomTraitTypes)
        {
            attributes.Data.Add(new Attribute
            {
                TraitType = traitType,
                Value = GetRandomItems(randomHash, traitType, tick)
            });
        }

        return attributes;
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

    private string GenerateSymbol(string tick, long symbolCount)
    {
        return tick + SchrodingerContractConstants.Separator + symbolCount;
    }

    private string GenerateTokenName(string symbol, int gen)
    {
        return symbol + SchrodingerContractConstants.TokenNameSuffix + gen;
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

    private void ValidateAdoptMaxGenInput(AdoptMaxGenInput input)
    {
        Assert(input != null, "Invalid input.");
        Assert(IsStringValid(input!.Tick), "Invalid tick.");
        Assert(input.Amount > 0, "Invalid amount.");
        Assert(IsStringValid(input.Domain), "Invalid domain.");
    }

    private string GetRandomItems(Hash randomHash, string traitType, string tick)
    {
        var upperWeightSums = State.UpperWeightSumsMap[tick][traitType].Data;

        var hash = HashHelper.ConcatAndCompute(randomHash, HashHelper.ComputeFrom(traitType));
        var random = Context.ConvertHashToInt64(hash, 0, State.TraitValueTotalWeightsMap[tick][traitType]);
        var index = BinarySearch(upperWeightSums, random);

        var traitValues = State.TraitValuesMap[tick][traitType][index];

        index = BinarySearch(traitValues.LowerWeightSums.Data, random);

        return traitValues.TraitValueList.Data[index].Name;
    }

    private int BinarySearch(RepeatedField<long> array, long value)
    {
        var low = 0;
        var high = array.Count - 1;

        while (low < high)
        {
            var mid = (low + high) / 2;
            if (value < array[mid])
            {
                high = mid;
            }
            else
            {
                low = mid + 1;
            }
        }

        return low;
    }

    private Attributes GenerateMaxAttributes(string tick, int amount, Hash randomHash)
    {
        var attributes = new Attributes();

        var fixedAttributeInfo = State.FixedTraitTypeMap[tick].Data;
        var randomAttributeInfo = State.RandomTraitTypeMap[tick].Data;

        foreach (var attributeInfo in fixedAttributeInfo)
        {
            attributes.Data.Add(new Attribute
            {
                TraitType = attributeInfo.Name,
                Value = GetRandomItems(randomHash, attributeInfo.Name, tick)
            });
        }

        // select trait types randomly
        var randomTraitTypes = GetRandomItems(randomHash, nameof(GenerateAttributes),
            randomAttributeInfo.Clone(), amount, 0);

        // select trait values randomly
        foreach (var traitType in randomTraitTypes)
        {
            attributes.Data.Add(new Attribute
            {
                TraitType = traitType,
                Value = GetRandomItems(randomHash, traitType, tick)
            });
        }

        return attributes;
    }

    private string GetInscriptionSymbol(string tick)
    {
        return tick + SchrodingerContractConstants.Separator + SchrodingerContractConstants.AncestorSymbolSuffix;
    }

    private AdoptInfo GetAndValidateAdoptInfo(Hash adoptId)
    {
        var adoptInfo = State.AdoptInfoMap[adoptId];
        Assert(adoptInfo != null, "Adopt id not exists.");
        Assert(adoptInfo!.Adopter == Context.Sender, "No permission.");

        Assert(!adoptInfo.IsConfirmed, "Adopt id already confirmed.");
        Assert(!adoptInfo.IsRerolled, "Adopt id already rerolled.");
        Assert(!adoptInfo.IsUpdated, "Adopt id already updated.");

        return adoptInfo;
    }

    private long CalculateRerollOutputAmount(string tick, long number, long amount)
    {
        var config = State.RerollConfigMap[tick];

        return config == null || number < config.Index
            ? amount
            : amount.Mul(config.Rate).Div(SchrodingerContractConstants.Denominator);
    }

    private void AddVoucher(string tick)
    {
        var config = State.VoucherAdoptionConfigMap[tick];

        if (config == null) return;

        State.AdoptionVoucherMap[tick][Context.Sender] = State.AdoptionVoucherMap[tick][Context.Sender]
            .Add(config.VoucherAmount);

        Context.Fire(new VoucherAdded
        {
            Amount = config.VoucherAmount,
            Tick = tick,
            Account = Context.Sender
        });
    }

    private long GenerateRebateAmount(RebateConfig config, Hash randomHash)
    {
        var intervals = config.Intervals;
        if (intervals == null || intervals.Count == 0) return 0;

        var rebateInterval = GetRandomIntervals(randomHash, intervals);

        var hash = HashHelper.ConcatAndCompute(randomHash, HashHelper.ComputeFrom(rebateInterval));
        return Context.ConvertHashToInt64(hash, rebateInterval.Start, rebateInterval.End + 1);
    }

    private RebateInterval GetRandomIntervals(Hash randomHash, RepeatedField<RebateInterval> intervals)
    {
        long totalWeights = 0;

        foreach (var interval in intervals)
        {
            totalWeights = totalWeights.Add(interval.Weight);
        }

        var hash = HashHelper.ConcatAndCompute(randomHash, HashHelper.ComputeFrom(nameof(RebateInterval)));

        var random = Context.ConvertHashToInt64(hash, 0, totalWeights + 1);
        var sum = 0L;

        foreach (var interval in intervals)
        {
            sum = sum.Add(interval.Weight);

            if (random <= sum) return interval;
        }

        return intervals.Last();
    }
}