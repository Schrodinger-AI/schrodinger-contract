using System.Linq;
using AElf.Sdk.CSharp;
using Google.Protobuf.WellKnownTypes;

namespace Schrodinger;

public partial class SchrodingerContract
{
    public override Empty SetAirdropAdmin(SetAirdropAdminInput input)
    {
        Assert(input != null, "Invalid input.");
        Assert(IsStringValid(input!.Tick), "Invalid tick.");
        Assert(IsAddressValid(input.Admin), "Invalid admin.");

        CheckInscriptionExistAndPermission(input.Tick);
        
        if (State.AirdropAdminMap[input.Tick] == input.Admin) return new Empty();
        
        State.AirdropAdminMap[input.Tick] = input.Admin;
        
        Context.Fire(new AirdropAdminSet
        {
            Tick = input.Tick,
            Admin = input.Admin
        });
        
        return new Empty();
    }

    public override Empty AirdropVoucher(AirdropVoucherInput input)
    {
        Assert(input != null, "Invalid input.");
        Assert(IsStringValid(input!.Tick), "Invalid tick.");
        Assert(input.List != null && input.List.Count > 0, "Invalid list.");
        Assert(input.Amount > 0, "Invalid amount.");
        
        var inscriptionInfo = State.InscriptionInfoMap[input.Tick];
        Assert(inscriptionInfo != null, "Tick not deployed.");
        
        var admin = State.AirdropAdminMap[input.Tick] ?? inscriptionInfo!.Admin;
        
        Assert(admin == Context.Sender, "No permission.");

        var list = input.List!.Distinct().ToList();

        foreach (var address in list)
        {
            Assert(IsAddressValid(address), "Invalid address.");
            State.AdoptionVoucherMap[input.Tick][address] += input.Amount;
        }
        
        Context.Fire(new VoucherAirdropped
        {
            Tick = input.Tick,
            List = new AddressList{Data = { list }},
            Amount = input.Amount
        });
        
        return new Empty();
    }
}