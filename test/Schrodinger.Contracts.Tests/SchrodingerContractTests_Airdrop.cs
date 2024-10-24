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
}