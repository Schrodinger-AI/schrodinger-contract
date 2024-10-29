using System.Collections.Generic;
using System.Linq;
using AElf.Sdk.CSharp;
using AElf.Types;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;

namespace Schrodinger;

public partial class SchrodingerContract
{
    public override Empty AirdropVoucher(AirdropVoucherInput input)
    {
        Assert(input != null, "Invalid input.");
        Assert(IsStringValid(input!.Tick), "Invalid tick.");
        Assert(input.List != null && input.List.Count > 0, "Invalid list.");
        Assert(input.Amount > 0, "Invalid amount.");

        var inscriptionInfo = State.InscriptionInfoMap[input.Tick];
        Assert(inscriptionInfo != null, "Tick not deployed.");

        var controller = State.AirdropControllerMap[input.Tick];
        Assert(
            (controller == null && Context.Sender == inscriptionInfo!.Admin) ||
            State.AirdropControllerMap[input.Tick].Data.Contains(Context.Sender), "No permission.");

        var list = input.List!.Distinct().ToList();

        foreach (var address in list)
        {
            Assert(IsAddressValid(address), "Invalid address.");
            State.AdoptionVoucherMap[input.Tick][address] += input.Amount;
        }

        Context.Fire(new VoucherAirdropped
        {
            Tick = input.Tick,
            List = new AddressList { Data = { list } },
            Amount = input.Amount
        });

        return new Empty();
    }

    public override Empty AddAirdropController(AddAirdropControllerInput input)
    {
        Assert(input != null, "Invalid input.");
        Assert(IsStringValid(input!.Tick), "Invalid tick.");
        Assert(input.List != null && input.List.Count > 0, "Invalid list.");

        CheckInscriptionExistAndPermission(input.Tick);

        var list = input.List!.Distinct().ToList();
        var added = new List<Address>();

        var controller = State.AirdropControllerMap[input.Tick]?.Data ?? new RepeatedField<Address>();

        foreach (var address in list)
        {
            Assert(IsAddressValid(address), "Invalid address.");

            if (controller.Contains(address)) continue;
            controller.Add(address);
            added.Add(address);
        }

        if (added.Count == 0) return new Empty();

        State.AirdropControllerMap[input.Tick] = new AddressList { Data = { controller } };
        
        Context.Fire(new AirdropControllerAdded
        {
            Tick = input.Tick,
            Addresses = new AddressList { Data = { added } }
        });

        return new Empty();
    }

    public override Empty RemoveAirdropController(RemoveAirdropControllerInput input)
    {
        Assert(input != null, "Invalid input.");
        Assert(IsStringValid(input!.Tick), "Invalid tick.");
        Assert(input.List != null && input.List.Count > 0, "Invalid list.");
        
        CheckInscriptionExistAndPermission(input.Tick);

        var list = input.List!.Distinct().ToList();
        var removed = new List<Address>();
        
        Assert(State.AirdropControllerMap[input.Tick] != null, "Controller not set before.");

        var controller = State.AirdropControllerMap[input.Tick].Data;

        foreach (var address in list)
        {
            Assert(IsAddressValid(address), "Invalid address.");

            if (!controller.Contains(address)) continue;
            
            controller.Remove(address);
            removed.Add(address);
        }
        
        if (removed.Count == 0) return new Empty();
        
        Context.Fire(new AirdropControllerRemoved
        {
            Tick = input.Tick,
            Addresses = new AddressList { Data = { removed } }
        });

        return new Empty();
    }
}