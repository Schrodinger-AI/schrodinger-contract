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
    public override Empty SetMergeRatesConfig(SetMergeRatesConfigInput input)
    {
        Assert(input != null, "Invalid input.");
        Assert(IsStringValid(input!.Tick), "Invalid tick.");
        Assert(input.MergeRates != null && input.MergeRates.Count > 0, "Invalid merge rates.");
        Assert(input.MaximumLevel > 0, "Invalid maximum level.");

        CheckInscriptionExistAndPermission(input.Tick);

        State.MaximumLevelMap[input.Tick] = input.MaximumLevel;

        var mergeRates = new List<MergeRate>();

        foreach (var mergeRate in input.MergeRates!.Distinct().ToList())
        {
            if (mergeRate.Level > input.MaximumLevel) continue;

            Assert(mergeRate.Rate >= 0, "Invalid merge rate.");

            mergeRates.Add(mergeRate);
            State.MergeRatesMap[input.Tick][mergeRate.Level] = mergeRate.Rate;
        }

        Context.Fire(new MergeRatesConfigSet
        {
            MergeRates = new MergeRates { Data = { mergeRates } },
            Tick = input.Tick,
            MaximumLevel = input.MaximumLevel
        });

        return new Empty();
    }

    public override Empty SetMergeConfig(SetMergeConfigInput input)
    {
        Assert(input != null, "Invalid input.");
        Assert(IsStringValid(input!.Tick), "Invalid tick.");
        Assert(input.CommissionAmount >= 0, "Invalid commission amount.");
        Assert(input.PoolAmount >= 0, "Invalid pool amount.");

        CheckInscriptionExistAndPermission(input.Tick);

        var config = new MergeConfig
        {
            CommissionAmount = input.CommissionAmount,
            PoolAmount = input.PoolAmount
        };

        if (config.Equals(State.MergeConfigMap[input.Tick])) return new Empty();

        State.MergeConfigMap[input.Tick] = config;

        Context.Fire(new MergeConfigSet
        {
            Tick = input.Tick,
            Config = config
        });

        return new Empty();
    }

    public override Empty Merge(MergeInput input)
    {
        Assert(input != null, "Invalid input.");
        Assert(IsStringValid(input!.Tick), "Invalid tick.");
        Assert(IsHashValid(input.AdoptIdA), "Invalid adopt id a.");
        Assert(IsHashValid(input.AdoptIdB), "Invalid adopt id b.");
        Assert(input.Level > 0, "Invalid level.");
        Assert(!input.Signature.IsNullOrEmpty(), "Invalid signature.");

        Assert(
            RecoverAddressFromSignature(ComputeMergeInputHash(input), input.Signature) ==
            State.SignatoryMap[input.Tick], "Invalid signature.");

        Assert(input.Level <= State.MaximumLevelMap[input.Tick], "Already reach maximum level.");

        var inscriptionInfo = GetInscriptionInfo(input.Tick);

        long.TryParse(new BigIntValue(SchrodingerContractConstants.Ten).Pow(inscriptionInfo.Decimals).Value,
            out var amount);

        var logEvent = new Merged
        {
            Tick = input.Tick,
            AdoptIdA = input.AdoptIdA,
            AdoptIdB = input.AdoptIdB,
            AmountA = ProcessAdoptId(input.AdoptIdA, inscriptionInfo.MaxGen, amount, input.Level, out var symbolA),
            AmountB = ProcessAdoptId(input.AdoptIdB, inscriptionInfo.MaxGen, amount, input.Level, out var symbolB),
            SymbolA = symbolA,
            SymbolB = symbolB
        };

        GenerateAdoptInfo(inscriptionInfo, input.Tick, input.Level, amount, logEvent);

        Context.Fire(logEvent);

        return new Empty();
    }

    private Hash ComputeMergeInputHash(MergeInput input)
    {
        return HashHelper.ComputeFrom(new MergeInput
        {
            Tick = input.Tick,
            AdoptIdA = input.AdoptIdA,
            AdoptIdB = input.AdoptIdB,
            Level = input.Level
        }.ToByteArray());
    }

    private long ProcessAdoptId(Hash adoptId, int maxGen, long amount, long level, out string symbol)
    {
        var adoptInfo = State.AdoptInfoMap[adoptId];

        Assert(adoptInfo != null, $"AdoptId {adoptId} not found.");
        Assert(!adoptInfo!.IsRerolled, $"AdoptId {adoptId} already rerolled.");
        Assert(adoptInfo.Gen == maxGen, $"AdoptId {adoptId} not reach max generation.");
        Assert(adoptInfo.Level == 0 || adoptInfo.Level == level, "Level not matched.");

        adoptInfo.Level = level;
        symbol = adoptInfo.Symbol;

        return adoptInfo.IsConfirmed ? ProcessToken(adoptInfo, amount) : ProcessAdoptInfo(adoptInfo, amount);
    }

    private long ProcessToken(AdoptInfo adoptInfo, long amount)
    {
        var output = State.TokenContract.GetBalance.Call(new GetBalanceInput
        {
            Owner = Context.Sender,
            Symbol = adoptInfo.Symbol
        });

        State.TokenContract.TransferFrom.Send(new TransferFromInput
        {
            Symbol = adoptInfo.Symbol,
            Amount = amount,
            From = Context.Sender,
            To = Context.Self
        });

        State.TokenContract.Burn.Send(new BurnInput
        {
            Symbol = adoptInfo.Symbol,
            Amount = amount
        });

        return output.Balance.Sub(amount);
    }

    private long ProcessAdoptInfo(AdoptInfo adoptInfo, long amount)
    {
        Assert(adoptInfo.Adopter == Context.Sender, "No permission.");
        Assert(adoptInfo.OutputAmount >= amount, "Not enough amount.");

        adoptInfo.OutputAmount = adoptInfo.OutputAmount.Sub(amount);

        return adoptInfo.OutputAmount;
    }

    private void GenerateAdoptInfo(InscriptionInfo inscriptionInfo, string tick, long level, long amount,
        Merged logEvent)
    {
        var symbolCount = State.SymbolCountMap[tick];
        var adoptId = GenerateAdoptId(tick, symbolCount);
        Assert(State.AdoptInfoMap[adoptId] == null, "Adopt id already exists.");

        var adoptInfo = new AdoptInfo
        {
            AdoptId = adoptId,
            BlockHeight = Context.CurrentHeight,
            Adopter = Context.Sender,
            ImageCount = inscriptionInfo!.ImageCount,
            Gen = inscriptionInfo.MaxGen
        };

        State.AdoptInfoMap[adoptId] = adoptInfo;

        var mergeConfig = State.MergeConfigMap[tick];

        logEvent.LossAmount = mergeConfig.PoolAmount;
        logEvent.CommissionAmount = mergeConfig.CommissionAmount;

        adoptInfo.InputAmount = amount.Mul(2);
        adoptInfo.OutputAmount = amount;

        ProcessMergeTransfer(logEvent.LossAmount, logEvent.CommissionAmount, inscriptionInfo.Recipient,
            inscriptionInfo.Ancestor);

        var randomHash = GetRandomHash(symbolCount);
        adoptInfo.Attributes = GenerateMaxAttributes(tick, adoptInfo.Gen.Sub(1), randomHash);
        adoptInfo.Symbol = GenerateSymbol(tick, symbolCount);
        adoptInfo.TokenName = GenerateTokenName(adoptInfo.Symbol, adoptInfo.Gen);
        adoptInfo.Level = GenerateLevel(tick, level, randomHash);

        State.SymbolCountMap[tick] = symbolCount.Add(1);

        logEvent.AdoptInfo = adoptInfo;
    }

    private void ProcessMergeTransfer(long lossAmount, long commissionAmount, Address recipient, string ancestor)
    {
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

    private long GenerateLevel(string tick, long level, Hash randomHash)
    {
        var rate = State.MergeRatesMap[tick][level];
        var random = Context.ConvertHashToInt64(randomHash, 0, SchrodingerContractConstants.Denominator);

        return random <= rate ? level.Add(1) : level;
    }
}