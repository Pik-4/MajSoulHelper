using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MajSoulHelper
{
    /// <summary>
    /// 配置持久化管理器
    /// 使用JSON格式保存和加载配置
    /// </summary>
    public static class ConfigPersistence
    {
        private static readonly string ConfigDir = "BepInEx/config";
        private static readonly string ConfigFile = "BepInEx/config/MajSoulHelper.json";
        private static bool _initialized = false;

        /// <summary>
        /// 运行时配置数据
        /// </summary>
        public static RuntimeConfig Config { get; private set; } = new RuntimeConfig();

        /// <summary>
        /// 当前配置（CurrentConfig 是 Config 的别名，用于 WebServer 兼容）
        /// </summary>
        public static RuntimeConfig CurrentConfig => Config;

        /// <summary>
        /// 初始化配置持久化
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;

            try
            {
                // 确保目录存在
                if (!Directory.Exists(ConfigDir))
                {
                    Directory.CreateDirectory(ConfigDir);
                }

                // 加载配置
                Load();
                _initialized = true;
                Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, "[ConfigPersistence] Initialized successfully!");
            }
            catch (Exception ex)
            {
                Utils.MyLogger(BepInEx.Logging.LogLevel.Error, $"[ConfigPersistence] Initialize failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 加载配置文件
        /// </summary>
        public static void Load()
        {
            try
            {
                if (File.Exists(ConfigFile))
                {
                    string json = File.ReadAllText(ConfigFile, Encoding.UTF8);
                    Config = SimpleJsonParser.Deserialize<RuntimeConfig>(json) ?? new RuntimeConfig();
                    ApplyToPluginConfig();
                    Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, "[ConfigPersistence] Config loaded from file");
                }
                else
                {
                    Config = new RuntimeConfig();
                    Save(); // 创建默认配置文件
                    Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, "[ConfigPersistence] Created default config file");
                }
            }
            catch (Exception ex)
            {
                Utils.MyLogger(BepInEx.Logging.LogLevel.Error, $"[ConfigPersistence] Load failed: {ex.Message}");
                Config = new RuntimeConfig();
            }
        }

        /// <summary>
        /// 保存配置到文件
        /// </summary>
        public static void Save()
        {
            try
            {
                SyncFromPluginConfig();
                string json = SimpleJsonParser.Serialize(Config);
                File.WriteAllText(ConfigFile, json, Encoding.UTF8);
                Utils.MyLogger(BepInEx.Logging.LogLevel.Debug, "[ConfigPersistence] Config saved to file");
            }
            catch (Exception ex)
            {
                Utils.MyLogger(BepInEx.Logging.LogLevel.Error, $"[ConfigPersistence] Save failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 将加载的配置应用到PluginConfig
        /// </summary>
        private static void ApplyToPluginConfig()
        {
            PluginConfig.EnableSkinUnlock = Config.EnableSkinUnlock;
            PluginConfig.EnableCharacterUnlock = Config.EnableCharacterUnlock;
            PluginConfig.EnableVoiceUnlock = Config.EnableVoiceUnlock;
            PluginConfig.HideLockUI = Config.HideLockUI;
            PluginConfig.EnableInGameSkinReplace = Config.EnableInGameSkinReplace;
            PluginConfig.EnableDebugLog = Config.EnableDebugLog;
            PluginConfig.EnableBlockedLogDisplay = Config.EnableBlockedLogDisplay;
            PluginConfig.BlockLogToServer = Config.BlockLogToServer;
            PluginConfig.BlockMatchInfo = Config.BlockMatchInfo;
            PluginConfig.EnableTitleUnlock = Config.EnableTitleUnlock;
            PluginConfig.EnableItemUnlock = Config.EnableItemUnlock;
            PluginConfig.EnableViewsUnlock = Config.EnableViewsUnlock;
            PluginConfig.EnableEmojiUnlock = Config.EnableEmojiUnlock;
            PluginConfig.EnableWebServer = Config.EnableWebServer;
            PluginConfig.WebServerPort = Config.WebServerPort;
            // 固定伪造角色配置
            PluginConfig.EnableFixedFakeCharacter = Config.EnableFixedFakeCharacter;
            PluginConfig.FixedCharacterId = Config.FixedCharacterId;
            PluginConfig.FixedSkinId = Config.FixedSkinId;
            PluginConfig.FixedTitleId = Config.FixedTitleId;
            PluginConfig.FixedViews = Config.FixedViews ?? new Dictionary<int, int>();
            PluginConfig.AllowDynamicRefresh = Config.AllowDynamicRefresh;
        }

        /// <summary>
        /// 从PluginConfig同步到运行时配置
        /// </summary>
        private static void SyncFromPluginConfig()
        {
            Config.EnableSkinUnlock = PluginConfig.EnableSkinUnlock;
            Config.EnableCharacterUnlock = PluginConfig.EnableCharacterUnlock;
            Config.EnableVoiceUnlock = PluginConfig.EnableVoiceUnlock;
            Config.HideLockUI = PluginConfig.HideLockUI;
            Config.EnableInGameSkinReplace = PluginConfig.EnableInGameSkinReplace;
            Config.EnableDebugLog = PluginConfig.EnableDebugLog;
            Config.EnableBlockedLogDisplay = PluginConfig.EnableBlockedLogDisplay;
            Config.BlockLogToServer = PluginConfig.BlockLogToServer;
            Config.BlockMatchInfo = PluginConfig.BlockMatchInfo;
            Config.EnableTitleUnlock = PluginConfig.EnableTitleUnlock;
            Config.EnableItemUnlock = PluginConfig.EnableItemUnlock;
            Config.EnableViewsUnlock = PluginConfig.EnableViewsUnlock;
            Config.EnableEmojiUnlock = PluginConfig.EnableEmojiUnlock;
            Config.EnableWebServer = PluginConfig.EnableWebServer;
            Config.WebServerPort = PluginConfig.WebServerPort;
            // 固定伪造角色配置
            Config.EnableFixedFakeCharacter = PluginConfig.EnableFixedFakeCharacter;
            Config.FixedCharacterId = PluginConfig.FixedCharacterId;
            Config.FixedSkinId = PluginConfig.FixedSkinId;
            Config.FixedTitleId = PluginConfig.FixedTitleId;
            Config.FixedViews = PluginConfig.FixedViews ?? new Dictionary<int, int>();
            Config.AllowDynamicRefresh = PluginConfig.AllowDynamicRefresh;
        }

        /// <summary>
        /// 更新单个配置项并保存
        /// </summary>
        public static void UpdateConfig(string key, object value)
        {
            try
            {
                var prop = typeof(RuntimeConfig).GetProperty(key);
                if (prop != null)
                {
                    if (prop.PropertyType == typeof(bool) && value is string strVal)
                    {
                        prop.SetValue(Config, strVal.ToLower() == "true");
                    }
                    else if (prop.PropertyType == typeof(int) && value is string intStrVal)
                    {
                        prop.SetValue(Config, int.Parse(intStrVal));
                    }
                    else
                    {
                        prop.SetValue(Config, value);
                    }
                    ApplyToPluginConfig();
                    Save();
                }
            }
            catch (Exception ex)
            {
                Utils.MyLogger(BepInEx.Logging.LogLevel.Error, $"[ConfigPersistence] UpdateConfig failed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 运行时配置数据类
    /// </summary>
    public class RuntimeConfig
    {
        // ======== 基础功能开关 ========
        public bool EnableSkinUnlock { get; set; } = true;
        public bool EnableCharacterUnlock { get; set; } = true;
        public bool EnableVoiceUnlock { get; set; } = true;
        public bool HideLockUI { get; set; } = true;
        public bool EnableInGameSkinReplace { get; set; } = true;

        // ======== 扩展功能开关 ========
        public bool EnableTitleUnlock { get; set; } = true;
        public bool EnableItemUnlock { get; set; } = true;
        public bool EnableViewsUnlock { get; set; } = true;
        public bool EnableEmojiUnlock { get; set; } = true;

        // ======== 调试选项 ========
        public bool EnableDebugLog { get; set; } = false;
        public bool EnableBlockedLogDisplay { get; set; } = true;
        public bool BlockLogToServer { get; set; } = true;
        public bool BlockMatchInfo { get; set; } = true;

        // ======== WebServer配置 ========
        public bool EnableWebServer { get; set; } = true;
        public int WebServerPort { get; set; } = 23333;

        // ======== 用户选择数据 ========
        public int SelectedTitle { get; set; } = 0;
        public int SelectedLoadingImage { get; set; } = 0;
        public Dictionary<int, int> CharacterSkins { get; set; } = new Dictionary<int, int>();
        public Dictionary<int, List<int>> CharacterViews { get; set; } = new Dictionary<int, List<int>>();

        // ======== 固定伪造角色配置 ========
        /// <summary>
        /// 是否启用固定伪造角色（启用后使用下方配置的角色/皮肤，而不是游戏内选择的）
        /// </summary>
        public bool EnableFixedFakeCharacter { get; set; } = false;

        /// <summary>
        /// 固定伪造的主角色ID（如 200001 = 一姬）
        /// </summary>
        public int FixedCharacterId { get; set; } = 0;

        /// <summary>
        /// 固定伪造的皮肤ID（如 400101 = 一姬默认皮肤）
        /// </summary>
        public int FixedSkinId { get; set; } = 0;

        /// <summary>
        /// 固定伪造的称号ID
        /// </summary>
        public int FixedTitleId { get; set; } = 0;

        /// <summary>
        /// 固定伪造的装扮列表（slot -> item_id）
        /// slot: 0=立绘, 1=头像框, 2=牌背, 3=牌桌, 4=特效, 5=BGM, 6=进场动画, 7=开立直动画, 8=和牌动画
        /// </summary>
        public Dictionary<int, int> FixedViews { get; set; } = new Dictionary<int, int>();

        /// <summary>
        /// 允许对局中动态刷新伪造配置
        /// </summary>
        public bool AllowDynamicRefresh { get; set; } = true;
    }

    /// <summary>
    /// 简易JSON解析器（避免依赖外部库）
    /// </summary>
    public static class SimpleJsonParser
    {
        public static string Serialize(RuntimeConfig config)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"EnableSkinUnlock\": {config.EnableSkinUnlock.ToString().ToLower()},");
            sb.AppendLine($"  \"EnableCharacterUnlock\": {config.EnableCharacterUnlock.ToString().ToLower()},");
            sb.AppendLine($"  \"EnableVoiceUnlock\": {config.EnableVoiceUnlock.ToString().ToLower()},");
            sb.AppendLine($"  \"HideLockUI\": {config.HideLockUI.ToString().ToLower()},");
            sb.AppendLine($"  \"EnableInGameSkinReplace\": {config.EnableInGameSkinReplace.ToString().ToLower()},");
            sb.AppendLine($"  \"EnableTitleUnlock\": {config.EnableTitleUnlock.ToString().ToLower()},");
            sb.AppendLine($"  \"EnableItemUnlock\": {config.EnableItemUnlock.ToString().ToLower()},");
            sb.AppendLine($"  \"EnableViewsUnlock\": {config.EnableViewsUnlock.ToString().ToLower()},");
            sb.AppendLine($"  \"EnableEmojiUnlock\": {config.EnableEmojiUnlock.ToString().ToLower()},");
            sb.AppendLine($"  \"EnableDebugLog\": {config.EnableDebugLog.ToString().ToLower()},");
            sb.AppendLine($"  \"EnableBlockedLogDisplay\": {config.EnableBlockedLogDisplay.ToString().ToLower()},");
            sb.AppendLine($"  \"BlockLogToServer\": {config.BlockLogToServer.ToString().ToLower()},");
            sb.AppendLine($"  \"BlockMatchInfo\": {config.BlockMatchInfo.ToString().ToLower()},");
            sb.AppendLine($"  \"EnableWebServer\": {config.EnableWebServer.ToString().ToLower()},");
            sb.AppendLine($"  \"WebServerPort\": {config.WebServerPort},");
            sb.AppendLine($"  \"SelectedTitle\": {config.SelectedTitle},");
            sb.AppendLine($"  \"SelectedLoadingImage\": {config.SelectedLoadingImage},");
            // 固定伪造角色配置
            sb.AppendLine($"  \"EnableFixedFakeCharacter\": {config.EnableFixedFakeCharacter.ToString().ToLower()},");
            sb.AppendLine($"  \"FixedCharacterId\": {config.FixedCharacterId},");
            sb.AppendLine($"  \"FixedSkinId\": {config.FixedSkinId},");
            sb.AppendLine($"  \"FixedTitleId\": {config.FixedTitleId},");
            sb.AppendLine($"  \"AllowDynamicRefresh\": {config.AllowDynamicRefresh.ToString().ToLower()},");
            // 序列化 FixedViews
            sb.Append("  \"FixedViews\": {");
            bool first = true;
            foreach (var kv in config.FixedViews)
            {
                if (!first) sb.Append(",");
                sb.Append($"\"{kv.Key}\":{kv.Value}");
                first = false;
            }
            sb.AppendLine("}");
            sb.AppendLine("}");
            return sb.ToString();
        }

        public static T Deserialize<T>(string json) where T : RuntimeConfig, new()
        {
            var config = new T();
            if (string.IsNullOrEmpty(json)) return config;

            try
            {
                // 简单的键值解析
                json = json.Replace("\r", "").Replace("\n", "").Replace(" ", "");
                
                config.EnableSkinUnlock = GetBoolValue(json, "EnableSkinUnlock", true);
                config.EnableCharacterUnlock = GetBoolValue(json, "EnableCharacterUnlock", true);
                config.EnableVoiceUnlock = GetBoolValue(json, "EnableVoiceUnlock", true);
                config.HideLockUI = GetBoolValue(json, "HideLockUI", true);
                config.EnableInGameSkinReplace = GetBoolValue(json, "EnableInGameSkinReplace", true);
                config.EnableTitleUnlock = GetBoolValue(json, "EnableTitleUnlock", true);
                config.EnableItemUnlock = GetBoolValue(json, "EnableItemUnlock", true);
                config.EnableViewsUnlock = GetBoolValue(json, "EnableViewsUnlock", true);
                config.EnableEmojiUnlock = GetBoolValue(json, "EnableEmojiUnlock", true);
                config.EnableDebugLog = GetBoolValue(json, "EnableDebugLog", false);
                config.EnableBlockedLogDisplay = GetBoolValue(json, "EnableBlockedLogDisplay", true);
                config.BlockLogToServer = GetBoolValue(json, "BlockLogToServer", true);
                config.BlockMatchInfo = GetBoolValue(json, "BlockMatchInfo", true);
                config.EnableWebServer = GetBoolValue(json, "EnableWebServer", true);
                config.WebServerPort = GetIntValue(json, "WebServerPort", 23333);
                config.SelectedTitle = GetIntValue(json, "SelectedTitle", 0);
                config.SelectedLoadingImage = GetIntValue(json, "SelectedLoadingImage", 0);
                // 固定伪造角色配置
                config.EnableFixedFakeCharacter = GetBoolValue(json, "EnableFixedFakeCharacter", false);
                config.FixedCharacterId = GetIntValue(json, "FixedCharacterId", 0);
                config.FixedSkinId = GetIntValue(json, "FixedSkinId", 0);
                config.FixedTitleId = GetIntValue(json, "FixedTitleId", 0);
                config.AllowDynamicRefresh = GetBoolValue(json, "AllowDynamicRefresh", true);
                config.FixedViews = GetDictIntValue(json, "FixedViews");
            }
            catch { }

            return config;
        }

        private static Dictionary<int, int> GetDictIntValue(string json, string key)
        {
            var result = new Dictionary<int, int>();
            try
            {
                string pattern = $"\"{key}\":{{";
                int idx = json.IndexOf(pattern);
                if (idx < 0) return result;
                
                int start = idx + pattern.Length;
                int end = json.IndexOf('}', start);
                if (end < 0) return result;
                
                string content = json.Substring(start, end - start);
                // 解析 "slot":item_id 格式
                var pairs = content.Split(',');
                foreach (var pair in pairs)
                {
                    var kv = pair.Split(':');
                    if (kv.Length == 2)
                    {
                        string keyStr = kv[0].Trim().Trim('"');
                        string valStr = kv[1].Trim();
                        if (int.TryParse(keyStr, out int k) && int.TryParse(valStr, out int v))
                        {
                            result[k] = v;
                        }
                    }
                }
            }
            catch { }
            return result;
        }

        private static bool GetBoolValue(string json, string key, bool defaultValue)
        {
            try
            {
                string pattern = $"\"{key}\":";
                int idx = json.IndexOf(pattern);
                if (idx < 0) return defaultValue;
                
                int start = idx + pattern.Length;
                int end = json.IndexOfAny(new[] { ',', '}' }, start);
                string value = json.Substring(start, end - start).Trim();
                return value.ToLower() == "true";
            }
            catch { return defaultValue; }
        }

        private static int GetIntValue(string json, string key, int defaultValue)
        {
            try
            {
                string pattern = $"\"{key}\":";
                int idx = json.IndexOf(pattern);
                if (idx < 0) return defaultValue;
                
                int start = idx + pattern.Length;
                int end = json.IndexOfAny(new[] { ',', '}' }, start);
                string value = json.Substring(start, end - start).Trim();
                return int.Parse(value);
            }
            catch { return defaultValue; }
        }
    }
}
