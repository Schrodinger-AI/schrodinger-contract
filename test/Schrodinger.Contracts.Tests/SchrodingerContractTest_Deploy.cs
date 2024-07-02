using System.Collections.Generic;
using System.Threading.Tasks;
using AElf;
using AElf.Contracts.MultiToken;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace Schrodinger;

public partial class SchrodingerContractTests
{
    private readonly string _image =
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAACgAAAAoCAYAAACM/rhtAAADfklEQVR4nO2Yy2sTQRzHv7t5lTa2HtRjaGhJpfRSkFAPtiJBL3rz0iBWkC6ilLTE+ijYg0p9hlaLoCko9pDc/Av2UFvBEq+12lgIBk9WkEJqk5rselh3O9nsYybbood+IWTnN7uzn/nOY3+7XCopyfiPxf9rADvtATrVHqBTuZ1cLAx5tePk9JZjGCPVDSgMedEvFgAA6Yif6nxVLJ2pC5CEm569hTQFWM/EMoLhAFVnHAEKQ170TCxrcLRg9coNAJGpkBYQh7PaMRkn64xuSA6hEZjaGSu3DQH1EPqyHsLMPTWuanGsE4sA+sWCpdO2gKwXGLn34/wIDox1avMSAIJiAblMHumIH2n1nNlJ5tXOfe9slw9eX9cCaw9atGMy7rqwhovzRabGSb051YRShWMGdJMQeihV+4SfOPE4WxNnUfedT3g/1sV8HdWTpFAyHloWBcMBHJ1YQlRgm1XK2b2cUprXpYa9HHxta4gkVhzBqaqnk5ycO2SZsPra1jDwdpOp0VwmXwVElnOZPMT4YaSSZaq2diVZGAicRv75yaqyGA9pkE1ejrotHgDGW4sYb61doUYxGp05m7Usb2zRv2W4SYh6gXZTOzLEuUxem2cAMPquiL6XnwEAsW4fvm3IWCpsu9bc6KFu2w0AT6aUvS823FJVqcTt+2C0OmPdPgBA+8i17djkQwDAK5YhVuFIUFIuSXHIbIvQr1gSzkzlMt0KBijs8UxKmB/tMK0PhgOWcKt/XVP/WcUDwNWnyk8vMk7OMb30c7DL76qqb5hJaMdHml3UeyAAcLeDMtWEuPuVR++jFdungeogCblUqAAAhl98YdqkmQDrgdSry8/mHgBw6reZ1Xu1u3v7Tbnq4d7i9WCzrKRLdqAkZD3O1QCaSZ99qDe5HHPhV8kYlJyPH26EsP4bSCUlZjgqQAA4d8kNiWifdELtgJeXDZMKJTkwf42wE1VyJll0vgr2OI/BuerHZTAcwOBC0XC/pBGVg4D5UJMauOJD3/2Ptps6i5jSWy/nwpZcMa1//ayEqMBjcME46QiGA0hF9mOjRJ+UUDsI0LkYFXhEElnHrwiqHH08MlIqKVm6yDrMTA4CO+PizLEG6vvtuIPAtotmkOqqFuMh2/2ROWHVO2b2GplKSpiLd5gOqbr9RAVrBMcZdaPHHHI2WYEYD1nOu0giawn5B8F9gRyqFJDiAAAAAElFTkSuQmCC";

    private readonly string _tick = "SGR";
    private readonly string _tokenName = "Schrödinger";
    private readonly int _mainChainId = 9992731;


    private async Task InitializeSchrodingerMain()
    {
        await SchrodingerMainContractStub.Initialize.SendAsync(new SchrodingerMain.InitializeInput
        {
            Admin = DefaultAddress,
            ImageMaxSize = 10240,
            SchrodingerContractAddress = SchrodingerContractAddress
        });
    }

