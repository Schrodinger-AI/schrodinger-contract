using AElf.Contracts.MultiToken;
using AElf.Standards.ACS0;

namespace Schrodinger.Main;

public partial class SchrodingerMainContractState
{
    internal ACS0Container.ACS0ReferenceState GenesisContract { get; set; }
    internal TokenContractContainer.TokenContractReferenceState TokenContract { get; set; }
}