using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace Schrodinger;

public partial class SchrodingerContractTests
{
    [Fact]
    public async Task SetPointsProportionTests()
    {
        await DeployCollectionTest();
        await Initialize();
        await SchrodingerContractStub.SetPointsProportionList.SendAsync(new SetPointsProportionListInput
        {
            Data =
            {
                new PointsProportion
                {
                    ActionName = "Adopt",
                    Proportion = 131400000000
                },
                new PointsProportion
                {
                    ActionName = "Reroll",
                    Proportion = 191900000000
                }
            }
        });
        var proportion = await SchrodingerContractStub.GetPointsProportion.CallAsync(new StringValue
        {
            Value = "Adopt"
        });
        proportion.Value.ShouldBe(131400000000);
        var proportion1 = await SchrodingerContractStub.GetPointsProportion.CallAsync(new StringValue
        {
            Value = "Reroll"
        });
        proportion1.Value.ShouldBe(191900000000);
    }

    // [Fact] public async Task SetPointsSettle
}