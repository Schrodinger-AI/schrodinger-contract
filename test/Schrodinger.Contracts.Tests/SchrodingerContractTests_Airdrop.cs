using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace Schrodinger;

public partial class SchrodingerContractTests
{
    [Fact]
    public async Task AddAirdropControllerTests()
    {
        await DeployTest();

        var list = await SchrodingerContractStub.GetAirdropController.CallAsync(new StringValue
        {
            Value = _tick
        });
        list.Data.Count.ShouldBe(0);

        var result = await SchrodingerContractStub.AddAirdropController.SendAsync(new AddAirdropControllerInput
        {
            Tick = _tick,
            List = { UserAddress, UserAddress }
        });
        result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);

        var log = GetLogEvent<AirdropControllerAdded>(result.TransactionResult);
        log.Tick.ShouldBe(_tick);
        log.Addresses.Data.Count.ShouldBe(1);
        log.Addresses.Data.First().ShouldBe(UserAddress);

        list = await SchrodingerContractStub.GetAirdropController.CallAsync(new StringValue
        {
            Value = _tick
        });
        list.Data.Count.ShouldBe(1);
        list.Data.First().ShouldBe(UserAddress);
    }

    [Fact]
    public async Task AddAirdropControllerTests_Fail()
    {
        await DeployTest();

        var result = await SchrodingerContractStub.AddAirdropController.SendWithExceptionAsync(new AddAirdropControllerInput());
        result.TransactionResult.Error.ShouldContain("Invalid tick.");
        
        result = await SchrodingerContractStub.AddAirdropController.SendWithExceptionAsync(new AddAirdropControllerInput
        {
            Tick = "test"
        });
        result.TransactionResult.Error.ShouldContain("Invalid list.");
        
        result = await SchrodingerContractStub.AddAirdropController.SendWithExceptionAsync(new AddAirdropControllerInput
        {
            Tick = "test",
            List = { new Address() }
        });
        result.TransactionResult.Error.ShouldContain("Inscription not found.");
        
        result = await SchrodingerContractStub.AddAirdropController.SendWithExceptionAsync(new AddAirdropControllerInput
        {
            Tick = _tick,
            List = { new Address() }
        });
        result.TransactionResult.Error.ShouldContain("Invalid address.");
        
        result = await UserSchrodingerContractStub.AddAirdropController.SendWithExceptionAsync(new AddAirdropControllerInput
        {
            Tick = _tick,
            List = { UserAddress }
        });
        result.TransactionResult.Error.ShouldContain("No permission.");
    }

    [Fact]
    public async Task RemoveAirdropControllerTests()
    {
        await AddAirdropControllerTests();

        var list = await SchrodingerContractStub.GetAirdropController.CallAsync(new StringValue
        {
            Value = _tick
        });
        list.Data.Count.ShouldBe(1);

        var result = await SchrodingerContractStub.RemoveAirdropController.SendAsync(new RemoveAirdropControllerInput
        {
            Tick = _tick,
            List = { UserAddress, UserAddress }
        });
        result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);

        var log = GetLogEvent<AirdropControllerRemoved>(result.TransactionResult);
        log.Tick.ShouldBe(_tick);
        log.Addresses.Data.Count.ShouldBe(1);
        log.Addresses.Data.First().ShouldBe(UserAddress);

        list = await SchrodingerContractStub.GetAirdropController.CallAsync(new StringValue
        {
            Value = _tick
        });
        list.Data.Count.ShouldBe(0);
    }
    
    [Fact]
    public async Task RemoveAirdropControllerTests_Fail()
    {
        await AddAirdropControllerTests();

        var result = await SchrodingerContractStub.RemoveAirdropController.SendWithExceptionAsync(new RemoveAirdropControllerInput());
        result.TransactionResult.Error.ShouldContain("Invalid tick.");
        
        result = await SchrodingerContractStub.RemoveAirdropController.SendWithExceptionAsync(new RemoveAirdropControllerInput
        {
            Tick = "test"
        });
        result.TransactionResult.Error.ShouldContain("Invalid list.");
        
        result = await SchrodingerContractStub.RemoveAirdropController.SendWithExceptionAsync(new RemoveAirdropControllerInput
        {
            Tick = "test",
            List = { new Address() }
        });
        result.TransactionResult.Error.ShouldContain("Inscription not found.");
        
        result = await SchrodingerContractStub.RemoveAirdropController.SendWithExceptionAsync(new RemoveAirdropControllerInput
        {
            Tick = _tick,
            List = { new Address() }
        });
        result.TransactionResult.Error.ShouldContain("Invalid address.");
        
        result = await UserSchrodingerContractStub.RemoveAirdropController.SendWithExceptionAsync(new RemoveAirdropControllerInput
        {
            Tick = _tick,
            List = { UserAddress }
        });
        result.TransactionResult.Error.ShouldContain("No permission.");
    }

    [Fact]
    public async Task AirdropVoucherTests()
    {
        await AddAirdropControllerTests();

        var amount = await SchrodingerContractStub.GetAdoptionVoucherAmount.CallAsync(new GetAdoptionVoucherAmountInput
        {
            Tick = _tick,
            Account = DefaultAddress
        });
        amount.Value.ShouldBe(0);

        var result = await UserSchrodingerContractStub.AirdropVoucher.SendAsync(new AirdropVoucherInput
        {
            Tick = _tick,
            Amount = 1,
            List = { DefaultAddress }
        });
        result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);

        var log = GetLogEvent<VoucherAirdropped>(result.TransactionResult);
        log.Tick.ShouldBe(_tick);
        log.Amount.ShouldBe(1);
        log.List.Data.ShouldBe(new List<Address> { DefaultAddress });

        amount = await SchrodingerContractStub.GetAdoptionVoucherAmount.CallAsync(new GetAdoptionVoucherAmountInput
        {
            Tick = _tick,
            Account = DefaultAddress
        });
        amount.Value.ShouldBe(1);
    }

    [Fact]
    public async Task AirdropVoucherTests_Fail()
    {
        await AddAirdropControllerTests();

        var result = await UserSchrodingerContractStub.AirdropVoucher.SendWithExceptionAsync(new AirdropVoucherInput());
        result.TransactionResult.Error.ShouldContain("Invalid tick.");

        result = await UserSchrodingerContractStub.AirdropVoucher.SendWithExceptionAsync(new AirdropVoucherInput
        {
            Tick = "test"
        });
        result.TransactionResult.Error.ShouldContain("Invalid list.");

        result = await UserSchrodingerContractStub.AirdropVoucher.SendWithExceptionAsync(new AirdropVoucherInput
        {
            Tick = "test",
            List = { }
        });
        result.TransactionResult.Error.ShouldContain("Invalid list.");

        result = await UserSchrodingerContractStub.AirdropVoucher.SendWithExceptionAsync(new AirdropVoucherInput
        {
            Tick = "test",
            List = { new Address() }
        });
        result.TransactionResult.Error.ShouldContain("Invalid amount.");

        result = await UserSchrodingerContractStub.AirdropVoucher.SendWithExceptionAsync(new AirdropVoucherInput
        {
            Tick = "test",
            List = { new Address() },
            Amount = 1
        });
        result.TransactionResult.Error.ShouldContain("Inscription not found.");

        result = await UserSchrodingerContractStub.AirdropVoucher.SendWithExceptionAsync(new AirdropVoucherInput
        {
            Tick = _tick,
            List = { new Address() },
            Amount = 1
        });
        result.TransactionResult.Error.ShouldContain("Invalid address.");
    }
}