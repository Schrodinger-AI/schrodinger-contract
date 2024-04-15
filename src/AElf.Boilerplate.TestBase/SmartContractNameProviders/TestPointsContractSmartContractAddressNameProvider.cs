using AElf.Kernel.Infrastructure;
using AElf.Types;

namespace AElf.Boilerplate.TestBase.SmartContractNameProviders;

public class TestPointsContractSmartContractAddressNameProvider
{
    public static readonly Hash Name = HashHelper.ComputeFrom("TestPointsContract");

    public static readonly string StringName = Name.ToStorageKey();
    public Hash ContractName => Name;
    public string ContractStringName => StringName;
}