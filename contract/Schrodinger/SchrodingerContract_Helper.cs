using System.Collections.Generic;
using System.Linq;
using System.Text;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.CSharp.Core;
using AElf.Sdk.CSharp;
using AElf.Types;
using Google.Protobuf;

namespace Schrodinger;

public partial class SchrodingerContract
{
    private void CheckAdminPermission()
    {
        Assert(Context.Sender == State.Admin.Value, "No permission.");
    }

    private void CheckInitialized()
    {
        Assert(State.Initialized.Value, "Not initialized.");
    }

    private void CheckSettleAdminPermission()
    {
        Assert(Context.Sender == State.PointsSettleAdmin.Value, "No permission.");
    }

    private bool IsAddressValid(Address input)
    {
        return input != null && !input.Value.IsNullOrEmpty();
    }

    private bool IsHashValid(Hash input)
    {
        return input != null && !input.Value.IsNullOrEmpty();
    }

    private bool IsStringValid(string input)
    {
        return !string.IsNullOrWhiteSpace(input);
    }

    private bool IsByteStringValid(ByteString input)
    {
        return !input.IsNullOrEmpty();
    }

    private bool IsSymbolValid(string input)
    {
        return IsStringValid(input) && input.Split(SchrodingerContractConstants.Separator).Length == 2;
    }

    private string GetTickFromSymbol(string symbol)
    {
        return symbol.Split(SchrodingerContractConstants.Separator)[0].ToUpper();
    }

    private InscriptionInfo CheckInscriptionExistAndPermission(string tick)
    {
        var inscription = State.InscriptionInfoMap[tick];
        Assert(inscription != null, "Inscription not found.");
        Assert(inscription.Admin == Context.Sender, "No permission.");
        return inscription;
    }

    private Address GetReceivingAddress(string tick)
    {
        return State.InscriptionInfoMap[tick] == null ? new Address() : Context.ConvertVirtualAddressToContractAddress(HashHelper.ComputeFrom(tick));
    }

    #region Deploy

    private ExternalInfo GenerateExternalInfo(string tick, string image, long totalSupply, string imageUri)
    {
        var externalInfo = new ExternalInfo();
        var dic = new Dictionary<string, string>
        {
            [SchrodingerContractConstants.InscriptionImageKey] = image,
            [SchrodingerContractConstants.InscriptionImageUriKey] = imageUri
        };

        var info = new DeployInscriptionInfo
        {
            P = SchrodingerContractConstants.InscriptionType,
            Op = SchrodingerContractConstants.DeployOp,
            Tick = tick,
            Max = totalSupply.ToString(),
            Lim = totalSupply.ToString(),
            Gen = SchrodingerContractConstants.AncestorGen
        };
        dic[SchrodingerContractConstants.InscriptionDeployKey] = info.ToString();

        externalInfo.Value.Add(dic);
        return externalInfo;
    }

    private void CreateInscription(string tick, int decimals, long totalSupply, ExternalInfo externalInfo,
        Address issuer)
    {
        var createTokenInput = new CreateInput
        {
            Symbol = GetInscriptionSymbol(tick),
            TokenName = tick,
            TotalSupply = totalSupply,
            Decimals = decimals,
            Issuer = issuer ?? Context.Sender,
            IsBurnable = true,
            IssueChainId = Context.ChainId,
            ExternalInfo = externalInfo,
            Owner = Context.Self
        };
        State.TokenContract.Create.Send(createTokenInput);
    }

    private void SetTokenContract()
    {
        if (State.TokenContract.Value == null)
        {
            State.TokenContract.Value =
                Context.GetContractAddressByName(SmartContractConstants.TokenContractSystemName);
        }
    }

    private string GetInscriptionSymbol(string tick)
    {
        return $"{tick}{SchrodingerContractConstants.Separator}{SchrodingerContractConstants.AncestorSymbolSuffix}";
    }

    private string GetInscriptionCollectionSymbol(string tick)
    {
        return $"{tick}{SchrodingerContractConstants.Separator}{SchrodingerContractConstants.CollectionSymbolSuffix}";
    }

