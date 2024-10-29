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
    
    // tick -> trait type -> upper weight sums
    public MappedState<string, string, LongList> UpperWeightSumsMap { get; set; }
    // tick -> trait type -> index -> trait values
    public MappedState<string, string, int, TraitValues> TraitValuesMap { get; set; }
    
    public SingletonState<string> OfficialDomainAlias { get; set; }
    
    // tick -> RewardList
    public MappedState<string, RewardList> RewardListMap { get; set; }
    
    public MappedState<Hash, bool> SpinSignatureMap { get; set; }

    // spinId -> spinInfo
    public MappedState<Hash, SpinInfo> SpinInfoMap { get; set; }
    
    public MappedState<Hash, VoucherInfo> VoucherInfoMap { get; set; }
    
    // tick -> address -> long
    public MappedState<string, Address, long> AdoptionVoucherMap { get; set; }
    
    // tick -> admin
    public MappedState<string, AddressList> AirdropControllerMap { get; set; }
    
    // tick -> count
    public MappedState<string, long> VoucherIdCountMap { get; set; }
}