using System.Threading.Tasks;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace Schrodinger;

public partial class SchrodingerContractTests
{
    private const string Gen0 = "SGR-1";

    // [Fact]
    // public async Task AdoptTests()
    // {
    //     Hash adoptId;
    //     string symbol;
    //     long amount;
    //
    //     await DeployTest();
    //     // await SetPointsProportion();
    //
    //     {
    //         var balance = await GetTokenBalance(Gen0, DefaultAddress);
    //         balance.ShouldBe(0);
    //     }
    //
    //     await TokenContractStub.Issue.SendAsync(new IssueInput
    //     {
    //         Symbol = Gen0,
    //         Amount = 2_00000000,
    //         To = DefaultAddress
    //     });
    //
    //     {
    //         var balance = await GetTokenBalance(Gen0, DefaultAddress);
    //         balance.ShouldBe(2_00000000);
    //     }
    //
    //     await TokenContractStub.Approve.SendAsync(new ApproveInput
    //     {
    //         Symbol = Gen0,
    //         Amount = 2_00000000,
    //         Spender = SchrodingerContractAddress
    //     });
    //
    //     {
    //         var result = await SchrodingerContractStub.Adopt.SendAsync(new AdoptInput
    //         {
    //             Parent = Gen0,
    //             Amount = 2_00000000,
    //             Domain = "test"
    //         });
    //
    //         var log = GetLogEvent<Adopted>(result.TransactionResult);
    //         log.Ancestor.ShouldBe(Gen0);
    //         log.Adopter.ShouldBe(DefaultAddress);
    //         log.InputAmount.ShouldBe(2_00000000);
    //         log.OutputAmount.ShouldBe(1_90000000);
    //         log.LossAmount.ShouldBe(9000000);
    //         log.CommissionAmount.ShouldBe(1000000);
    //
    //         adoptId = log.AdoptId;
    //         symbol = log.Symbol;
    //
    //         var receivingAddress = await SchrodingerContractStub.GetReceivingAddress.CallAsync(new StringValue
    //         {
    //             Value = _tick
    //         });
    //
    //         GetTokenBalance(Gen0, receivingAddress).Result.ShouldBe(log.LossAmount);
    //     }
    //
    //     {
    //         var confirmInput = new ConfirmInput
    //         {
    //             AdoptId = adoptId,
    //             Image = "test",
    //             ImageUri = "test"
    //         };
    //
    //         confirmInput.Signature = GenerateSignature(DefaultKeyPair.PrivateKey, confirmInput.AdoptId,
    //             confirmInput.Image, confirmInput.ImageUri);
    //
    //         var result = await SchrodingerContractStub.Confirm.SendAsync(confirmInput);
    //
    //         var log = GetLogEvent<Confirmed>(result.TransactionResult);
    //     }
    //
    //     {
    //         var balance = await GetTokenBalance(Gen0, DefaultAddress);
    //         balance.ShouldBe(1000000);
    //     }
    //     {
    //         var balance = await GetTokenBalance(symbol, DefaultAddress);
    //         balance.ShouldBe(1_90000000);
    //     }
    //
    //     {
    //         await TokenContractStub.Approve.SendAsync(new ApproveInput
    //         {
    //             Spender = SchrodingerContractAddress,
    //             Symbol = symbol,
    //             Amount = 1_90000000
    //         });
    //
    //         var result = await SchrodingerContractStub.Adopt.SendAsync(new AdoptInput
    //         {
    //             Parent = symbol,
    //             Amount = 1_90000000,
    //             Domain = "test"
    //         });
    //
    //         var log = GetLogEvent<Adopted>(result.TransactionResult);
    //
    //         adoptId = log.AdoptId;
    //         symbol = log.Symbol;
    //         amount = log.OutputAmount;
    //     }
    //
    //     {
    //         var confirmInput = new ConfirmInput
    //         {
    //             AdoptId = adoptId,
    //             Image = "test",
    //             ImageUri = "test"
    //         };
    //
    //         confirmInput.Signature = GenerateSignature(DefaultKeyPair.PrivateKey, confirmInput.AdoptId,
    //             confirmInput.Image, confirmInput.ImageUri);
    //
    //         var result = await SchrodingerContractStub.Confirm.SendAsync(confirmInput);
    //
    //         var log = GetLogEvent<Confirmed>(result.TransactionResult);
    //     }
    //
    //     {
    //         await TokenContractStub.Approve.SendAsync(new ApproveInput
    //         {
    //             Spender = SchrodingerContractAddress,
    //             Symbol = symbol,
    //             Amount = amount
    //         });
    //
    //         var result = await SchrodingerContractStub.Reroll.SendAsync(new RerollInput
    //         {
    //             Symbol = symbol,
    //             Amount = amount,
    //             Domain = "test"
    //         });
    //
    //         var log = GetLogEvent<Rerolled>(result.TransactionResult);
    //     }
    //
    //     {
    //         var balance = await GetTokenBalance(symbol, DefaultAddress);
    //         balance.ShouldBe(0);
    //     }
    // }

