using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.Cryptography;
using AElf.CSharp.Core;
using AElf.CSharp.Core.Extension;
using AElf.Types;
using Google.Protobuf;
using Shouldly;

namespace Schrodinger;

public partial class SchrodingerContractTests
{
    private T GetLogEvent<T>(TransactionResult transactionResult) where T : IEvent<T>, new()
    {
        var log = transactionResult.Logs.FirstOrDefault(l => l.Name == typeof(T).Name);
        log.ShouldNotBeNull();

        var logEvent = new T();
        logEvent.MergeFrom(log.NonIndexed);

        return logEvent;
    }

    private ByteString GenerateSignature(byte[] privateKey, Hash adoptId, string image, string imageUri)
    {
        var data = new ConfirmInput
        {
            AdoptId = adoptId,
            Image = image,
            ImageUri = imageUri
        };
        var dataHash = HashHelper.ComputeFrom(data);
        var signature = CryptoHelper.SignWithPrivateKey(privateKey, dataHash.ToByteArray());
        return ByteStringHelper.FromHexString(signature.ToHex());
    }

    private async Task<long> GetTokenBalance(string symbol, Address sender)
    {
        var output = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
        {
            Owner = sender,
            Symbol = symbol
        });

        return output.Balance;
    }

    private async Task BuySeed()
    {
        await TokenContractStub.Create.SendAsync(new CreateInput
        {
            Symbol = "SEED-0",
            TokenName = "SEED-0 token",
            TotalSupply = 1,
            Decimals = 0,
            Issuer = DefaultAddress,
            IsBurnable = true,
            IssueChainId = 0,
        });

        var seedOwnedSymbol = "SGR" + "-0";
        var seedExpTime = BlockTimeProvider.GetBlockTime().AddDays(1);
        await TokenContractStub.Create.SendAsync(new CreateInput
        {
            Symbol = "SEED-1",
            TokenName = "SEED-1 token",
            TotalSupply = 1,
            Decimals = 0,
            Issuer = DefaultAddress,
            IsBurnable = true,
            IssueChainId = 0,
            LockWhiteList = { TokenContractAddress },
            ExternalInfo = new ExternalInfo()
            {
                Value =
                {
                    {
                        "__seed_owned_symbol",
                        seedOwnedSymbol
                    },
                    {
                        "__seed_exp_time",
                        seedExpTime.Seconds.ToString()
                    }
                }
            }
        });

        await TokenContractStub.Issue.SendAsync(new IssueInput
        {
            Symbol = "SEED-1",
            Amount = 1,
            To = DefaultAddress,
            Memo = ""
        });

        var balance = await TokenContractStub.GetBalance.SendAsync(new GetBalanceInput()
        {
            Owner = DefaultAddress,
            Symbol = "SEED-1"
        });
        balance.Output.Balance.ShouldBe(1);
        await TokenContractStub.Approve.SendAsync(new ApproveInput()
        {
            Symbol = "SEED-1",
            Amount = 1,
            Spender = SchrodingerMainContractAddress
        });
    }

    #region Attribute

    private AttributeLists GetAttributeLists()
    {
        var traitValues1 = new List<AttributeInfo>
        {
            new AttributeInfo { Name = "Black", Weight = 8 },
            new AttributeInfo { Name = "white", Weight = 2 },
            new AttributeInfo { Name = "Red", Weight = 14 }
        };
        var traitValues2 = new List<AttributeInfo>
        {
            new AttributeInfo { Name = "Big", Weight = 5 },
            new AttributeInfo { Name = "Small", Weight = 10 },
            new AttributeInfo { Name = "Medium", Weight = 9 }
        };
        var traitValues3 = new List<AttributeInfo>
        {
            new AttributeInfo { Name = "Halo", Weight = 170 },
            new AttributeInfo { Name = "Tiara", Weight = 38 },
            new AttributeInfo { Name = "Crown", Weight = 100 }
        };
        var traitValues4 = new List<AttributeInfo>
        {
            new AttributeInfo { Name = "Pizza", Weight = 310 },
            new AttributeInfo { Name = "Rose", Weight = 210 },
            new AttributeInfo { Name = "Roar", Weight = 160 }
        };
        var traitValues5 = new List<AttributeInfo>
        {
            new AttributeInfo { Name = "Alien", Weight = 400 },
            new AttributeInfo { Name = "Elf", Weight = 10 },
            new AttributeInfo { Name = "Star", Weight = 199 }
        };
        var traitValues6 = new List<AttributeInfo>
        {
            new AttributeInfo { Name = "Sad", Weight = 600 },
            new AttributeInfo { Name = "Happy", Weight = 120 },
            new AttributeInfo { Name = "Angry", Weight = 66 }
        };
        var traitValues7 = new List<AttributeInfo>
        {
            new AttributeInfo { Name = "Hoddie", Weight = 127 },
            new AttributeInfo { Name = "Kimono", Weight = 127 },
            new AttributeInfo { Name = "Student", Weight = 127 }
        };
        var fixedAttributes = new List<AttributeSet>()
        {
            new AttributeSet
            {
                TraitType = new AttributeInfo
                {
                    Name = "Background",
                    Weight = 170
                },
                Values = new AttributeInfos
                {
                    Data = { traitValues1 }
                }
            },
            new AttributeSet
            {
                TraitType = new AttributeInfo
                {
                    Name = "Eyes",
                    Weight = 100
                },
                Values = new AttributeInfos
                {
                    Data = { traitValues2 }
                }
            },
            new AttributeSet
            {
                TraitType = new AttributeInfo
                {
                    Name = "Clothes",
                    Weight = 200
                },
                Values = new AttributeInfos
                {
                    Data = { traitValues7 }
                }
            }
        };
        var randomAttributes = new List<AttributeSet>()
        {
            new AttributeSet
            {
                TraitType = new AttributeInfo
                {
                    Name = "Hat",
                    Weight = 170
                },
                Values = new AttributeInfos
                {
                    Data = { traitValues3 }
                }
            },
            new AttributeSet
            {
                TraitType = new AttributeInfo
                {
                    Name = "Mouth",
                    Weight = 200
                },
                Values = new AttributeInfos
                {
                    Data = { traitValues4 }
                }
            },
            new AttributeSet
            {
                TraitType = new AttributeInfo
                {
                    Name = "Pet",
                    Weight = 300
                },
                Values = new AttributeInfos
                {
                    Data = { traitValues5 }
                }
            },
            new AttributeSet
            {
                TraitType = new AttributeInfo
                {
                    Name = "Face",
                    Weight = 450
                },
                Values = new AttributeInfos
                {
                    Data = { traitValues6 }
                }
            }
        };
        return new AttributeLists
        {
            FixedAttributes = { fixedAttributes },
            RandomAttributes = { randomAttributes }
        };
    }

    private AttributeLists GetAttributeLists_remove_duplicated_values()
    {
        var traitValues1 = new List<AttributeInfo>
        {
            new AttributeInfo { Name = "Alien", Weight = 300 },
            new AttributeInfo { Name = "Ape", Weight = 20 },
            new AttributeInfo { Name = "Zombie", Weight = 95 },
            new AttributeInfo { Name = "Ape", Weight = 35 },
        };
        var traitValues2 = new List<AttributeInfo>
        {
            new AttributeInfo { Name = "Boots", Weight = 720 },
            new AttributeInfo { Name = "Clogs", Weight = 10 },
            new AttributeInfo { Name = "Brogues", Weight = 60 },
            new AttributeInfo { Name = "Brogues", Weight = 10 }
        };
        var fixedAttributes = new List<AttributeSet>()
        {
            new AttributeSet
            {
                TraitType = new AttributeInfo
                {
                    Name = "Clothes",
                    Weight = 200
                }
            }
        };
        var randomAttributes = new List<AttributeSet>()
        {
            new AttributeSet
            {
                TraitType = new AttributeInfo
                {
                    Name = "Mouth",
                    Weight = 200
                }
            },
            new AttributeSet
            {
                TraitType = new AttributeInfo
                {
                    Name = "Pet",
                    Weight = 300
                },
                Values = new AttributeInfos
                {
                    Data = { traitValues1 }
                }
            },
            new AttributeSet
            {
                TraitType = new AttributeInfo
                {
                    Name = "Face",
                    Weight = 450
                },
                Values = new AttributeInfos
                {
                    Data = { traitValues2 }
                }
            }
        };
        return new AttributeLists
        {
            FixedAttributes = { fixedAttributes },
            RandomAttributes = { randomAttributes }
        };
    }

    private AttributeLists GetAttributeLists_other()
    {
        var traitValues1 = new List<AttributeInfo>
        {
            new AttributeInfo { Name = "Alien", Weight = 760 },
            new AttributeInfo { Name = "Ape", Weight = 95 },
            new AttributeInfo { Name = "Zombie", Weight = 95 }
        };
        var traitValues2 = new List<AttributeInfo>
        {
            new AttributeInfo { Name = "Boots", Weight = 5 },
            new AttributeInfo { Name = "Clogs", Weight = 10 },
            new AttributeInfo { Name = "Brogues", Weight = 9 }
        };
        var fixedAttributes = new List<AttributeSet>()
        {
            new AttributeSet
            {
                TraitType = new AttributeInfo
                {
                    Name = "Breed",
                    Weight = 170
                },
                Values = new AttributeInfos
                {
                    Data = { traitValues1 }
                }
            }
        };
        var randomAttributes = new List<AttributeSet>()
        {
            new AttributeSet
            {
                TraitType = new AttributeInfo
                {
                    Name = "Shoes",
                    Weight = 170
                },
                Values = new AttributeInfos
                {
                    Data = { traitValues2 }
                }
            }
        };
        return new AttributeLists
        {
            FixedAttributes = { fixedAttributes },
            RandomAttributes = { randomAttributes }
        };
    }

    private AttributeSet GetFixedAttributeLists()
    {
        var traitValues1 = new List<AttributeInfo>
        {
            new AttributeInfo { Name = "Alien", Weight = 760 },
            new AttributeInfo { Name = "Ape", Weight = 95 },
            new AttributeInfo { Name = "Zombie", Weight = 95 }
        };
        var fixedAttributes =
            new AttributeSet
            {
                TraitType = new AttributeInfo
                {
                    Name = "Breed",
                    Weight = 170
                },
                Values = new AttributeInfos
                {
                    Data = { traitValues1 }
                }
            };
        return fixedAttributes;
    }

    private AttributeSet GetRandomAttributeLists()
    {
        var traitValues2 = new List<AttributeInfo>
        {
            new AttributeInfo { Name = "Boots", Weight = 5 },
            new AttributeInfo { Name = "Clogs", Weight = 10 },
            new AttributeInfo { Name = "Brogues", Weight = 9 }
        };
        var randomAttributes =
            new AttributeSet
            {
                TraitType = new AttributeInfo
                {
                    Name = "Shoes",
                    Weight = 170
                },
                Values = new AttributeInfos
                {
                    Data = { traitValues2 }
                }
            };
        return randomAttributes;
    }

    #endregion
}