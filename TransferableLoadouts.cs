using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Terraria.Audio;
using Terraria.GameContent.Drawing;
using Terraria.ID;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;
using Terraria.ModLoader.Default;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using Newtonsoft.Json.Linq;
using ReLogic.Content;
using Terraria.Localization;
using System.Collections.ObjectModel;
using Terraria.ModLoader.IO;

namespace TransferableLoadouts
{
    public class TransferableLoadouts : Mod
    {
        public static List<string> incompatibleModNames = [];
        public override void Load()
        {
            // The type that contains the field we want to change.
            var itemSlotType = typeof(ItemSlot);

            // Get the FieldInfo for the private static field "canFavoriteAt".
            // We use BindingFlags to find non-public (private) and static members.
            FieldInfo canFavoriteAtField = itemSlotType.GetField("canFavoriteAt", BindingFlags.NonPublic | BindingFlags.Static);

            // It's good practice to check if the field was found.
            // This prevents your mod from crashing if a future game update renames or removes the field.
            if (canFavoriteAtField != null)
            {
                // Get the actual boolean array from the field.
                // For static fields, the first argument of GetValue is always null.
                bool[] canFavoriteAt = (bool[])canFavoriteAtField.GetValue(null);

                // Now, modify the array to allow favoriting in equipment slots.
                // Using the ItemSlot.Context constants makes the code readable and safe from number changes.
                canFavoriteAt[ItemSlot.Context.EquipArmor] = true;
                canFavoriteAt[ItemSlot.Context.EquipArmorVanity] = true;
                canFavoriteAt[ItemSlot.Context.EquipAccessory] = true;
                canFavoriteAt[ItemSlot.Context.EquipAccessoryVanity] = true;
                canFavoriteAt[ItemSlot.Context.EquipDye] = true;

                //Second page of equips
                //canFavoriteAt[ItemSlot.Context.EquipGrapple] = true;
                //canFavoriteAt[ItemSlot.Context.EquipMount] = true;
                //canFavoriteAt[ItemSlot.Context.EquipMinecart] = true;
                //canFavoriteAt[ItemSlot.Context.EquipPet] = true;
                //canFavoriteAt[ItemSlot.Context.EquipLight] = true;

                // Since we modified the array object directly, we don't need to call SetValue.
                // The changes are already applied to the game's instance of the array.

                Logger.Info("Successfully modified ItemSlot.canFavoriteAt using Reflection.");
            }
            else
            {
                Logger.Error("Could not find field 'canFavoriteAt' in Terraria.UI.ItemSlot. This may be due to a game update.");
            }
            incompatibleModNames = GetIncompatibleModNames();
            bool noIncompatibilities = incompatibleModNames.Count == 0;

            if (noIncompatibilities)
                On_Player.TrySwitchingLoadout += TrySwitchingLoadoutWithFavorites;

            On_ItemSlot.Draw_SpriteBatch_ItemArray_int_int_Vector2_Color += DrawWithFavoritedOverlay;
            On_ItemSlot.LeftClick_ItemArray_int_int += DontUnfavorite1;//WhenLeftClickEquipping;
            On_ItemSlot.ArmorSwap += DontUnfavorite2; //whenrightClickingInventoryItemToArmor"
            //On_ItemSlot.EquipSwap += DontUnfavorite3; // whenrightclickinginventoryitemto"BonusEquips"
        }
        public static List<string> GetIncompatibleModNames()
        {
            List<string> modNames = [];
            if (ModLoader.TryGetMod("ExtraLoadouts", out Mod extraLoadouts))
                modNames.Add($"{extraLoadouts.DisplayName} ({extraLoadouts.Name})");
            return modNames;
        }
        private void DontUnfavorite1(On_ItemSlot.orig_LeftClick_ItemArray_int_int orig, Item[] inv, int context, int slot)
        {
            Item oldMouseItem = Main.mouseItem.Clone();
            bool triedToEquipItem = !Main.mouseItem.IsAir && Main.mouseLeftRelease && Main.mouseLeft && Main.cursorOverride == -1
                && context is ItemSlot.Context.EquipArmor or ItemSlot.Context.EquipAccessory or ItemSlot.Context.EquipArmorVanity or ItemSlot.Context.EquipAccessoryVanity or ItemSlot.Context.EquipDye;

            bool mouseItemWasFavorited = Main.mouseItem.favorited;
            bool slotItemWasFavorited = inv[slot].favorited;

            orig(inv, context, slot);

            if (triedToEquipItem) //vanilla tries to unfavorite even if it doesn't and can't actually equip (e.g, wrong slot)
            {
                bool failedToEquip = oldMouseItem.IsTheSameAs(Main.mouseItem) && !Main.mouseItem.IsTheSameAs(inv[slot]); //sort of fragile, assumes swaps of same item type will always work
                if (failedToEquip)
                {
                    Main.mouseItem.favorited = mouseItemWasFavorited;
                    inv[slot].favorited = slotItemWasFavorited;
                }
                else
                    inv[slot].favorited = mouseItemWasFavorited;
            } //we love using detours instead of IL
        }