    [Fact]
    public async Task TransferFromReceivingAddressTests()
    {
        await Adopt();

        GetTokenBalance(Gen0, User2Address).Result.ShouldBe(0);

        var result = await UserSchrodingerContractStub.TransferFromReceivingAddress.SendWithExceptionAsync(
            new TransferFromReceivingAddressInput
            {
                Tick = _tick,
                Amount = 1,
                Recipient = User2Address
            });
        result.TransactionResult.Error.ShouldContain("No permission.");

        result = await SchrodingerContractStub.TransferFromReceivingAddress.SendAsync(
            new TransferFromReceivingAddressInput
            {
                Tick = _tick,
                Amount = 1,
                Recipient = User2Address
            });
        result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);

        GetTokenBalance(Gen0, User2Address).Result.ShouldBe(1);
    }

    [Fact]
    public async Task AdoptMaxGenTests()
    {
        await DeployForMaxGen();

        await TokenContractStub.Issue.SendAsync(new IssueInput
        {
            Symbol = Gen0,
            Amount = 2_00000000,
            To = DefaultAddress
        });

        await TokenContractStub.Approve.SendAsync(new ApproveInput
        {
            Symbol = Gen0,
            Amount = 2_00000000,
            Spender = SchrodingerContractAddress
        });

        var result = await SchrodingerContractStub.AdoptMaxGen.SendAsync(new AdoptMaxGenInput
        {
            Tick = "SGR",
            Amount = 2_00000000,
            Domain = "test"
        });

        var log = GetLogEvent<Adopted>(result.TransactionResult);
        log.Parent.ShouldBe(Gen0);
        log.ParentGen.ShouldBe(0);
        log.InputAmount.ShouldBe(2_00000000);
        log.OutputAmount.ShouldBe(1_00000000);
        log.Attributes.Data.Count.ShouldBe(11);
        log.Gen.ShouldBe(9);
        log.Ancestor.ShouldBe(Gen0);
        log.Symbol.ShouldBe("SGR-2");
        log.TokenName.ShouldBe("SGR-2GEN9");

        var adoptInfo = await SchrodingerContractStub.GetAdoptInfo.CallAsync(log.AdoptId);
        adoptInfo.Parent.ShouldBe(Gen0);
        adoptInfo.ParentGen.ShouldBe(0);
        adoptInfo.InputAmount.ShouldBe(2_00000000);
        adoptInfo.OutputAmount.ShouldBe(1_00000000);
        adoptInfo.Attributes.Data.Count.ShouldBe(11);
        adoptInfo.Gen.ShouldBe(9);
        adoptInfo.ParentAttributes.Data.Count.ShouldBe(0);
        adoptInfo.Symbol.ShouldBe("SGR-2");
        adoptInfo.TokenName.ShouldBe("SGR-2GEN9");

        await TokenContractStub.Issue.SendAsync(new IssueInput
        {
            Symbol = Gen0,
            Amount = 2_00000000,
            To = UserAddress
        });

        await TokenContractUserStub.Approve.SendAsync(new ApproveInput
        {
            Symbol = Gen0,
            Amount = 2_00000000,
            Spender = SchrodingerContractAddress
        });

        result = await UserSchrodingerContractStub.AdoptMaxGen.SendAsync(new AdoptMaxGenInput
        {
            Tick = "SGR",
            Amount = 2_00000000,
            Domain = "test"
        });
        result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);

        var output = await SchrodingerContractStub.GetSymbolCount.CallAsync(new StringValue { Value = _tick });
        output.Value.ShouldBe(4);
    }

    [Fact]
    public async Task AdoptMaxGenTests_Fail()
    {
        await DeployForMaxGen();

        await TokenContractStub.Issue.SendAsync(new IssueInput
        {
            Symbol = Gen0,
            Amount = 2_00000000,
            To = DefaultAddress
        });

        await TokenContractStub.Approve.SendAsync(new ApproveInput
        {
            Symbol = Gen0,
            Amount = 2_00000000,
            Spender = SchrodingerContractAddress
        });

        var result = await SchrodingerContractStub.AdoptMaxGen.SendWithExceptionAsync(new AdoptMaxGenInput());
        result.TransactionResult.Error.ShouldContain("Invalid tick.");

        result = await SchrodingerContractStub.AdoptMaxGen.SendWithExceptionAsync(new AdoptMaxGenInput
        {
            Tick = "test"
        });
        result.TransactionResult.Error.ShouldContain("Invalid amount.");

        result = await SchrodingerContractStub.AdoptMaxGen.SendWithExceptionAsync(new AdoptMaxGenInput
        {
            Tick = "test",
            Amount = -1
        });
        result.TransactionResult.Error.ShouldContain("Invalid amount.");

        result = await SchrodingerContractStub.AdoptMaxGen.SendWithExceptionAsync(new AdoptMaxGenInput
        {
            Tick = "test",
            Amount = 1
        });
        result.TransactionResult.Error.ShouldContain("Invalid domain.");

        result = await SchrodingerContractStub.AdoptMaxGen.SendWithExceptionAsync(new AdoptMaxGenInput
        {
            Tick = "test",
            Amount = 1,
            Domain = "test"
        });
        result.TransactionResult.Error.ShouldContain("Inscription not found.");

        result = await SchrodingerContractStub.AdoptMaxGen.SendWithExceptionAsync(new AdoptMaxGenInput
        {
            Tick = _tick,
            Amount = 1,
            Domain = "test"
        });
        result.TransactionResult.Error.ShouldContain("Input amount not enough.");
    }

    [Fact]
    public async Task RerollAdoptionTests()
    {
        await DeployForMaxGen();

        await TokenContractStub.Issue.SendAsync(new IssueInput
        {
            Symbol = Gen0,
            Amount = 2_00000000,
            To = UserAddress
        });

        await TokenContractUserStub.Approve.SendAsync(new ApproveInput
        {
            Symbol = Gen0,
            Amount = 2_00000000,
            Spender = SchrodingerContractAddress
        });

        var balance = await GetTokenBalance(Gen0, UserAddress);
        balance.ShouldBe(2_00000000);

        var result = await UserSchrodingerContractStub.AdoptMaxGen.SendAsync(new AdoptMaxGenInput
        {
            Tick = _tick,
            Amount = 2_00000000,
            Domain = "test"
        });
        var adoptId = GetLogEvent<Adopted>(result.TransactionResult).AdoptId;

        balance = await GetTokenBalance(Gen0, UserAddress);
        balance.ShouldBe(0);

        var adoptInfo = await UserSchrodingerContractStub.GetAdoptInfo.CallAsync(adoptId);
        adoptInfo.IsRerolled.ShouldBeFalse();

        result = await UserSchrodingerContractStub.RerollAdoption.SendAsync(adoptId);
        var log = GetLogEvent<AdoptionRerolled>(result.TransactionResult);

        log.AdoptId.ShouldBe(adoptId);
        log.Symbol.ShouldBe(Gen0);
        log.Amount.ShouldBe(adoptInfo.OutputAmount);
        log.Account.ShouldBe(UserAddress);

        adoptInfo = await UserSchrodingerContractStub.GetAdoptInfo.CallAsync(adoptId);
        adoptInfo.IsRerolled.ShouldBeTrue();

        balance = await GetTokenBalance(Gen0, UserAddress);
        balance.ShouldBe(log.Amount);
    }

    [Fact]
    public async Task RerollAdoptionTests_Fail()
    {
        await DeployForMaxGen();

        await TokenContractStub.Issue.SendAsync(new IssueInput
        {
            Symbol = Gen0,
            Amount = 6_00000000,
            To = UserAddress
        });

        await TokenContractUserStub.Approve.SendAsync(new ApproveInput
        {
            Symbol = Gen0,
            Amount = 6_00000000,
            Spender = SchrodingerContractAddress
        });

        var result = await UserSchrodingerContractStub.AdoptMaxGen.SendAsync(new AdoptMaxGenInput
        {
            Tick = _tick,
            Amount = 2_00000000,
            Domain = "test"
        });
        var adoptId = GetLogEvent<Adopted>(result.TransactionResult).AdoptId;

        result = await UserSchrodingerContractStub.RerollAdoption.SendWithExceptionAsync(new Hash());
        result.TransactionResult.Error.ShouldContain("Invalid input.");

        result = await UserSchrodingerContractStub.RerollAdoption
            .SendWithExceptionAsync(HashHelper.ComputeFrom("test"));
        result.TransactionResult.Error.ShouldContain("Adopt id not exists.");

        result = await SchrodingerContractStub.RerollAdoption.SendWithExceptionAsync(adoptId);
        result.TransactionResult.Error.ShouldContain("No permission.");

        await UserSchrodingerContractStub.Confirm.SendAsync(new ConfirmInput
        {
            AdoptId = adoptId,
            Image = "image",
            ImageUri = "uri",
            Signature = GenerateSignature(DefaultKeyPair.PrivateKey, adoptId, "image", "uri")
        });

        result = await UserSchrodingerContractStub.RerollAdoption.SendWithExceptionAsync(adoptId);
        result.TransactionResult.Error.ShouldContain("Already confirmed.");

        result = await UserSchrodingerContractStub.AdoptMaxGen.SendAsync(new AdoptMaxGenInput
        {
            Tick = _tick,
            Amount = 2_00000000,
            Domain = "test"
        });
        adoptId = GetLogEvent<Adopted>(result.TransactionResult).AdoptId;

        await UserSchrodingerContractStub.RerollAdoption.SendAsync(adoptId);

        result = await UserSchrodingerContractStub.RerollAdoption.SendWithExceptionAsync(adoptId);
        result.TransactionResult.Error.ShouldContain("Already rerolled.");

        result = await UserSchrodingerContractStub.AdoptMaxGen.SendAsync(new AdoptMaxGenInput
        {
            Tick = _tick,
            Amount = 2_00000000,
            Domain = "test"
        });
        adoptId = GetLogEvent<Adopted>(result.TransactionResult).AdoptId;

        await UserSchrodingerContractStub.RerollAdoption.SendAsync(adoptId);

        result = await UserSchrodingerContractStub.Confirm.SendWithExceptionAsync(new ConfirmInput
        {
            AdoptId = adoptId,
            Image = "image",
            ImageUri = "uri",
            Signature = GenerateSignature(DefaultKeyPair.PrivateKey, adoptId, "image", "uri")
        });
        result.TransactionResult.Error.ShouldContain("Already rerolled.");
    }

    // [Fact]
    // public async Task UpdateAdoptionTests()
    // {
    //     var adoptId = await Adopt();
    //     var adoptInfo = await SchrodingerContractStub.GetAdoptInfo.CallAsync(adoptId);
    //     adoptInfo.IsUpdated.ShouldBeFalse();
    //
    //     var result = await SchrodingerContractStub.UpdateAdoption.SendAsync(adoptId);
    //     result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
    //
    //     var log = GetLogEvent<AdoptionUpdated>(result.TransactionResult);
    //     log.Parent.ShouldBe(adoptInfo.Symbol);
    //     log.ParentGen.ShouldBe(adoptInfo.Gen);
    //     log.InputAmount.ShouldBe(adoptInfo.OutputAmount);
    //     log.LossAmount.ShouldBe(adoptInfo.OutputAmount * 5 / 100 * 90 / 100);
    //     log.OutputAmount.ShouldBe(adoptInfo.OutputAmount * 95 / 100);
    //     log.ImageCount.ShouldBe(adoptInfo.ImageCount);
    //     log.Adopter.ShouldBe(adoptInfo.Adopter);
    //     log.BlockHeight.ShouldBe(result.TransactionResult.BlockNumber);
    //     log.Attributes.Data.Count.ShouldBe(adoptInfo.Attributes.Data.Count + 2);
    //     log.Gen.ShouldBe(adoptInfo.Gen + 2);
    //     log.Ancestor.ShouldBe(Gen0);
    //     log.Symbol.ShouldBe("SGR-3");
    //     log.TokenName.ShouldBe("SGR-3GEN4");
    //
    //     var newAdoptInfo = await SchrodingerContractStub.GetAdoptInfo.CallAsync(log.AdoptId);
    //     newAdoptInfo.AdoptId.ShouldBe(log.AdoptId);
    //     newAdoptInfo.Parent.ShouldBe(log.Parent);
    //     newAdoptInfo.ParentGen.ShouldBe(log.ParentGen);
    //     newAdoptInfo.InputAmount.ShouldBe(log.InputAmount);
    //     newAdoptInfo.OutputAmount.ShouldBe(log.OutputAmount);
    //     newAdoptInfo.ParentAttributes.ShouldBe(adoptInfo.Attributes);
    //     newAdoptInfo.Attributes.ShouldBe(log.Attributes);
    //     newAdoptInfo.ImageCount.ShouldBe(log.ImageCount);
    //     newAdoptInfo.BlockHeight.ShouldBe(log.BlockHeight);
    //     newAdoptInfo.Symbol.ShouldBe(log.Symbol);
    //     newAdoptInfo.TokenName.ShouldBe(log.TokenName);
    //     newAdoptInfo.Gen.ShouldBe(log.Gen);
    //     newAdoptInfo.Adopter.ShouldBe(log.Adopter);
    //     newAdoptInfo.IsUpdated.ShouldBeFalse();
    //
    //     adoptInfo = await SchrodingerContractStub.GetAdoptInfo.CallAsync(adoptId);
    //     adoptInfo.IsUpdated.ShouldBeTrue();
    // }
    //
    // [Fact]
    // public async Task UpdateAdoptionTests_Fail()
    // {
    //     await DeployForMaxGen();
    //
    //     await TokenContractStub.Issue.SendAsync(new IssueInput
    //     {
    //         Symbol = Gen0,
    //         Amount = 6_00000000,
    //         To = UserAddress
    //     });
    //
    //     await TokenContractUserStub.Approve.SendAsync(new ApproveInput
    //     {
    //         Symbol = Gen0,
    //         Amount = 6_00000000,
    //         Spender = SchrodingerContractAddress
    //     });
    //
    //     var result = await UserSchrodingerContractStub.Adopt.SendAsync(new AdoptInput
    //     {
    //         Parent = Gen0,
    //         Amount = 2_00000000,
    //         Domain = "test"
    //     });
    //     var adoptId = GetLogEvent<Adopted>(result.TransactionResult).AdoptId;
    //
    //     result = await UserSchrodingerContractStub.UpdateAdoption.SendWithExceptionAsync(new Hash());
    //     result.TransactionResult.Error.ShouldContain("Invalid input.");
    //
    //     result = await UserSchrodingerContractStub.UpdateAdoption
    //         .SendWithExceptionAsync(HashHelper.ComputeFrom("test"));
    //     result.TransactionResult.Error.ShouldContain("Adopt id not exists.");
    //
    //     result = await SchrodingerContractStub.UpdateAdoption.SendWithExceptionAsync(adoptId);
    //     result.TransactionResult.Error.ShouldContain("No permission.");
    //
    //     await UserSchrodingerContractStub.Confirm.SendAsync(new ConfirmInput
    //     {
    //         AdoptId = adoptId,
    //         Image = "image",
    //         ImageUri = "uri",
    //         Signature = GenerateSignature(DefaultKeyPair.PrivateKey, adoptId, "image", "uri")
    //     });
    //
    //     result = await UserSchrodingerContractStub.UpdateAdoption.SendWithExceptionAsync(adoptId);
    //     result.TransactionResult.Error.ShouldContain("Already confirmed.");
    //
    //     result = await UserSchrodingerContractStub.Adopt.SendAsync(new AdoptInput
    //     {
    //         Parent = Gen0,
    //         Amount = 2_00000000,
    //         Domain = "test"
    //     });
    //     adoptId = GetLogEvent<Adopted>(result.TransactionResult).AdoptId;
    //
    //     await UserSchrodingerContractStub.RerollAdoption.SendAsync(adoptId);
    //
    //     result = await UserSchrodingerContractStub.UpdateAdoption.SendWithExceptionAsync(adoptId);
    //     result.TransactionResult.Error.ShouldContain("Already rerolled.");
    //
    //     result = await UserSchrodingerContractStub.Adopt.SendAsync(new AdoptInput
    //     {
    //         Parent = Gen0,
    //         Amount = 2_00000000,
    //         Domain = "test"
    //     });
    //     adoptId = GetLogEvent<Adopted>(result.TransactionResult).AdoptId;
    //
    //     await UserSchrodingerContractStub.UpdateAdoption.SendAsync(adoptId);
    //
    //     result = await UserSchrodingerContractStub.UpdateAdoption.SendWithExceptionAsync(adoptId);
    //     result.TransactionResult.Error.ShouldContain("Already updated.");
    // }

    [Fact]
    public async Task RerollTests_WithRerollConfig()
    {
        await DeployForMaxGen();

        await TokenContractStub.Issue.SendAsync(new IssueInput
        {
            Symbol = Gen0,
            Amount = 4_00000000,
            To = UserAddress
        });

        var balance = await GetTokenBalance($"{_tick}-1", UserAddress);
        balance.ShouldBe(4_00000000);

        await TokenContractUserStub.Approve.SendAsync(new ApproveInput
        {
            Symbol = Gen0,
            Amount = 4_00000000,
            Spender = SchrodingerContractAddress
        });

        var result = await UserSchrodingerContractStub.AdoptMaxGen.SendAsync(new AdoptMaxGenInput
        {
            Amount = 2_00000000,
            Domain = "test",
            Tick = _tick
        });
        var adoptId = GetLogEvent<Adopted>(result.TransactionResult).AdoptId;
        await UserSchrodingerContractStub.Confirm.SendAsync(new ConfirmInput
        {
            AdoptId = adoptId,
            Image = "image",
            ImageUri = "uri",
            Signature = GenerateSignature(DefaultKeyPair.PrivateKey, adoptId, "image", "uri")
        });

        result = await UserSchrodingerContractStub.AdoptMaxGen.SendAsync(new AdoptMaxGenInput
        {
            Amount = 2_00000000,
            Domain = "test",
            Tick = _tick
        });
        var adoptId2 = GetLogEvent<Adopted>(result.TransactionResult).AdoptId;

        await UserSchrodingerContractStub.Confirm.SendAsync(new ConfirmInput
        {
            AdoptId = adoptId2,
            Image = "image",
            ImageUri = "uri",
            Signature = GenerateSignature(DefaultKeyPair.PrivateKey, adoptId2, "image", "uri")
        });

        balance = await GetTokenBalance($"{_tick}-1", UserAddress);
        balance.ShouldBe(0);

        balance = await GetTokenBalance($"{_tick}-2", UserAddress);
        balance.ShouldBe(1_00000000);

        balance = await GetTokenBalance($"{_tick}-3", UserAddress);
        balance.ShouldBe(1_00000000);

        await TokenContractUserStub.Approve.SendAsync(new ApproveInput
        {
            Symbol = $"{_tick}-2",
            Spender = SchrodingerContractAddress,
            Amount = 1_00000000
        });

        await TokenContractUserStub.Approve.SendAsync(new ApproveInput
        {
            Symbol = $"{_tick}-3",
            Spender = SchrodingerContractAddress,
            Amount = 1_00000000
        });

        await SchrodingerContractStub.SetRerollConfig.SendAsync(new SetRerollConfigInput
        {
            Tick = _tick,
            Index = 3,
            Rate = 5000
        });

        await UserSchrodingerContractStub.Reroll.SendAsync(new RerollInput
        {
            Amount = 1_00000000,
            Symbol = $"{_tick}-2",
            Domain = "test"
        });

        balance = await GetTokenBalance($"{_tick}-1", UserAddress);
        balance.ShouldBe(1_00000000);

        await UserSchrodingerContractStub.Reroll.SendAsync(new RerollInput
        {
            Amount = 1_00000000,
            Symbol = $"{_tick}-3",
            Domain = "test"
        });

        balance = await GetTokenBalance($"{_tick}-1", UserAddress);
        balance.ShouldBe(1_50000000);
    }

    [Fact]
    public async Task RerollAdoptionTests_WithRerollConfig()
    {
        await DeployForMaxGen();

        await TokenContractStub.Issue.SendAsync(new IssueInput
        {
            Symbol = Gen0,
            Amount = 4_00000000,
            To = UserAddress
        });

        var balance = await GetTokenBalance($"{_tick}-1", UserAddress);
        balance.ShouldBe(4_00000000);

        await TokenContractUserStub.Approve.SendAsync(new ApproveInput
        {
            Symbol = Gen0,
            Amount = 4_00000000,
            Spender = SchrodingerContractAddress
        });

        var result = await UserSchrodingerContractStub.AdoptMaxGen.SendAsync(new AdoptMaxGenInput
        {
            Amount = 2_00000000,
            Domain = "test",
            Tick = _tick
        });
        var adoptId = GetLogEvent<Adopted>(result.TransactionResult).AdoptId;

        result = await UserSchrodingerContractStub.AdoptMaxGen.SendAsync(new AdoptMaxGenInput
        {
            Amount = 2_00000000,
            Domain = "test",
            Tick = _tick
        });
        var adoptId2 = GetLogEvent<Adopted>(result.TransactionResult).AdoptId;

        balance = await GetTokenBalance($"{_tick}-1", UserAddress);
        balance.ShouldBe(0);

        await SchrodingerContractStub.SetRerollConfig.SendAsync(new SetRerollConfigInput
        {
            Tick = _tick,
            Index = 3,
            Rate = 5000
        });

        await UserSchrodingerContractStub.RerollAdoption.SendAsync(adoptId);

        balance = await GetTokenBalance($"{_tick}-1", UserAddress);
        balance.ShouldBe(1_00000000);

        await UserSchrodingerContractStub.RerollAdoption.SendAsync(adoptId2);

        balance = await GetTokenBalance($"{_tick}-1", UserAddress);
        balance.ShouldBe(1_50000000);
    }
    
    [Fact]
    public async Task AdoptMaxGenTests_Voucher()
    {
        await DeployForMaxGen();

        await SchrodingerContractStub.SetVoucherAdoptionConfig.SendAsync(new SetVoucherAdoptionConfigInput
        {
            Tick = _tick,
            VoucherAmount = 5
        });

        var output = await SchrodingerContractStub.GetAdoptionVoucherAmount.CallAsync(new GetAdoptionVoucherAmountInput
        {
            Tick = _tick,
            Account = DefaultAddress
        });
        output.Value.ShouldBe(0);
        
        await AdoptMaxGen();
        
        output = await SchrodingerContractStub.GetAdoptionVoucherAmount.CallAsync(new GetAdoptionVoucherAmountInput
        {
            Tick = _tick,
            Account = DefaultAddress
        });
        output.Value.ShouldBe(5);
    }

    private async Task DeployForMaxGen()
    {
        await DeployCollectionTest();
        await Initialize();
        await SchrodingerContractStub.Deploy.SendAsync(new DeployInput
        {
            Tick = _tick,
            AttributesPerGen = 1,
            MaxGeneration = 9,
            ImageCount = 2,
            Decimals = 8,
            CommissionRate = 1000,
            LossRate = 500,
            AttributeLists = GetAttributeListsForMaxGen(),
            Image = _image,
            IsWeightEnabled = true,
            TotalSupply = 21000000_00000000,
            CrossGenerationConfig = new CrossGenerationConfig
            {
                Gen = 2,
                CrossGenerationProbability = 10000,
                IsWeightEnabled = true,
                Weights = { 10, 10 },
                CrossGenerationFixed = false
            },
            Signatory = DefaultAddress,
            ImageUri = "uri",
            MaxGenLossRate = 5000
        });
    }

    private AttributeLists GetAttributeListsForMaxGen()
    {
        return new AttributeLists
        {
            FixedAttributes = { GenerateAttributeSet(1), GenerateAttributeSet(2), GenerateAttributeSet(3) },
            RandomAttributes =
            {
                GenerateAttributeSet(4), GenerateAttributeSet(5), GenerateAttributeSet(6), GenerateAttributeSet(7),
                GenerateAttributeSet(8), GenerateAttributeSet(9), GenerateAttributeSet(10), GenerateAttributeSet(11),
                GenerateAttributeSet(12), GenerateAttributeSet(13), GenerateAttributeSet(14), GenerateAttributeSet(15)
            }
        };
    }

    private AttributeSet GenerateAttributeSet(int num)
    {
        var result = new AttributeSet
        {
            TraitType = new AttributeInfo
            {
                Name = $"T{num.ToString()}",
                Weight = num
            },
            Values = new AttributeInfos()
        };

        for (var i = 0; i < 3; i++)
        {
            var info = new AttributeInfo
            {
                Name = $"{result.TraitType.Name}V{(i + 1).ToString()}",
                Weight = i + 1
            };

            result.Values.Data.Add(info);
        }

        return result;
    }

    private async Task<Hash> Adopt()
    {
        await DeployTest();

        await TokenContractStub.Issue.SendAsync(new IssueInput
        {
            Symbol = Gen0,
            Amount = 2_00000000,
            To = DefaultAddress
        });

        await TokenContractStub.Approve.SendAsync(new ApproveInput
        {
            Symbol = Gen0,
            Amount = 2_00000000,
            Spender = SchrodingerContractAddress
        });

        var result = await SchrodingerContractStub.AdoptMaxGen.SendAsync(new AdoptMaxGenInput
        {
            Tick = _tick,
            Amount = 2_00000000,
            Domain = "test"
        });

        return GetLogEvent<Adopted>(result.TransactionResult).AdoptId;
    }
}