using System.Threading.Tasks;
using AElf.Contracts.MultiToken;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace Schrodinger;

public partial class SchrodingerContractTests
{
    private const string Gen0 = "SGR-1";

    [Fact]
    public async Task AdoptTests()
    {
        Hash adoptId;
        string symbol;
        long amount;

        await DeployTest();
        // await SetPointsProportion();

        {
            var balance = await GetTokenBalance(Gen0, DefaultAddress);
            balance.ShouldBe(0);
        }

        await TokenContractStub.Issue.SendAsync(new IssueInput
        {
            Symbol = Gen0,
            Amount = 1000,
            To = DefaultAddress
        });

        {
            var balance = await GetTokenBalance(Gen0, DefaultAddress);
            balance.ShouldBe(1000);
        }

        await TokenContractStub.Approve.SendAsync(new ApproveInput
        {
            Symbol = Gen0,
            Amount = 1000,
            Spender = SchrodingerContractAddress
        });

        {
            var result = await SchrodingerContractStub.Adopt.SendAsync(new AdoptInput
            {
                Parent = Gen0,
                Amount = 1000,
                Domain = "test"
            });

            var log = GetLogEvent<Adopted>(result.TransactionResult);
            log.Ancestor.ShouldBe(Gen0);
            log.Adopter.ShouldBe(DefaultAddress);
            log.InputAmount.ShouldBe(1000);
            log.OutputAmount.ShouldBe(950);
            log.LossAmount.ShouldBe(45);
            log.CommissionAmount.ShouldBe(5);

            adoptId = log.AdoptId;
            symbol = log.Symbol;
            
            var receivingAddress = await SchrodingerContractStub.GetReceivingAddress.CallAsync(new StringValue
            {
                Value = _tick
            });
            
            GetTokenBalance(Gen0, receivingAddress).Result.ShouldBe(log.LossAmount);
        }

        {
            var confirmInput = new ConfirmInput
            {
                AdoptId = adoptId,
                Image = "test",
                ImageUri = "test"
            };

            confirmInput.Signature = GenerateSignature(DefaultKeyPair.PrivateKey, confirmInput.AdoptId,
                confirmInput.Image, confirmInput.ImageUri);

            var result = await SchrodingerContractStub.Confirm.SendAsync(confirmInput);

            var log = GetLogEvent<Confirmed>(result.TransactionResult);
        }

        {
            var balance = await GetTokenBalance(Gen0, DefaultAddress);
            balance.ShouldBe(5);
        }
        {
            var balance = await GetTokenBalance(symbol, DefaultAddress);
            balance.ShouldBe(950);
        }

        {
            await TokenContractStub.Approve.SendAsync(new ApproveInput
            {
                Spender = SchrodingerContractAddress,
                Symbol = symbol,
                Amount = 950
            });

            var result = await SchrodingerContractStub.Adopt.SendAsync(new AdoptInput
            {
                Parent = symbol,
                Amount = 950,
                Domain = "test"
            });

            var log = GetLogEvent<Adopted>(result.TransactionResult);

            adoptId = log.AdoptId;
            symbol = log.Symbol;
            amount = log.OutputAmount;
        }

        {
            var confirmInput = new ConfirmInput
            {
                AdoptId = adoptId,
                Image = "test",
                ImageUri = "test"
            };

            confirmInput.Signature = GenerateSignature(DefaultKeyPair.PrivateKey, confirmInput.AdoptId,
                confirmInput.Image, confirmInput.ImageUri);

            var result = await SchrodingerContractStub.Confirm.SendAsync(confirmInput);

            var log = GetLogEvent<Confirmed>(result.TransactionResult);
        }

        {
            await TokenContractStub.Approve.SendAsync(new ApproveInput
            {
                Spender = SchrodingerContractAddress,
                Symbol = symbol,
                Amount = amount
            });

            var result = await SchrodingerContractStub.Reroll.SendAsync(new RerollInput
            {
                Symbol = symbol,
                Amount = amount,
                Domain = "test"
            });

            var log = GetLogEvent<Rerolled>(result.TransactionResult);
        }

        {
            var balance = await GetTokenBalance(symbol, DefaultAddress);
            balance.ShouldBe(0);
        }
    }

    [Fact]
    public async Task TransferFromReceivingAddressTests()
    {
        await AdoptTests();
        
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

    private async Task SetPointsProportion()
    {
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
    }
}