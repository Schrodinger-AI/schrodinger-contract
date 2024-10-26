namespace Schrodinger;

public static class SchrodingerContractConstants
{
    // token
    public const string CollectionSymbolSuffix = "0";
    public const string AncestorSymbolSuffix = "1";
    public const char Separator = '-';
    public const string TokenNameSuffix = "GEN";
    public const int DefaultSymbolIndexStart = 2;
    public const string AncestorGen = "0";

    // external info
    public const string InscriptionDeployKey = "__inscription_deploy";
    public const string InscriptionAdoptKey = "__inscription_adopt";
    public const string InscriptionImageKey = "__inscription_image";
    public const string InscriptionImageUriKey = "__nft_image_uri";
    public const string AttributesKey = "__nft_attributes";
    public const string InscriptionType = "aelf";
    public const string DeployOp = "deploy";
    public const string AdoptOp = "adopt";
    public const string Amt = "1";

    // config
    public const int DefaultMinGen = 1;
    public const int DefaultMaxGen = 10;
    public const long DefaultImageMaxSize = 10240; // 10kb
    public const long DefaultImageUriMaxSize = 64;
    public const long DefaultImageMaxCount = 10;
    public const long DefaultTraitValueMaxCount = 100;
    public const long DefaultAttributeMaxLength = 80;
    public const long DefaultMaxWeight = 10000;
    public const int DefaultMaxAttributePerGen = 1;
    public const int DefaultMaxAttributeTraitTypeCount = 50;
    public const int DefaultFixedTraitTypeMaxCount = 5;
    
    // math
    public const int Ten = 10;
    public const long Denominator = 10000;
    
    // points
    public const int DefaultMaxProportionListCount = 10;
    public const long DefaultAdoptProportion = 131400000000;
    public const long DefaultRerollProportion = 191900000000;
    public const long DefaultProportion = 100000000;
    public const long DefaultAdoptMaxGenProportion = 966600000000;
    
    // spin
    public const string Spin = "spin";
}