    #endregion

    #region Attribute

    /// <param name="tick"></param>
    /// <param name="maxGen"></param>
    /// <param name="sourceAttributeList">to add attribute list, contains fixed and random</param>
    /// <param name="attributesPerGen"></param>
    /// <returns></returns>
    private AttributeLists SetAttributeList(string tick, long maxGen, AttributeLists sourceAttributeList,
        int attributesPerGen)
    {
        var fixedAttributes = sourceAttributeList?.FixedAttributes.ToList();
        var randomAttributes = sourceAttributeList?.RandomAttributes.ToList();
        CheckAttributeList(fixedAttributes, randomAttributes);
        SetFixedAttributeSets(tick, fixedAttributes);
        SetRandomAttributeSet(tick, randomAttributes, maxGen, attributesPerGen);
        var result = new AttributeLists
        {
            FixedAttributes = { fixedAttributes },
            RandomAttributes = { randomAttributes }
        };
        return result;
    }


    /// <summary>
    /// Set fixed attribute sets.
    /// </summary>
    /// <param name="tick"></param>
    /// <param name="sourceAttributeSets"> to set attribute sets</param>
    /// <returns></returns>
    private void SetFixedAttributeSets(string tick, List<AttributeSet> sourceAttributeSets)
    {
        var traitTypeMap = State.FixedTraitTypeMap[tick] ?? new AttributeInfos();
        SetAttributeSet(tick, traitTypeMap, sourceAttributeSets);
        State.FixedTraitTypeMap[tick] = traitTypeMap;
    }

    /// <summary>
    /// Set random attribute sets.
    /// After changed, check random attribute list count according to maxGen and attributesPerGen.
    /// </summary>
    /// <param name="tick"></param>
    /// <param name="sourceAttributeSets"> to set attribute sets</param>
    /// <param name="maxGen"></param>
    /// <param name="attributesPerGen"></param>
    /// <returns></returns>
    private void SetRandomAttributeSet(string tick, List<AttributeSet> sourceAttributeSets,
        long maxGen, int attributesPerGen)
    {
        var traitTypeMap = State.RandomTraitTypeMap[tick] ?? new AttributeInfos();
        SetAttributeSet(tick, traitTypeMap, sourceAttributeSets);
        State.RandomTraitTypeMap[tick] = traitTypeMap;
        CheckRandomAttributeList(traitTypeMap.Data.ToList(), maxGen, attributesPerGen);
    }

    // 
    /// <summary>
    /// return trait type list,out update attribute sets(trait type and values),out to remove attribute sets.
    /// </summary>
    /// <param name="tick"></param>
    /// <param name="traitTypeMap">trait type list from state</param>
    /// <param name="sourceAttributeSets">input attributeSets</param>
    /// <param name="isRandom">if is random trait type</param>
    /// <returns></returns>
    private void SetAttributeSet(string tick, AttributeInfos traitTypeMap,
        List<AttributeSet> sourceAttributeSets)
    {
        var config = State.Config?.Value;
        foreach (var sourceAttributeSet in sourceAttributeSets)
        {
            var traitType = sourceAttributeSet.TraitType;
            var attributeMaxLength = config?.AttributeMaxLength ??
                                     SchrodingerContractConstants.DefaultAttributeMaxLength;
            Assert(!string.IsNullOrWhiteSpace(traitType.Name), "Invalid trait type name.");
            Assert(traitType.Name.Length <= attributeMaxLength, "Invalid trait type name length.");
            Assert(traitType.Weight >= 0 && traitType.Weight <= SchrodingerContractConstants.DefaultMaxWeight,
                "Invalid weight.");
            SetTraitValues(tick, traitType.Name, sourceAttributeSet.Values, config);
            traitTypeMap.Data.Add(traitType);
        }
    }

