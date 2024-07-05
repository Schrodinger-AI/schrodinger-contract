using AElf.Sdk.CSharp.State;
using AElf.Types;

namespace Schrodinger;

public partial class SchrodingerContractState : ContractState
{
    // contract
    public SingletonState<bool> Initialized { get; set; }
    public SingletonState<Address> Admin { get; set; }

    // inscription
    // tick -> fix attribute infos
    public MappedState<string, AttributeInfos> FixedTraitTypeMap { get; set; }

    // tick -> random attribute infos(greater than 1)
    public MappedState<string, AttributeInfos> RandomTraitTypeMap { get; set; }

    // tick -> attribute type -> attribute infos
    public MappedState<string, string, AttributeInfos> TraitValueMap { get; set; }

    // tick -> inscription info
    public MappedState<string, InscriptionInfo> InscriptionInfoMap { get; set; }

    // adopt id -> adopt info
    public MappedState<Hash, AdoptInfo> AdoptInfoMap { get; set; }

    // symbol -> adopt id
    public MappedState<string, Hash> SymbolAdoptIdMap { get; set; }

    // tick -> count, start from 2
    public MappedState<string, long> SymbolCountMap { get; set; }

    // tick -> trait type -> value weights
    public MappedState<string, string, long> TraitValueTotalWeightsMap { get; set; }
    public MappedState<string, long> GenTotalWeightsMap { get; set; }
    
    // tick -> signatory
    public MappedState<string, Address> SignatoryMap { get; set; }

    // config
    public SingletonState<Config> Config { get; set; }

    // point contract
    public MappedState<Address, bool> JoinRecord { get; set; }
    public SingletonState<Hash> PointsContractDAppId { get; set; }
    
    // action name -> proportion
    public MappedState<string, long> PointsProportion { get; set; }
    public SingletonState<Address> PointsSettleAdmin { get; set; }
    
    public SingletonState<string> OfficialDomainAlias { get; set; }
}