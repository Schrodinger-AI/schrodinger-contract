using System.Threading.Tasks;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace Schrodinger;

public partial class SchrodingerContractTests : SchrodingerContractTestBase
{
    [Fact]
    public async Task SetOfficialDomainAliasTests()
    {
        await DeployTest();

        var result = await SchrodingerContractStub.SetOfficialDomainAlias.SendAsync(new SetOfficialDomainAliasInput
        {
            Alias = "test"
        });
        result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);

        var log = GetLogEvent<OfficialDomainAliasSet>(result.TransactionResult);
        log.Alias.ShouldBe("test");

        var output = await SchrodingerContractStub.GetOfficialDomainAlias.CallAsync(new Empty());
        output.Value.ShouldBe("test");
    }
}