    /// <param name="tick"></param>
    /// <param name="traitTypeName"></param>
    /// <param name="sourceTraitValues"> input trait values</param>
    /// <returns>after changed,example remove duplicates</returns>
    private void SetTraitValues(string tick, string traitTypeName, AttributeInfos sourceTraitValues, Config config)
    {
        Assert(sourceTraitValues != null && sourceTraitValues.Data.Count > 0, "Invalid attribute trait values.");
        var uniqueSet = new HashSet<string>();
        var traitValueMap = State.TraitValueMap[tick][traitTypeName] ?? new AttributeInfos();
        traitValueMap.Data.Clear();
        var data = traitValueMap.Data.ToList();
        var weight = 0L;
        foreach (var sourceTraitValue in sourceTraitValues.Data)
        {
            Assert(uniqueSet.Add(sourceTraitValue.Name), $"Duplicate trait type {sourceTraitValue.Name}");
            weight += sourceTraitValue.Weight;
            var attributeMaxLength =
                config?.AttributeMaxLength ?? SchrodingerContractConstants.DefaultAttributeMaxLength;
            Assert(!string.IsNullOrWhiteSpace(sourceTraitValue.Name), "Invalid trait type name.");
            Assert(sourceTraitValue.Name.Length <= attributeMaxLength, "Invalid trait type name length.");
            Assert(
                sourceTraitValue.Weight >= 0 &&
                sourceTraitValue.Weight <= SchrodingerContractConstants.DefaultMaxWeight, "Invalid weight.");
            data.Add(sourceTraitValue);
        }

        var maxTraitValueCount = config?.TraitValueMaxCount ?? SchrodingerContractConstants.DefaultTraitValueMaxCount;
        var count = data.Count;
        Assert(count > 0 && count <= maxTraitValueCount, "Invalid attribute trait values count.");
        traitValueMap.Data.AddRange(data);
        State.TraitValueMap[tick][traitTypeName] = traitValueMap;
        State.TraitValueTotalWeightsMap[tick][traitTypeName] = weight;
    }

    private InscriptionInfo CheckParamsAndGetInscription(SetAttributeInput input)
    {
        Assert(IsStringValid(input.Tick), "Invalid input.");
        var inscription = CheckInscriptionExistAndPermission(input.Tick);
        Assert(input.AttributeSet != null, "Invalid input attribute set.");
        Assert(input.AttributeSet.TraitType != null && IsStringValid(input.AttributeSet.TraitType.Name),
            "Invalid input trait type.");
        return inscription;
    }

    private AttributeInfos UpdateAttributeSet(string tick, AttributeInfos traitTypes, AttributeInfos traitValues,
        AttributeInfo toAddTraitType, AttributeInfos toAddTraitValues, out AttributeInfo toRemove)
    {
        toRemove = null;
        var config = State.Config?.Value;
        var traitTypeName = toAddTraitType.Name;
        if (traitValues != null)
        {
            CheckTraitTypeExist(traitTypeName, traitTypes);
            // trait type exist
            if (toAddTraitValues == null || toAddTraitValues.Data.Count <= 0)
            {
                // remove trait type
                State.TraitValueMap[tick].Remove(traitTypeName);
                foreach (var traitType in traitTypes.Data)
                {
                    if (traitType.Name != traitTypeName) continue;
                    traitTypes.Data.Remove(traitType);
                    toRemove = traitType;
                    break;
                }
            }
            else
            {
                // update trait values
                SetTraitValues(tick, traitTypeName, toAddTraitValues, config);
            }
        }
        else
        {
            // trait type not exist,add.
            CheckAttributeInfo(toAddTraitType);
            traitTypes.Data.Add(toAddTraitType);
            Assert(toAddTraitValues != null && toAddTraitValues.Data.Count > 0, "Invalid input trait values.");
            SetTraitValues(tick, traitTypeName, toAddTraitValues, config);
        }

        return traitTypes;
    }

    #endregion

    #region Attribute param check

    private void CheckAttributeListDuplicate(List<AttributeSet> attributeSets)
    {
        var unique = new HashSet<string>();
        foreach (var set in attributeSets)
        {
            Assert(unique.Add(set.TraitType.Name), "Duplicate attribute type.");
        }
    }

