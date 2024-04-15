using System.IO;
using System.Threading.Tasks;
using AElf;
using AElf.Boilerplate.TestBase;
using AElf.Contracts.MultiToken;
using AElf.ContractTestBase.ContractTestKit;
using AElf.Cryptography;
using AElf.Cryptography.ECDSA;
using AElf.CSharp.Core;
using AElf.Kernel;
using AElf.Standards.ACS0;
using AElf.Types;
using Google.Protobuf;
using Schrodinger.Contracts.TestPointsContract;
using Schrodinger.Main;
using SchrodingerMain;
using Volo.Abp.Threading;

namespace Schrodinger;

public class SchrodingerContractTestBase : DAppContractTestBase<SchrodingerContractTestModule>
{
    internal ACS0Container.ACS0Stub ZeroContractStub { get; set; }
    internal Address SchrodingerMainContractAddress { get; set; }
    internal Address SchrodingerContractAddress { get; set; }
    internal Address TestPointsContractAddress { get; set; }

    internal SchrodingerContractContainer.SchrodingerContractStub SchrodingerContractStub { get; set; }
    internal SchrodingerContractContainer.SchrodingerContractStub UserSchrodingerContractStub { get; set; }
    internal SchrodingerContractContainer.SchrodingerContractStub User2SchrodingerContractStub { get; set; }

    internal TokenContractContainer.TokenContractStub TokenContractStub { get; set; }
    internal TokenContractContainer.TokenContractStub TokenContractUserStub { get; set; }

    internal SchrodingerMainContractContainer.SchrodingerMainContractStub SchrodingerMainContractStub { get; set; }

    protected ECKeyPair DefaultKeyPair => Accounts[0].KeyPair;
    protected Address DefaultAddress => Accounts[0].Address;
    protected ECKeyPair UserKeyPair => Accounts[1].KeyPair;
    protected Address UserAddress => Accounts[1].Address;
    protected ECKeyPair User2KeyPair => Accounts[2].KeyPair;
    protected Address User2Address => Accounts[2].Address;
    protected readonly IBlockTimeProvider BlockTimeProvider;

    protected SchrodingerContractTestBase()
    {
        BlockTimeProvider = GetRequiredService<IBlockTimeProvider>();

        ZeroContractStub = GetContractStub<ACS0Container.ACS0Stub>(BasicContractZeroAddress, DefaultKeyPair);

        var code = File.ReadAllBytes(typeof(SchrodingerMainContract).Assembly.Location);
        var contractOperation = new ContractOperation
        {
            ChainId = 9992731,
            CodeHash = HashHelper.ComputeFrom(code),
            Deployer = DefaultAddress,
            Salt = HashHelper.ComputeFrom("schrodinger.main"),
            Version = 1
        };
        contractOperation.Signature = GenerateContractSignature(DefaultKeyPair.PrivateKey, contractOperation);

        var result = AsyncHelper.RunSync(async () => await ZeroContractStub.DeploySmartContract.SendAsync(
            new ContractDeploymentInput
            {
                Category = KernelConstants.CodeCoverageRunnerCategory,
                Code = ByteString.CopyFrom(code),
                ContractOperation = contractOperation
            }));

        SchrodingerMainContractAddress = Address.Parser.ParseFrom(result.TransactionResult.ReturnValue);
        SchrodingerMainContractStub = GetContractStub<SchrodingerMainContractContainer.SchrodingerMainContractStub>(
            SchrodingerMainContractAddress, DefaultKeyPair);
        
        code = File.ReadAllBytes(typeof(SchrodingerContract).Assembly.Location);
        contractOperation = new ContractOperation
        {
            ChainId = 9992731,
            CodeHash = HashHelper.ComputeFrom(code),
            Deployer = DefaultAddress,
            Salt = HashHelper.ComputeFrom("schrodinger"),
            Version = 1
        };
        contractOperation.Signature = GenerateContractSignature(DefaultKeyPair.PrivateKey, contractOperation);

        result = AsyncHelper.RunSync(async () => await ZeroContractStub.DeploySmartContract.SendAsync(
            new ContractDeploymentInput
            {
                Category = KernelConstants.CodeCoverageRunnerCategory,
                Code = ByteString.CopyFrom(code),
                ContractOperation = contractOperation
            }));

        SchrodingerContractAddress = Address.Parser.ParseFrom(result.TransactionResult.ReturnValue);
        SchrodingerContractStub =
            GetContractStub<SchrodingerContractContainer.SchrodingerContractStub>(SchrodingerContractAddress,
                DefaultKeyPair);
        UserSchrodingerContractStub =
            GetContractStub<SchrodingerContractContainer.SchrodingerContractStub>(SchrodingerContractAddress,
                UserKeyPair);
        User2SchrodingerContractStub =
            GetContractStub<SchrodingerContractContainer.SchrodingerContractStub>(SchrodingerContractAddress,
                User2KeyPair);

        TokenContractStub =
            GetContractStub<TokenContractContainer.TokenContractStub>(TokenContractAddress, DefaultKeyPair);
        TokenContractUserStub =
            GetContractStub<TokenContractContainer.TokenContractStub>(TokenContractAddress, UserKeyPair);

        result = AsyncHelper.RunSync(async () => await ZeroContractStub.DeploySmartContract.SendAsync(
            new ContractDeploymentInput
            {
                Category = KernelConstants.CodeCoverageRunnerCategory,
                Code = ByteString.CopyFrom(
                    File.ReadAllBytes(typeof(TestPointsContract).Assembly.Location))
            }));

        TestPointsContractAddress = Address.Parser.ParseFrom(result.TransactionResult.ReturnValue);
    }

    internal T GetContractStub<T>(Address contractAddress, ECKeyPair senderKeyPair)
        where T : ContractStubBase, new()
    {
        return GetTester<T>(contractAddress, senderKeyPair);
    }

    private ByteString GenerateContractSignature(byte[] privateKey, ContractOperation contractOperation)
    {
        var dataHash = HashHelper.ComputeFrom(contractOperation);
        var signature = CryptoHelper.SignWithPrivateKey(privateKey, dataHash.ToByteArray());
        return ByteStringHelper.FromHexString(signature.ToHex());
    }
}