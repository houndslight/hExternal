using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Swed64;
using hExternal.Modules;
using hExternal.modules;

namespace hExternal.Features
{
    internal class SkinChanger
    {
        private static Swed swed = new Swed("cs2");
        private static nint clientcs = swed.GetModuleBase("client.dll");
        private static nint engine = swed.GetModuleBase("engine2.dll");

        private static SCOffsets offsets = SCOffsets.Instance;

        // Dictionary to map UI weapon selections to weapon IDs
        private static readonly Dictionary<string, int> weaponIdMap = new Dictionary<string, int>
        {
            { "AK-47", 7 },
            { "M4A4", 16 },
            { "M4A1-S", 60 },
            { "AWP", 9 },
            { "Desert Eagle", 1 },
            { "USP-S", 61 },
            { "Glock-18", 4 },
            { "P250", 36 }
        };

        // Dictionary to map UI skin selections to paint kit IDs
        private static readonly Dictionary<string, int> skinIdMap = new Dictionary<string, int>
        {
            { "Asiimov", 279 },
            { "Dragon Lore", 344 },
            { "Hyper Beast", 430 },
            { "Neo-Noir", 692 },
            { "Fade", 38 },
            { "Doppler", 415 },
            { "Marble Fade", 413 },
            { "Slaughter", 59 }
        };

        // Current skin settings
        private static Dictionary<int, SkinSettings> skinSettingsPerWeapon = new Dictionary<int, SkinSettings>();
        private static SkinSettings globalSkinSettings = new SkinSettings();
        private static bool applyToAllWeapons = false;

        // SkinSettings struct to hold all settings
        public struct SkinSettings
        {
            public int PaintKit;
            public int Seed;
            public float Wear;
            public string CustomName;
            public int StatTrak;
            public int Quality;
            public bool DisallowSOC;

            public SkinSettings()
            {
                PaintKit = 279; // Default to Asiimov
                Seed = 0;
                Wear = 0.01f;
                CustomName = "hExternal";
                StatTrak = -1;
                Quality = 3;
                DisallowSOC = false;
            }

            public SkinSettings(int paintKit, int seed, float wear, string customName, int statTrak, int quality, bool disallowSOC)
            {
                PaintKit = paintKit;
                Seed = seed;
                Wear = wear;
                CustomName = customName;
                StatTrak = statTrak;
                Quality = quality;
                DisallowSOC = disallowSOC;
            }
        }

        public static void set_weapon_skin(int weapon_id, int steam_id, IntPtr weapon, SkinSettings settings)
        {
            try
            {
                // Validate weapon pointer more thoroughly
                if (weapon == IntPtr.Zero)
                {
                    Log.Error("Invalid weapon pointer in set_weapon_skin");
                    return; // Exit early instead of crashing
                }

                // Add more validation for other critical pointers

                // Rest of the method remains the same
            }
            catch (Exception e)
            {
                Log.Error($"SkinChanger::set_weapon_skin feature encountered an error: {e.Message}");
                // Don't rethrow - just log and continue
            }
        }

        private static int TryGetOffset(string className, string key)
        {
            try
            {
                return offsets.Client(className, key);
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to get offset for {className}.{key}: {ex.Message}");
                // Return a fallback value that won't crash
                return 0;
            }
        }

        public static void perform_full_update()
        {
            try
            {
                int dwNetworkGameClient_offset = offsets.Engine2dll("dwNetworkGameClient");
                var game_client = swed.ReadPointer(engine + dwNetworkGameClient_offset);
                if (game_client == IntPtr.Zero)
                {
                    Log.Error("Failed to get game client in force update");
                    return;
                }

                // Force delta tick to update
                int dwNetworkGameClient_deltaTick_offset = offsets.Engine2dll("dwNetworkGameClient_deltaTick");
                swed.WriteInt(game_client + dwNetworkGameClient_deltaTick_offset, -1);

                // Optionally: Add a small delay for the update to process
                Task.Delay(100).Wait();
            }
            catch (Exception e)
            {
                Log.Error($"Failed to perform full update: {e.Message}");
            }
        }