    private async Task Initialize()
    {
        await SchrodingerContractStub.Initialize.SendAsync(new InitializeInput
        {
            Admin = DefaultAddress,
            PointsContract = TestPointsContractAddress,
            PointsContractDappId = HashHelper.ComputeFrom("PointsContractDappId"),
            MaxGen = 10,
            ImageMaxSize = 10240,
            ImageMaxCount = 2,
            TraitTypeMaxCount = 50,
            TraitValueMaxCount = 100,
            AttributeMaxLength = 80,
            MaxAttributesPerGen = 5,
            FixedTraitTypeMaxCount = 5,
            ImageUriMaxSize = 64
        });

        await SchrodingerContractStub.SetConfig.SendAsync(new Config
        {
            MaxGen = 10,
            ImageMaxSize = 10240,
            ImageMaxCount = 2,
            TraitTypeMaxCount = 50,
            TraitValueMaxCount = 100,
            AttributeMaxLength = 80,
            MaxAttributesPerGen = 5,
            FixedTraitTypeMaxCount = 5,
            ImageUriMaxSize = 64
        });

        await SchrodingerContractStub.SetPointsContract.SendAsync(TestPointsContractAddress);
        await SchrodingerContractStub.SetPointsContractDAppId.SendAsync(HashHelper.ComputeFrom("PointsContractDappId"));
    }

    [Fact]
    public async Task DeployCollectionTest()
    {
        await InitializeSchrodingerMain();
        await BuySeed();

        await SchrodingerMainContractStub.Deploy.SendAsync(new SchrodingerMain.DeployInput
        {
            Tick = _tick,
            Image = _image,
            SeedSymbol = "SEED-1",
            TokenName = _tokenName,
            Decimals = 0,
            IssueChainId = _mainChainId,
            ImageUri = "uri"
        });

        var tokenInfo = await TokenContractStub.GetTokenInfo.CallAsync(new GetTokenInfoInput
        {
            Symbol = $"{_tick}-0"
        });
        tokenInfo.Symbol.ShouldBe($"{_tick}-0");
        tokenInfo.Owner.ShouldBe(SchrodingerContractAddress);
    }