    private void CheckAttributeList(List<AttributeSet> fixedAttributeSets,
        List<AttributeSet> randomAttributeSets)
    {
        Assert(fixedAttributeSets != null && randomAttributeSets != null, "Invalid input attribute list.");
        var fixedCount = fixedAttributeSets.Count;
        var randomCount = randomAttributeSets.Count;
        var config = State.Config?.Value;
        var traitTypeMaxCount = config?.TraitTypeMaxCount ??
                                SchrodingerContractConstants.DefaultMaxAttributeTraitTypeCount;
        var fixedTraitTypeMaxCount = config?.FixedTraitTypeMaxCount ??
                                     SchrodingerContractConstants.DefaultFixedTraitTypeMaxCount;
        Assert(fixedCount > 0 && fixedCount <= fixedTraitTypeMaxCount, "Invalid input fixed attribute list count.");
        Assert(randomCount > 0, "Invalid input random attribute list count.");
        Assert(fixedCount.Add(randomCount) <= traitTypeMaxCount, "Fixed and random list exceed.");
        CheckAttributeListDuplicate(fixedAttributeSets.Concat(randomAttributeSets).ToList());
    }

    private void CheckTraitTypeCount(int fixedCount, int randomCount)
    {
        var traitTypeMaxCount = State.Config?.Value?.TraitTypeMaxCount ??
                                SchrodingerContractConstants.DefaultMaxAttributeTraitTypeCount;
        Assert(fixedCount.Add(randomCount) <= traitTypeMaxCount, "Fixed and random list exceed.");
    }

    private int CheckAndGetFixedAttributesCount<T>(List<T> traitTypes)
    {
        var fixedCount = traitTypes.Count;
        var fixedTraitTypeMaxCount = State.Config.Value?.FixedTraitTypeMaxCount ??
                                     SchrodingerContractConstants.DefaultFixedTraitTypeMaxCount;
        Assert(fixedCount > 0 && fixedCount <= fixedTraitTypeMaxCount, "Invalid fixed trait type list count.");
        return fixedCount;
    }

    private int CheckAndGetRandomAttributesCount<T>(List<T> traitTypes)
    {
        var randomCount = traitTypes.Count;
        Assert(randomCount > 0, "Invalid input random attribute list count.");
        return randomCount;
    }

    private void CheckRandomAttributeList(List<AttributeInfo> randomAttributes, long maxGen,
        int attributesPerGen)
    {
        Assert(randomAttributes?.Count >= ((long)attributesPerGen).Mul(maxGen),
            "Invalid random attribute list count.");
    }

    private void CheckAttributePerGen(int attributesPerGen, int maxGen)
    {
        var config = State.Config?.Value;
        var maxAttributePerGen = config?.MaxAttributesPerGen ?? SchrodingerContractConstants.DefaultMaxAttributePerGen;
        Assert(attributesPerGen > 0 && attributesPerGen <= maxGen,
            "Invalid attributes per gen.");
        Assert(attributesPerGen <= maxAttributePerGen, "Attributes per generation need smaller than max.");
    }

    private void CheckAttributeInfo(AttributeInfo attributeInfo)
    {
        var attributeMaxLength =
            State.Config?.Value?.AttributeMaxLength ?? SchrodingerContractConstants.DefaultAttributeMaxLength;
        Assert(IsStringValid(attributeInfo.Name), "Invalid trait type name.");
        Assert(attributeInfo.Name.Length <= attributeMaxLength, "Invalid trait type name length.");
        CheckWeight(attributeInfo.Weight);
    }


    private void CheckWeight(long weight)
    {
        Assert(weight >= 0 && weight <= SchrodingerContractConstants.DefaultMaxWeight, "Invalid weight.");
    }

    private void CheckTraitTypeExist(string traitTypeName, AttributeInfos traitTypeMap)
    {
        Assert(traitTypeMap.Data.Select(t => t.Name).Contains(traitTypeName), "Trait type not exist.");
    }

