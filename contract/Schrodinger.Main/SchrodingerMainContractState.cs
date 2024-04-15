using AElf.Sdk.CSharp.State;
using AElf.Types;

namespace Schrodinger.Main;

public partial class SchrodingerMainContractState : ContractState
{
    // contract
    public SingletonState<bool> Initialized { get; set; }
    public SingletonState<Address> Admin { get; set; }
    public SingletonState<long> ImageMaxSize { get; set; }
    public SingletonState<Address> SchrodingerContractAddress { get; set; }
}