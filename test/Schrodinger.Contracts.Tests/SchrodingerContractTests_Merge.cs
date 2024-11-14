using System.Linq;
using System.Threading.Tasks;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.Cryptography;
using AElf.Types;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace Schrodinger;

public partial class SchrodingerContractTests
{
    [Fact]
    public async Task SetMergeRatesConfigTests()
    {
        await DeployTest();

        var result = await SchrodingerContractStub.SetMergeRatesConfig.SendAsync(new SetMergeRatesConfigInput
        {
            Tick = _tick,
            MaximumLevel = 2,
            MergeRates =
            {
                new MergeRate
                {
                    Level = 1,
                    Rate = 5
                },
                new MergeRate
                {
                    Level = 2,
                    Rate = 5
                },
                new MergeRate
                {
                    Level = 3,
                    Rate = 5
                }
            }
        });

        result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);

        var log = GetLogEvent<MergeRatesConfigSet>(result.TransactionResult);
        log.MergeRates.Data.Count.ShouldBe(2);
        log.MaximumLevel.ShouldBe(2);
        log.Tick.ShouldBe(_tick);
        log.MergeRates.Data.First().ShouldBe(new MergeRate
        {
            Level = 1,
            Rate = 5
        });
        log.MergeRates.Data.Last().ShouldBe(new MergeRate
        {
            Level = 2,
            Rate = 5
        });