    #endregion

    #region Param check

    private void CheckDeployParams(DeployInput input)
    {
        CheckInitialized();
        Assert(IsStringValid(input.Tick), "Invalid input tick.");
        Assert(input.Decimals >= 0, "Invalid input decimals.");
        Assert(input.TotalSupply > 0, "Invalid input total supply.");

        CheckRate(input.LossRate, input.CommissionRate, input.MaxGenLossRate);
        CheckDeployPermission(input.Tick);
        CheckGeneration(input.MaxGeneration);
        CheckAttributePerGen(input.AttributesPerGen, input.MaxGeneration);
        CheckImageSize(input.Image, input.ImageUri);
        CheckImageCount(input.ImageCount);
        CheckAndSetCrossGenerationConfig(input.Tick, input.CrossGenerationConfig, input.MaxGeneration);

        Assert(IsAddressValid(input.Signatory), "Invalid input signatory.");
        Assert(IsStringValid(input.ImageUri), "Invalid input image uri.");
    }

    private void CheckRate(long lossRate, long commissionRate, long maxGenLossRate)
    {
        Assert(lossRate >= 0 && lossRate <= SchrodingerContractConstants.Denominator, "Invalid loss rate.");
        Assert(commissionRate >= 0 && commissionRate <= SchrodingerContractConstants.Denominator,
            "Invalid commission rate.");
        Assert(maxGenLossRate >= 0 && maxGenLossRate <= SchrodingerContractConstants.Denominator,
            "Invalid max gen loss rate.");
    }

    private void CheckDeployPermission(string tick)
    {
        SetTokenContract();
        var issuer = State.TokenContract.GetTokenInfo.Call(new GetTokenInfoInput
        {
            Symbol = GetInscriptionCollectionSymbol(tick)
        }).Issuer;
        Assert(issuer == Context.Sender, "No permission to create.");
    }

    private void CheckGeneration(int maxGen)
    {
        var config = State.Config?.Value;
        var max = config?.MaxGen ?? SchrodingerContractConstants.DefaultMaxGen;
        Assert(maxGen >= SchrodingerContractConstants.DefaultMinGen && maxGen <= max, "Invalid max generation.");
    }

    private void CheckImageSize(string image, string imageUri)
    {
        var config = State.Config?.Value;
        var maxImageSize = config?.ImageMaxSize ?? SchrodingerContractConstants.DefaultImageMaxSize;
        var maxImageUriSize = config?.ImageUriMaxSize ?? SchrodingerContractConstants.DefaultImageUriMaxSize;
        Assert(IsStringValid(image) && Encoding.UTF8.GetByteCount(image) <= maxImageSize,
            "Invalid image data.");
        Assert(IsStringValid(imageUri) && imageUri.Length <= maxImageUriSize, "Invalid image uri.");
    }

    private void CheckImageCount(int imageCount)
    {
        var config = State.Config?.Value;
        var maxImageCount = config?.ImageMaxCount ?? SchrodingerContractConstants.DefaultImageMaxCount;
        Assert(imageCount > 0 && imageCount <= maxImageCount, "Invalid image count.");
    }

    private void CheckAndSetCrossGenerationConfig(string tick, CrossGenerationConfig crossGenerationConfig, int maxGen)
    {
        Assert(crossGenerationConfig.Gen >= 0 && crossGenerationConfig.Gen <= maxGen,
            "Invalid cross generation config gen.");
        Assert(crossGenerationConfig.CrossGenerationProbability >= 0 &&
               crossGenerationConfig.CrossGenerationProbability <= SchrodingerContractConstants.Denominator,
            "Invalid cross generation probability.");
        Assert(crossGenerationConfig.Weights.Count == crossGenerationConfig.Gen, "Invalid cross generation weights.");
        var totalWeights = 0L;
        foreach (var weight in crossGenerationConfig.Weights)
        {
            CheckWeight(weight);
            totalWeights += weight;
        }

        State.GenTotalWeightsMap[tick] = totalWeights;
    }

    #endregion
}