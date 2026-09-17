using System;
using System.Reflection;
using System.Runtime.InteropServices;
using HarmonyLib;
using Il2CppPhoton.Realtime;
using Il2CppRecRoom.AntiCheat;
using Il2CppOrg.BouncyCastle.Crypto.Tls;
using MelonLoader;
using static Il2CppInterop.Runtime.IL2CPP;

[assembly: MelonInfo(typeof(_2022RecRoomPatch.Patch), "RecRoom 2022 Patch", "1.7.0", "Legacy)]
[assembly: MelonGame("Against Gravity", "Rec Room")]

namespace _2022RecRoomPatch
{
    public class Patch : MelonMod
    {
        private static readonly HarmonyLib.Harmony harmony = new HarmonyLib.Harmony("com.rr2022.patch");

        public override void OnInitializeMelon()
        {
            MelonLogger.Msg("Patching Rec Room 2022...");

            // 1. Bypass EAC
            try
            {
                var genMethod = typeof(EACManager).GetMethod("GenerateChallengeResponse",
                    BindingFlags.Public | BindingFlags.Static);
                if (genMethod != null)
                {
                    harmony.Patch(genMethod,
                        prefix: new HarmonyMethod(typeof(Patches).GetMethod(nameof(Patches.EAC_Prefix))));
                    MelonLogger.Msg("  [OK] EAC bypassed");
                }
            }
            catch (Exception ex) { MelonLogger.Error($"  [FAIL] EAC: {ex.Message}"); }

            // 2. Bypass TLS
            try
            {
                var notifyMethod = typeof(LegacyTlsAuthentication).GetMethod("NotifyServerCertificate");
                if (notifyMethod != null)
                {
                    harmony.Patch(notifyMethod,
                        prefix: new HarmonyMethod(typeof(Patches).GetMethod(nameof(Patches.TLS_Prefix))));
                    MelonLogger.Msg("  [OK] TLS bypassed");
                }
            }
            catch (Exception ex) { MelonLogger.Error($"  [FAIL] TLS: {ex.Message}"); }

            // 3. Skip CheatManager
            try
            {
                var awakeMethod = typeof(Il2Cpp.CheatManager).GetMethod("Awake",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (awakeMethod != null)
                {
                    harmony.Patch(awakeMethod,
                        prefix: new HarmonyMethod(typeof(Patches).GetMethod(nameof(Patches.Cheat_Prefix))));
                    MelonLogger.Msg("  [OK] CheatManager skipped");
                }
            }
            catch (Exception ex) { MelonLogger.Error($"  [FAIL] CheatManager: {ex.Message}"); }

            // 4. Patch Photon IDs
            try
            {
                Patches.PatchPhotonSettings();
            }
            catch (Exception ex) { MelonLogger.Error($"  [FAIL] Photon IDs: {ex.Message}"); }

            MelonLogger.Msg("RecRoom 2022 Patch loaded!");
        }
    }

    public static class Patches
    {
        private const string PUN_ID = "73d1077c-e4ef-4526-8c9a-6284b31c8778";

        public static bool EAC_Prefix(string FKJANDFEMBG, ref string __result)
        {
            MelonLogger.Msg("[PATCH] EAC bypassed");
            __result = FKJANDFEMBG;
            return false;
        }

        public static bool TLS_Prefix()
        {
            MelonLogger.Msg("[PATCH] TLS bypassed");
            return false;
        }

        public static bool Cheat_Prefix()
        {
            MelonLogger.Msg("[PATCH] CheatManager skipped");
            return false;
        }

        public static void PatchPhotonSettings()
        {
            MelonLogger.Msg("[PATCH] Patching Photon settings...");

            var settingsObj = UnityEngine.Resources.Load("PhotonServerSettings");
            if (settingsObj == null)
            {
                MelonLogger.Warning("  PhotonServerSettings not found in Resources!");
                return;
            }

            var objPtr = settingsObj.Pointer;
            if (objPtr == IntPtr.Zero) { MelonLogger.Warning("  Pointer is null"); return; }

            var objClass = il2cpp_object_get_class(objPtr);

            // Get the AppSettings field at offset 0x18 from dump.cs
            var appSettingsPtr = Marshal.ReadIntPtr(objPtr, 0x18);
            if (appSettingsPtr == IntPtr.Zero)
            {
                MelonLogger.Warning("  AppSettings is null at offset 0x18");
                return;
            }
            MelonLogger.Msg($"  AppSettings ptr: 0x{appSettingsPtr:X}");

            var asClass = il2cpp_object_get_class(appSettingsPtr);

            // Set fields using il2cpp_field_set_value
            SetField(asClass, appSettingsPtr, "AppIdRealtime", PUN_ID);
            SetField(asClass, appSettingsPtr, "AppIdVoice", PUN_ID);
            SetField(asClass, appSettingsPtr, "AppVersion", "20221209");
            SetField(asClass, appSettingsPtr, "FixedRegion", "us");

            MelonLogger.Msg("  [OK] Photon IDs set!");
        }

        private static void SetField(IntPtr objClass, IntPtr objPtr, string fieldName, string value)
        {
            var field = il2cpp_class_get_field_from_name(objClass, fieldName);
            if (field == IntPtr.Zero)
            {
                MelonLogger.Warning($"  Field '{fieldName}' not found");
                return;
            }
            var strPtr = il2cpp_string_new(value);
            // il2cpp_field_set_value takes void* for the value, so use unsafe or IntPtr
            unsafe
            {
                il2cpp_field_set_value(objPtr, field, (void*)strPtr);
            }
            MelonLogger.Msg($"  Set {fieldName} = {value}");
        }
    }
}