        var output = await SchrodingerContractStub.GetMergeConfig.CallAsync(new StringValue { Value = _tick });
        output.Tick.ShouldBe(_tick);
        output.MaximumLevel.ShouldBe(2);
        output.MergeRates.ShouldBe(log.MergeRates);
    }

    [Fact]
    public async Task SetMergeRatesConfigTests_Fail()
    {
        await DeployTest();

        var result =
            await SchrodingerContractStub.SetMergeRatesConfig.SendWithExceptionAsync(new SetMergeRatesConfigInput());
        result.TransactionResult.Error.ShouldContain("Invalid tick.");

        result = await SchrodingerContractStub.SetMergeRatesConfig.SendWithExceptionAsync(new SetMergeRatesConfigInput
        {
            Tick = "test"
        });
        result.TransactionResult.Error.ShouldContain("Invalid merge rates.");

        result = await SchrodingerContractStub.SetMergeRatesConfig.SendWithExceptionAsync(new SetMergeRatesConfigInput
        {
            Tick = "test",
            MergeRates =
            {
                new MergeRate
                {
                    Rate = -1
                }
            }
        });
        result.TransactionResult.Error.ShouldContain("Invalid maximum level.");

        result = await SchrodingerContractStub.SetMergeRatesConfig.SendWithExceptionAsync(new SetMergeRatesConfigInput
        {
            Tick = "test",
            MergeRates =
            {
                new MergeRate
                {
                    Rate = -1
                }
            },
            MaximumLevel = 1
        });
        result.TransactionResult.Error.ShouldContain("Inscription not found.");

        result = await SchrodingerContractStub.SetMergeRatesConfig.SendWithExceptionAsync(new SetMergeRatesConfigInput
        {
            Tick = _tick,
            MergeRates =
            {
                new MergeRate
                {
                    Rate = -1
                }
            },
            MaximumLevel = 1
        });
        result.TransactionResult.Error.ShouldContain("Invalid merge rate.");

        result = await UserSchrodingerContractStub.SetMergeRatesConfig.SendWithExceptionAsync(
            new SetMergeRatesConfigInput
            {
                Tick = _tick,
                MergeRates =
                {
                    new MergeRate
                    {
                        Rate = 0
                    }
                },
                MaximumLevel = 1
            });
        result.TransactionResult.Error.ShouldContain("No permission.");
    }

    [Fact]
    public async Task SetMergeConfigTests()
    {
        await DeployTest();

        var result = await SchrodingerContractStub.SetMergeConfig.SendAsync(new SetMergeConfigInput
        {
            Tick = _tick,
            CommissionAmount = 100,
            PoolAmount = 100
        });
        result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);

        var log = GetLogEvent<MergeConfigSet>(result.TransactionResult);
        log.Tick.ShouldBe(_tick);
        log.Config.CommissionAmount.ShouldBe(100);
        log.Config.PoolAmount.ShouldBe(100);

        var output = await SchrodingerContractStub.GetMergeConfig.CallAsync(new StringValue { Value = _tick });
        output.Tick.ShouldBe(_tick);
        output.Config.ShouldBe(log.Config);
    }

    [Fact]
    public async Task SetMergeConfigTests_Fail()
    {
        await DeployTest();

        var result = await SchrodingerContractStub.SetMergeConfig.SendWithExceptionAsync(new SetMergeConfigInput());
        result.TransactionResult.Error.ShouldContain("Invalid tick.");

        result = await SchrodingerContractStub.SetMergeConfig.SendWithExceptionAsync(new SetMergeConfigInput
        {
            Tick = "test",
            CommissionAmount = -1
        });
        result.TransactionResult.Error.ShouldContain("Invalid commission amount.");

        result = await SchrodingerContractStub.SetMergeConfig.SendWithExceptionAsync(new SetMergeConfigInput
        {
            Tick = "test",
            PoolAmount = -1
        });
        result.TransactionResult.Error.ShouldContain("Invalid pool amount.");

        result = await SchrodingerContractStub.SetMergeConfig.SendWithExceptionAsync(new SetMergeConfigInput
        {
            Tick = "test"
        });
        result.TransactionResult.Error.ShouldContain("Inscription not found.");

        result = await UserSchrodingerContractStub.SetMergeConfig.SendWithExceptionAsync(new SetMergeConfigInput
        {
            Tick = _tick
        });
        result.TransactionResult.Error.ShouldContain("No permission.");
    }

    [Fact]
    public async Task MergeTests()
    {
        var sgr = $"{_tick}-1";
        Hash symbolAId;
        Hash symbolBId;
        string symbolA;
        string symbolB;
        Hash adoptInfoAId;
        Hash adoptInfoBId;

        await PrepareForMergeTests();

        {
            await TokenContractStub.Issue.SendAsync(new IssueInput
            {
                Amount = 1_60000000,
                Symbol = sgr,
                To = DefaultAddress
            });
            await TokenContractStub.Approve.SendAsync(new ApproveInput
            {
                Amount = 1_60000000,
                Symbol = sgr,
                Spender = SchrodingerContractAddress
            });
            var result = await SchrodingerContractStub.AdoptMaxGen.SendAsync(new AdoptMaxGenInput
            {
                Amount = 1_60000000,
                Domain = "test",
                Tick = _tick
            });
            var log = GetLogEvent<Adopted>(result.TransactionResult);
            log.LossAmount.ShouldBe(0_55000000);
            log.CommissionAmount.ShouldBe(0_55000000);
            log.OutputAmount.ShouldBe(1_00000000);

            symbolAId = log.AdoptId;
            symbolA = log.Symbol;

            await SchrodingerContractStub.Confirm.SendAsync(new ConfirmInput
            {
                AdoptId = symbolAId,
                Image = "image",
                ImageUri = "uri",
                Signature = GenerateSignature(DefaultKeyPair.PrivateKey, symbolAId, "image", "uri")
            });

            var balance = await GetTokenBalance(symbolA, DefaultAddress);
            balance.ShouldBe(1_00000000);

            await TokenContractStub.Approve.SendAsync(new ApproveInput
            {
                Amount = 1_00000000,
                Symbol = symbolA,
                Spender = SchrodingerContractAddress
            });
        }

        {
            await TokenContractStub.Issue.SendAsync(new IssueInput
            {
                Amount = 3_20000000,
                Symbol = sgr,
                To = DefaultAddress
            });
            await TokenContractStub.Approve.SendAsync(new ApproveInput
            {
                Amount = 3_20000000,
                Symbol = sgr,
                Spender = SchrodingerContractAddress
            });
            var result = await SchrodingerContractStub.AdoptMaxGen.SendAsync(new AdoptMaxGenInput
            {
                Amount = 3_20000000,
                Domain = "test",
                Tick = _tick
            });

            var log = GetLogEvent<Adopted>(result.TransactionResult);
            log.OutputAmount.ShouldBe(2_00000000);

            symbolBId = log.AdoptId;
            symbolB = log.Symbol;

            await SchrodingerContractStub.Confirm.SendAsync(new ConfirmInput
            {
                AdoptId = symbolBId,
                Image = "image",
                ImageUri = "uri",
                Signature = GenerateSignature(DefaultKeyPair.PrivateKey, symbolBId, "image", "uri")
            });

            var balance = await GetTokenBalance(symbolB, DefaultAddress);
            balance.ShouldBe(2_00000000);

            await TokenContractStub.Approve.SendAsync(new ApproveInput
            {
                Amount = 2_00000000,
                Symbol = symbolB,
                Spender = SchrodingerContractAddress
            });
        }

        {
            await TokenContractStub.Issue.SendAsync(new IssueInput
            {
                Amount = 1_60000000,
                Symbol = sgr,
                To = DefaultAddress
            });
            await TokenContractStub.Approve.SendAsync(new ApproveInput
            {
                Amount = 1_60000000,
                Symbol = sgr,
                Spender = SchrodingerContractAddress
            });
            var result = await SchrodingerContractStub.AdoptMaxGen.SendAsync(new AdoptMaxGenInput
            {
                Amount = 1_60000000,
                Domain = "test",
                Tick = _tick
            });
            var log = GetLogEvent<Adopted>(result.TransactionResult);
            adoptInfoAId = log.AdoptId;
        }

        {
            await TokenContractStub.Issue.SendAsync(new IssueInput
            {
                Amount = 3_20000000,
                Symbol = sgr,
                To = DefaultAddress
            });
            await TokenContractStub.Approve.SendAsync(new ApproveInput
            {
                Amount = 3_20000000,
                Symbol = sgr,
                Spender = SchrodingerContractAddress
            });
            var result = await SchrodingerContractStub.AdoptMaxGen.SendAsync(new AdoptMaxGenInput
            {
                Amount = 3_20000000,
                Domain = "test",
                Tick = _tick
            });

            var log = GetLogEvent<Adopted>(result.TransactionResult);
            log.OutputAmount.ShouldBe(2_00000000);
            adoptInfoBId = log.AdoptId;
        }

        // token x token
        {
            var result = await SchrodingerContractStub.Merge.SendAsync(new MergeInput
            {
                Tick = _tick,
                Level = 1,
                AdoptIdA = symbolAId,
                AdoptIdB = symbolBId,
                Signature = GenerateSignature(DefaultKeyPair.PrivateKey, _tick, symbolAId, symbolBId, 1)
            });

            var log = GetLogEvent<Merged>(result.TransactionResult);
            log.Tick.ShouldBe(_tick);
            log.AdoptIdA.ShouldBe(symbolAId);
            log.AdoptIdB.ShouldBe(symbolBId);
            log.AmountA.ShouldBe(0);
            log.AmountB.ShouldBe(1_00000000);
            log.SymbolA.ShouldBe($"{_tick}-2");
            log.SymbolB.ShouldBe($"{_tick}-3");
            log.LossAmount.ShouldBe(0_25000000);
            log.CommissionAmount.ShouldBe(0_25000000);
            log.AdoptInfo.Adopter.ShouldBe(DefaultAddress);
            log.AdoptInfo.InputAmount.ShouldBe(2_00000000);
            log.AdoptInfo.OutputAmount.ShouldBe(1_00000000);
            log.AdoptInfo.ImageCount.ShouldBe(2);
            log.AdoptInfo.BlockHeight.ShouldBe(result.TransactionResult.BlockNumber);
            log.AdoptInfo.Symbol.ShouldBe($"{_tick}-6");
            log.AdoptInfo.TokenName.ShouldBe($"{_tick}-6GEN9");
            log.AdoptInfo.Attributes.Data.Count.ShouldBe(11);
            log.AdoptInfo.Gen.ShouldBe(9);
            log.AdoptInfo.IsConfirmed.ShouldBeFalse();
            log.AdoptInfo.IsRerolled.ShouldBeFalse();
            log.AdoptInfo.IsUpdated.ShouldBeFalse();
            log.AdoptInfo.Level.ShouldBe(2);

            var balance = await GetTokenBalance(symbolA, DefaultAddress);
            balance.ShouldBe(0);
            balance = await GetTokenBalance(symbolB, DefaultAddress);
            balance.ShouldBe(1_00000000);

            var adoptInfo = await SchrodingerContractStub.GetAdoptInfo.CallAsync(log.AdoptInfo.AdoptId);
            adoptInfo.ShouldBe(log.AdoptInfo);

            await SchrodingerContractStub.Confirm.SendAsync(new ConfirmInput
            {
                AdoptId = adoptInfo.AdoptId,
                ImageUri = "uri",
                Image = "image",
                Signature = GenerateSignature(DefaultKeyPair.PrivateKey, adoptInfo.AdoptId, "image", "uri")
            });
        }

        {
            var result = await SchrodingerContractStub.Merge.SendAsync(new MergeInput
            {
                Tick = _tick,
                Level = 1,
                AdoptIdA = symbolBId,
                AdoptIdB = adoptInfoBId,
                Signature = GenerateSignature(DefaultKeyPair.PrivateKey, _tick, symbolBId, adoptInfoBId, 1)
            });

            var log = GetLogEvent<Merged>(result.TransactionResult);
            log.AdoptIdA.ShouldBe(symbolBId);
            log.AdoptIdB.ShouldBe(adoptInfoBId);
            var balance = await GetTokenBalance(symbolB, DefaultAddress);
            balance.ShouldBe(0);

            var adoptInfo = await SchrodingerContractStub.GetAdoptInfo.CallAsync(adoptInfoBId);
            adoptInfo.OutputAmount.ShouldBe(1_00000000);
        }

        {
            var result = await SchrodingerContractStub.Merge.SendAsync(new MergeInput
            {
                Tick = _tick,
                Level = 1,
                AdoptIdA = adoptInfoAId,
                AdoptIdB = adoptInfoBId,
                Signature = GenerateSignature(DefaultKeyPair.PrivateKey, _tick, adoptInfoAId, adoptInfoBId, 1)
            });

            var log = GetLogEvent<Merged>(result.TransactionResult);
            log.AdoptIdA.ShouldBe(adoptInfoAId);
            log.AdoptIdB.ShouldBe(adoptInfoBId);

            var adoptInfo = await SchrodingerContractStub.GetAdoptInfo.CallAsync(adoptInfoAId);
            adoptInfo.OutputAmount.ShouldBe(0);

            adoptInfo = await SchrodingerContractStub.GetAdoptInfo.CallAsync(adoptInfoBId);
            adoptInfo.OutputAmount.ShouldBe(0);
        }
    }

    [Fact]
    public async Task MergeTests_Fail()
    {
        await PrepareForMergeTests();

        var result = await SchrodingerContractStub.Merge.SendWithExceptionAsync(new MergeInput());
        result.TransactionResult.Error.ShouldContain("Invalid tick.");

        result = await SchrodingerContractStub.Merge.SendWithExceptionAsync(new MergeInput
        {
            Tick = "test"
        });
        result.TransactionResult.Error.ShouldContain("Invalid adopt id a.");

        result = await SchrodingerContractStub.Merge.SendWithExceptionAsync(new MergeInput
        {
            Tick = "test",
            AdoptIdA = HashHelper.ComputeFrom("test")
        });
        result.TransactionResult.Error.ShouldContain("Invalid adopt id b.");

        result = await SchrodingerContractStub.Merge.SendWithExceptionAsync(new MergeInput
        {
            Tick = "test",
            AdoptIdA = HashHelper.ComputeFrom("test"),
            AdoptIdB = HashHelper.ComputeFrom("test")
        });
        result.TransactionResult.Error.ShouldContain("Invalid level.");

        result = await SchrodingerContractStub.Merge.SendWithExceptionAsync(new MergeInput
        {
            Tick = "test",
            AdoptIdA = HashHelper.ComputeFrom("test"),
            AdoptIdB = HashHelper.ComputeFrom("test"),
            Level = 1
        });
        result.TransactionResult.Error.ShouldContain("Invalid signature.");

        result = await SchrodingerContractStub.Merge.SendWithExceptionAsync(new MergeInput
        {
            Tick = "test",
            AdoptIdA = HashHelper.ComputeFrom("test"),
            AdoptIdB = HashHelper.ComputeFrom("test"),
            Level = 1,
            Signature = GenerateSignature(DefaultKeyPair.PrivateKey, _tick, HashHelper.ComputeFrom("test"),
                HashHelper.ComputeFrom("test"), 1)
        });
        result.TransactionResult.Error.ShouldContain("Invalid signature.");
        
        result = await SchrodingerContractStub.Merge.SendWithExceptionAsync(new MergeInput
        {
            Tick = _tick,
            AdoptIdA = HashHelper.ComputeFrom("test"),
            AdoptIdB = HashHelper.ComputeFrom("test"),
            Level = 5,
            Signature = GenerateSignature(DefaultKeyPair.PrivateKey, _tick, HashHelper.ComputeFrom("test"),
                HashHelper.ComputeFrom("test"), 5)
        });
        result.TransactionResult.Error.ShouldContain("Already reach maximum level.");
        
        result = await SchrodingerContractStub.Merge.SendWithExceptionAsync(new MergeInput
        {
            Tick = _tick,
            AdoptIdA = HashHelper.ComputeFrom("test"),
            AdoptIdB = HashHelper.ComputeFrom("test"),
            Level = 1,
            Signature = GenerateSignature(DefaultKeyPair.PrivateKey, _tick, HashHelper.ComputeFrom("test"),
                HashHelper.ComputeFrom("test"), 1)
        });
        result.TransactionResult.Error.ShouldContain("not found.");
        
        var adoptId = await AdoptMaxGen();
        
        var adoptIdA = await AdoptMaxGen();
        await SchrodingerContractStub.RerollAdoption.SendAsync(adoptIdA);
        result = await SchrodingerContractStub.Merge.SendWithExceptionAsync(new MergeInput
        {
            Tick = _tick,
            AdoptIdA = adoptId,
            AdoptIdB = adoptIdA,
            Level = 1,
            Signature = GenerateSignature(DefaultKeyPair.PrivateKey, _tick, adoptId, adoptIdA, 1)
        });
        result.TransactionResult.Error.ShouldContain("already rerolled.");
        
        // adoptIdA = await AdoptTest();
        // result = await SchrodingerContractStub.Merge.SendWithExceptionAsync(new MergeInput
        // {
        //     Tick = _tick,
        //     AdoptIdA = adoptId,
        //     AdoptIdB = adoptIdA,
        //     Level = 1,
        //     Signature = GenerateSignature(DefaultKeyPair.PrivateKey, _tick, adoptId, adoptIdA, 1)
        // });
        // result.TransactionResult.Error.ShouldContain("not reach max generation.");
        
        adoptIdA = await AdoptMaxGen();
        result = await UserSchrodingerContractStub.Merge.SendWithExceptionAsync(new MergeInput
        {
            Tick = _tick,
            AdoptIdA = adoptId,
            AdoptIdB = adoptIdA,
            Level = 1,
            Signature = GenerateSignature(DefaultKeyPair.PrivateKey, _tick, adoptId, adoptIdA, 1)
        });
        result.TransactionResult.Error.ShouldContain("No permission.");
        
        result = await SchrodingerContractStub.Merge.SendAsync(new MergeInput
        {
            Tick = _tick,
            AdoptIdA = adoptId,
            AdoptIdB = adoptIdA,
            Level = 1,
            Signature = GenerateSignature(DefaultKeyPair.PrivateKey, _tick, adoptId, adoptIdA, 1)
        });

        var adoptIdB = GetLogEvent<Merged>(result.TransactionResult).AdoptInfo.AdoptId;
        
        result = await SchrodingerContractStub.Merge.SendWithExceptionAsync(new MergeInput
        {
            Tick = _tick,
            AdoptIdA = adoptId,
            AdoptIdB = adoptIdB,
            Level = 1,
            Signature = GenerateSignature(DefaultKeyPair.PrivateKey, _tick, adoptId, adoptIdB, 1)
        });
        result.TransactionResult.Error.ShouldContain("Level not matched.");
    }

    private async Task PrepareForMergeTests()
    {
        await DeployForMaxGen();
        await SchrodingerContractStub.SetRates.SendAsync(new SetRatesInput
        {
            Tick = _tick,
            CommissionRate = 5000,
            LossRate = 5285,
            MaxGenLossRate = 6875
        });
        await SchrodingerContractStub.SetMergeConfig.SendAsync(new SetMergeConfigInput
        {
            Tick = _tick,
            CommissionAmount = 25000000,
            PoolAmount = 25000000
        });
        await SchrodingerContractStub.SetMergeRatesConfig.SendAsync(new SetMergeRatesConfigInput
        {
            Tick = _tick,
            MaximumLevel = 3,
            MergeRates =
            {
                new MergeRate
                {
                    Level = 1,
                    Rate = 10000
                }
            }
        });
        await SchrodingerContractStub.SetRerollConfig.SendAsync(new SetRerollConfigInput
        {
            Tick = _tick,
            Index = 2,
            Rate = 5000
        });
    }

    private ByteString GenerateSignature(byte[] privateKey, string tick, Hash a, Hash b, long level)
    {
        var data = new MergeInput
        {
            Tick = tick,
            AdoptIdA = a,
            AdoptIdB = b,
            Level = level
        };
        var dataHash = HashHelper.ComputeFrom(data);
        var signature = CryptoHelper.SignWithPrivateKey(privateKey, dataHash.ToByteArray());
        return ByteStringHelper.FromHexString(signature.ToHex());
    }

    private async Task<Hash> AdoptMaxGen()
    {
        await TokenContractStub.Issue.SendAsync(new IssueInput
        {
            Amount = 3_20000000,
            Symbol = $"{_tick}-1",
            To = DefaultAddress
        });
        await TokenContractStub.Approve.SendAsync(new ApproveInput
        {
            Amount = 3_20000000,
            Symbol = $"{_tick}-1",
            Spender = SchrodingerContractAddress
        });
        var result = await SchrodingerContractStub.AdoptMaxGen.SendAsync(new AdoptMaxGenInput
        {
            Amount = 3_20000000,
            Domain = "test",
            Tick = _tick
        });
        var log = GetLogEvent<Adopted>(result.TransactionResult);
        return log.AdoptId;
    }
    
    // private async Task<Hash> AdoptTest()
    // {
    //     await TokenContractStub.Issue.SendAsync(new IssueInput
    //     {
    //         Symbol = Gen0,
    //         Amount = 2_00000000,
    //         To = DefaultAddress
    //     });
    //
    //     await TokenContractStub.Approve.SendAsync(new ApproveInput
    //     {
    //         Symbol = Gen0,
    //         Amount = 2_00000000,
    //         Spender = SchrodingerContractAddress
    //     });
    //
    //     var result = await SchrodingerContractStub.Adopt.SendAsync(new AdoptInput
    //     {
    //         Parent = Gen0,
    //         Amount = 2_00000000,
    //         Domain = "test"
    //     });
    //
    //     return GetLogEvent<Adopted>(result.TransactionResult).AdoptId;
    // }
}