        private Item DontUnfavorite2(On_ItemSlot.orig_ArmorSwap orig, Item item, out bool success)
        {
            bool wasFavorited = item.favorited;
            Item result = orig(item, out success);
            if (success && wasFavorited && Main.LocalPlayer.armor.FirstOrDefault(item.IsTheSameAs) is Item equippedItem)
                Main.LocalPlayer.armor.First(equippedItem => item.IsTheSameAs(equippedItem)).favorited = true;
            return result;
        }

        private void DrawWithFavoritedOverlay(On_ItemSlot.orig_Draw_SpriteBatch_ItemArray_int_int_Vector2_Color orig, SpriteBatch spriteBatch, Item[] inv, int context, int slot, Vector2 position, Color lightColor)
        {
            orig(spriteBatch, inv, context, slot, position, lightColor);
            if (context is ItemSlot.Context.EquipArmor or ItemSlot.Context.EquipAccessory or ItemSlot.Context.EquipArmorVanity or ItemSlot.Context.EquipAccessoryVanity or ItemSlot.Context.EquipDye
                && inv[slot].favorited)
            {
                inv[slot].favorited = false;
                    orig(spriteBatch, inv, context, slot, position, lightColor);
                inv[slot].favorited = true; //Could This Be The Hackiest Code Of All Time?

                Color borderColor = ItemSlot.GetColorByLoadout(slot, context).Brighten(1.6f);
                Texture2D tex = ModContent.Request<Texture2D>("TransferableLoadouts/Inventory_Back13_FavoriteOverlay", AssetRequestMode.ImmediateLoad).Value;
                spriteBatch.Draw(tex, position, null, borderColor, 0f, default, Main.inventoryScale, SpriteEffects.None, 0f);
            }
            //else
                //orig(spriteBatch, inv, context, slot, position, lightColor);
        }

