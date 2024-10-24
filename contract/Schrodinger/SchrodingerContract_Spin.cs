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

        var reward = Spin(input.Tick);

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

        var inscriptionInfo = State.InscriptionInfoMap[input.Tick];
        Assert(inscriptionInfo != null, "Tick not deployed.");

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

        long.TryParse(new BigIntValue(SchrodingerContractConstants.Ten).Pow(inscriptionInfo.Decimals).Value,
            out var outputAmount);

        CalculateAmountReverse(inscriptionInfo.MaxGenLossRate, inscriptionInfo.CommissionRate, outputAmount,
            out var lossAmount, out var commissionAmount);

        ProcessAdoptWithVoucherTransfer(lossAmount, commissionAmount, inscriptionInfo.Recipient,
            inscriptionInfo.Ancestor, GetPoolHash(input.Tick));

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

        var voucherInfo = State.VoucherInfoMap[input.VoucherId];
        Assert(voucherInfo != null, "Voucher id not exists.");
        Assert(voucherInfo!.Account == Context.Sender, "No permission.");
        Assert(voucherInfo.AdoptId == null, "Already confirmed.");

        var inscriptionInfo = State.InscriptionInfoMap[voucherInfo!.Tick];
        Assert(inscriptionInfo != null, "Tick not deployed.");

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

        CalculateAmountReverse(inscriptionInfo.LossRate, inscriptionInfo.CommissionRate, outputAmount,
            out var lossAmount, out var commissionAmount);

        adoptInfo.OutputAmount = outputAmount;

        adoptInfo.Attributes = voucherInfo.Attributes;
        adoptInfo.Symbol = GenerateSymbol(voucherInfo.Tick, symbolCount);
        adoptInfo.TokenName = GenerateTokenName(adoptInfo.Symbol, adoptInfo.Gen);

        State.SymbolCountMap[voucherInfo.Tick] = symbolCount.Add(1);
        
        State.TokenContract.Transfer.VirtualSend(GetPoolHash(voucherInfo.Tick), new TransferInput
        {
            To = Context.Self,
            Symbol = parent,
            Amount = outputAmount
        });

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
            TokenName = adoptInfo.TokenName
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

    private Reward Spin(string tick)
    {
        var randomHash = GetRandomHash();

        var rewardList = State.RewardListMap[tick].Data.OrderBy(r => r.Weight).ToList();
        var weightSum = rewardList.Sum(x => x.Weight);

        var random = Context.ConvertHashToInt64(randomHash, 1, weightSum + 1);

        var currentWeight = 0L;
        foreach (var reward in rewardList)
        {
            currentWeight += reward.Weight;
            if (currentWeight < random)
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
                State.AdoptionVoucherMap[tick][Context.Sender]++;
                break;
            case RewardType.Token:
                var symbol = State.InscriptionInfoMap[tick].Ancestor;
                State.TokenContract.Transfer.VirtualSend(GetPoolHash(tick), new TransferInput
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
            HashHelper.ConcatAndCompute(Context.TransactionId, HashHelper.ComputeFrom(Context.Sender)));
    }

    private Hash GetRandomHash(Hash hash)
    {
        return HashHelper.ConcatAndCompute(hash, GetRandomHash());
    }

    private void CalculateAmountReverse(long lossRate, long commissionRate, long outputAmount, out long lossAmount,
        out long commissionAmount)
    {
        // calculate amount
        lossAmount = outputAmount.Div(SchrodingerContractConstants.Denominator.Sub(lossRate)).Mul(lossRate);

        commissionAmount = lossAmount.Mul(commissionRate).Div(SchrodingerContractConstants.Denominator);
        if (commissionAmount == 0 && commissionRate != 0) commissionAmount = 1;

        lossAmount = lossAmount.Sub(commissionAmount);
    }

    private void ProcessAdoptWithVoucherTransfer(long lossAmount, long commissionAmount, Address recipient,
        string ancestor, Hash poolHash)
    {
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
    }
}