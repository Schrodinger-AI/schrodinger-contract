using System.Linq;
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

        rewardList = new RepeatedField<Reward>
        {
            new Reward { Name = "Item2", Amount = 1, Type = RewardType.Point, Weight = 1 },
            new Reward { Name = "Item2", Amount = 1, Type = RewardType.Point, Weight = 1 }
        };

        await SchrodingerContractStub.SetRewardConfig.SendAsync(new SetRewardConfigInput
        {
            Tick = _tick,
            Rewards = { rewardList }
        });

        output = await SchrodingerContractStub.GetRewardConfig.CallAsync(new StringValue
        {
            Value = _tick
        });
        output.List.Data.Count.ShouldBe(1);

        rewardList = new RepeatedField<Reward>
        {
            new Reward { Name = "Item2", Amount = 1, Type = RewardType.Point, Weight = 1 }
        };

        result = await SchrodingerContractStub.SetRewardConfig.SendAsync(new SetRewardConfigInput
        {
            Tick = _tick,
            Rewards = { rewardList }
        });
        result.TransactionResult.Logs.FirstOrDefault(l => l.Name == nameof(RewardConfigSet)).ShouldBeNull();
    }

    [Fact]
    public async Task SetRewardConfigTests_Fail()
    {
        await DeployTest();

        var result = await SchrodingerContractStub.SetRewardConfig.SendWithExceptionAsync(new SetRewardConfigInput());
        result.TransactionResult.Error.ShouldContain("Invalid tick.");

        result = await SchrodingerContractStub.SetRewardConfig.SendWithExceptionAsync(new SetRewardConfigInput
        {
            Tick = "test"
        });
        result.TransactionResult.Error.ShouldContain("Invalid rewards.");

        result = await SchrodingerContractStub.SetRewardConfig.SendWithExceptionAsync(new SetRewardConfigInput
        {
            Tick = "test",
            Rewards = { }
        });
        result.TransactionResult.Error.ShouldContain("Invalid rewards.");

        result = await SchrodingerContractStub.SetRewardConfig.SendWithExceptionAsync(new SetRewardConfigInput
        {
            Tick = "test",
            Rewards = { new Reward() }
        });
        result.TransactionResult.Error.ShouldContain("Invalid reward name.");

        result = await SchrodingerContractStub.SetRewardConfig.SendWithExceptionAsync(new SetRewardConfigInput
        {
            Tick = "test",
            Rewards = { new Reward { Name = "Item1", Type = RewardType.Point, Amount = -1 } }
        });
        result.TransactionResult.Error.ShouldContain("Invalid reward amount.");

        result = await SchrodingerContractStub.SetRewardConfig.SendWithExceptionAsync(new SetRewardConfigInput
        {
            Tick = "test",
            Rewards = { new Reward { Name = "Item1", Type = RewardType.Point, Amount = 1, Weight = -1 } }
        });
        result.TransactionResult.Error.ShouldContain("Invalid reward weight.");

        result = await SchrodingerContractStub.SetRewardConfig.SendWithExceptionAsync(new SetRewardConfigInput
        {
            Tick = "test",
            Rewards =
            {
                new Reward { Name = "Item1", Type = RewardType.Point, Amount = 1, Weight = 1 },
                new Reward { Name = "Item1" }
            }
        });
        result.TransactionResult.Error.ShouldContain("Rewards contains duplicate names.");

        result = await SchrodingerContractStub.SetRewardConfig.SendWithExceptionAsync(new SetRewardConfigInput
        {
            Tick = "test",
            Rewards =
            {
                new Reward { Name = "Item1", Type = RewardType.Point, Amount = 1, Weight = 1 }
            }
        });
        result.TransactionResult.Error.ShouldContain("Inscription not found.");

        result = await UserSchrodingerContractStub.SetRewardConfig.SendWithExceptionAsync(new SetRewardConfigInput
        {
            Tick = _tick,
            Rewards =
            {
                new Reward { Name = "Item1", Type = RewardType.Point, Amount = 1, Weight = 1 }
            }
        });
        result.TransactionResult.Error.ShouldContain("No permission.");
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
    public async Task SpinTests_Fail()
    {
        await DeployTest();

        await SchrodingerContractStub.SetRewardConfig.SendAsync(new SetRewardConfigInput
        {
            Tick = _tick,
            Rewards = { new Reward { Name = "name", Type = RewardType.Point, Amount = 1, Weight = 1 } }
        });

        var result = await SchrodingerContractStub.Spin.SendWithExceptionAsync(new SpinInput());
        result.TransactionResult.Error.ShouldContain("Invalid tick.");

        result = await SchrodingerContractStub.Spin.SendWithExceptionAsync(new SpinInput
        {
            Tick = "test"
        });
        result.TransactionResult.Error.ShouldContain("Invalid seed.");

        result = await SchrodingerContractStub.Spin.SendWithExceptionAsync(new SpinInput
        {
            Tick = "test",
            Seed = new Hash()
        });
        result.TransactionResult.Error.ShouldContain("Invalid seed.");

        var seed = HashHelper.ComputeFrom("seed");

        result = await SchrodingerContractStub.Spin.SendWithExceptionAsync(new SpinInput
        {
            Tick = "test",
            Seed = seed
        });
        result.TransactionResult.Error.ShouldContain("Invalid expiration time.");

        result = await SchrodingerContractStub.Spin.SendWithExceptionAsync(new SpinInput
        {
            Tick = "test",
            Seed = seed,
            ExpirationTime = BlockTimeProvider.GetBlockTime().Seconds
        });
        result.TransactionResult.Error.ShouldContain("Invalid signature.");

        result = await SchrodingerContractStub.Spin.SendWithExceptionAsync(new SpinInput
        {
            Tick = "test",
            Seed = seed,
            ExpirationTime = BlockTimeProvider.GetBlockTime().Seconds,
            Signature = ByteString.Empty
        });
        result.TransactionResult.Error.ShouldContain("Invalid signature.");

        result = await SchrodingerContractStub.Spin.SendWithExceptionAsync(new SpinInput
        {
            Tick = "test",
            Seed = seed,
            ExpirationTime = BlockTimeProvider.GetBlockTime().Seconds,
            Signature = ByteString.CopyFrom(Hash.Empty.ToByteArray())
        });
        result.TransactionResult.Error.ShouldContain("Signature expired.");

        var expirationTime = BlockTimeProvider.GetBlockTime().AddDays(1).Seconds;

        result = await SchrodingerContractStub.Spin.SendWithExceptionAsync(new SpinInput
        {
            Tick = "test",
            Seed = seed,
            ExpirationTime = expirationTime,
            Signature = ByteString.CopyFrom(Hash.Empty.ToByteArray())
        });
        result.TransactionResult.Error.ShouldContain("Invalid signature.");

        result = await SchrodingerContractStub.Spin.SendWithExceptionAsync(new SpinInput
        {
            Tick = "test",
            Seed = seed,
            ExpirationTime = expirationTime,
            Signature = GenerateSignature(UserKeyPair.PrivateKey, "test", seed, expirationTime)
        });
        result.TransactionResult.Error.ShouldContain("Signature not valid.");

        var config = await SchrodingerContractStub.GetRewardConfig.CallAsync(new StringValue { Value = _tick });

        await TokenContractStub.Issue.SendAsync(new IssueInput
        {
            Amount = 1_00000000,
            Symbol = $"{_tick}-1",
            To = config.Pool
        });

        await SchrodingerContractStub.Spin.SendAsync(new SpinInput
        {
            Tick = _tick,
            Seed = seed,
            ExpirationTime = expirationTime,
            Signature = GenerateSignature(DefaultKeyPair.PrivateKey, _tick, seed, expirationTime)
        });

        result = await SchrodingerContractStub.Spin.SendWithExceptionAsync(new SpinInput
        {
            Tick = _tick,
            Seed = seed,
            ExpirationTime = expirationTime,
            Signature = GenerateSignature(DefaultKeyPair.PrivateKey, _tick, seed, expirationTime)
        });
        result.TransactionResult.Error.ShouldContain("Signature used.");
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

        return log.VoucherInfo.VoucherId;
    }

    [Fact]
    public async Task AdoptWithVoucherTests_Fail()
    {
        await DeployTest();

        var result = await SchrodingerContractStub.AdoptWithVoucher.SendWithExceptionAsync(new AdoptWithVoucherInput());
        result.TransactionResult.Error.ShouldContain("Invalid tick.");

        result = await SchrodingerContractStub.AdoptWithVoucher.SendWithExceptionAsync(new AdoptWithVoucherInput
        {
            Tick = "test"
        });
        result.TransactionResult.Error.ShouldContain("Inscription not found.");

        result = await SchrodingerContractStub.AdoptWithVoucher.SendWithExceptionAsync(new AdoptWithVoucherInput
        {
            Tick = _tick
        });
        result.TransactionResult.Error.ShouldContain("Voucher not enough.");

        await SchrodingerContractStub.AirdropVoucher.SendAsync(new AirdropVoucherInput
        {
            Tick = _tick,
            List = { DefaultAddress },
            Amount = 1
        });
    }

    [Fact]
    public async Task ConfirmVoucherTests()
    {
        var voucherId = await AdoptWithVoucherTests();

        var voucherInfo = await SchrodingerContractStub.GetVoucherInfo.CallAsync(voucherId);
        voucherInfo.AdoptId.ShouldBeNull();

        var config = await SchrodingerContractStub.GetRewardConfig.CallAsync(new StringValue { Value = _tick });
        await TokenContractStub.Issue.SendAsync(new IssueInput
        {
            Amount = 1_60000000,
            Symbol = $"{_tick}-1",
            To = config.Pool
        });

        var balance = await GetTokenBalance($"{_tick}-1", config.Pool);
        balance.ShouldBe(1_60000000);

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

        balance = await GetTokenBalance($"{_tick}-1", config.Pool);
        balance.ShouldBe(0);
    }

    [Fact]
    public async Task ConfirmVoucherTests_Fail()
    {
        var voucherId = await AdoptWithVoucherTests();

        var result = await SchrodingerContractStub.ConfirmVoucher.SendWithExceptionAsync(new ConfirmVoucherInput());
        result.TransactionResult.Error.ShouldContain("Invalid voucher id.");

        result = await SchrodingerContractStub.ConfirmVoucher.SendWithExceptionAsync(new ConfirmVoucherInput
        {
            VoucherId = HashHelper.ComputeFrom("test")
        });
        result.TransactionResult.Error.ShouldContain("Invalid signature.");

        result = await SchrodingerContractStub.ConfirmVoucher.SendWithExceptionAsync(new ConfirmVoucherInput
        {
            VoucherId = HashHelper.ComputeFrom("test"),
            Signature = GenerateSignature(UserKeyPair.PrivateKey, HashHelper.ComputeFrom("test"))
        });
        result.TransactionResult.Error.ShouldContain("Voucher id not exists.");

        result = await UserSchrodingerContractStub.ConfirmVoucher.SendWithExceptionAsync(new ConfirmVoucherInput
        {
            VoucherId = voucherId,
            Signature = GenerateSignature(UserKeyPair.PrivateKey, HashHelper.ComputeFrom("test"))
        });
        result.TransactionResult.Error.ShouldContain("No permission.");

        result = await SchrodingerContractStub.ConfirmVoucher.SendWithExceptionAsync(new ConfirmVoucherInput
        {
            VoucherId = voucherId,
            Signature = GenerateSignature(UserKeyPair.PrivateKey, HashHelper.ComputeFrom("test"))
        });
        result.TransactionResult.Error.ShouldContain("Signature not valid.");

        var config = await SchrodingerContractStub.GetRewardConfig.CallAsync(new StringValue { Value = _tick });

        await TokenContractStub.Issue.SendAsync(new IssueInput
        {
            Amount = 1_60000000,
            Symbol = $"{_tick}-1",
            To = config.Pool
        });

        await SchrodingerContractStub.ConfirmVoucher.SendAsync(new ConfirmVoucherInput
        {
            VoucherId = voucherId,
            Signature = GenerateSignature(DefaultKeyPair.PrivateKey, voucherId)
        });

        result = await SchrodingerContractStub.ConfirmVoucher.SendWithExceptionAsync(new ConfirmVoucherInput
        {
            VoucherId = voucherId,
            Signature = GenerateSignature(DefaultKeyPair.PrivateKey, voucherId)
        });
        result.TransactionResult.Error.ShouldContain("Already confirmed.");
    }

    [Fact]
    public async Task SetVoucherAdoptionConfigTests()
    {
        var voucherId = await AdoptWithVoucherTests();

        var result = await SchrodingerContractStub.SetVoucherAdoptionConfig.SendAsync(new SetVoucherAdoptionConfigInput
        {
            Tick = _tick,
            CommissionAmount = 25000000,
            PoolAmount = 25000000,
            VoucherAmount = 5
        });
        result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);

        var log = GetLogEvent<VoucherAdoptionConfigSet>(result.TransactionResult);
        log.Tick.ShouldBe(_tick);
        log.Config.CommissionAmount.ShouldBe(25000000);
        log.Config.PoolAmount.ShouldBe(25000000);
        log.Config.VoucherAmount.ShouldBe(5);

        var output =
            await SchrodingerContractStub.GetVoucherAdoptionConfig.CallAsync(new StringValue { Value = _tick });
        output.ShouldBe(log.Config);

        var config = await SchrodingerContractStub.GetRewardConfig.CallAsync(new StringValue { Value = _tick });
        await TokenContractStub.Issue.SendAsync(new IssueInput
        {
            Amount = 1_50000000,
            Symbol = $"{_tick}-1",
            To = config.Pool
        });

        var balance = await GetTokenBalance($"{_tick}-1", config.Pool);
        balance.ShouldBe(1_50000000);

        result = await SchrodingerContractStub.ConfirmVoucher.SendAsync(new ConfirmVoucherInput
        {
            VoucherId = voucherId,
            Signature = GenerateSignature(DefaultKeyPair.PrivateKey, voucherId)
        });

        var adopted = GetLogEvent<Adopted>(result.TransactionResult);
        adopted.LossAmount.ShouldBe(25000000);
        adopted.CommissionAmount.ShouldBe(25000000);

        balance = await GetTokenBalance($"{_tick}-1", config.Pool);
        balance.ShouldBe(0);
    }

    [Fact]
    public async Task SetVoucherAdoptionConfigTests_Fail()
    {
        await DeployTest();

        var result = await SchrodingerContractStub.SetVoucherAdoptionConfig.SendWithExceptionAsync(
            new SetVoucherAdoptionConfigInput());
        result.TransactionResult.Error.ShouldContain("Invalid tick.");

        result = await SchrodingerContractStub.SetVoucherAdoptionConfig.SendWithExceptionAsync(
            new SetVoucherAdoptionConfigInput
            {
                Tick = "test",
                CommissionAmount = -1
            });
        result.TransactionResult.Error.ShouldContain("Invalid commission amount.");

        result = await SchrodingerContractStub.SetVoucherAdoptionConfig.SendWithExceptionAsync(
            new SetVoucherAdoptionConfigInput
            {
                Tick = "test",
                PoolAmount = -1
            });
        result.TransactionResult.Error.ShouldContain("Invalid pool amount.");
        
        result = await SchrodingerContractStub.SetVoucherAdoptionConfig.SendWithExceptionAsync(
            new SetVoucherAdoptionConfigInput
            {
                Tick = "test",
                VoucherAmount = -1
            });
        result.TransactionResult.Error.ShouldContain("Invalid voucher amount.");

        result = await SchrodingerContractStub.SetVoucherAdoptionConfig.SendWithExceptionAsync(
            new SetVoucherAdoptionConfigInput
            {
                Tick = "test"
            });
        result.TransactionResult.Error.ShouldContain("Inscription not found.");

        result = await UserSchrodingerContractStub.SetVoucherAdoptionConfig.SendWithExceptionAsync(
            new SetVoucherAdoptionConfigInput
            {
                Tick = _tick
            });
        result.TransactionResult.Error.ShouldContain("No permission.");
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