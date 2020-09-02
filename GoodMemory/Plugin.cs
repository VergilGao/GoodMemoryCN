using Dalamud.Hooking;
using Dalamud.Plugin;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace GoodMemory {
    public class Plugin : IDalamudPlugin {
        private bool disposedValue;

        public string Name => "Good Memory";

        public DalamudPluginInterface Interface { get; private set; }
        private GameFunctions Functions { get; set; }
        private readonly IntPtr alloc = Marshal.AllocHGlobal(4096);
        private Hook<TooltipDelegate> tooltipHook;

        private delegate IntPtr TooltipDelegate(IntPtr a1, IntPtr a2, IntPtr a3);

        public void Initialize(DalamudPluginInterface pluginInterface) {
            this.Interface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface), "DalamudPluginInterface cannot be null");
            this.Functions = new GameFunctions(this);
            this.SetUpHook();
        }

        protected virtual void Dispose(bool disposing) {
            if (!this.disposedValue) {
                if (disposing) {
                    this.tooltipHook?.Dispose();
                    Marshal.FreeHGlobal(this.alloc);
                }

                this.disposedValue = true;
            }
        }

        public void Dispose() {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        private void SetUpHook() {
            IntPtr tooltipPtr = this.Interface.TargetModuleScanner.ScanText("48 89 5C 24 ?? 55 56 57 41 54 41 55 41 56 41 57 48 83 EC 50 48 8B 42 ??");
            if (tooltipPtr == IntPtr.Zero) {
                throw new ApplicationException("Could not set up tooltip hook because of null pointer");
            }
            this.tooltipHook = new Hook<TooltipDelegate>(tooltipPtr, new TooltipDelegate(this.OnTooltip));
            this.tooltipHook.Enable();
        }

        private IntPtr OnTooltip(IntPtr a1, IntPtr a2, IntPtr a3) {
            IntPtr v3 = Marshal.ReadIntPtr(a2 + 32);
            uint v9 = (uint)Marshal.ReadInt32(v3 + 16);

            if ((v9 & 2) == 0) {
                goto Return;
            }

            ulong itemId = this.Interface.Framework.Gui.HoveredItem;
            if (itemId > 2_000_000) {
                goto Return;
            } else if (itemId > 1_000_000) {
                itemId -= 1_000_000;
            }

            Item item = this.Interface.Data.GetExcelSheet<Item>().GetRow((uint)itemId);

            if (item == null) {
                goto Return;
            }

            // get the pointer to the text
            IntPtr startPtr = Marshal.ReadIntPtr(a3 + 32) + 104;
            // get the text pointer
            IntPtr start = Marshal.ReadIntPtr(startPtr);

            // work around function being called twice
            if (start == this.alloc) {
                goto Return;
            }

            string overwrite;

            // Faded Copies
            if (item.FilterGroup == 12 && item.ItemUICategory.Value?.RowId == 94 && item.LevelItem.Value?.RowId == 1) {
                Item[] recipeResults = this.Interface.Data.GetExcelSheet<Recipe>()
                    .Where(recipe => recipe.UnkStruct5.Any(ritem => ritem.ItemIngredient == item.RowId))
                    .Select(recipe => recipe.ItemResult.Value)
                    .Where(result => result != null)
                    .ToArray();

                overwrite = ReadString(start);

                foreach (Item result in recipeResults) {
                    ItemAction resultAction = result.ItemAction.Value;
                    if (!ActionTypeExt.IsValidAction(resultAction)) {
                        continue;
                    }

                    this.AppendIfAcquired(ref overwrite, result, true);
                }
            } else {
                ItemAction action = item.ItemAction?.Value;

                if (!ActionTypeExt.IsValidAction(action)) {
                    goto Return;
                }

                // get the text
                overwrite = ReadString(start);

                // generate our replacement text
                this.AppendIfAcquired(ref overwrite, item);
            }

            // write our replacement text into our own managed memory (4096 bytes)
            WriteString(this.alloc, overwrite, true);

            // overwrite the original pointer with our own
            Marshal.WriteIntPtr(startPtr, this.alloc);

        Return:
            return this.tooltipHook.Original(a1, a2, a3);
        }

        private void AppendIfAcquired(ref string txt, Item item, bool useItem = false) {
            string has = this.Functions.HasAcquired(item) ? "Yes" : "No";
            if (useItem) {
                txt = $"{txt}\nAcquired ({item.Name}): {has}";
            } else {
                txt = $"{txt}\nAcquired: {has}";
            }
        }

        private static string ReadString(IntPtr ptr) {
            int offset = 0;
            List<byte> stringBytes = new List<byte>();
            while (true) {
                byte b = Marshal.ReadByte(ptr + offset);
                if (b == 0) {
                    break;
                }
                stringBytes.Add(b);
                offset += 1;
            }
            return Encoding.UTF8.GetString(stringBytes.ToArray());
        }

        private static void WriteString(IntPtr dst, string s, bool finalise = false) {
            byte[] bytes = Encoding.UTF8.GetBytes(s);
            Marshal.Copy(bytes, 0, dst, bytes.Length);
            if (finalise) {
                Marshal.WriteByte(dst + bytes.Length, 0);
            }
        }
    }
}
