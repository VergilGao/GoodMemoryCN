using Lumina.Excel.GeneratedSheets;
using System;
using System.Runtime.InteropServices;

namespace GoodMemory {
    public class GameFunctions {
        private Plugin Plugin { get; }

        private delegate byte HasItemActionUnlockedDelegate(long itemActionId);

        private readonly HasItemActionUnlockedDelegate _hasItemActionUnlocked;

        private delegate byte HasCardDelegate(IntPtr localPlayer, ushort cardId);

        private readonly HasCardDelegate _hasCard;

        private readonly IntPtr _cardStaticAddr;

        public GameFunctions(Plugin plugin) {
            this.Plugin = plugin ?? throw new ArgumentNullException(nameof(plugin), "Plugin cannot be null");

            var hasIaUnlockedPtr = plugin.Interface.TargetModuleScanner.ScanText("48 83 EC 28 E8 ?? ?? ?? ?? 48 85 C0 0F 84 ?? ?? ?? ??");
            var hasCardPtr = plugin.Interface.TargetModuleScanner.ScanText("40 53 48 83 EC 20 48 8B D9 66 85 D2 74 ??");
            this._cardStaticAddr = plugin.Interface.TargetModuleScanner.GetStaticAddressFromSig("41 0F B7 17 48 8D 0D ?? ?? ?? ??");

            if (hasIaUnlockedPtr == IntPtr.Zero || hasCardPtr == IntPtr.Zero || this._cardStaticAddr == IntPtr.Zero) {
                throw new ApplicationException("Could not get pointers for game functions");
            }

            this._hasItemActionUnlocked = Marshal.GetDelegateForFunctionPointer<HasItemActionUnlockedDelegate>(hasIaUnlockedPtr);
            this._hasCard = Marshal.GetDelegateForFunctionPointer<HasCardDelegate>(hasCardPtr);
        }

        public bool HasAcquired(Item item) {
            var action = item.ItemAction.Value;

            if (action == null) {
                return false;
            }

            var type = (ActionType) action.Type;

            if (type != ActionType.Cards) {
                return this.HasItemActionUnlocked(action.RowId);
            }

            var cardId = item.AdditionalData;
            var card = this.Plugin.Interface.Data.GetExcelSheet<TripleTriadCard>().GetRow(cardId);
            return card != null && this.HasCard((ushort) card.RowId);
        }

        private bool HasItemActionUnlocked(long itemActionId) {
            return this._hasItemActionUnlocked(itemActionId) == 1;
        }

        private bool HasCard(ushort cardId) {
            return this._hasCard(this._cardStaticAddr, cardId) == 1;
        }
    }
}
