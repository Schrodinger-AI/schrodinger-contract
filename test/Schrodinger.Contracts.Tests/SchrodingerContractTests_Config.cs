using System.Threading.Tasks;
using AElf;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace Schrodinger;

public partial class SchrodingerContractTests
{
    [Fact]
    public async Task ConfigTests()
    {
        await DeployTest();

        await SchrodingerContractStub.SetConfig.SendAsync(new Config
        {
            MaxGen = 1,
            ImageMaxSize = 1,
            ImageMaxCount = 1,
            ImageUriMaxSize = 1,
            AttributeMaxLength = 1,
            MaxAttributesPerGen = 1,
            TraitTypeMaxCount = 1,
            TraitValueMaxCount = 1,
            FixedTraitTypeMaxCount = 1
        });

        await SchrodingerContractStub.SetMaxGenerationConfig.SendAsync(new Int32Value
        {
            Value = 2
        });
        
        await SchrodingerContractStub.SetImageMaxSize.SendAsync(new Int64Value
        {
            Value = 2
        });
        
        await SchrodingerContractStub.SetImageMaxCount.SendAsync(new Int64Value
        {
            Value = 2
        });

        await SchrodingerContractStub.SetAttributeConfig.SendAsync(new SetAttributeConfigInput
        {
            MaxAttributesPerGen = 5,
            AttributeMaxLength = 2,
            TraitTypeMaxCount = 2,
            TraitValueMaxCount = 2,
            FixedTraitTypeMaxCount = 2
        });

        await SchrodingerContractStub.SetImageUriMaxSize.SendAsync(new Int64Value
        {
            Value = 2
        });

        var output = await SchrodingerContractStub.GetImageUriMaxSize.CallAsync(new Empty());
        output.Value.ShouldBe(2);

        await SchrodingerContractStub.SetAdmin.SendAsync(UserAddress);
        var admin = await SchrodingerContractStub.GetAdmin.CallAsync(new Empty());
        admin.ShouldBe(UserAddress);

        var pointsContractDAppId = await SchrodingerContractStub.GetPointsContractDAppId.CallAsync(new Empty());
        pointsContractDAppId.ShouldBe(HashHelper.ComputeFrom("PointsContractDappId"));

        var pointsContract = await SchrodingerContractStub.GetPointsContract.CallAsync(new Empty());
        pointsContract.ShouldBe(TestPointsContractAddress);

        var config = await SchrodingerContractStub.GetConfig.CallAsync(new Empty());
        config.AttributeMaxLength.ShouldBe(2);

        var signatory = await SchrodingerContractStub.GetSignatory.CallAsync(new StringValue
        {
            Value = _tick
        });
        signatory.ShouldBe(DefaultAddress);

        var tick = await SchrodingerContractStub.GetTick.CallAsync(new StringValue
        {
            Value = Gen0
        });
        tick.Value.ShouldBe(_tick);

        var parent = await SchrodingerContractStub.GetParent.CallAsync(new StringValue
        {
            Value = "SGR-2"
        });
        parent.Value.ShouldBe("");

        var tokenInfo = await SchrodingerContractStub.GetTokenInfo.CallAsync(new StringValue
        {
            Value = Gen0
        });
        tokenInfo.Gen.ShouldBe(0);

        await SchrodingerContractStub.SetImageCount.SendAsync(new SetImageCountInput
        {
            Tick = _tick,
            ImageCount = 1
        });

        await SchrodingerContractStub.SetMaxGeneration.SendAsync(new SetMaxGenerationInput
        {
            Tick = _tick,
            Gen = 2
        });

        await SchrodingerContractStub.SetRecipient.SendAsync(new SetRecipientInput
        {
            Tick = _tick,
            Recipient = UserAddress
        });

        await SchrodingerContractStub.SetSignatory.SendAsync(new SetSignatoryInput
        {
            Tick = _tick,
            Signatory = UserAddress
        });

        await SchrodingerContractStub.SetAttributesPerGen.SendAsync(new SetAttributesPerGenInput
        {
            Tick = _tick,
            AttributesPerGen = 2
        });

        await SchrodingerContractStub.SetCrossGenerationConfig.SendAsync(new SetCrossGenerationConfigInput
        {
            Tick = _tick,
            Config = new CrossGenerationConfig
            {
                CrossGenerationProbability = 10000,
                CrossGenerationFixed = false,
                IsWeightEnabled = true,
                Gen = 2,
                Weights = { 100, 200 }
            }
        });

        await SchrodingerContractStub.SetInscriptionAdmin.SendAsync(new SetInscriptionAdminInput
        {
            Tick = _tick,
            Admin = UserAddress
        });

        await SchrodingerMainContractStub.SetImageMaxSize.SendAsync(new Int64Value
        {
            Value = 100
        });
        SchrodingerMainContractStub.GetImageMaxSize.CallAsync(new Empty()).Result.Value.ShouldBe(100);
        
        await SchrodingerMainContractStub.SetSchrodingerContractAddress.SendAsync(UserAddress);
        SchrodingerMainContractStub.GetSchrodingerContractAddress.CallAsync(new Empty()).Result.ShouldBe(UserAddress);
        
        await SchrodingerMainContractStub.SetAdmin.SendAsync(UserAddress);
        SchrodingerMainContractStub.GetAdmin.CallAsync(new Empty()).Result.ShouldBe(UserAddress);
    }
}