        public void TrySwitchingLoadoutWithFavorites(On_Player.orig_TrySwitchingLoadout orig, Player player, int loadoutIndex)//int loadoutIndex, Player player)
        {
            //TODO: possible design solution: only favorite one accessory per slot, or first loadout only. but then what if you have, e.g, wings,
            //but you want to replace the wings with boots in the second slot if they're incompatible? just dont do that and use the same slot for wings always, i guess

            if (player.whoAmI != Main.myPlayer || player.itemTime > 0 || player.itemAnimation > 0 || player.CCed || player.dead) return;
            if (loadoutIndex == player.CurrentLoadoutIndex || loadoutIndex < 0 || loadoutIndex >= player.Loadouts.Length) return;

            int currentLoadoutIndex = player.CurrentLoadoutIndex;
            EquipmentLoadout oldLoadout = player.Loadouts[currentLoadoutIndex];
            EquipmentLoadout newLoadout = player.Loadouts[loadoutIndex];

            // --- Step 1: Place equips back into their loadout and clear the player's equips. ---
            for (int i = 0; i < oldLoadout.Armor.Length; i++) { oldLoadout.Armor[i] = player.armor[i].Clone(); player.armor[i].TurnToAir(true); } //im only certain this is necessary for armor slots, not dye slots
            for (int i = 0; i < oldLoadout.Dye.Length; i++) { oldLoadout.Dye[i] = player.dye[i].Clone(); player.dye[i].TurnToAir(true); }
            for (int i = 0; i < oldLoadout.Hide.Length; i++) { oldLoadout.Hide[i] = player.hideVisibleAccessory[i]; }
            // --- Step 2: Equip the new loadout and empty it. ---
            for (int i = 0; i < newLoadout.Armor.Length; i++) { if (!newLoadout.Armor[i].IsAir) { player.armor[i] = newLoadout.Armor[i].Clone(); newLoadout.Armor[i].TurnToAir(true); } }
            for (int i = 0; i < newLoadout.Dye.Length; i++) { if (!newLoadout.Dye[i].IsAir) { player.dye[i] = newLoadout.Dye[i].Clone(); newLoadout.Dye[i].TurnToAir(true); } }
            for (int i = 0; i < newLoadout.Hide.Length; i++) { player.hideVisibleAccessory[i] = newLoadout.Hide[i]; }

            // --- Step 3: Fill empty slots with the first valid favorited item. ---
            for (int i = 0; i < player.armor.Length; i++)
            {
                if (player.armor[i].IsAir)
                {
                    for (int j = 0; j < player.Loadouts.Length; j++)
                    {
                        Item potentialItem = player.Loadouts[j].Armor[i];
                        if (potentialItem.IsAir || !potentialItem.favorited) continue;

                        bool vanity = i >= 10;
                        bool canEquip = true;

                        if (potentialItem.accessory && !IsAccessoryCompatible(player, potentialItem, vanity))
                            canEquip = false;

                        if (canEquip)
                        {
                            player.armor[i] = potentialItem.Clone();
                            potentialItem.TurnToAir(true); //probably also here as well! actually we can just swap here instead of cloning?
                            break;
                        }
                    }
                }
            }

            // Dyes
            for (int i = 0; i < player.dye.Length; i++)
            {
                if (player.dye[i].IsAir)
                {
                    for (int j = 0; j < player.Loadouts.Length; j++)
                    {
                        Item potentialDye = player.Loadouts[j].Dye[i];
                        if (!potentialDye.IsAir && potentialDye.favorited)
                        {
                            player.dye[i] = potentialDye.Clone();
                            potentialDye.TurnToAir(true); //may not need dye
                            break;
                        }
                    }
                }
            }

            player.CurrentLoadoutIndex = loadoutIndex;
            if (player == Main.LocalPlayer) //Don't run this code if syncing on other clients
            {
                CloneLoadouts(player, Main.clientPlayer);
                Main.mouseLeftRelease = false;
                ItemSlot.RecordLoadoutChange();
                SoundEngine.PlaySound(SoundID.MenuTick);
                NetMessage.TrySendData(MessageID.SyncLoadout, -1, -1, null, player.whoAmI, loadoutIndex);
                ParticleOrchestrator.RequestParticleSpawn(clientOnly: false, ParticleOrchestraType.LoadoutChange, new ParticleOrchestraSettings
                {
                    PositionInWorld = player.Center,
                    UniqueInfoPiece = loadoutIndex
                }, player.whoAmI);
            }
        }

        
        //copied from Vanilla private method
        private static void CloneLoadouts(Player player, Player clonePlayer)
        {
            Item[] array = player.armor;
            Item[] array2 = clonePlayer.armor;
            for (int i = 0; i < array.Length; i++)
            {
                array[i].CopyNetStateTo(array2[i]);
            }
            array = player.dye;
            array2 = clonePlayer.dye;
            for (int j = 0; j < array.Length; j++)
            {
                array[j].CopyNetStateTo(array2[j]);
            }
            for (int k = 0; k < player.Loadouts.Length; k++)
            {
                array = player.Loadouts[k].Armor;
                array2 = clonePlayer.Loadouts[k].Armor;
                for (int l = 0; l < array.Length; l++)
                {
                    array[l].CopyNetStateTo(array2[l]);
                }
                array = player.Loadouts[k].Dye;
                array2 = clonePlayer.Loadouts[k].Dye;
                for (int m = 0; m < array.Length; m++)
                {
                    array[m].CopyNetStateTo(array2[m]);
                }
            }
        }


