using AElf.Sdk.CSharp;
using Google.Protobuf.WellKnownTypes;

namespace Schrodinger;

public partial class SchrodingerContract
{
    public override Empty SetRebateConfig(SetRebateConfigInput input)
    {
        Assert(input != null, "Invalid input.");
        Assert(IsStringValid(input!.Tick), "Invalid tick.");

        CheckInscriptionExistAndPermission(input.Tick);

        var rebateConfig = new RebateConfig
        {
            Intervals = { input.Intervals },
            InputAmount = input.InputAmount
        };
        
        State.RebateConfig[input.Tick] = rebateConfig;

        Context.Fire(new RebateConfigSet
        {
            Tick = input.Tick,
            Config = rebateConfig
        });

        return new Empty();
    }
}