        public static IntPtr get_controller_from_handle(IntPtr entityList, int handle)
        {
            IntPtr list_entry = swed.ReadPointer(entityList + 0x8 * ((handle & 0x7FFF) >> 9) + 16);
            if (list_entry == IntPtr.Zero) return IntPtr.Zero;
            return swed.ReadPointer(list_entry + 120 * (handle & 0x1FF));
        }

        // Public methods to interface with UI
        public static void ApplySkinChanges(string weaponName, string skinName, float wear, int statTrak, bool applyAll, string customName = "hExternal")
        {
            if (!weaponIdMap.ContainsKey(weaponName) || !skinIdMap.ContainsKey(skinName))
            {
                Log.Error("Invalid weapon or skin selection");
                return;
            }

            // Create settings
            var settings = new SkinSettings
            {
                PaintKit = skinIdMap[skinName],
                Seed = 0,
                Wear = wear,
                CustomName = customName,
                StatTrak = statTrak,
                Quality = 3,
                DisallowSOC = false
            };

            // Update global settings if applying to all weapons
            if (applyAll)
            {
                globalSkinSettings = settings;
                applyToAllWeapons = true;

                // Also update all existing weapon entries
                foreach (var weaponId in weaponIdMap.Values)
                {
                    skinSettingsPerWeapon[weaponId] = settings;
                }
            }
            else
            {
                // Just update this specific weapon
                int weaponId = weaponIdMap[weaponName];
                skinSettingsPerWeapon[weaponId] = settings;
                applyToAllWeapons = false;
            }

            // Force update
            perform_full_update();
        }

        public static void ResetAllSkins()
        {
            skinSettingsPerWeapon.Clear();
            globalSkinSettings = new SkinSettings();
            applyToAllWeapons = false;
            perform_full_update();
        }

