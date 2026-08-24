using System;
using System.Reflection;
using HarmonyLib;
using Il2CppPhoton.Realtime;
using Il2CppRecRoom.AntiCheat;
using Il2CppOrg.BouncyCastle.Crypto.Tls;
using MelonLoader;

[assembly: MelonInfo(typeof(_2022RecRoomPatch.Patch), "RecRoom 2022 Patch", "2.0.0", "Legacy")]
[assembly: MelonGame("Against Gravity", "Rec Room")]

namespace _2022RecRoomPatch
{
    public class Patch : MelonMod
    {
        private static readonly HarmonyLib.Harmony harmony = new HarmonyLib.Harmony("com.rr2022.patch");

        // Your Photon App IDs
        private const string PHOTON_PUN = "REPLACEME";
        private const string PHOTON_VOICE = "REPLACEME";

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

            // 4. Patch Photon — find AppSettings and set our IDs
            try
            {
                // Patch all methods that return AppSettings to inject our Photon IDs
                // PUNNetworkManager has: static AppSettings OBKJFGNBMOO(string, bool)
                var punType = typeof(Il2Cpp.CheatManager).Assembly
                    .GetType("Il2Cpp.PUNNetworkManager");
                if (punType != null)
                {
                    var methods = punType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                        BindingFlags.Static | BindingFlags.Instance);
                    foreach (var method in methods)
                    {
                        if (method.ReturnType == typeof(AppSettings))
                        {
                            MelonLogger.Msg($"  [Photon] Patching {method.Name} (returns AppSettings)");
                            harmony.Patch(method,
                                postfix: new HarmonyMethod(typeof(Patches).GetMethod(nameof(Patches.Photon_AppSettings))));
                        }
                    }
                    MelonLogger.Msg("  [OK] Photon AppSettings patched");
                }
                else
                {
                    MelonLogger.Warning("  [WARN] PUNNetworkManager not found, trying direct approach...");
                    // Fallback: patch using Il2Cpp type search
                    PatchPhotonDirect();
                }
            }
            catch (Exception ex) { MelonLogger.Error($"  [FAIL] Photon: {ex.Message}"); }

            MelonLogger.Msg("RecRoom 2022 Patch loaded!");
        }

        private void PatchPhotonDirect()
        {
            // Scan only Il2Cpp assemblies for methods returning AppSettings
            var il2cppAssemblies = new[]
            {
                typeof(AppSettings).Assembly,
                typeof(Il2Cpp.CheatManager).Assembly
            };

            foreach (var asm in il2cppAssemblies)
            {
                try
                {
                    foreach (var type in asm.GetTypes())
                    {
                        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                            BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                        {
                            if (method.ReturnType == typeof(AppSettings))
                            {
                                MelonLogger.Msg($"  [Photon] Patching {type.Name}.{method.Name}");
                                harmony.Patch(method,
                                    postfix: new HarmonyMethod(typeof(Patches).GetMethod(nameof(Patches.Photon_AppSettings))));
                            }
                        }
                    }
                }
                catch { /* skip assemblies that fail */ }
            }
        }
    }

    public static class Patches
    {
        public static bool EAC_Prefix(string FKJANDFEMBG, ref string __result)
        {
            MelonLogger.Msg($"[PATCH] EAC bypassed");
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

        // Inject Photon App IDs into any AppSettings object
        public static void Photon_AppSettings(ref AppSettings __result)
        {
            if (__result == null) return;
            __result.AppIdRealtime = "73d1077c-e4ef-4526-8c9a-6284b31c8778";
            __result.AppIdVoice = "73d1077c-e4ef-4526-8c9a-6284b31c8778";
            __result.AppVersion = "1.0.0.0";
            __result.FixedRegion = "us";
            MelonLogger.Msg($"[PATCH] Photon IDs set: RT={__result.AppIdRealtime} Voice={__result.AppIdVoice}");
        }
    }
}
