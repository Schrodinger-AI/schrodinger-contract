using AElf.Contracts.Consensus.AEDPoS;
using AElf.Contracts.MultiToken;
using AElf.Standards.ACS0;
using Points.Contracts.Point;

namespace Schrodinger;

public partial class SchrodingerContractState
{
    internal ACS0Container.ACS0ReferenceState GenesisContract { get; set; }
    internal AEDPoSContractContainer.AEDPoSContractReferenceState ConsensusContract { get; set; }
    internal TokenContractContainer.TokenContractReferenceState TokenContract { get; set; }
    internal PointsContractContainer.PointsContractReferenceState PointsContract { get; set; }
}