        public static void SkinChangerThread(Program.Renderer renderer)
        {
            Log.Info("SkinChanger thread started");

            // Track when we last performed a full update to avoid spamming
            DateTime lastFullUpdate = DateTime.MinValue;
            bool needsUpdate = false;

            while (true)
            {
                try
                {
                    // Don't do anything if skin changer is disabled in the menu
                    if (!renderer.skinChangerEnabled)
                    {
                        Task.Delay(1000).Wait();
                        continue;
                    }

                    int dwLocalPlayerPawn_offset = offsets.Offset("dwLocalPlayerPawn");
                    IntPtr local_player = swed.ReadPointer(clientcs + dwLocalPlayerPawn_offset);
                    if (local_player == IntPtr.Zero)
                    {
                        Task.Delay(1000).Wait();
                        continue;
                    }

                    int dwEntityList_offset = offsets.Offset("dwEntityList");
                    IntPtr entity_list = swed.ReadPointer(clientcs, dwEntityList_offset);
                    if (entity_list == IntPtr.Zero)
                    {
                        Task.Delay(1000).Wait();
                        continue;
                    }

                    int m_pWeaponServices_offset = TryGetOffset("C_BasePlayerPawn", "m_pWeaponServices");
                    IntPtr weapon_service = swed.ReadPointer(local_player + m_pWeaponServices_offset);
                    if (weapon_service == IntPtr.Zero)
                    {
                        Task.Delay(1000).Wait();
                        continue;
                    }

                    int m_hMyWeapons_offset = TryGetOffset("CPlayer_WeaponServices", "m_hMyWeapons");
                    int weapons_count = swed.ReadInt(weapon_service + m_hMyWeapons_offset);
                    IntPtr weapons = swed.ReadPointer(weapon_service + m_hMyWeapons_offset + 0x8);
                    if (weapons == IntPtr.Zero)
                    {
                        Task.Delay(1000).Wait();
                        continue;
                    }

                    // Get account ID
                    int m_OriginalOwnerXuidLow_offset = TryGetOffset("C_EconEntity", "m_OriginalOwnerXuidLow");
                    int account_id = swed.ReadInt(clientcs + m_OriginalOwnerXuidLow_offset);

                    // Process each weapon only once
                    bool weaponModified = false;

                    for (int i = 0; i < weapons_count; i++)
                    {
                        var weapon_handle = swed.ReadInt(weapons + i * 0x4);
                        if (weapon_handle == 0) continue;

                        var weapon_controller = get_controller_from_handle(entity_list, weapon_handle);
                        if (weapon_controller == IntPtr.Zero) continue;

                        // Get weapon ID using safe offset retrieval
                        int m_AttributeManager_offset = TryGetOffset("C_EconEntity", "m_AttributeManager");
                        int m_Item_offset = TryGetOffset("C_AttributeContainer", "m_Item");
                        int m_iItemDefinitionIndex_offset = TryGetOffset("C_EconItemView", "m_iItemDefinitionIndex");

                        short weapon_id = swed.ReadShort(
                            weapon_controller,
                            m_AttributeManager_offset +
                            m_Item_offset +
                            m_iItemDefinitionIndex_offset
                        );
                        if (weapon_id == 0) continue;

                        // Get appropriate settings for this weapon
                        SkinSettings settings;
                        if (applyToAllWeapons)
                        {
                            settings = globalSkinSettings;
                        }
                        else if (skinSettingsPerWeapon.ContainsKey(weapon_id))
                        {
                            settings = skinSettingsPerWeapon[weapon_id];
                        }
                        else
                        {
                            // Skip weapons we don't have settings for
                            continue;
                        }

                        // Check if the skin is already set correctly to avoid unnecessary updates
                        int m_nFallbackPaintKit_offset = TryGetOffset("C_EconEntity", "m_nFallbackPaintKit");
                        int m_flFallbackWear_offset = TryGetOffset("C_EconEntity", "m_flFallbackWear");

                        int currentPaintKit = swed.ReadInt(weapon_controller + m_nFallbackPaintKit_offset);
                        float currentWear = swed.ReadFloat(weapon_controller + m_flFallbackWear_offset);

                        // Only update if needed
                        if (currentPaintKit != settings.PaintKit || Math.Abs(currentWear - settings.Wear) > 0.001f)
                        {
                            // Set skin for the weapon
                            try
                            {
                                set_weapon_skin(weapon_id, account_id, weapon_controller, settings);
                                weaponModified = true;
                            }
                            catch (Exception e)
                            {
                                Log.Error($"Failed to set skin for weapon ID {weapon_id}: {e.Message}");
                            }
                        }

                        // Check if any of the critical pointers are null before proceeding
                        if (local_player == IntPtr.Zero || entity_list == IntPtr.Zero ||
                            weapon_service == IntPtr.Zero || weapons == IntPtr.Zero)
                        {
                            Task.Delay(1000).Wait();
                            continue;
                        }

                        // Handle view model visibility - only if needed
                        try
                        {
                            int m_pViewModelServices_offset = TryGetOffset("C_CSPlayerPawnBase", "m_pViewModelServices");
                            int m_hViewModel_offset = TryGetOffset("CCSPlayer_ViewModelServices", "m_hViewModel");

                            var view_handle = swed.ReadInt(local_player + m_pViewModelServices_offset +
                                                          m_hViewModel_offset);
                            var view_controller = get_controller_from_handle(entity_list, view_handle);

                            if (view_controller != IntPtr.Zero)
                            {
                                int m_pGameSceneNode_offset = TryGetOffset("C_BaseEntity", "m_pGameSceneNode");
                                var view_node = swed.ReadPointer(view_controller + m_pGameSceneNode_offset);
                                if (view_node != IntPtr.Zero)
                                {
                                    int m_modelState_offset = TryGetOffset("CSkeletonInstance", "m_modelState");
                                    int m_MeshGroupMask_offset = TryGetOffset("CModelState", "m_MeshGroupMask");

                                    swed.WriteInt(view_node + m_modelState_offset +
                                                 m_MeshGroupMask_offset, 2);
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Log.Error($"Failed to update view model: {e.Message}");
                        }
                    }

                    // Only update game if we modified any weapon and we haven't updated recently
                    if (weaponModified && (DateTime.Now - lastFullUpdate).TotalSeconds >= 2.0)
                    {
                        perform_full_update();
                        lastFullUpdate = DateTime.Now;
                        Log.Info("Performed full update for skin changes");
                    }

                    // Wait a bit longer between checks - 2 seconds instead of 500ms
                    Task.Delay(2000).Wait();
                }
                catch (Exception e)
                {
                    Log.Error($"SkinChanger thread encountered an error: {e.Message}");
                    // Wait longer after an error
                    Task.Delay(500).Wait();
                }
            }
        }
    }
}