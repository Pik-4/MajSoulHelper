using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MajSoulHelper
{
    //public struct AllPatch
    //{
    //    Harmony mHarmony;
    //    Type mType;
    //}
    public static class PatchManager
    {
        public static List<Harmony> AllHarmony = new List<Harmony>();    //保存补丁信息，方便定向卸载。
        public static List<string> AllHarmonyName = new List<string>();

        public static void LoadPatch(Type loadType)
        {
            try
            {
                Harmony harmony;
                int harmonyCount;
                harmony = Harmony.CreateAndPatchAll(loadType);
                harmonyCount = harmony.GetPatchedMethods().Count();
                Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, $"{loadType.Name} => Patched {harmonyCount} methods");
                AllHarmony.Add(harmony);
                AllHarmonyName.Add(loadType.Name);
            }
            catch (Exception ex)
            {
                Utils.MyLogger(BepInEx.Logging.LogLevel.Error, ex.Message);
                Utils.MyLogger(BepInEx.Logging.LogLevel.Error, ex.StackTrace);
                Utils.MyLogger(BepInEx.Logging.LogLevel.Error, ex.InnerException);
            }
        }
        public static bool UnPatch(string name)
        {

            for (int i = 0; i < AllHarmonyName.Count; i++)
            {
                if (AllHarmonyName[i] == name)
                {
                    AllHarmony[i].UnpatchSelf();
                    AllHarmonyName.Remove(AllHarmonyName[i]);
                    AllHarmony.Remove(AllHarmony[i]);
                    Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, $"Unpatched {name}!");
                    return true;
                }
            }
            return false;
        }

        //委托绑定
        //public static void PatchSettingDelegate()
        //{
        //    isPluginEnable.SettingChanged += delegate
        //    {
        //        if (isPluginEnable.Value)
        //        {
        //            PatchAll();
        //        }
        //        else UnPatchAll();
        //    };
        //}
        public static void PatchAll()
        {
            LoadPatch(typeof(PatchSteam));
            //LoadPatch(typeof(PatchOfficial));
            LoadPatch(typeof(PatchFPS));
            LoadPatch(typeof(PatchGuiInput));

        }
        public static void UnPatchAll()
        {
            for (int i = 0; i < AllHarmony.Count; i++)
            {
                AllHarmony[i].UnpatchSelf();
                Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, $"Unpatched => {AllHarmonyName[i]}");
            }
            AllHarmony.Clear();
            AllHarmonyName.Clear();
        }
        public static void RePatchAll()
        {
            UnPatchAll();
            PatchAll();
        }
    }
    /*
     * tolua_loadbuffer(IntPtr luaState, byte[] buff, int size, string name);
public static extern int tolua_loadbuffer(IntPtr luaState, byte[] buff, int size, string name);
public static int luaL_loadbuffer(IntPtr luaState, byte[] buff, int size, string name)
		private const string LUADLL = "tolua";
    //public enum DDJEGBALAIC  LUA_TLIGHTUSERDATA

	public static extern int KJOGHPHGALG(IntPtr HBFONMEIAEN, byte[] DIHOEHFEMIK, int IGBJDCHGLHD, string MKOCLBCHJJG);
	public static int MIDGPDIJLIE(IntPtr HBFONMEIAEN, byte[] DIHOEHFEMIK, int IGBJDCHGLHD, string MKOCLBCHJJG)
	{
		return 0;
	}
     * */
    //public class PatchOfficial
    //{
    //    [HarmonyPrefix]
    //    [HarmonyPatch(typeof(MEDENAGDFGP), "KJOGHPHGALG")]
    //    [HarmonyPatch(typeof(MEDENAGDFGP), "MIDGPDIJLIE")]
    //    public static void PatchTolua_loadbuffer(ref IntPtr HBFONMEIAEN, ref byte[] DIHOEHFEMIK, ref int IGBJDCHGLHD, ref string MKOCLBCHJJG)
    //    {
    //        System.Diagnostics.StackTrace stackTrace = new System.Diagnostics.StackTrace();
    //        System.Diagnostics.StackFrame[] stackFrames = stackTrace.GetFrames();
    //        foreach (System.Diagnostics.StackFrame stackFrame in stackFrames)
    //        {
    //            Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, $"StackBacktrace: {stackFrame.GetMethod()?.Name}");
    //        }

    //        Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, $"Pre tolua_loadbuffer {MKOCLBCHJJG}  {HBFONMEIAEN.ToString("X")} {DIHOEHFEMIK?.Count()} {IGBJDCHGLHD}");

    //        try
    //        {
    //            var file_name = "BepInEx/leak/Pre_" + MKOCLBCHJJG.Replace('/', '_');
    //            using (var fs = new FileStream(file_name, FileMode.Create, FileAccess.Write))
    //            {
    //                fs.Write(DIHOEHFEMIK, 0, IGBJDCHGLHD);
    //            }
    //        }
    //        catch (Exception ex)
    //        {
    //            Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, $"Exception caught in process: {ex}");
    //        }
    //    }

    //    [HarmonyPostfix]
    //    [HarmonyPatch(typeof(MEDENAGDFGP), "KJOGHPHGALG")]
    //    [HarmonyPatch(typeof(MEDENAGDFGP), "MIDGPDIJLIE")]
    //    public static void PatchluaL_loadbuffer(ref IntPtr HBFONMEIAEN, ref byte[] DIHOEHFEMIK, ref int IGBJDCHGLHD, ref string MKOCLBCHJJG)
    //    {
    //        System.Diagnostics.StackTrace stackTrace = new System.Diagnostics.StackTrace();
    //        System.Diagnostics.StackFrame[] stackFrames = stackTrace.GetFrames();
    //        foreach (System.Diagnostics.StackFrame stackFrame in stackFrames)
    //        {
    //            Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, $"StackBacktrace: {stackFrame.GetMethod()?.Name}");
    //        }
    //        Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, $"Post tolua_loadbuffer {MKOCLBCHJJG} {HBFONMEIAEN.ToString("X")} {DIHOEHFEMIK?.Count()} {IGBJDCHGLHD}");

    //        //if (!MKOCLBCHJJG.EndsWith(".lua")) return;
    //        try
    //        {
    //            var file_name = "BepInEx/leak/Post_" + MKOCLBCHJJG.Replace('/', '_');
    //            using (var fs = new FileStream(file_name, FileMode.Create, FileAccess.Write))
    //            {
    //                fs.Write(DIHOEHFEMIK, 0, IGBJDCHGLHD);
    //            }
    //        }
    //        catch (Exception ex)
    //        {
    //            Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, $"Exception caught in process: {ex}");
    //        }
    //    }
    //}
    public class PatchSteam
    {
        // 静态标志，确保SkinUnlocker只初始化一次
        private static bool _skinUnlockerInitialized = false;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(LuaInterface.LuaDLL), "tolua_loadbuffer")]
        [HarmonyPatch(typeof(LuaInterface.LuaDLL), "luaL_loadbuffer")]
        public static void PatchTolua_loadbuffer(ref IntPtr luaState, ref Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<byte> buff, ref int size, ref string name)
        {
            // 初始化皮肤解锁器
            if (!_skinUnlockerInitialized)
            {
                _skinUnlockerInitialized = true;
                try
                {
                    SkinUnlocker.Initialize();
                }
                catch (Exception ex)
                {
                    Utils.MyLogger(BepInEx.Logging.LogLevel.Error, $"[SkinUnlocker] Initialize failed: {ex.Message}");
                }
            }

            // 仅在调试模式下打印堆栈
            if (PluginConfig.EnableDebugLog)
            {
                System.Diagnostics.StackTrace stackTrace = new System.Diagnostics.StackTrace();
                System.Diagnostics.StackFrame[] stackFrames = stackTrace.GetFrames();
                foreach (System.Diagnostics.StackFrame stackFrame in stackFrames)
                {
                    Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, $"StackBacktrace: {stackFrame.GetMethod()?.Name}");
                }
                Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, $"Pre tolua_loadbuffer {name}  {luaState.ToString("X")} {buff?.Count()} {size}");
            }

            // ========== 皮肤解锁核心逻辑 ==========
            try
            {
                // 将Il2Cpp数组转为byte数组
                byte[] originalData = buff.ToArray();
                
                // 尝试应用补丁
                if (SkinUnlocker.TryPatchLuaBuffer(name, originalData, size, out byte[] patchedData, out int newSize))
                {
                    // 如果补丁成功，替换buffer内容
                    // 注意：需要调整buff大小以容纳修改后的数据
                    if (newSize != size)
                    {
                        // 创建新的Il2Cpp数组
                        var newBuff = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<byte>(newSize);
                        for (int i = 0; i < newSize; i++)
                        {
                            newBuff[i] = patchedData[i];
                        }
                        buff = newBuff;
                        size = newSize;
                    }
                    else
                    {
                        // 大小相同，直接复制
                        for (int i = 0; i < newSize; i++)
                        {
                            buff[i] = patchedData[i];
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Utils.MyLogger(BepInEx.Logging.LogLevel.Error, $"[SkinUnlocker] Patch failed for {name}: {ex.Message}");
            }

            // ========== 原始文件保存逻辑 ==========
            try
            {
                var file_name = "BepInEx/leak/Pre_" + name.Replace('/', '_');
                using (var fs = new FileStream(file_name, FileMode.Create, FileAccess.Write))
                {
                    fs.Write(buff, 0, size);
                }
            }
            catch (Exception ex)
            {
                if (PluginConfig.EnableDebugLog)
                {
                    Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, $"Exception caught in process: {ex}");
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(LuaInterface.LuaDLL), "tolua_loadbuffer")]
        [HarmonyPatch(typeof(LuaInterface.LuaDLL), "luaL_loadbuffer")]
        public static void PatchluaL_loadbuffer(ref IntPtr luaState, ref Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<byte> buff, ref int size, ref string name)
        {
            // 仅在调试模式下打印堆栈
            if (PluginConfig.EnableDebugLog)
            {
                System.Diagnostics.StackTrace stackTrace = new System.Diagnostics.StackTrace();
                System.Diagnostics.StackFrame[] stackFrames = stackTrace.GetFrames();
                foreach (System.Diagnostics.StackFrame stackFrame in stackFrames)
                {
                    Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, $"StackBacktrace: {stackFrame.GetMethod()?.Name}");
                }
                Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, $"Post tolua_loadbuffer {name}  {luaState.ToString("X")} {buff?.Count()} {size}");
            }

            // 保存修改后的文件（用于调试）
            try
            {
                var file_name = "BepInEx/leak/Post_" + name.Replace('/', '_');
                using (var fs = new FileStream(file_name, FileMode.Create, FileAccess.Write))
                {
                    fs.Write(buff.ToArray(), 0, size);
                }
            }
            catch (Exception ex)
            {
                if (PluginConfig.EnableDebugLog)
                {
                    Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, $"Exception caught in process: {ex}");
                }
            }
        }
    }
    public class PatchFPS
    {
        //https://docs.unity3d.com/6000.2/Documentation/ScriptReference/Application-targetFrameRate.html
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Application), "targetFrameRate", MethodType.Setter)]
        public static void Patchset_targetFramerate(ref int value)
        {
            value = ConfigValue.Get().TargetFrameRate;
        }
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Application), "targetFrameRate", MethodType.Getter)]
        public static void Patchget_targetFramerate(ref int __result)
        {
            __result = ConfigValue.Get().TargetFrameRate;
        }
        [HarmonyPrefix]
        [HarmonyPatch(typeof(QualitySettings), "vSyncCount", MethodType.Setter)]
        public static void Patchset_vSyncCount(ref int value)
        {
            value = 0;
        }
        [HarmonyPrefix]
        [HarmonyPatch(typeof(QualitySettings), "vSyncCount", MethodType.Getter)]
        public static void Patchget_vSyncCount(ref int __result)
        {
            __result = 0;
        }

        // https://docs.unity3d.com/6000.2/Documentation/ScriptReference/Time-timeScale.html
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Time), "timeScale", MethodType.Setter)]
        public static void Patchset_timeScale(ref float value)
        {
            value = PluginConfig.TargetTimeScale;
        }
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Time), "timeScale", MethodType.Getter)]
        public static void Patchget_timeScale(ref float __result)
        {
            __result = PluginConfig.TargetTimeScale;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Screen), "currentResolution", MethodType.Getter)]
        public static void Patchget_currentResolution(ref UnityEngine.Resolution __result)
        {
            if (PluginConfig.isFrameRateBaseNeedUpdate)
            {
                PluginConfig.isFrameRateBaseNeedUpdate = false;
                PluginConfig.FrameRateBase = __result.refreshRate > 120 ? __result.refreshRate : 120;
            }
            __result.refreshRate = ConfigValue.Get().TargetFrameRate;
        }

    }

    public class PatchGuiInput
    {
        [HarmonyPostfix]
        //[HarmonyPatch(typeof(UnityEngine.Input), "GetKey", typeof(KeyCode))]
        //[HarmonyPatch(typeof(UnityEngine.Input), "GetKeyInt", typeof(KeyCode))]
        //[HarmonyPatch(typeof(UnityEngine.Input), "GetKeyUpInt", typeof(KeyCode))]
        [HarmonyPatch(typeof(UnityEngine.Input), "GetKeyDownInt", typeof(KeyCode))]
        public static void PatchMouseEvents(ref KeyCode key, ref bool __result)
        {
            bool save_result = __result;
            //System.Diagnostics.StackTrace stackTrace = new System.Diagnostics.StackTrace();
            //System.Diagnostics.StackFrame[] stackFrames = stackTrace.GetFrames();
            //foreach (System.Diagnostics.StackFrame stackFrame in stackFrames)
            //{
            //    Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, $"StackBacktrace: {stackFrame}");
            //}
            //if (Input.GetKeyDown("right"))
            if (Input.GetKeyUp(KeyCode.UpArrow))
            {
                if (PluginConfig.TargetTimeScale < 32)
                    PluginConfig.TargetTimeScale += 1f;
                ConfigValue.Get().UpdateFrameConfig();

            }
            else if (Input.GetKeyUp(KeyCode.DownArrow))
            {
                if (PluginConfig.TargetTimeScale >= 2)
                    PluginConfig.TargetTimeScale -= 1f;
                ConfigValue.Get().UpdateFrameConfig();
            }
            else if (Input.GetKeyUp(KeyCode.RightArrow))
            {
                PluginConfig.TargetTimeScale = PluginConfig.TargetTimeScale >= 4f ? 8f : 4f;
                ConfigValue.Get().UpdateFrameConfig();
            }
            else if (Input.GetKeyUp(KeyCode.LeftArrow))
            {
                PluginConfig.TargetTimeScale = 1f;
                ConfigValue.Get().UpdateFrameConfig();
            }
            else if (Input.GetKeyUp(KeyCode.Plus) || Input.GetKeyUp(KeyCode.KeypadPlus))
            {
                PluginConfig.FrameRateBase = PluginConfig.FrameRateBase < 720 ? PluginConfig.FrameRateBase + 60 : 720;
                Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, $"PluginConfig.FrameRateBase {PluginConfig.FrameRateBase}");
                ConfigValue.Get().UpdateFrameConfig();
            }
            else if (Input.GetKeyUp(KeyCode.Minus) || Input.GetKeyUp(KeyCode.KeypadMinus))
            {
                PluginConfig.FrameRateBase = PluginConfig.FrameRateBase > 180 ? PluginConfig.FrameRateBase - 60 : 120;
                Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, $"PluginConfig.FrameRateBase {PluginConfig.FrameRateBase}");
                ConfigValue.Get().UpdateFrameConfig();
            }
            __result = save_result;
            //Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, $"key {key}");

        }

    }
}