    [Fact]
    public async Task DeployTest()
    {
        await DeployCollectionTest();
        await Initialize();
        var result = await SchrodingerContractStub.Deploy.SendAsync(new DeployInput()
        {
            Tick = _tick,
            AttributesPerGen = 1,
            MaxGeneration = 4,
            ImageCount = 2,
            Decimals = 0,
            CommissionRate = 1000,
            LossRate = 500,
            AttributeLists = GetAttributeLists(),
            Image = _image,
            IsWeightEnabled = true,
            TotalSupply = 21000000,
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
        var log = GetLogEvent<Deployed>(result.TransactionResult);
        var inscription = await SchrodingerContractStub.GetInscriptionInfo.CallAsync(new StringValue
        {
            Value = _tick
        });
        inscription.ImageCount.ShouldBe(2);
        inscription.MaxGen.ShouldBe(4);
        inscription.CommissionRate.ShouldBe(1000);
        inscription.LossRate.ShouldBe(500);
        inscription.MaxGenLossRate.ShouldBe(5000);
        inscription.IsWeightEnabled.ShouldBe(true);
        var attributeList = await SchrodingerContractStub.GetAttributeTypes.CallAsync(new StringValue
        {
            Value = _tick
        });
        attributeList.Data.Count.ShouldBe(7);
        attributeList.Data[0].Name.ShouldBe("Background");
        attributeList.Data[0].Weight.ShouldBe(170);
        var attributeValues = await SchrodingerContractStub.GetAttributeValues.CallAsync(new GetAttributeValuesInput
        {
            Tick = _tick,
            TraitType = attributeList.Data[0].Name
        });
        attributeValues.Data.Count.ShouldBe(3);
        attributeValues.Data[0].Name.ShouldBe("Black");
        attributeValues.Data[0].Weight.ShouldBe(8);
        attributeValues.Data[1].Name.ShouldBe("white");
        attributeValues.Data[1].Weight.ShouldBe(2);
        attributeValues.Data[2].Name.ShouldBe("Red");
        attributeValues.Data[2].Weight.ShouldBe(14);
        attributeList.Data[1].Name.ShouldBe("Eyes");
        attributeList.Data[1].Weight.ShouldBe(100);
        attributeValues = await SchrodingerContractStub.GetAttributeValues.CallAsync(new GetAttributeValuesInput
        {
            Tick = _tick,
            TraitType = attributeList.Data[1].Name
        });
        attributeValues.Data.Count.ShouldBe(3);
        attributeValues.Data[0].Name.ShouldBe("Big");
        attributeValues.Data[0].Weight.ShouldBe(5);
        attributeValues.Data[1].Name.ShouldBe("Small");
        attributeValues.Data[1].Weight.ShouldBe(10);
        attributeValues.Data[2].Name.ShouldBe("Medium");
        attributeValues.Data[2].Weight.ShouldBe(9);
        
        attributeList.Data[2].Name.ShouldBe("Clothes");
        attributeList.Data[2].Weight.ShouldBe(200);
        attributeValues = await SchrodingerContractStub.GetAttributeValues.CallAsync(new GetAttributeValuesInput
        {
            Tick = _tick,
            TraitType = attributeList.Data[2].Name
        });
        attributeValues.Data.Count.ShouldBe(3);
        attributeValues.Data[0].Name.ShouldBe("Hoddie");
        attributeValues.Data[0].Weight.ShouldBe(127);
        attributeValues.Data[1].Name.ShouldBe("Kimono");
        attributeValues.Data[1].Weight.ShouldBe(127);
        attributeValues.Data[2].Name.ShouldBe("Student");
        attributeValues.Data[2].Weight.ShouldBe(127);
        
        attributeList.Data[3].Name.ShouldBe("Hat"); 
        attributeList.Data[3].Weight.ShouldBe(170);
        attributeValues = await SchrodingerContractStub.GetAttributeValues.CallAsync(new GetAttributeValuesInput
        {
            Tick = _tick,
            TraitType = attributeList.Data[3].Name
        });
        attributeValues.Data.Count.ShouldBe(3);
        attributeValues.Data[0].Name.ShouldBe("Halo");
        attributeValues.Data[0].Weight.ShouldBe(170);
        attributeValues.Data[1].Name.ShouldBe("Tiara");
        attributeValues.Data[1].Weight.ShouldBe(38);
        attributeValues.Data[2].Name.ShouldBe("Crown");
        attributeValues.Data[2].Weight.ShouldBe(100);
        
        attributeList.Data[4].Name.ShouldBe("Mouth");
        attributeList.Data[4].Weight.ShouldBe(200);
        attributeValues = await SchrodingerContractStub.GetAttributeValues.CallAsync(new GetAttributeValuesInput
        {
            Tick = _tick,
            TraitType = attributeList.Data[4].Name
        });
        attributeValues.Data.Count.ShouldBe(3);
        attributeValues.Data[0].Name.ShouldBe("Pizza");
        attributeValues.Data[0].Weight.ShouldBe(310);
        attributeValues.Data[1].Name.ShouldBe("Rose");
        attributeValues.Data[1].Weight.ShouldBe(210);
        attributeValues.Data[2].Name.ShouldBe("Roar");
        attributeValues.Data[2].Weight.ShouldBe(160);

        attributeList.Data[5].Name.ShouldBe("Pet");
        attributeList.Data[5].Weight.ShouldBe(300);
        attributeValues = await SchrodingerContractStub.GetAttributeValues.CallAsync(new GetAttributeValuesInput
        {
            Tick = _tick,
            TraitType = attributeList.Data[5].Name
        });
        attributeValues.Data.Count.ShouldBe(3);
        attributeValues.Data[0].Name.ShouldBe("Alien");
        attributeValues.Data[0].Weight.ShouldBe(400);
        attributeValues.Data[1].Name.ShouldBe("Elf");
        attributeValues.Data[1].Weight.ShouldBe(10);
        attributeValues.Data[2].Name.ShouldBe("Star");
        attributeValues.Data[2].Weight.ShouldBe(199);
        
        attributeList.Data[6].Name.ShouldBe("Face");
        attributeList.Data[6].Weight.ShouldBe(450);
        attributeValues = await SchrodingerContractStub.GetAttributeValues.CallAsync(new GetAttributeValuesInput
        {
            Tick = _tick,
            TraitType = attributeList.Data[6].Name
        });
        attributeValues.Data.Count.ShouldBe(3);
        attributeValues.Data[0].Name.ShouldBe("Sad");
        attributeValues.Data[0].Weight.ShouldBe(600);
        attributeValues.Data[1].Name.ShouldBe("Happy");
        attributeValues.Data[1].Weight.ShouldBe(120);
        attributeValues.Data[2].Name.ShouldBe("Angry");
        attributeValues.Data[2].Weight.ShouldBe(66);
        
    }

    [Fact]
    public async Task SetFixedAttributeListTest()
    {
        await DeployTest();
        var attribute = GetFixedAttributeLists();
        var result = await SchrodingerContractStub.SetFixedAttribute.SendAsync(new SetAttributeInput
        {
            Tick = _tick,
            AttributeSet = attribute
        });
        var attributeList = await SchrodingerContractStub.GetAttributeTypes.CallAsync(new StringValue
        {
            Value = _tick
        });
        attributeList.Data.Count.ShouldBe(8);
        attributeList.Data[3].Name.ShouldBe("Breed");
        attributeList.Data[3].Weight.ShouldBe(170);
        var values = await SchrodingerContractStub.GetAttributeValues.CallAsync(new GetAttributeValuesInput
        {
            Tick = _tick,
            TraitType = attributeList.Data[3].Name
        });
        values.Data.Count.ShouldBe(3);
        values.Data[0].Name.ShouldBe("Alien");
        values.Data[0].Weight.ShouldBe(760);
        values.Data[1].Name.ShouldBe("Ape");
        values.Data[1].Weight.ShouldBe(95);
        values.Data[2].Name.ShouldBe("Zombie");
        values.Data[2].Weight.ShouldBe(95);
        

        var log = GetLogEvent<FixedAttributeSet>(result.TransactionResult);
        log.AddedAttribute.TraitType.Name.ShouldBe("Breed");
        log.AddedAttribute.Values.Data.Count.ShouldBe(3);
    }

    [Fact]
    public async Task SetRandomAttributeListTest()
    {
        await DeployTest();
        var attribute = GetRandomAttributeLists();
        var result = await SchrodingerContractStub.SetRandomAttribute.SendAsync(new SetAttributeInput
        {
            Tick = _tick,
            AttributeSet = attribute
        });
        var attributeList = await SchrodingerContractStub.GetAttributeTypes.CallAsync(new StringValue
        {
            Value = _tick
        });

        attributeList.Data.Count.ShouldBe(8);
        attributeList.Data[7].Name.ShouldBe("Shoes");
        attributeList.Data[7].Weight.ShouldBe(170);
        var values = await SchrodingerContractStub.GetAttributeValues.CallAsync(new GetAttributeValuesInput
        {
            Tick = _tick,
            TraitType = attributeList.Data[7].Name
        });
        values.Data.Count.ShouldBe(3);
        values.Data[0].Name.ShouldBe("Boots");
        values.Data[0].Weight.ShouldBe(5);
        values.Data[1].Name.ShouldBe("Clogs");
        values.Data[1].Weight.ShouldBe(10);
        values.Data[2].Name.ShouldBe("Brogues");
        values.Data[2].Weight.ShouldBe(9);

        var log = GetLogEvent<RandomAttributeSet>(result.TransactionResult);
        log.AddedAttribute.TraitType.Name.ShouldBe("Shoes");
        log.AddedAttribute.Values.Data.Count.ShouldBe(3);
    }

    [Fact]
    public async Task SetFixedAttributeListTest_Remove()
    {
        await DeployTest();
        var result = await SchrodingerContractStub.SetFixedAttribute.SendAsync(new SetAttributeInput
        {
            Tick = _tick,
            AttributeSet = new AttributeSet
            {
                TraitType = new AttributeInfo
                {
                    Name = "Clothes"
                }
            }
        });
        var attributeList = await SchrodingerContractStub.GetAttributeTypes.CallAsync(new StringValue
        {
            Value = _tick
        });
        attributeList.Data.Count.ShouldBe(6);
        attributeList.Data[1].Name.ShouldBe("Eyes");
        attributeList.Data[1].Weight.ShouldBe(100);
        attributeList.Data[2].Name.ShouldBe("Hat");
        attributeList.Data[2].Weight.ShouldBe(170);


        var log = GetLogEvent<FixedAttributeSet>(result.TransactionResult);
        log.RemovedAttribute.TraitType.Name.ShouldBe("Clothes");
        log.RemovedAttribute.Values.ShouldBeNull();
    }

    [Fact]
    public async Task SetFixedAttributeListTest_Update()
    {
        await DeployTest();
        var traitValues1 = new List<AttributeInfo>
        {
            new AttributeInfo { Name = "Alien", Weight = 760 },
            new AttributeInfo { Name = "Ape", Weight = 95 },
            new AttributeInfo { Name = "Zombie", Weight = 95 }
        };
        await SchrodingerContractStub.SetFixedAttribute.SendAsync(new SetAttributeInput
        {
            Tick = _tick,
            AttributeSet = new AttributeSet
            {
                TraitType = new AttributeInfo
                {
                    Name = "Clothes"
                },
                Values = new AttributeInfos
                {
                    Data = { traitValues1 }
                }
            }
        });
        var attributeList = await SchrodingerContractStub.GetAttributeTypes.CallAsync(new StringValue
        {
            Value = _tick
        });
        attributeList.Data.Count.ShouldBe(7);
        attributeList.Data[2].Name.ShouldBe("Clothes");
        attributeList.Data[2].Weight.ShouldBe(200);
        var values = await SchrodingerContractStub.GetAttributeValues.CallAsync(new GetAttributeValuesInput
        {
            Tick = _tick,
            TraitType = attributeList.Data[2].Name
        });
        values.Data.Count.ShouldBe(3);
        values.Data[0].Name.ShouldBe("Alien");
        values.Data[0].Weight.ShouldBe(760);
        values.Data[1].Name.ShouldBe("Ape");
        values.Data[1].Weight.ShouldBe(95);
        values.Data[2].Name.ShouldBe("Zombie");
        values.Data[2].Weight.ShouldBe(95);
    }

    // [Fact]
    // public async Task SetAttributeList_Remove_Test()
    // {
    //     await SetAttributeListTest();
    //     var attribute = GetAttributeLists_remove_duplicated_values();
    //     await SchrodingerContractStub.SetAttributes.SendAsync(new SetAttributesInput
    //     {
    //         Tick = _tick,
    //         Attributes = attribute
    //     });
    //     var attributeList = await SchrodingerContractStub.GetAttributes.CallAsync(new StringValue
    //     {
    //         Value = _tick
    //     });
    //     attributeList.FixedAttributes.Count.ShouldBe(3);
    //     attributeList.RandomAttributes.Count.ShouldBe(4);
    //     attributeList.FixedAttributes[0].TraitType.Name.ShouldBe("Background");
    //     attributeList.FixedAttributes[1].TraitType.Name.ShouldBe("Eyes");
    //     attributeList.FixedAttributes[2].TraitType.Name.ShouldBe("Breed");
    //     attributeList.FixedAttributes[2].Values.Data.Count.ShouldBe(3);
    //     attributeList.FixedAttributes[2].Values.Data[0].Name.ShouldBe("Alien");
    //     attributeList.FixedAttributes[2].Values.Data[0].Weight.ShouldBe(760);
    //     attributeList.FixedAttributes[2].Values.Data[1].Name.ShouldBe("Ape");
    //     attributeList.FixedAttributes[2].Values.Data[1].Weight.ShouldBe(95);
    //     attributeList.FixedAttributes[2].Values.Data[2].Name.ShouldBe("Zombie");
    //     attributeList.FixedAttributes[2].Values.Data[2].Weight.ShouldBe(95);
    //     attributeList.RandomAttributes[0].TraitType.Name.ShouldBe("Hat");
    //     attributeList.RandomAttributes[1].TraitType.Name.ShouldBe("Pet");
    //     attributeList.RandomAttributes[2].TraitType.Name.ShouldBe("Face");
    //     attributeList.RandomAttributes[3].TraitType.Name.ShouldBe("Shoes");
    //     attributeList.RandomAttributes[1].Values.Data.Count.ShouldBe(3);
    //     attributeList.RandomAttributes[1].Values.Data[0].Name.ShouldBe("Alien");
    //     attributeList.RandomAttributes[1].Values.Data[0].Weight.ShouldBe(300);
    //     attributeList.RandomAttributes[1].Values.Data[1].Name.ShouldBe("Ape");
    //     attributeList.RandomAttributes[1].Values.Data[1].Weight.ShouldBe(20);
    //     attributeList.RandomAttributes[1].Values.Data[2].Name.ShouldBe("Zombie");
    //     attributeList.RandomAttributes[1].Values.Data[2].Weight.ShouldBe(95);
    //     attributeList.RandomAttributes[2].Values.Data[0].Name.ShouldBe("Boots");
    //     attributeList.RandomAttributes[2].Values.Data[0].Weight.ShouldBe(720);
    //     attributeList.RandomAttributes[2].Values.Data[1].Name.ShouldBe("Clogs");
    //     attributeList.RandomAttributes[2].Values.Data[1].Weight.ShouldBe(10);
    //     attributeList.RandomAttributes[2].Values.Data[2].Name.ShouldBe("Brogues");
    //     attributeList.RandomAttributes[2].Values.Data[2].Weight.ShouldBe(60);
    // }
}