        /// <summary>
        /// Whether an equippable accessory can be put into a specific slot.
        /// This method assumes that the item is allowed to be generally equipped, since it is used on already equipped accessories.
        /// </summary>
        /// <param name="player">The player instance.</param>
        /// <param name="favoriteItem">The favorited accessory we are trying to equip.</param>
        /// <param name="vanity">Whether we're trying to place the item in an vanity slot or a functional slot.</param>
        /// <returns>True if the item can be equipped with the player's other equipment, false otherwise.</returns>
        private bool IsAccessoryCompatible(Player player, Item favoriteItem, bool vanity)
        {
            //NO accessory can be a duplicate, vanity or not
            for (int i = 0 ; i < player.armor.Length; i++)
            {
                if (!player.armor[i].IsAir && favoriteItem.IsTheSameAs(player.armor[i]))
                    return false;
            }

            int firstAccessoryIndex = (vanity ? 13 : 3);
            int lastAccessoryIndex = (vanity ? 19 : 9);

            //items of the same group (vanity/functional) can't both be wings/other modded restrictions
            for (int i = firstAccessoryIndex; i <= lastAccessoryIndex; i++)
            {
                Item equippedItem = player.armor[i];
                if (equippedItem.IsAir)
                    continue;

                // Can't equip two wings
                if (favoriteItem.wingSlot > 0 && equippedItem.wingSlot > 0)
                    return false;

                // General compatibility hook
                if (!ItemLoader.CanAccessoryBeEquippedWith(equippedItem, favoriteItem))
                    return false;
            }
            return true;
        }
    }
    public class TooltipChange : GlobalItem
    {
        //public static Item hoverItem;
        public bool worn = false;
        public override bool InstancePerEntity => true;

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
        {
            for (int i = 0; i < tooltips.Count; i++)
            {
                TooltipLine line = tooltips[i];

                if (line.Name == "FavoriteDesc" && worn)//(item.wornArmor || worn))
                    tooltips[i] = new TooltipLine(Mod, "FavoriteEquipDesc", Language.GetText("Mods.TransferableLoadouts.FavoriteEquipDesc").Value);
            }
        }
    }
    public class HoverItemTracker : ModSystem
    {
        public override void Load()
        {
            On_ItemSlot.MouseHover_ItemArray_int_int += GetHoverIndex;
        }
        //inv can be inventory, armor, shop, dye or hover item, or chest. we gotta use context.
        private void GetHoverIndex(On_ItemSlot.orig_MouseHover_ItemArray_int_int orig, Item[] inv, int context, int slot)
        {
            orig(inv, context, slot);
            if (context is ItemSlot.Context.EquipArmor or ItemSlot.Context.EquipAccessory or ItemSlot.Context.EquipArmorVanity or ItemSlot.Context.EquipAccessoryVanity or ItemSlot.Context.EquipDye
                && inv[slot].TryGetGlobalItem<TooltipChange>(out var global))
                global.worn = true;
        }
    }
    public class SaveFavoritedItems : ModPlayer //messing with vanilla I/O seems like a horrible idea
    {
        public override void LoadData(TagCompound tag)
        {
            var isFavoritedList = tag.GetList<bool>("favoritedEquips");
            Item[] allEquips = GetAllEquipsForAllLoadouts();
            for (int i = 0; i < allEquips.Length && i < isFavoritedList.Count; i++)
            {
                if (allEquips[i] is null || allEquips[i].IsAir)
                    continue;
                allEquips[i].favorited = isFavoritedList[i];
            }
        }
        public override void SaveData(TagCompound tag)// this WILL run when the game autosaves! it's not the same as onworldunload!
        {
            var list = new List<bool>();
            foreach (Item equip in GetAllEquipsForAllLoadouts())
            {
                list.Add(equip.favorited);
            }
            tag["favoritedEquips"] = list;
        }
        public Item[] GetAllEquipsForAllLoadouts()
        {
            List<Item> allEquips = [];
            for (int i = 0; i < Player.Loadouts.Length; i++)
            {
                if (Player.CurrentLoadoutIndex == i)
                {
                    allEquips.AddRange(Player.armor);
                    allEquips.AddRange(Player.dye);
                }
                else
                {
                    allEquips.AddRange(Player.Loadouts[i].Armor);
                    allEquips.AddRange(Player.Loadouts[i].Dye);
                }
            }
            return allEquips.ToArray();
        }
    }
    public static class Utils
    {
        public static bool IsTheSameAs(this Item item, Item compareItem)
        {
            if (item.netID == compareItem.netID)
            {
                return item.type == compareItem.type;
            }
            return false;
        }
        /// <summary>
        /// Returns a new color, with each RGB component multiplied by the factor and capped at 255.
        /// 
        public static Color Brighten(this Color color, float factor)
        {
            // factor > 1.0 makes it brighter, e.g. 1.2f = 20% brighter
            int r = (int)(color.R * factor);
            int g = (int)(color.G * factor);
            int b = (int)(color.B * factor);

            // clamp to 255
            r = Math.Min(255, r);
            g = Math.Min(255, g);
            b = Math.Min(255, b);

            return new Color(r, g, b, color.A);
        }
        public static string ContextToString(int context)
        {
            // Get all public constant fields in ItemSlot.Context
            var fields = typeof(ItemSlot.Context).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);

