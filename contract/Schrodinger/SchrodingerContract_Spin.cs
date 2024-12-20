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
    public override Empty SetRewardConfig(SetRewardConfigInput input)
    {
        Assert(input != null, "Invalid input.");
        Assert(IsStringValid(input!.Tick), "Invalid tick.");
        var rewardList = ValidateRewardList(input.Rewards);

        CheckInscriptionExistAndPermission(input.Tick);
        if (rewardList.Equals(State.RewardListMap[input.Tick])) return new Empty();

        State.RewardListMap[input.Tick] = rewardList;

        Context.Fire(new RewardConfigSet
        {
            Tick = input.Tick,
            List = new RewardList { Data = { input.Rewards } },
            Pool = GetSpinPoolAddress(input.Tick)
        });

        return new Empty();
    }

    public override Empty Spin(SpinInput input)
    {
        Assert(input != null, "Invalid input.");
        Assert(IsStringValid(input!.Tick), "Invalid tick.");
        Assert(IsHashValid(input.Seed), "Invalid seed.");
        ValidateSignature(input.Signature, input.ExpirationTime);

        Assert(
            RecoverAddressFromSignature(ComputeSpinInputHash(input), input.Signature) ==
            State.SignatoryMap[input.Tick], "Signature not valid.");

        var spinId = GenerateSpinId(input.Tick, input.Seed);

        Assert(State.SpinInfoMap[spinId] == null, "Spin id exists.");

        var reward = Spin(input.Tick, input.Seed);

        var spinInfo = new SpinInfo
        {
            SpinId = spinId,
            Account = Context.Sender,
            Amount = reward.Amount,
            Name = reward.Name,
            Type = reward.Type
        };

        State.SpinInfoMap[spinId] = spinInfo;

        ProcessReward(reward, input.Tick);

        Context.Fire(new Spun
        {
            Tick = input.Tick,
            SpinInfo = spinInfo,
            Seed = input.Seed
        });

        return new Empty();
    }

    public override Empty AdoptWithVoucher(AdoptWithVoucherInput input)
    {
        Assert(input != null, "Invalid input.");
        Assert(IsStringValid(input!.Tick), "Invalid tick.");

        var inscriptionInfo = GetInscriptionInfo(input.Tick);

        Assert(State.AdoptionVoucherMap[input.Tick][Context.Sender] > 0, "Voucher not enough.");
        State.AdoptionVoucherMap[input.Tick][Context.Sender]--;

        var voucherId = GenerateVoucherId(input.Tick);
        Assert(State.VoucherInfoMap[voucherId] == null, "Voucher id exists.");

        var randomHash = GetRandomHash(voucherId);
        var attributes = GenerateMaxAttributes(input.Tick, inscriptionInfo!.MaxGen.Sub(1), randomHash);

        var voucherInfo = new VoucherInfo
        {
            VoucherId = voucherId,
            Account = Context.Sender,
            Attributes = attributes,
            Tick = input.Tick
        };

        State.VoucherInfoMap[voucherId] = voucherInfo;

        Context.Fire(new AdoptedWithVoucher
        {
            VoucherInfo = voucherInfo
        });

        return new Empty();
    }

    public override Empty ConfirmVoucher(ConfirmVoucherInput input)
    {
        Assert(input != null, "Invalid input.");
        Assert(IsHashValid(input!.VoucherId), "Invalid voucher id.");
        Assert(!input.Signature.IsNullOrEmpty(), "Invalid signature.");

        var voucherInfo = State.VoucherInfoMap[input.VoucherId];
        Assert(voucherInfo != null, "Voucher id not exists.");
        Assert(voucherInfo!.Account == Context.Sender, "No permission.");
        Assert(voucherInfo.AdoptId == null, "Already confirmed.");

        Assert(
            RecoverAddressFromSignature(ComputeConfirmVoucherInputHash(input), input.Signature) ==
            State.SignatoryMap[voucherInfo!.Tick], "Signature not valid.");

        var inscriptionInfo = State.InscriptionInfoMap[voucherInfo.Tick];

        var symbolCount = State.SymbolCountMap[voucherInfo.Tick];
        var adoptId = GenerateAdoptId(voucherInfo.Tick, symbolCount);
        Assert(State.AdoptInfoMap[adoptId] == null, "Adopt id already exists.");

        voucherInfo.AdoptId = adoptId;

        var parent = GetInscriptionSymbol(voucherInfo.Tick);

        var adoptInfo = new AdoptInfo
        {
            AdoptId = adoptId,
            Parent = parent,
            ParentGen = 0,
            ParentAttributes = new Attributes(),
            BlockHeight = Context.CurrentHeight,
            Adopter = Context.Sender,
            ImageCount = inscriptionInfo!.ImageCount,
            Gen = inscriptionInfo.MaxGen,
            InputAmount = 0
        };

        State.AdoptInfoMap[adoptId] = adoptInfo;

        long.TryParse(new BigIntValue(SchrodingerContractConstants.Ten).Pow(inscriptionInfo.Decimals).Value,
            out var outputAmount);

        CalculateAmountReverse(voucherInfo.Tick, inscriptionInfo.MaxGenLossRate, inscriptionInfo.CommissionRate,
            outputAmount, out var lossAmount, out var commissionAmount);

        adoptInfo.OutputAmount = outputAmount;

        adoptInfo.Attributes = voucherInfo.Attributes;
        adoptInfo.Symbol = GenerateSymbol(voucherInfo.Tick, symbolCount);
        adoptInfo.TokenName = GenerateTokenName(adoptInfo.Symbol, adoptInfo.Gen);

        State.SymbolCountMap[voucherInfo.Tick] = symbolCount.Add(1);
        
        ProcessAdoptWithVoucherTransfer(lossAmount, commissionAmount, outputAmount, inscriptionInfo.Recipient,
            inscriptionInfo.Ancestor, voucherInfo.Tick, out var subsidyAmount);

        Context.Fire(new VoucherConfirmed
        {
            VoucherInfo = voucherInfo
        });

        Context.Fire(new Adopted
        {
            AdoptId = adoptId,
            Parent = parent,
            ParentGen = 0,
            InputAmount = 0,
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
            SubsidyAmount = subsidyAmount
        });

        return new Empty();
    }

    public override Empty SetVoucherAdoptionConfig(SetVoucherAdoptionConfigInput input)
    {
        Assert(input != null, "Invalid input.");
        Assert(IsStringValid(input!.Tick), "Invalid tick.");
        Assert(input.CommissionAmount >= 0, "Invalid commission amount.");
        Assert(input.PoolAmount >= 0, "Invalid pool amount.");
        Assert(input.VoucherAmount >= 0, "Invalid voucher amount.");
        
        CheckInscriptionExistAndPermission(input.Tick);

        var config = new VoucherAdoptionConfig
        {
            CommissionAmount = input.CommissionAmount,
            PoolAmount = input.PoolAmount,
            VoucherAmount = input.VoucherAmount
        };

        if (config.Equals(State.VoucherAdoptionConfigMap[input.Tick])) return new Empty();

        State.VoucherAdoptionConfigMap[input.Tick] = config;
        
        Context.Fire(new VoucherAdoptionConfigSet
        {
            Tick = input.Tick,
            Config = config
        });
        
        return new Empty();
    }

    private void ValidateSignature(ByteString signature, long expirationTime)
    {
        Assert(expirationTime > 0, "Invalid expiration time.");
        Assert(!signature.IsNullOrEmpty(), "Invalid signature.");
        Assert(Context.CurrentBlockTime.Seconds < expirationTime, "Signature expired.");

        var signatureHash = HashHelper.ComputeFrom(signature.ToByteArray());
        Assert(!State.SpinSignatureMap[signatureHash], "Signature used.");
        State.SpinSignatureMap[signatureHash] = true;
    }

    private Hash GenerateSpinId(string tick, Hash seed)
    {
        return HashHelper.ConcatAndCompute(HashHelper.ComputeFrom(tick), seed);
    }

    private Reward Spin(string tick, Hash seed)
    {
        var randomHash = HashHelper.ConcatAndCompute(GetRandomHash(), HashHelper.ComputeFrom(seed));

        var rewardList = State.RewardListMap[tick].Data.OrderBy(r => r.Weight).ToList();
        var weightSum = rewardList.Sum(x => x.Weight);

        var random = Context.ConvertHashToInt64(randomHash, 1, weightSum + 1);

        var currentWeight = 0L;
        foreach (var reward in rewardList)
        {
            currentWeight += reward.Weight;
            if (currentWeight >= random)
            {
                return reward;
            }
        }

        return rewardList.Last();
    }

    private void ProcessReward(Reward reward, string tick)
    {
        switch (reward.Type)
        {
            case RewardType.AdoptionVoucher:
                State.AdoptionVoucherMap[tick][Context.Sender] =
                    State.AdoptionVoucherMap[tick][Context.Sender].Add(reward.Amount);
                break;
            case RewardType.Token:
                var symbol = State.InscriptionInfoMap[tick].Ancestor;
                State.TokenContract.Transfer.VirtualSend(GetSpinPoolHash(tick), new TransferInput
                {
                    Amount = reward.Amount,
                    Memo = "spin",
                    Symbol = symbol,
                    To = Context.Sender
                });
                break;
            case RewardType.Other:
            case RewardType.Point:
            default:
                return;
        }
    }

    private Address RecoverAddressFromSignature(Hash input, ByteString signature)
    {
        var publicKey = Context.RecoverPublicKey(signature.ToByteArray(), input.ToByteArray());
        Assert(publicKey != null, "Invalid signature.");

        return Address.FromPublicKey(publicKey);
    }

    private Hash ComputeSpinInputHash(SpinInput input)
    {
        return HashHelper.ComputeFrom(new SpinInput
        {
            Tick = input.Tick,
            Seed = input.Seed,
            ExpirationTime = input.ExpirationTime
        });
    }

    private Hash GenerateVoucherId(string tick)
    {
        return HashHelper.ConcatAndCompute(HashHelper.ComputeFrom(tick),
            HashHelper.ConcatAndCompute(HashHelper.ComputeFrom(State.VoucherIdCountMap[tick]++),
                HashHelper.ComputeFrom(Context.Sender)));
    }

    private Hash GetRandomHash(Hash hash)
    {
        return HashHelper.ConcatAndCompute(hash, GetRandomHash());
    }

    private Hash ComputeConfirmVoucherInputHash(ConfirmVoucherInput input)
    {
        return HashHelper.ComputeFrom(new ConfirmVoucherInput
        {
            VoucherId = input.VoucherId
        });
    }

    private void CalculateAmountReverse(string tick, long lossRate, long commissionRate, long outputAmount, out long lossAmount,
        out long commissionAmount)
    {
        if (State.VoucherAdoptionConfigMap[tick] == null)
        {
            // calculate amount
            lossAmount = outputAmount.Div(SchrodingerContractConstants.Denominator.Sub(lossRate)).Mul(lossRate);

            commissionAmount = lossAmount.Mul(commissionRate).Div(SchrodingerContractConstants.Denominator);
            if (commissionAmount == 0 && commissionRate != 0) commissionAmount = 1;

            lossAmount = lossAmount.Sub(commissionAmount);
        }
        else
        {
            var config = State.VoucherAdoptionConfigMap[tick];
            lossAmount = config.PoolAmount;
            commissionAmount = config.CommissionAmount;
        }
    }

    private void ProcessAdoptWithVoucherTransfer(long lossAmount, long commissionAmount, long outputAmount,
        Address recipient, string ancestor, string tick, out long subsidyAmount)
    {
        var poolHash = GetSpinPoolHash(tick);

        // transfer ancestor to virtual address
        if (lossAmount > 0)
        {
            State.TokenContract.Transfer.VirtualSend(poolHash, new TransferInput
            {
                Symbol = ancestor,
                Amount = lossAmount,
                To = GetReceivingAddress(GetTickFromSymbol(ancestor))
            });
        }

        // send commission to recipient
        if (commissionAmount > 0)
        {
            State.TokenContract.Transfer.VirtualSend(poolHash, new TransferInput
            {
                Amount = commissionAmount,
                To = recipient,
                Symbol = ancestor
            });
        }

        var config = State.RerollConfigMap[tick];
        var amount = config != null
            ? outputAmount.Mul(config.Rate).Div(SchrodingerContractConstants.Denominator)
            : outputAmount;
        
        State.TokenContract.Transfer.VirtualSend(poolHash, new TransferInput
        {
            To = Context.Self,
            Symbol = ancestor,
            Amount = amount
        });

        subsidyAmount = lossAmount.Add(commissionAmount).Add(amount);
    }
}