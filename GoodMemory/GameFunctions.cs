using System;
using System.Runtime.InteropServices;
using Lumina.Excel.GeneratedSheets;

namespace GoodMemory;

public class GameFunctions
{
    private readonly IntPtr _cardStaticAddr;

    private readonly HasCardDelegate _hasCard;

    private readonly HasItemActionUnlockedDelegate _hasItemActionUnlocked;

    private readonly ItemToUlongDelegate _itemToUlong;

    public GameFunctions()
    {
        var hasIaUnlockedPtr = Service.SigScanner.ScanText("E8 ?? ?? ?? ?? 84 C0 75 A6 32 C0");
        var hasCardPtr = Service.SigScanner.ScanText("40 53 48 83 EC 20 48 8B D9 66 85 D2 74");
        var itemToUlongPtr = Service.SigScanner.ScanText("E8 ?? ?? ?? ?? 48 85 C0 74 33 83 7F 04 00");

        _cardStaticAddr = Service.SigScanner.GetStaticAddressFromSig("41 0F B7 17 48 8D 0D");

        if (hasIaUnlockedPtr == IntPtr.Zero || hasCardPtr == IntPtr.Zero || _cardStaticAddr == IntPtr.Zero || itemToUlongPtr == IntPtr.Zero)
            throw new ApplicationException("Could not get pointers for game functions");

        _hasItemActionUnlocked = Marshal.GetDelegateForFunctionPointer<HasItemActionUnlockedDelegate>(hasIaUnlockedPtr);
        _hasCard = Marshal.GetDelegateForFunctionPointer<HasCardDelegate>(hasCardPtr);
        _itemToUlong = Marshal.GetDelegateForFunctionPointer<ItemToUlongDelegate>(itemToUlongPtr);
    }

    public bool HasAcquired(Item item)
    {
        var action = item.ItemAction.Value;

        if (action == null) return false;

        var type = (ActionType)action.Type;

        if (type != ActionType.Cards) return HasItemActionUnlocked(item);

        var cardId = item.AdditionalData;
        var card = Service.DataManager.GetExcelSheet<TripleTriadCard>()!.GetRow(cardId);
        return card != null && HasCard((ushort)card.RowId);
    }

    private unsafe bool HasItemActionUnlocked(Item item)
    {
        var itemAction = item.ItemAction.Value;
        if (itemAction == null) return false;

        var result = _itemToUlong(item.RowId);
        if (result == 0)
            return false;

        var type = (ActionType)itemAction.Type;

        var mem = Marshal.AllocHGlobal(256);
        *(uint*)(mem + 142) = itemAction.RowId;

        if (type == ActionType.OrchestrionRolls) *(ushort*)(mem + 112) = (ushort)item.AdditionalData;

        var ret = _hasItemActionUnlocked(result, 0, (long*)mem) == 1;

        Marshal.FreeHGlobal(mem);

        return ret;
    }

    private bool HasCard(ushort cardId)
    {
        return _hasCard(_cardStaticAddr, cardId) == 1;
    }

    private delegate long ItemToUlongDelegate(uint a1);

    private unsafe delegate byte HasItemActionUnlockedDelegate(long mem, long a2, long* a3);

    private delegate byte HasCardDelegate(IntPtr localPlayer, ushort cardId);
}