            foreach (var field in fields)
            {
                if (field.IsLiteral && !field.IsInitOnly)
                {
                    var fieldValue = (int)field.GetRawConstantValue();
                    if (fieldValue == context)
                        return field.Name;
                }
            }

            return $"Unknown({context})";
        }
    }
}
public class JoinMessage : ModPlayer
{
    int timer = 5;
    public override void OnEnterWorld()
    {
        timer = 5;
    }
    public override void PostUpdate()
    {
        if (timer > -1)
            timer--;
        if (timer == 0 && TransferableLoadouts.TransferableLoadouts.incompatibleModNames.Count > 0)
        {
            Main.NewText(Language.GetTextValue("Mods.TransferableLoadouts.IncompatabilityWarning", $"[c/EEEEEE:{string.Join(", ", TransferableLoadouts.TransferableLoadouts.incompatibleModNames)}]"), Color.Red);
        }
    }
    //public override void Load()
    //{
    //    Terraria.On_Player.UpdateVisibleAccessory += VanityDebug;
    //}

    //private void VanityDebug(On_Player.orig_UpdateVisibleAccessory orig, Player self, int itemSlot, Item item, bool modded)
    //{
    //    if (item.frontSlot > -1)
    //        Main.NewText($"{itemSlot}, {item}, {modded}, {item.frontSlot}", Color.Cyan);
    //    //if (itemSlot == 14)
    //    //    Main.NewText($"{itemSlot}, {item}, {modded}", Color.Red);
    //    orig(self, itemSlot, item, modded);
    //}
}
    //    private void TrySwitchingLoadoutWithFavorites(On_Player.orig_TrySwitchingLoadout orig, Player self, int loadoutIndex)
    //    {
    //        // --- PRE-SWITCH CHECKS (Similar to vanilla) ---
    //        bool isPlayerBusy = self.itemTime > 0 || self.itemAnimation > 0;
    //        if (self.whoAmI != Main.myPlayer || isPlayerBusy || self.CCed || self.dead)
    //        {
    //            return; // Player is busy, cannot switch
    //        }

    //        if (loadoutIndex == self.CurrentLoadoutIndex || loadoutIndex < 0 || loadoutIndex >= self.Loadouts.Length)
    //        {
    //            return; // Invalid index or switching to the same loadout
    //        }

    //        int currentLoadoutIndex = self.CurrentLoadoutIndex;
    //        EquipmentLoadout oldLoadout = self.Loadouts[currentLoadoutIndex];
    //        EquipmentLoadout newLoadout = self.Loadouts[loadoutIndex];

    //        // --- ALGORITHM IMPLEMENTATION ---

    //        // Step 1: Return the player's current equipment to its corresponding loadout storage.
    //        // We do this for armor, accessories, and dyes.
    //        for (int i = 0; i < oldLoadout.Armor.Length; i++)
    //        {
    //            oldLoadout.Armor[i] = self.armor[i].Clone(); // Store a copy
    //            self.armor[i].TurnToAir(); // Clear the player's slot
    //        }
    //        for (int i = 0; i < oldLoadout.Dye.Length; i++)
    //        {
    //            oldLoadout.Dye[i] = self.dye[i].Clone();
    //            self.dye[i].TurnToAir();
    //        }
    //        // For visibility toggles, we just copy the value.
    //        for (int i = 0; i < oldLoadout.Hide.Length; i++)
    //        {
    //            oldLoadout.Hide[i] = self.hideVisibleAccessory[i];
    //        }


    //        // Step 2: Take all non-air items from the loadout you're swapping TO and equip them.
    //        // We clear the item from the new loadout's storage as we equip it.
    //        for (int i = 0; i < newLoadout.Armor.Length; i++)
    //        {
    //            if (!newLoadout.Armor[i].IsAir)
    //            {
    //                self.armor[i] = newLoadout.Armor[i].Clone();
    //                newLoadout.Armor[i].TurnToAir(); // Remove from storage
    //            }
    //        }
    //        for (int i = 0; i < newLoadout.Dye.Length; i++)
    //        {
    //            if (!newLoadout.Dye[i].IsAir)
    //            {
    //                self.dye[i] = newLoadout.Dye[i].Clone();
    //                newLoadout.Dye[i].TurnToAir();
    //            }
    //        }
    //        for (int i = 0; i < newLoadout.Hide.Length; i++)
    //        {
    //            self.hideVisibleAccessory[i] = newLoadout.Hide[i];
    //        }


    //        // Step 3: For each empty item slot, find the first loadout with a favorited item for that slot.
    //        // This searches through ALL loadouts (0, 1, 2) in order.

    //        // Armor and Accessories
    //        for (int i = 0; i < self.armor.Length; i++)
    //        {
    //            if (self.armor[i].IsAir) // Check if the slot is still empty
    //            {
    //                // Search all loadouts for a favorited item
    //                for (int j = 0; j < self.Loadouts.Length; j++)
    //                {
    //                    Item potentialItem = self.Loadouts[j].Armor[i];
    //                    if (!potentialItem.IsAir && potentialItem.favorited)
    //                    {
    //                        self.armor[i] = potentialItem.Clone(); // Equip the favorited item
    //                        potentialItem.TurnToAir(); // Remove it from its original storage
    //                        break; // Stop searching for this slot and move to the next
    //                    }
    //                }
    //            }
    //        }

    //        // Dyes
    //        for (int i = 0; i < self.dye.Length; i++)
    //        {
    //            if (self.dye[i].IsAir) // Check if the dye slot is empty
    //            {
    //                for (int j = 0; j < self.Loadouts.Length; j++)
    //                {
    //                    Item potentialDye = self.Loadouts[j].Dye[i];
    //                    if (!potentialDye.IsAir && potentialDye.favorited)
    //                    {
    //                        self.dye[i] = potentialDye.Clone();
    //                        potentialDye.TurnToAir();
    //                        break;
    //                    }
    //                }
    //            }
    //        }

    //        // --- FINALIZE THE SWITCH (Copied from vanilla) ---
    //        self.CurrentLoadoutIndex = loadoutIndex;

    //        // These calls are crucial for effects, sounds, and multiplayer synchronization.
    //        Main.mouseLeftRelease = false;
    //        ItemSlot.RecordLoadoutChange();
    //        SoundEngine.PlaySound(SoundID.Grab);
    //        NetMessage.TrySendData(MessageID.SyncLoadout, -1, -1, null, self.whoAmI, loadoutIndex); //TODO: this would need a custom senddata handler too, probably just use a modpacket
    //        ParticleOrchestrator.RequestParticleSpawn(clientOnly: false, ParticleOrchestraType.LoadoutChange, new ParticleOrchestraSettings
    //        {
    //            PositionInWorld = self.Center,
    //            UniqueInfoPiece = loadoutIndex
    //        }, self.whoAmI);
    //    }
    //}   
/*
			case 147:
			{
				int num209 = this.reader.ReadByte();
				if (Main.netMode == 2)
				{
					num209 = this.whoAmI;
				}
				int num219 = this.reader.ReadByte();
				Main.player[num209].TrySwitchingLoadout(num219);
				MessageBuffer.ReadAccessoryVisibility(this.reader, Main.player[num209].hideVisibleAccessory);
				if (Main.netMode == 2)
				{
					NetMessage.TrySendData(b, -1, num209, null, num209, num219);
				}
				break;
			}
*/