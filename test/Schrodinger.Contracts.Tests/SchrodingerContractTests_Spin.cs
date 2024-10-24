using System.Threading.Tasks;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.Cryptography;
using AElf.CSharp.Core.Extension;
using AElf.Types;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace Schrodinger;

public partial class SchrodingerContractTests
{
    [Fact]
    public async Task SetRewardConfigTests()
    {
        await DeployTest();

        var rewardList = new RepeatedField<Reward>
            { new Reward { Name = "Item1", Amount = 1, Type = RewardType.Point, Weight = 1 } };

        var result = await SchrodingerContractStub.SetRewardConfig.SendAsync(new SetRewardConfigInput
        {
            Tick = _tick,
            Rewards = { rewardList }
        });
        result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);

        var log = GetLogEvent<RewardConfigSet>(result.TransactionResult);
        log.Tick.ShouldBe(_tick);
        log.List.Data.ShouldBe(rewardList);
        log.Pool.ShouldNotBeNull();

        var output = await SchrodingerContractStub.GetRewardConfig.CallAsync(new StringValue
        {
            Value = _tick
        });
        output.List.Data.ShouldBe(rewardList);
        output.Pool.ShouldBe(log.Pool);
    }

    [Fact]
    public async Task SpinTests()
    {
        const string name = "item";
        const int amount = 1;
        const int weight = 10;
        var expirationTime = BlockTimeProvider.GetBlockTime().AddDays(1).Seconds;

        await DeployTest();

        // Point
        {
            var seed = HashHelper.ComputeFrom("Seed");

            await SchrodingerContractStub.SetRewardConfig.SendAsync(new SetRewardConfigInput
            {
                Tick = _tick,
                Rewards = { new Reward { Name = name, Amount = amount, Type = RewardType.Point, Weight = weight } }
            });

            var result = await SchrodingerContractStub.Spin.SendAsync(new SpinInput
            {
                Tick = _tick,
                Seed = seed,
                ExpirationTime = expirationTime,
                Signature = GenerateSignature(DefaultKeyPair.PrivateKey, _tick, seed, expirationTime)
            });
            result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);

            var log = GetLogEvent<Spun>(result.TransactionResult);
            log.Tick.ShouldBe(_tick);
            log.Seed.ShouldBe(seed);
            log.SpinInfo.Name.ShouldBe(name);
            log.SpinInfo.Amount.ShouldBe(amount);
            log.SpinInfo.Account.ShouldBe(DefaultAddress);
            log.SpinInfo.Type.ShouldBe(RewardType.Point);
            log.SpinInfo.SpinId.ShouldNotBeNull();

            var spinInfo = await SchrodingerContractStub.GetSpinInfo.CallAsync(log.SpinInfo.SpinId);
            spinInfo.ShouldBe(log.SpinInfo);
        }

        // Voucher
        {
            var seed = HashHelper.ComputeFrom("Seed2");

            await SchrodingerContractStub.SetRewardConfig.SendAsync(new SetRewardConfigInput
            {
                Tick = _tick,
                Rewards =
                {
                    new Reward { Name = name, Amount = amount, Type = RewardType.AdoptionVoucher, Weight = weight }
                }
            });

            var voucherAmount = await SchrodingerContractStub.GetAdoptionVoucherAmount.CallAsync(
                new GetAdoptionVoucherAmountInput
                {
                    Tick = _tick,
                    Account = DefaultAddress
                });
            voucherAmount.Value.ShouldBe(0);

            var result = await SchrodingerContractStub.Spin.SendAsync(new SpinInput
            {
                Tick = _tick,
                Seed = seed,
                ExpirationTime = expirationTime,
                Signature = GenerateSignature(DefaultKeyPair.PrivateKey, _tick, seed, expirationTime)
            });
            result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);

            var log = GetLogEvent<Spun>(result.TransactionResult);
            log.Tick.ShouldBe(_tick);
            log.Seed.ShouldBe(seed);
            log.SpinInfo.Name.ShouldBe(name);
            log.SpinInfo.Amount.ShouldBe(amount);
            log.SpinInfo.Account.ShouldBe(DefaultAddress);
            log.SpinInfo.Type.ShouldBe(RewardType.AdoptionVoucher);
            log.SpinInfo.SpinId.ShouldNotBeNull();

            var spinInfo = await SchrodingerContractStub.GetSpinInfo.CallAsync(log.SpinInfo.SpinId);
            spinInfo.ShouldBe(log.SpinInfo);

            voucherAmount = await SchrodingerContractStub.GetAdoptionVoucherAmount.CallAsync(
                new GetAdoptionVoucherAmountInput
                {
                    Tick = _tick,
                    Account = DefaultAddress
                });
            voucherAmount.Value.ShouldBe(amount);
        }

        // Token
        {
            var seed = HashHelper.ComputeFrom("Seed3");
            var symbol = $"{_tick}-1";

            var config = await SchrodingerContractStub.GetRewardConfig.CallAsync(new StringValue
            {
                Value = _tick
            });

            await TokenContractStub.Issue.SendAsync(new IssueInput
            {
                To = config.Pool,
                Amount = amount,
                Symbol = symbol
            });

            await SchrodingerContractStub.SetRewardConfig.SendAsync(new SetRewardConfigInput
            {
                Tick = _tick,
                Rewards = { new Reward { Name = name, Amount = amount, Type = RewardType.Token, Weight = weight } }
            });

            var poolBalance = await GetTokenBalance(symbol, config.Pool);
            poolBalance.ShouldBe(amount);

            var defaultBalance = await GetTokenBalance(symbol, DefaultAddress);
            defaultBalance.ShouldBe(0);

            var result = await SchrodingerContractStub.Spin.SendAsync(new SpinInput
            {
                Tick = _tick,
                Seed = seed,
                ExpirationTime = expirationTime,
                Signature = GenerateSignature(DefaultKeyPair.PrivateKey, _tick, seed, expirationTime)
            });
            result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);

            var log = GetLogEvent<Spun>(result.TransactionResult);
            log.Tick.ShouldBe(_tick);
            log.Seed.ShouldBe(seed);
            log.SpinInfo.Name.ShouldBe(name);
            log.SpinInfo.Amount.ShouldBe(amount);
            log.SpinInfo.Account.ShouldBe(DefaultAddress);
            log.SpinInfo.Type.ShouldBe(RewardType.Token);
            log.SpinInfo.SpinId.ShouldNotBeNull();

            var spinInfo = await SchrodingerContractStub.GetSpinInfo.CallAsync(log.SpinInfo.SpinId);
            spinInfo.ShouldBe(log.SpinInfo);

            poolBalance = await GetTokenBalance(symbol, config.Pool);
            poolBalance.ShouldBe(0);

            defaultBalance = await GetTokenBalance(symbol, DefaultAddress);
            defaultBalance.ShouldBe(amount);
        }
    }

    [Fact]
    public async Task<Hash> AdoptWithVoucherTests()
    {
        var seed = HashHelper.ComputeFrom("Seed");
        var expirationTime = BlockTimeProvider.GetBlockTime().AddDays(1).Seconds;

        await DeployTest();

        var result = await SchrodingerContractStub.SetRewardConfig.SendAsync(new SetRewardConfigInput
        {
            Tick = _tick,
            Rewards = { new Reward { Name = "item", Amount = 1, Type = RewardType.AdoptionVoucher, Weight = 10 } }
        });
        var pool = GetLogEvent<RewardConfigSet>(result.TransactionResult).Pool;

        await SchrodingerContractStub.Spin.SendAsync(new SpinInput
        {
            Tick = _tick,
            Seed = seed,
            ExpirationTime = expirationTime,
            Signature = GenerateSignature(DefaultKeyPair.PrivateKey, _tick, seed, expirationTime)
        });

        await TokenContractStub.Issue.SendAsync(new IssueInput
        {
            Amount = 1_00000000,
            Symbol = $"{_tick}-1",
            To = pool
        });

        var balance = await GetTokenBalance($"{_tick}-1", pool);
        balance.ShouldBe(1_00000000);

        result = await SchrodingerContractStub.AdoptWithVoucher.SendAsync(new AdoptWithVoucherInput
        {
            Tick = _tick
        });
        result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);

        var log = GetLogEvent<AdoptedWithVoucher>(result.TransactionResult);
        log.VoucherInfo.Account.ShouldBe(DefaultAddress);
        log.VoucherInfo.Tick.ShouldBe(_tick);
        log.VoucherInfo.VoucherId.ShouldNotBeNull();
        log.VoucherInfo.Attributes.ShouldNotBeNull();
        log.VoucherInfo.AdoptId.ShouldBeNull();

        var voucherInfo = await SchrodingerContractStub.GetVoucherInfo.CallAsync(log.VoucherInfo.VoucherId);
        voucherInfo.ShouldBe(log.VoucherInfo);

        var voucherAmount = await SchrodingerContractStub.GetAdoptionVoucherAmount.CallAsync(
            new GetAdoptionVoucherAmountInput
            {
                Tick = _tick,
                Account = DefaultAddress
            });
        voucherAmount.Value.ShouldBe(0);

        balance = await GetTokenBalance($"{_tick}-1", pool);
        balance.ShouldBe(40000000);

        return log.VoucherInfo.VoucherId;
    }

    [Fact]
    public async Task ConfirmVoucherTests()
    {
        var voucherId = await AdoptWithVoucherTests();

        var voucherInfo = await SchrodingerContractStub.GetVoucherInfo.CallAsync(voucherId);
        voucherInfo.AdoptId.ShouldBeNull();

        var balance = await GetTokenBalance($"{_tick}-1", SchrodingerContractAddress);
        balance.ShouldBe(0);

        var config = await SchrodingerContractStub.GetRewardConfig.CallAsync(new StringValue { Value = _tick });
        await TokenContractStub.Issue.SendAsync(new IssueInput
        {
            Amount = 1_00000000,
            Symbol = $"{_tick}-1",
            To = config.Pool
        });

        var result = await SchrodingerContractStub.ConfirmVoucher.SendAsync(new ConfirmVoucherInput
        {
            VoucherId = voucherId,
            Signature = GenerateSignature(DefaultKeyPair.PrivateKey, voucherId)
        });
        result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);

        var confirmed = GetLogEvent<VoucherConfirmed>(result.TransactionResult);
        var adopted = GetLogEvent<Adopted>(result.TransactionResult);

        voucherInfo = await SchrodingerContractStub.GetVoucherInfo.CallAsync(voucherId);
        confirmed.VoucherInfo.ShouldBe(voucherInfo);

        adopted.AdoptId.ShouldBe(voucherInfo.AdoptId);
    }

    private ByteString GenerateSignature(byte[] privateKey, string tick, Hash seed, long expirationTime)
    {
        var data = new SpinInput
        {
            Tick = tick,
            Seed = seed,
            ExpirationTime = expirationTime
        };
        var dataHash = HashHelper.ComputeFrom(data);
        var signature = CryptoHelper.SignWithPrivateKey(privateKey, dataHash.ToByteArray());
        return ByteStringHelper.FromHexString(signature.ToHex());
    }

    private ByteString GenerateSignature(byte[] privateKey, Hash voucherId)
    {
        var data = new ConfirmVoucherInput
        {
            VoucherId = voucherId
        };
        var dataHash = HashHelper.ComputeFrom(data);
        var signature = CryptoHelper.SignWithPrivateKey(privateKey, dataHash.ToByteArray());
        return ByteStringHelper.FromHexString(signature.ToHex());
    }
}