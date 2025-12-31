using BepInEx;
using BepInEx.Unity.IL2CPP;
using System;
using System.Text;

namespace MajSoulHelper
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BasePlugin
    {
        public override void Load()
        {
            // 修复控制台编码问题 - 设置为 UTF-8
            try
            {
                Console.OutputEncoding = Encoding.UTF8;
                Console.InputEncoding = Encoding.UTF8;
            }
            catch (Exception)
            {
                // 忽略编码设置失败（可能在某些环境下不支持）
            }
            
            Utils.MyLogger(BepInEx.Logging.LogLevel.Debug, $"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
            
            // 初始化配置持久化
            ConfigPersistence.Initialize();
            
            // 初始化角色/皮肤数据缓存
            CharacterDataCache.Initialize();
            
            //PatchManager.PatchSettingDelegate();
            PatchManager.PatchAll();
            ConfigValue.Get().UpdateFrameConfig();
            
            // 启动Web配置服务器
            WebServer.Start();
            
            Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, 
                $"[MajSoulHelper] All systems initialized! Web UI: http://127.0.0.1:{PluginConfig.WebServerPort}/");
        }

        public override bool Unload()
        {
            WebServer.Stop();
            PatchManager.UnPatchAll();
            return base.Unload();
        }

    }

}