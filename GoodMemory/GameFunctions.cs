using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace GoodMemory {
    public class GameFunctions {
        private readonly Plugin plugin;

        private delegate byte HasItemActionUnlockedDelegate(long itemActionId);
        private readonly HasItemActionUnlockedDelegate hasItemActionUnlocked;

        private delegate byte HasCardDelegate(IntPtr localPlayer, ushort cardId);
        private readonly HasCardDelegate hasCard;

        private readonly IntPtr cardStaticAddr;

        public GameFunctions(Plugin plugin) {
            this.plugin = plugin ?? throw new ArgumentNullException(nameof(plugin), "Plugin cannot be null");

            IntPtr hasIAUnlockedPtr = plugin.Interface.TargetModuleScanner.ScanText("48 83 EC 28 E8 ?? ?? ?? ?? 48 85 C0 0F 84 ?? ?? ?? ??");
            IntPtr hasCardPtr = plugin.Interface.TargetModuleScanner.ScanText("40 53 48 83 EC 20 48 8B D9 66 85 D2 74 ??");
            this.cardStaticAddr = plugin.Interface.TargetModuleScanner.GetStaticAddressFromSig("48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 84 C0 74 ?? 48 8B 53 ?? 48 8D 4B ?? 48 83 C2 0C 48 8D 14 ?? E8 ?? ?? ?? ?? 40 FE C7 40 3A FD 72 ?? 48 8B 5C 24 ??");

            if (hasIAUnlockedPtr == IntPtr.Zero || hasCardPtr == IntPtr.Zero || this.cardStaticAddr == IntPtr.Zero) {
                throw new ApplicationException("Could not get pointers for game functions");
            }

            this.hasItemActionUnlocked = Marshal.GetDelegateForFunctionPointer<HasItemActionUnlockedDelegate>(hasIAUnlockedPtr);
            this.hasCard = Marshal.GetDelegateForFunctionPointer<HasCardDelegate>(hasCardPtr);
        }

        public bool HasAcquired(Item item) {
            ItemAction action = item.ItemAction.Value;

            if (action == null) {
                return false;
            }

            ActionType type = (ActionType)action.Type;

            if (type == ActionType.Cards) {
                uint cardId = item.AdditionalData;
                TripleTriadCard card = this.plugin.Interface.Data.GetExcelSheet<TripleTriadCard>().GetRow(cardId);
                if (card == null) {
                    return false;
                }
                return this.HasCard((ushort)card.RowId);
            }

            return this.HasItemActionUnlocked(action.RowId);
        }

        private bool HasItemActionUnlocked(long itemActionId) {
            return this.hasItemActionUnlocked(itemActionId) == 1;
        }

        private bool HasCard(ushort cardId) {
            return this.hasCard(this.cardStaticAddr, cardId) == 1;
        }
    }
}
