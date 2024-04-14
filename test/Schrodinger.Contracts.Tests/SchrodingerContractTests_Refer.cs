using System.Threading.Tasks;
using AElf.Types;
using Shouldly;
using Xunit;

namespace Schrodinger;

public partial class SchrodingerContractTests
{
    [Fact]
    public async Task AcceptReferralTests()
    {
        await Initialize();
        
        await SchrodingerContractStub.Join.SendAsync(new JoinInput
        {
            Domain = "test"
        });
        
        var result = await UserSchrodingerContractStub.AcceptReferral.SendAsync(new AcceptReferralInput
        {
            Referrer = DefaultAddress
        });
        
        var log = GetLogEvent<ReferralAccepted>(result.TransactionResult);
        log.Referrer.ShouldBe(DefaultAddress);
        log.Invitee.ShouldBe(UserAddress);
    }
    
    [Fact]
    public async Task AcceptReferralTests_Fail()
    {
        await Initialize();
        
        var result = await UserSchrodingerContractStub.AcceptReferral.SendWithExceptionAsync(new AcceptReferralInput());
        result.TransactionResult.Error.ShouldContain("Invalid referrer.");
        
        result = await UserSchrodingerContractStub.AcceptReferral.SendWithExceptionAsync(new AcceptReferralInput
        {
            Referrer = new Address()
        });
        result.TransactionResult.Error.ShouldContain("Invalid referrer.");
        
        result = await UserSchrodingerContractStub.AcceptReferral.SendWithExceptionAsync(new AcceptReferralInput
        {
            Referrer = DefaultAddress
        });
        result.TransactionResult.Error.ShouldContain("Invalid referrer.");
        
        await SchrodingerContractStub.Join.SendAsync(new JoinInput
        {
            Domain = "test"
        });
        
        await UserSchrodingerContractStub.Join.SendAsync(new JoinInput
        {
            Domain = "test"
        });
        
        result = await UserSchrodingerContractStub.AcceptReferral.SendWithExceptionAsync(new AcceptReferralInput
        {
            Referrer = DefaultAddress
        });
        result.TransactionResult.Error.ShouldContain("Already joined.");
    }
}