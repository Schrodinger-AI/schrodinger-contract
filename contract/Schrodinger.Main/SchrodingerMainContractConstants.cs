namespace Schrodinger.Main;

public static class SchrodingerMainContractConstants
{
    // token
    public const string CollectionSymbolSuffix = "0";
    public const char Separator = '-';
    public const int DefaultCollectionTotalSupply = 1;


    // external info
    public const string InscriptionDeployKey = "__inscription_deploy";
    public const string InscriptionImageKey = "__inscription_image";
    public const string InscriptionImageUriKey = "__nft_image_uri";
    public const string InscriptionCreateChainIdKey = "__nft_create_chain_id";
    public const string InscriptionType = "aelf";
    public const string DeployOp = "deploy";
    public const string Lim = "0";

    // config
    public const long DefaultImageMaxSize = 10240; // 10kb
}