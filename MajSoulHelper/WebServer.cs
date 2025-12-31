using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace MajSoulHelper
{
    /// <summary>
    /// 轻量级HTTP服务器（使用TcpListener实现，避免HttpListener兼容性问题）
    /// 提供Web配置界面和REST API
    /// </summary>
    public static class WebServer
    {
        private static TcpListener _listener;
        private static Thread _listenerThread;
        private static bool _isRunning = false;
        private static readonly object _lock = new object();

        /// <summary>
        /// 启动Web服务器
        /// </summary>
        public static void Start()
        {
            if (!PluginConfig.EnableWebServer) return;
            if (_isRunning) return;

            lock (_lock)
            {
                try
                {
                    _listener = new TcpListener(IPAddress.Loopback, PluginConfig.WebServerPort);
                    _listener.Start();
                    _isRunning = true;

                    _listenerThread = new Thread(ListenLoop)
                    {
                        IsBackground = true,
                        Name = "MajSoulHelper-WebServer"
                    };
                    _listenerThread.Start();

                    Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, 
                        $"[WebServer] Started at http://127.0.0.1:{PluginConfig.WebServerPort}/");
                }
                catch (Exception ex)
                {
                    Utils.MyLogger(BepInEx.Logging.LogLevel.Error, 
                        $"[WebServer] Failed to start: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 停止Web服务器
        /// </summary>
        public static void Stop()
        {
            lock (_lock)
            {
                _isRunning = false;
                try
                {
                    _listener?.Stop();
                }
                catch { }
                Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, "[WebServer] Stopped");
            }
        }

        /// <summary>
        /// 监听循环
        /// </summary>
        private static void ListenLoop()
        {
            while (_isRunning)
            {
                try
                {
                    var client = _listener.AcceptTcpClient();
                    ThreadPool.QueueUserWorkItem(_ => HandleClient(client));
                }
                catch (SocketException)
                {
                    // 服务器停止时正常退出
                    break;
                }
                catch (Exception ex)
                {
                    if (_isRunning)
                    {
                        Utils.MyLogger(BepInEx.Logging.LogLevel.Error, 
                            $"[WebServer] Error: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// 处理客户端连接
        /// </summary>
        private static void HandleClient(TcpClient client)
        {
            try
            {
                using (client)
                using (var stream = client.GetStream())
                {
                    client.ReceiveTimeout = 5000;
                    client.SendTimeout = 5000;

                    // 读取HTTP请求
                    var reader = new StreamReader(stream, Encoding.UTF8);
                    string requestLine = reader.ReadLine();
                    if (string.IsNullOrEmpty(requestLine)) return;

                    // 解析请求行
                    string[] parts = requestLine.Split(' ');
                    if (parts.Length < 2) return;

                    string method = parts[0].ToUpper();
                    string path = parts[1].ToLower();

                    // 读取请求头
                    var headers = new Dictionary<string, string>();
                    string line;
                    int contentLength = 0;
                    while (!string.IsNullOrEmpty(line = reader.ReadLine()))
                    {
                        int colonIndex = line.IndexOf(':');
                        if (colonIndex > 0)
                        {
                            string key = line.Substring(0, colonIndex).Trim().ToLower();
                            string value = line.Substring(colonIndex + 1).Trim();
                            headers[key] = value;
                            if (key == "content-length")
                            {
                                int.TryParse(value, out contentLength);
                            }
                        }
                    }

                    // 读取请求体
                    string requestBody = "";
                    if (contentLength > 0)
                    {
                        char[] buffer = new char[contentLength];
                        reader.Read(buffer, 0, contentLength);
                        requestBody = new string(buffer);
                    }

                    // 处理请求
                    string responseText;
                    string contentType = "application/json";
                    int statusCode = 200;

                    // CORS预检请求
                    if (method == "OPTIONS")
                    {
                        SendResponse(stream, 200, "text/plain", "", true);
                        return;
                    }

                    switch (path)
                    {
                        case "/":
                        case "/index.html":
                            responseText = WebUI.GetIndexHtml();
                            contentType = "text/html; charset=utf-8";
                            break;

                        case "/api/config":
                            if (method == "GET")
                            {
                                responseText = HandleGetConfig();
                            }
                            else if (method == "POST")
                            {
                                responseText = HandlePostConfig(requestBody);
                            }
                            else
                            {
                                responseText = "{\"error\": \"Method not allowed\"}";
                                statusCode = 405;
                            }
                            break;

                        case "/api/status":
                            responseText = HandleGetStatus();
                            break;

                        case "/api/cache/clear":
                            if (method == "POST")
                            {
                                responseText = HandleClearCache();
                            }
                            else
                            {
                                responseText = "{\"error\": \"Method not allowed\"}";
                                statusCode = 405;
                            }
                            break;

                        case "/api/save":
                            if (method == "POST")
                            {
                                responseText = HandleSaveConfig();
                            }
                            else
                            {
                                responseText = "{\"error\": \"Method not allowed\"}";
                                statusCode = 405;
                            }
                            break;

                        // ======== 固定伪造角色API ========
                        case "/api/fake/config":
                            if (method == "GET")
                            {
                                responseText = HandleGetFakeConfig();
                            }
                            else if (method == "POST")
                            {
                                responseText = HandlePostFakeConfig(requestBody);
                            }
                            else
                            {
                                responseText = "{\"error\": \"Method not allowed\"}";
                                statusCode = 405;
                            }
                            break;

                        case "/api/fake/characters":
                            responseText = HandleGetCharacterList();
                            break;

                        case "/api/fake/skins":
                            responseText = HandleGetSkinList(requestBody);
                            break;

                        case "/api/fake/refresh":
                            if (method == "POST")
                            {
                                responseText = HandleRefreshFakeData();
                            }
                            else
                            {
                                responseText = "{\"error\": \"Method not allowed\"}";
                                statusCode = 405;
                            }
                            break;

                        default:
                            responseText = "{\"error\": \"Not found\"}";
                            statusCode = 404;
                            break;
                    }

                    SendResponse(stream, statusCode, contentType, responseText, true);
                }
            }
            catch (Exception ex)
            {
                Utils.MyLogger(BepInEx.Logging.LogLevel.Error, 
                    $"[WebServer] HandleClient error: {ex.Message}");
            }
        }

        /// <summary>
        /// 发送HTTP响应
        /// </summary>
        private static void SendResponse(NetworkStream stream, int statusCode, string contentType, string body, bool cors = false)
        {
            string statusText = statusCode == 200 ? "OK" : (statusCode == 404 ? "Not Found" : "Error");
            byte[] bodyBytes = Encoding.UTF8.GetBytes(body);

            var sb = new StringBuilder();
            sb.AppendLine($"HTTP/1.1 {statusCode} {statusText}");
            sb.AppendLine($"Content-Type: {contentType}");
            sb.AppendLine($"Content-Length: {bodyBytes.Length}");
            sb.AppendLine("Connection: close");
            if (cors)
            {
                sb.AppendLine("Access-Control-Allow-Origin: *");
                sb.AppendLine("Access-Control-Allow-Methods: GET, POST, OPTIONS");
                sb.AppendLine("Access-Control-Allow-Headers: Content-Type");
            }
            sb.AppendLine();

            byte[] headerBytes = Encoding.UTF8.GetBytes(sb.ToString());
            stream.Write(headerBytes, 0, headerBytes.Length);
            stream.Write(bodyBytes, 0, bodyBytes.Length);
        }

        /// <summary>
        /// 获取当前配置
        /// </summary>
        private static string HandleGetConfig()
        {
            var config = ConfigPersistence.CurrentConfig;
            return $@"{{
  ""enableSkinUnlock"": {config.EnableSkinUnlock.ToString().ToLower()},
  ""enableCharacterUnlock"": {config.EnableCharacterUnlock.ToString().ToLower()},
  ""enableVoiceUnlock"": {config.EnableVoiceUnlock.ToString().ToLower()},
  ""enableTitleUnlock"": {config.EnableTitleUnlock.ToString().ToLower()},
  ""enableItemUnlock"": {config.EnableItemUnlock.ToString().ToLower()},
  ""enableViewsUnlock"": {config.EnableViewsUnlock.ToString().ToLower()},
  ""enableEmojiUnlock"": {config.EnableEmojiUnlock.ToString().ToLower()},
  ""hideLockUI"": {config.HideLockUI.ToString().ToLower()},
  ""enableInGameSkinReplace"": {config.EnableInGameSkinReplace.ToString().ToLower()},
  ""blockLogToServer"": {config.BlockLogToServer.ToString().ToLower()},
  ""blockMatchInfo"": {config.BlockMatchInfo.ToString().ToLower()},
  ""enableDebugLog"": {config.EnableDebugLog.ToString().ToLower()},
  ""enableBlockedLogDisplay"": {config.EnableBlockedLogDisplay.ToString().ToLower()},
  ""enableFixedFakeCharacter"": {config.EnableFixedFakeCharacter.ToString().ToLower()},
  ""fixedCharacterId"": {config.FixedCharacterId},
  ""fixedSkinId"": {config.FixedSkinId},
  ""fixedTitleId"": {config.FixedTitleId},
  ""frameRateBase"": {PluginConfig.FrameRateBase},
  ""targetTimeScale"": {PluginConfig.TargetTimeScale.ToString(System.Globalization.CultureInfo.InvariantCulture)},
  ""webServerPort"": {config.WebServerPort}
}}";
        }

        /// <summary>
        /// 更新配置
        /// </summary>
        private static string HandlePostConfig(string requestBody)
        {
            try
            {
                // 简单的JSON解析
                var config = ConfigPersistence.CurrentConfig;
                
                // 解析每个字段
                config.EnableSkinUnlock = ParseJsonBool(requestBody, "enableSkinUnlock", config.EnableSkinUnlock);
                config.EnableCharacterUnlock = ParseJsonBool(requestBody, "enableCharacterUnlock", config.EnableCharacterUnlock);
                config.EnableVoiceUnlock = ParseJsonBool(requestBody, "enableVoiceUnlock", config.EnableVoiceUnlock);
                config.EnableTitleUnlock = ParseJsonBool(requestBody, "enableTitleUnlock", config.EnableTitleUnlock);
                config.EnableItemUnlock = ParseJsonBool(requestBody, "enableItemUnlock", config.EnableItemUnlock);
                config.EnableViewsUnlock = ParseJsonBool(requestBody, "enableViewsUnlock", config.EnableViewsUnlock);
                config.EnableEmojiUnlock = ParseJsonBool(requestBody, "enableEmojiUnlock", config.EnableEmojiUnlock);
                config.HideLockUI = ParseJsonBool(requestBody, "hideLockUI", config.HideLockUI);
                config.EnableInGameSkinReplace = ParseJsonBool(requestBody, "enableInGameSkinReplace", config.EnableInGameSkinReplace);
                config.BlockLogToServer = ParseJsonBool(requestBody, "blockLogToServer", config.BlockLogToServer);
                config.BlockMatchInfo = ParseJsonBool(requestBody, "blockMatchInfo", config.BlockMatchInfo);
                config.EnableDebugLog = ParseJsonBool(requestBody, "enableDebugLog", config.EnableDebugLog);
                config.EnableBlockedLogDisplay = ParseJsonBool(requestBody, "enableBlockedLogDisplay", config.EnableBlockedLogDisplay);
                config.WebServerPort = ParseJsonInt(requestBody, "webServerPort", config.WebServerPort);

                // 解析固定伪造角色配置
                config.EnableFixedFakeCharacter = ParseJsonBool(requestBody, "enableFixedFakeCharacter", config.EnableFixedFakeCharacter);
                config.FixedCharacterId = ParseJsonInt(requestBody, "fixedCharacterId", config.FixedCharacterId);
                config.FixedSkinId = ParseJsonInt(requestBody, "fixedSkinId", config.FixedSkinId);
                config.FixedTitleId = ParseJsonInt(requestBody, "fixedTitleId", config.FixedTitleId);

                // 解析帧率配置
                int frameRate = ParseJsonInt(requestBody, "frameRateBase", PluginConfig.FrameRateBase);
                if (frameRate > 0 && frameRate <= 240)
                {
                    PluginConfig.FrameRateBase = frameRate;
                    PluginConfig.isFrameRateBaseNeedUpdate = true;
                }
                float timeScale = ParseJsonFloat(requestBody, "targetTimeScale", PluginConfig.TargetTimeScale);
                if (timeScale > 0 && timeScale <= 4.0f)
                {
                    PluginConfig.TargetTimeScale = timeScale;
                    PluginConfig.isFrameRateBaseNeedUpdate = true;
                }

                // 同步到静态配置
                SyncToPluginConfig(config);

                return "{\"success\": true, \"message\": \"Configuration updated\"}";
            }
            catch (Exception ex)
            {
                return $"{{\"success\": false, \"error\": \"{ex.Message}\"}}";
            }
        }

        /// <summary>
        /// 获取运行状态
        /// </summary>
        private static string HandleGetStatus()
        {
            return $@"{{
  ""running"": true,
  ""version"": ""{PluginInfo.PLUGIN_VERSION}"",
  ""patchedModules"": {SkinUnlocker.GetPatchedModuleCount()},
  ""cachedPatches"": {SkinUnlocker.GetCachedPatchCount()}
}}";
        }

        /// <summary>
        /// 清除补丁缓存
        /// </summary>
        private static string HandleClearCache()
        {
            SkinUnlocker.ClearCache();
            return "{\"success\": true, \"message\": \"Cache cleared\"}";
        }

        /// <summary>
        /// 保存配置到文件
        /// </summary>
        private static string HandleSaveConfig()
        {
            try
            {
                ConfigPersistence.Save();
                return "{\"success\": true, \"message\": \"Configuration saved to file\"}";
            }
            catch (Exception ex)
            {
                return $"{{\"success\": false, \"error\": \"{ex.Message}\"}}";
            }
        }

        // ======== 固定伪造角色API处理方法 ========

        /// <summary>
        /// 获取固定伪造配置
        /// </summary>
        private static string HandleGetFakeConfig()
        {
            var config = ConfigPersistence.CurrentConfig;
            var viewsJson = new StringBuilder("{");
            bool first = true;
            foreach (var kv in config.FixedViews)
            {
                if (!first) viewsJson.Append(",");
                viewsJson.Append($"\"{kv.Key}\":{kv.Value}");
                first = false;
            }
            viewsJson.Append("}");

            return $@"{{
  ""enableFixedFakeCharacter"": {config.EnableFixedFakeCharacter.ToString().ToLower()},
  ""fixedCharacterId"": {config.FixedCharacterId},
  ""fixedSkinId"": {config.FixedSkinId},
  ""fixedTitleId"": {config.FixedTitleId},
  ""fixedViews"": {viewsJson},
  ""allowDynamicRefresh"": {config.AllowDynamicRefresh.ToString().ToLower()}
}}";
        }

        /// <summary>
        /// 更新固定伪造配置
        /// </summary>
        private static string HandlePostFakeConfig(string requestBody)
        {
            try
            {
                var config = ConfigPersistence.CurrentConfig;

                config.EnableFixedFakeCharacter = ParseJsonBool(requestBody, "enableFixedFakeCharacter", config.EnableFixedFakeCharacter);
                config.FixedCharacterId = ParseJsonInt(requestBody, "fixedCharacterId", config.FixedCharacterId);
                config.FixedSkinId = ParseJsonInt(requestBody, "fixedSkinId", config.FixedSkinId);
                config.FixedTitleId = ParseJsonInt(requestBody, "fixedTitleId", config.FixedTitleId);
                config.AllowDynamicRefresh = ParseJsonBool(requestBody, "allowDynamicRefresh", config.AllowDynamicRefresh);

                // 解析 fixedViews
                var views = ParseJsonDict(requestBody, "fixedViews");
                if (views.Count > 0)
                {
                    config.FixedViews = views;
                }

                // 同步到PluginConfig
                PluginConfig.EnableFixedFakeCharacter = config.EnableFixedFakeCharacter;
                PluginConfig.FixedCharacterId = config.FixedCharacterId;
                PluginConfig.FixedSkinId = config.FixedSkinId;
                PluginConfig.FixedTitleId = config.FixedTitleId;
                PluginConfig.FixedViews = config.FixedViews;
                PluginConfig.AllowDynamicRefresh = config.AllowDynamicRefresh;

                // 通知Lua层刷新（如果允许动态刷新）
                if (config.AllowDynamicRefresh)
                {
                    SkinUnlocker.NotifyConfigChanged();
                }

                return "{\"success\": true, \"message\": \"Fake character config updated\"}";
            }
            catch (Exception ex)
            {
                return $"{{\"success\": false, \"error\": \"{ex.Message}\"}}";
            }
        }

        /// <summary>
        /// 获取可用角色列表（从 CharacterDataCache 获取）
        /// </summary>
        private static string HandleGetCharacterList()
        {
            // 优先从 CharacterDataCache 获取
            var characters = CharacterDataCache.GetAllCharacters();
            
            // 如果缓存为空，尝试从 SkinUnlocker 获取
            if (characters.Count == 0)
            {
                characters = SkinUnlocker.GetCachedCharacters();
                
                // 同步到 CharacterDataCache
                foreach (var kv in characters)
                {
                    CharacterDataCache.AddCharacter(kv.Key, kv.Value);
                }
                if (characters.Count > 0)
                {
                    CharacterDataCache.Save();
                }
            }
            
            // 如果仍然为空，提供默认的角色列表
            if (characters.Count == 0)
            {
                characters = GetDefaultCharacterList();
            }
            
            var sb = new StringBuilder();
            sb.Append("{\"characters\":[");
            bool first = true;
            foreach (var c in characters)
            {
                if (!first) sb.Append(",");
                sb.Append($"{{\"id\":{c.Key},\"name\":\"{EscapeJson(c.Value)}\"}}");
                first = false;
            }
            sb.Append("]}");
            return sb.ToString();
        }

        /// <summary>
        /// 获取指定角色的皮肤列表（从 CharacterDataCache 获取）
        /// </summary>
        private static string HandleGetSkinList(string requestBody)
        {
            int charId = ParseJsonInt(requestBody, "characterId", 0);
            
            // 优先从 CharacterDataCache 获取
            var skins = charId > 0 
                ? CharacterDataCache.GetCharacterSkins(charId)
                : CharacterDataCache.GetAllSkins();
            
            // 如果缓存为空，尝试从 SkinUnlocker 获取
            if (skins.Count == 0)
            {
                skins = SkinUnlocker.GetCachedSkins(charId);
            }
            
            // 如果仍然为空，提供默认的皮肤列表
            if (skins.Count == 0 && charId > 0)
            {
                skins = GetDefaultSkinList(charId);
            }
            
            var sb = new StringBuilder();
            sb.Append("{\"skins\":[");
            bool first = true;
            foreach (var s in skins)
            {
                if (!first) sb.Append(",");
                sb.Append($"{{\"id\":{s.Key},\"name\":\"{EscapeJson(s.Value)}\"}}");
                first = false;
            }
            sb.Append("]}");
            return sb.ToString();
        }

        /// <summary>
        /// 获取默认角色列表（硬编码的常用角色）
        /// </summary>
        private static Dictionary<int, string> GetDefaultCharacterList()
        {
            return new Dictionary<int, string>
            {
                { 200001, "一姬" },
                { 200002, "二阶堂美树" },
                { 200003, "藤田佳奈" },
                { 200004, "三上千织" },
                { 200005, "相原舞" },
                { 200006, "抚子" },
                { 200007, "八木唯" },
                { 200008, "九条璃雨" },
                { 200009, "泽尼娅" },
                { 200010, "卡维" },
                { 200011, "四宫夏生" },
                { 200012, "汪次郎" },
                { 200013, "一之濑空" },
                { 200014, "明智英树" },
                { 200015, "轻库娘" },
                { 200016, "莎拉" },
                { 200017, "二之宫花" },
                { 200018, "白石奈奈" },
                { 200019, "小鸟游雏田" },
                { 200020, "五十岚阳菜" },
                { 200021, "凉宫杏树" },
                { 200022, "约瑟夫" },
                { 200023, "斋藤治" },
                { 200024, "北见纱和子" },
                { 200025, "艾因" },
                { 200026, "雏桃" },
                { 200027, "月见山" },
                { 200028, "藤本绮罗" },
                { 200029, "辉夜姬" },
                { 200030, "如月莲" },
                { 200031, "石原碓海" },
                { 200032, "艾丽莎" },
                { 200033, "寺崎千穗理" },
                { 200034, "宫永咲" },
                { 200035, "原村和" },
                { 200036, "天江衣" },
                { 200037, "宫永照" },
                { 200038, "福姬" },
                { 200039, "七夕" },
                { 200040, "蛇喰梦子" },
                { 200041, "早乙女芽亚里" },
                { 200042, "生志摩妄" },
                { 200043, "桃喰绮罗莉" },
                { 200044, "七海礼奈" },
                { 200045, "A-37" },
                { 200046, "姬川响" },
                { 200047, "莱恩" },
                { 200048, "森川绫子" },
                { 200049, " 的川夏彦" },
                { 200050, "赤木茂" },
            };
        }

        /// <summary>
        /// 获取默认皮肤列表（根据角色ID生成，包含实际皮肤名称）
        /// </summary>
        private static Dictionary<int, string> GetDefaultSkinList(int characterId)
        {
            // 皮肤名称数据（从 leak 文件中提取）
            var skinNames = GetSkinNamesForCharacter(characterId);
            if (skinNames.Count > 0)
            {
                return skinNames;
            }
            
            // 如果没有预定义数据，生成默认列表
            var skins = new Dictionary<int, string>();
            int baseId = characterId + 200000; // 200001 -> 400001
            skins[baseId] = "默认皮肤";
            skins[baseId + 1] = "契约";
            for (int i = 2; i < 10; i++)
            {
                int skinId = baseId + i;
                skins[skinId] = $"特殊皮肤 {i}";
            }
            return skins;
        }

        /// <summary>
        /// 获取特定角色的皮肤名称（硬编码常用角色皮肤）
        /// </summary>
        private static Dictionary<int, string> GetSkinNamesForCharacter(int characterId)
        {
            var skins = new Dictionary<int, string>();
            switch (characterId)
            {
                case 200001: // 一姬
                    skins[400101] = "一姬";
                    skins[400102] = "契约";
                    skins[400103] = "海滩派对";
                    skins[400104] = "新年初诣";
                    skins[400105] = "一姬当千";
                    skins[400106] = "绮春歌";
                    skins[400107] = "校园微风";
                    break;
                case 200002: // 二阶堂美树
                    skins[400201] = "二阶堂美树";
                    skins[400202] = "契约";
                    skins[400203] = "化妆舞会";
                    skins[400206] = "万象沐春";
                    skins[400207] = "鸢尾花之夜";
                    skins[400208] = "玩转夏日";
                    break;
                case 200003: // 藤田佳奈
                    skins[400301] = "藤田佳奈";
                    skins[400302] = "契约";
                    skins[400303] = "圣诞嘉年华";
                    skins[400304] = "暗夜法则";
                    break;
                case 200004: // 三上千织
                    skins[400401] = "三上千织";
                    skins[400402] = "契约";
                    break;
                case 200005: // 相原舞
                    skins[400501] = "相原舞";
                    skins[400502] = "契约";
                    skins[400505] = "昭华年";
                    break;
                case 200006: // 抚子
                    skins[400601] = "抚子";
                    skins[400602] = "契约";
                    break;
                case 200007: // 八木唯
                    skins[400701] = "八木唯";
                    skins[400702] = "契约";
                    skins[400706] = "魇魔之约";
                    break;
                case 200008: // 九条璃雨
                    skins[400801] = "九条璃雨";
                    skins[400802] = "契约";
                    break;
                case 200044: // 七海礼奈
                    skins[404401] = "七海礼奈";
                    skins[404402] = "契约";
                    skins[404404] = "云窗春几枝";
                    break;
                default:
                    // 默认生成
                    int baseId = 400001 + (characterId - 200000) * 100;
                    skins[baseId] = "默认皮肤";
                    skins[baseId + 1] = "契约";
                    break;
            }
            return skins;
        }

        /// <summary>
        /// 强制刷新伪造数据到游戏
        /// </summary>
        private static string HandleRefreshFakeData()
        {
            try
            {
                SkinUnlocker.NotifyConfigChanged();
                return "{\"success\": true, \"message\": \"Fake data refreshed\"}";
            }
            catch (Exception ex)
            {
                return $"{{\"success\": false, \"error\": \"{ex.Message}\"}}";
            }
        }

        /// <summary>
        /// 从JSON解析字典
        /// </summary>
        private static Dictionary<int, int> ParseJsonDict(string json, string key)
        {
            var result = new Dictionary<int, int>();
            try
            {
                string pattern = $"\"{key}\"\\s*:\\s*\\{{([^}}]*)\\}}";
                var match = System.Text.RegularExpressions.Regex.Match(json, pattern);
                if (match.Success)
                {
                    string content = match.Groups[1].Value;
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
            }
            catch { }
            return result;
        }

        /// <summary>
        /// 转义JSON字符串
        /// </summary>
        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }

        /// <summary>
        /// 从JSON字符串解析布尔值
        /// </summary>
        private static bool ParseJsonBool(string json, string key, bool defaultValue)
        {
            string pattern = $"\"{key}\"\\s*:\\s*(true|false)";
            var match = System.Text.RegularExpressions.Regex.Match(json, pattern, 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return match.Groups[1].Value.ToLower() == "true";
            }
            return defaultValue;
        }

        /// <summary>
        /// 从JSON字符串解析整数值
        /// </summary>
        private static int ParseJsonInt(string json, string key, int defaultValue)
        {
            string pattern = $"\"{key}\"\\s*:\\s*(\\d+)";
            var match = System.Text.RegularExpressions.Regex.Match(json, pattern);
            if (match.Success && int.TryParse(match.Groups[1].Value, out int value))
            {
                return value;
            }
            return defaultValue;
        }

        /// <summary>
        /// 从JSON字符串解析浮点数值
        /// </summary>
        private static float ParseJsonFloat(string json, string key, float defaultValue)
        {
            string pattern = $"\"{key}\"\\s*:\\s*([\\d.]+)";
            var match = System.Text.RegularExpressions.Regex.Match(json, pattern);
            if (match.Success && float.TryParse(match.Groups[1].Value, 
                System.Globalization.NumberStyles.Float, 
                System.Globalization.CultureInfo.InvariantCulture, out float value))
            {
                return value;
            }
            return defaultValue;
        }

        /// <summary>
        /// 同步配置到PluginConfig静态类
        /// </summary>
        private static void SyncToPluginConfig(RuntimeConfig config)
        {
            PluginConfig.EnableSkinUnlock = config.EnableSkinUnlock;
            PluginConfig.EnableCharacterUnlock = config.EnableCharacterUnlock;
            PluginConfig.EnableVoiceUnlock = config.EnableVoiceUnlock;
            PluginConfig.EnableTitleUnlock = config.EnableTitleUnlock;
            PluginConfig.EnableItemUnlock = config.EnableItemUnlock;
            PluginConfig.EnableViewsUnlock = config.EnableViewsUnlock;
            PluginConfig.EnableEmojiUnlock = config.EnableEmojiUnlock;
            PluginConfig.HideLockUI = config.HideLockUI;
            PluginConfig.EnableInGameSkinReplace = config.EnableInGameSkinReplace;
            PluginConfig.BlockLogToServer = config.BlockLogToServer;
            PluginConfig.BlockMatchInfo = config.BlockMatchInfo;
            PluginConfig.EnableDebugLog = config.EnableDebugLog;
            PluginConfig.EnableBlockedLogDisplay = config.EnableBlockedLogDisplay;
            // 固定伪造角色配置同步，确保 /api/save 能持久化
            PluginConfig.EnableFixedFakeCharacter = config.EnableFixedFakeCharacter;
            PluginConfig.FixedCharacterId = config.FixedCharacterId;
            PluginConfig.FixedSkinId = config.FixedSkinId;
            PluginConfig.FixedTitleId = config.FixedTitleId;
            PluginConfig.FixedViews = config.FixedViews ?? new Dictionary<int, int>();
            PluginConfig.AllowDynamicRefresh = config.AllowDynamicRefresh;
        }
    }

    /// <summary>
    /// Web界面HTML生成器
    /// </summary>
    public static class WebUI
    {
        public static string GetIndexHtml()
        {
            return @"<!DOCTYPE html>
<html lang=""zh-CN"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>MajSoulHelper 控制面板</title>
    <style>
        * { box-sizing: border-box; margin: 0; padding: 0; }
        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, sans-serif;
            background: linear-gradient(135deg, #1a1a2e 0%, #16213e 100%);
            color: #e8e8e8;
            min-height: 100vh;
            padding: 20px;
        }
        .container {
            max-width: 800px;
            margin: 0 auto;
        }
        h1 {
            text-align: center;
            margin-bottom: 30px;
            color: #00d4ff;
            text-shadow: 0 0 10px rgba(0, 212, 255, 0.3);
        }
        .card {
            background: rgba(255, 255, 255, 0.05);
            border-radius: 12px;
            padding: 20px;
            margin-bottom: 20px;
            border: 1px solid rgba(255, 255, 255, 0.1);
        }
        .card h2 {
            color: #00d4ff;
            margin-bottom: 15px;
            font-size: 1.2em;
            border-bottom: 1px solid rgba(0, 212, 255, 0.2);
            padding-bottom: 10px;
        }
        .setting-row {
            display: flex;
            justify-content: space-between;
            align-items: center;
            padding: 12px 0;
            border-bottom: 1px solid rgba(255, 255, 255, 0.05);
        }
        .setting-row:last-child { border-bottom: none; }
        .setting-label {
            display: flex;
            flex-direction: column;
        }
        .setting-label span { font-size: 14px; }
        .setting-label small { color: #888; font-size: 12px; margin-top: 4px; }
        .toggle {
            position: relative;
            width: 50px;
            height: 26px;
        }
        .toggle input {
            opacity: 0;
            width: 0;
            height: 0;
        }
        .toggle .slider {
            position: absolute;
            cursor: pointer;
            top: 0;
            left: 0;
            right: 0;
            bottom: 0;
            background-color: #444;
            transition: 0.3s;
            border-radius: 26px;
        }
        .toggle .slider:before {
            position: absolute;
            content: '';
            height: 20px;
            width: 20px;
            left: 3px;
            bottom: 3px;
            background-color: white;
            transition: 0.3s;
            border-radius: 50%;
        }
        .toggle input:checked + .slider {
            background-color: #00d4ff;
        }
        .toggle input:checked + .slider:before {
            transform: translateX(24px);
        }
        .status-bar {
            display: flex;
            gap: 20px;
            flex-wrap: wrap;
        }
        .status-item {
            display: flex;
            align-items: center;
            gap: 8px;
        }
        .status-dot {
            width: 10px;
            height: 10px;
            border-radius: 50%;
            background: #00ff88;
            box-shadow: 0 0 10px #00ff88;
        }
        .btn-group {
            display: flex;
            gap: 10px;
            margin-top: 20px;
        }
        .btn {
            padding: 10px 20px;
            border: none;
            border-radius: 6px;
            cursor: pointer;
            font-size: 14px;
            transition: all 0.2s;
        }
        .btn-primary {
            background: linear-gradient(135deg, #00d4ff, #0099cc);
            color: white;
        }
        .btn-primary:hover {
            transform: translateY(-2px);
            box-shadow: 0 4px 15px rgba(0, 212, 255, 0.4);
        }
        .btn-secondary {
            background: rgba(255, 255, 255, 0.1);
            color: #e8e8e8;
            border: 1px solid rgba(255, 255, 255, 0.2);
        }
        .btn-secondary:hover {
            background: rgba(255, 255, 255, 0.15);
        }
        .toast {
            position: fixed;
            bottom: 20px;
            right: 20px;
            padding: 15px 25px;
            background: #00d4ff;
            color: #1a1a2e;
            border-radius: 8px;
            opacity: 0;
            transform: translateY(20px);
            transition: all 0.3s;
            font-weight: 500;
        }
        .toast.show {
            opacity: 1;
            transform: translateY(0);
        }
    </style>
</head>
<body>
    <div class=""container"">
        <h1>🀄 MajSoulHelper 控制面板</h1>
        
        <div class=""card"">
            <h2>📊 运行状态</h2>
            <div class=""status-bar"">
                <div class=""status-item"">
                    <div class=""status-dot""></div>
                    <span id=""status-running"">运行中</span>
                </div>
                <div class=""status-item"">
                    <span>版本: <strong id=""status-version"">-</strong></span>
                </div>
                <div class=""status-item"">
                    <span>已补丁模块: <strong id=""status-modules"">-</strong></span>
                </div>
                <div class=""status-item"">
                    <span>缓存补丁: <strong id=""status-cached"">-</strong></span>
                </div>
            </div>
        </div>

        <div class=""card"">
            <h2>🔓 解锁功能</h2>
            <div class=""setting-row"">
                <div class=""setting-label"">
                    <span>皮肤解锁</span>
                    <small>解锁所有角色皮肤（本地显示）</small>
                </div>
                <label class=""toggle"">
                    <input type=""checkbox"" id=""enableSkinUnlock"">
                    <span class=""slider""></span>
                </label>
            </div>
            <div class=""setting-row"">
                <div class=""setting-label"">
                    <span>角色解锁</span>
                    <small>解锁所有角色（本地显示）</small>
                </div>
                <label class=""toggle"">
                    <input type=""checkbox"" id=""enableCharacterUnlock"">
                    <span class=""slider""></span>
                </label>
            </div>
            <div class=""setting-row"">
                <div class=""setting-label"">
                    <span>语音解锁</span>
                    <small>解锁所有角色语音</small>
                </div>
                <label class=""toggle"">
                    <input type=""checkbox"" id=""enableVoiceUnlock"">
                    <span class=""slider""></span>
                </label>
            </div>
            <div class=""setting-row"">
                <div class=""setting-label"">
                    <span>称号解锁</span>
                    <small>解锁所有称号（本地显示）</small>
                </div>
                <label class=""toggle"">
                    <input type=""checkbox"" id=""enableTitleUnlock"">
                    <span class=""slider""></span>
                </label>
            </div>
            <div class=""setting-row"">
                <div class=""setting-label"">
                    <span>道具解锁</span>
                    <small>解锁所有装饰道具（本地显示）</small>
                </div>
                <label class=""toggle"">
                    <input type=""checkbox"" id=""enableItemUnlock"">
                    <span class=""slider""></span>
                </label>
            </div>
            <div class=""setting-row"">
                <div class=""setting-label"">
                    <span>装扮方案</span>
                    <small>解锁装扮方案槽位</small>
                </div>
                <label class=""toggle"">
                    <input type=""checkbox"" id=""enableViewsUnlock"">
                    <span class=""slider""></span>
                </label>
            </div>
            <div class=""setting-row"">
                <div class=""setting-label"">
                    <span>表情解锁</span>
                    <small>解锁所有角色表情</small>
                </div>
                <label class=""toggle"">
                    <input type=""checkbox"" id=""enableEmojiUnlock"">
                    <span class=""slider""></span>
                </label>
            </div>
        </div>

        <div class=""card"">
            <h2>🎮 对局设置</h2>
            <div class=""setting-row"">
                <div class=""setting-label"">
                    <span>对局内皮肤替换</span>
                    <small>在对局中使用本地选择的皮肤</small>
                </div>
                <label class=""toggle"">
                    <input type=""checkbox"" id=""enableInGameSkinReplace"">
                    <span class=""slider""></span>
                </label>
            </div>
            <div class=""setting-row"">
                <div class=""setting-label"">
                    <span>隐藏锁定图标</span>
                    <small>隐藏皮肤/角色的锁定UI</small>
                </div>
                <label class=""toggle"">
                    <input type=""checkbox"" id=""hideLockUI"">
                    <span class=""slider""></span>
                </label>
            </div>
        </div>

        <div class=""card"">
            <h2>🎭 固定伪造角色</h2>
            <div class=""setting-row"">
                <div class=""setting-label"">
                    <span>启用固定伪造</span>
                    <small>使用固定角色/皮肤替代游戏内选择（重启生效）</small>
                </div>
                <label class=""toggle"">
                    <input type=""checkbox"" id=""enableFixedFakeCharacter"">
                    <span class=""slider""></span>
                </label>
            </div>
            <div class=""setting-row"">
                <div class=""setting-label"">
                    <span>选择角色</span>
                    <small>选择要伪造的角色</small>
                </div>
                <select id=""fixedCharacterId"" onchange=""onCharacterChange()"" 
                    style=""width:180px;padding:8px;border-radius:6px;border:1px solid #444;background:#2a2a3e;color:#e8e8e8;"">
                    <option value=""0"">-- 请选择角色 --</option>
                </select>
            </div>
            <div class=""setting-row"">
                <div class=""setting-label"">
                    <span>选择皮肤</span>
                    <small>选择要伪造的皮肤</small>
                </div>
                <select id=""fixedSkinId"" 
                    style=""width:180px;padding:8px;border-radius:6px;border:1px solid #444;background:#2a2a3e;color:#e8e8e8;"">
                    <option value=""0"">-- 请先选择角色 --</option>
                </select>
            </div>
            <div class=""setting-row"">
                <div class=""setting-label"">
                    <span>称号ID</span>
                    <small>固定使用的称号ID（0表示不指定）</small>
                </div>
                <input type=""number"" id=""fixedTitleId"" min=""0"" value=""0"" 
                    style=""width:100px;padding:8px;border-radius:6px;border:1px solid #444;background:#2a2a3e;color:#e8e8e8;"">
            </div>
            <div class=""btn-group"" style=""margin-top:10px;"">
                <button class=""btn btn-secondary"" onclick=""loadCharacterList()"">🔄 刷新角色列表</button>
            </div>
        </div>

        <div class=""card"">
            <h2>🛡️ 安全设置</h2>
            <div class=""setting-row"">
                <div class=""setting-label"">
                    <span>屏蔽日志上传</span>
                    <small>阻止日志发送到服务器（推荐开启）</small>
                </div>
                <label class=""toggle"">
                    <input type=""checkbox"" id=""blockLogToServer"">
                    <span class=""slider""></span>
                </label>
            </div>
            <div class=""setting-row"">
                <div class=""setting-label"">
                    <span>屏蔽对局信息</span>
                    <small>阻止某些对局信息上报（显示被屏蔽的内容）</small>
                </div>
                <label class=""toggle"">
                    <input type=""checkbox"" id=""blockMatchInfo"">
                    <span class=""slider""></span>
                </label>
            </div>
        </div>

        <div class=""card"">
            <h2>🔧 调试设置</h2>
            <div class=""setting-row"">
                <div class=""setting-label"">
                    <span>调试日志</span>
                    <small>输出详细的调试信息到控制台</small>
                </div>
                <label class=""toggle"">
                    <input type=""checkbox"" id=""enableDebugLog"">
                    <span class=""slider""></span>
                </label>
            </div>
            <div class=""setting-row"">
                <div class=""setting-label"">
                    <span>显示屏蔽内容</span>
                    <small>在控制台显示被屏蔽的日志/请求内容</small>
                </div>
                <label class=""toggle"">
                    <input type=""checkbox"" id=""enableBlockedLogDisplay"">
                    <span class=""slider""></span>
                </label>
            </div>
        </div>

        <div class=""card"">
            <h2>⚡ 性能设置</h2>
            <div class=""setting-row"">
                <div class=""setting-label"">
                    <span>目标帧率</span>
                    <small>设置游戏目标帧率 (30-240)</small>
                </div>
                <input type=""number"" id=""frameRateBase"" min=""30"" max=""240"" value=""120"" 
                    style=""width:80px;padding:8px;border-radius:6px;border:1px solid #444;background:#2a2a3e;color:#e8e8e8;"">
            </div>
            <div class=""setting-row"">
                <div class=""setting-label"">
                    <span>时间倍率</span>
                    <small>游戏加速倍率 (0.5-4.0)</small>
                </div>
                <input type=""number"" id=""targetTimeScale"" min=""0.5"" max=""4"" step=""0.1"" value=""1"" 
                    style=""width:80px;padding:8px;border-radius:6px;border:1px solid #444;background:#2a2a3e;color:#e8e8e8;"">
            </div>
        </div>

        <div class=""btn-group"">
            <button class=""btn btn-primary"" onclick=""saveConfig()"">💾 保存配置</button>
            <button class=""btn btn-secondary"" onclick=""clearCache()"">🗑️ 清除缓存</button>
            <button class=""btn btn-secondary"" onclick=""loadConfig()"">🔄 刷新</button>
        </div>
    </div>

    <div class=""toast"" id=""toast""></div>

    <script>
        const configKeys = [
            'enableSkinUnlock', 'enableCharacterUnlock', 'enableVoiceUnlock',
            'enableTitleUnlock', 'enableItemUnlock', 'enableViewsUnlock',
            'enableEmojiUnlock', 'hideLockUI', 'enableInGameSkinReplace',
            'blockLogToServer', 'blockMatchInfo', 'enableDebugLog', 'enableBlockedLogDisplay',
            'enableFixedFakeCharacter'
        ];
        const numberKeys = ['frameRateBase', 'targetTimeScale', 'fixedTitleId'];
        
        // 角色和皮肤数据缓存
        let characterList = [];
        let skinList = [];
        let currentCharacterId = 0;

        function showToast(msg) {
            const toast = document.getElementById('toast');
            toast.textContent = msg;
            toast.classList.add('show');
            setTimeout(() => toast.classList.remove('show'), 2000);
        }

        // 加载角色列表
        async function loadCharacterList() {
            try {
                const res = await fetch('/api/fake/characters');
                const data = await res.json();
                characterList = data.characters || [];
                
                const select = document.getElementById('fixedCharacterId');
                const currentValue = select.value;
                select.innerHTML = '<option value=""0"">-- 请选择角色 --</option>';
                
                characterList.forEach(c => {
                    const opt = document.createElement('option');
                    opt.value = c.id;
                    opt.textContent = `${c.name} (${c.id})`;
                    select.appendChild(opt);
                });
                
                // 恢复之前的选择
                if (currentValue > 0) {
                    select.value = currentValue;
                }
                
                if (characterList.length > 0) {
                    showToast(`✓ 已加载 ${characterList.length} 个角色`);
                }
            } catch (e) {
                console.error('Load character list failed', e);
            }
        }

        // 加载皮肤列表
        async function loadSkinList(characterId) {
            try {
                const res = await fetch('/api/fake/skins', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ characterId: parseInt(characterId) })
                });
                const data = await res.json();
                skinList = data.skins || [];
                
                const select = document.getElementById('fixedSkinId');
                const currentValue = select.value;
                select.innerHTML = '<option value=""0"">-- 请选择皮肤 --</option>';
                
                skinList.forEach(s => {
                    const opt = document.createElement('option');
                    opt.value = s.id;
                    opt.textContent = `${s.name} (${s.id})`;
                    select.appendChild(opt);
                });
                
                // 恢复之前的选择
                if (currentValue > 0) {
                    select.value = currentValue;
                }
            } catch (e) {
                console.error('Load skin list failed', e);
            }
        }

        // 角色选择变化时加载皮肤列表
        async function onCharacterChange() {
            const charId = document.getElementById('fixedCharacterId').value;
            currentCharacterId = parseInt(charId);
            if (charId > 0) {
                await loadSkinList(charId);
            } else {
                const select = document.getElementById('fixedSkinId');
                select.innerHTML = '<option value=""0"">-- 请先选择角色 --</option>';
            }
        }

        async function loadConfig() {
            try {
                const res = await fetch('/api/config');
                const config = await res.json();
                configKeys.forEach(key => {
                    const el = document.getElementById(key);
                    if (el) el.checked = config[key];
                });
                numberKeys.forEach(key => {
                    const el = document.getElementById(key);
                    if (el) el.value = config[key];
                });
                
                // 处理角色和皮肤选择
                const charId = config.fixedCharacterId || 0;
                const skinId = config.fixedSkinId || 0;
                
                // 加载角色列表
                await loadCharacterList();
                
                // 设置角色选择
                const charSelect = document.getElementById('fixedCharacterId');
                charSelect.value = charId;
                
                // 如果有选择角色，加载皮肤列表
                if (charId > 0) {
                    await loadSkinList(charId);
                    const skinSelect = document.getElementById('fixedSkinId');
                    skinSelect.value = skinId;
                }
            } catch (e) {
                showToast('加载配置失败');
            }
        }

        async function loadStatus() {
            try {
                const res = await fetch('/api/status');
                const status = await res.json();
                document.getElementById('status-version').textContent = status.version;
                document.getElementById('status-modules').textContent = status.patchedModules;
                document.getElementById('status-cached').textContent = status.cachedPatches;
            } catch (e) {
                console.error('Load status failed', e);
            }
        }

        async function saveConfig() {
            const config = {};
            configKeys.forEach(key => {
                const el = document.getElementById(key);
                if (el) config[key] = el.checked;
            });
            numberKeys.forEach(key => {
                const el = document.getElementById(key);
                if (el) config[key] = parseFloat(el.value);
            });
            
            // 处理下拉选择框
            const charSelect = document.getElementById('fixedCharacterId');
            const skinSelect = document.getElementById('fixedSkinId');
            if (charSelect) config.fixedCharacterId = parseInt(charSelect.value) || 0;
            if (skinSelect) config.fixedSkinId = parseInt(skinSelect.value) || 0;

            try {
                // 先更新配置
                await fetch('/api/config', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(config)
                });
                // 再保存到文件
                await fetch('/api/save', { method: 'POST' });
                showToast('✓ 配置已保存');
            } catch (e) {
                showToast('保存失败');
            }
        }

        async function clearCache() {
            try {
                await fetch('/api/cache/clear', { method: 'POST' });
                showToast('✓ 缓存已清除');
                loadStatus();
            } catch (e) {
                showToast('清除失败');
            }
        }

        // 配置更改时自动应用（不保存到文件）
        configKeys.forEach(key => {
            const el = document.getElementById(key);
            if (el) {
                el.addEventListener('change', async () => {
                    const config = {};
                    configKeys.forEach(k => {
                        const e = document.getElementById(k);
                        if (e) config[k] = e.checked;
                    });
                    await fetch('/api/config', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify(config)
                    });
                });
            }
        });

        // 初始化加载
        loadConfig();
        loadStatus();
        setInterval(loadStatus, 5000);
    </script>
</body>
</html>";
        }
    }
}
