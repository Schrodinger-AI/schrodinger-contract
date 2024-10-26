using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace Schrodinger;

public partial class SchrodingerContractTests
{
    [Fact]
    public async Task SetAirdropAdminTests()
    {
        await DeployTest();

        var admin = await SchrodingerContractStub.GetAirdropAdmin.CallAsync(new StringValue
        {
            Value = _tick
        });
        admin.ShouldBe(new Address());

        var result = await SchrodingerContractStub.SetAirdropAdmin.SendAsync(new SetAirdropAdminInput
        {
            Tick = _tick,
            Admin = UserAddress
        });
        result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);

        var log = GetLogEvent<AirdropAdminSet>(result.TransactionResult);
        log.Tick.ShouldBe(_tick);
        log.Admin.ShouldBe(UserAddress);

        admin = await SchrodingerContractStub.GetAirdropAdmin.CallAsync(new StringValue
        {
            Value = _tick
        });
        admin.ShouldBe(UserAddress);
    }

    [Fact]
    public async Task SetAirdropAdminTests_Fail()
    {
        await DeployTest();
        
        var result = await SchrodingerContractStub.SetAirdropAdmin.SendWithExceptionAsync(new SetAirdropAdminInput());
        result.TransactionResult.Error.ShouldContain("Invalid tick.");
        
        result = await SchrodingerContractStub.SetAirdropAdmin.SendWithExceptionAsync(new SetAirdropAdminInput
        {
            Tick = "test"
        });
        result.TransactionResult.Error.ShouldContain("Invalid admin.");
        
        result = await SchrodingerContractStub.SetAirdropAdmin.SendWithExceptionAsync(new SetAirdropAdminInput
        {
            Tick = "test",
            Admin = UserAddress
        });
        result.TransactionResult.Error.ShouldContain("Inscription not found.");
        
        result = await UserSchrodingerContractStub.SetAirdropAdmin.SendWithExceptionAsync(new SetAirdropAdminInput
        {
            Tick = _tick,
            Admin = UserAddress
        });
        result.TransactionResult.Error.ShouldContain("No permission.");
        
    }

    [Fact]
    public async Task AirdropVoucherTests()
    {
        await SetAirdropAdminTests();

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
        await SetAirdropAdminTests();
        
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
            List = {  }
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
        result.TransactionResult.Error.ShouldContain("Tick not deployed.");
        
        result = await UserSchrodingerContractStub.AirdropVoucher.SendWithExceptionAsync(new AirdropVoucherInput
        {
            Tick = _tick,
            List = { new Address() },
            Amount = 1
        });
        result.TransactionResult.Error.ShouldContain("Invalid address.");
    }
}