using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace MajSoulHelper
{
    /// <summary>
    /// 角色和皮肤数据缓存管理器
    /// 用于保存从游戏中获取的角色/皮肤信息，供 WebUI 选择使用
    /// 同时保存用户的皮肤选择（类似 MajsoulMax 的 settings['config']['characters']）
    /// </summary>
    public static class CharacterDataCache
    {
        private static readonly string CacheDir = "BepInEx/config";
        private static readonly string CacheFile = "BepInEx/config/MajSoulHelper_CharacterData.json";
        private static readonly DataContractJsonSerializerSettings _jsonSettings = new DataContractJsonSerializerSettings
        {
            UseSimpleDictionaryFormat = true
        };
        private static bool _initialized = false;

        /// <summary>
        /// 缓存数据
        /// </summary>
        public static CachedCharacterData Data { get; private set; } = new CachedCharacterData();

        /// <summary>
        /// 初始化缓存
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;

            try
            {
                if (!Directory.Exists(CacheDir))
                {
                    Directory.CreateDirectory(CacheDir);
                }

                Load();
                _initialized = true;
                Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, 
                    $"[CharacterDataCache] Initialized with {Data.Characters.Count} characters, {Data.Skins.Count} skins");
            }
            catch (Exception ex)
            {
                Utils.MyLogger(BepInEx.Logging.LogLevel.Error, $"[CharacterDataCache] Initialize failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 加载缓存文件
        /// </summary>
        public static void Load()
        {
            try
            {
                if (File.Exists(CacheFile))
                {
                    string json = File.ReadAllText(CacheFile, Encoding.UTF8);
                    // 优先使用标准JSON反序列化，失败时回退旧解析逻辑以保持兼容
                    Data = DeserializeCacheJson(json) ?? ParseCacheJson(json) ?? new CachedCharacterData();
                }
                else
                {
                    Data = new CachedCharacterData();
                }
            }
            catch (Exception ex)
            {
                Utils.MyLogger(BepInEx.Logging.LogLevel.Error, $"[CharacterDataCache] Load failed: {ex.Message}");
                Data = new CachedCharacterData();
            }
        }

        /// <summary>
        /// 保存缓存到文件
        /// </summary>
        public static void Save()
        {
            try
            {
                string json = SerializeCacheJson(Data);
                File.WriteAllText(CacheFile, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Utils.MyLogger(BepInEx.Logging.LogLevel.Error, $"[CharacterDataCache] Save failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 添加或更新角色信息
        /// </summary>
        public static void AddCharacter(int characterId, string name)
        {
            Data.Characters[characterId] = name;
        }

        /// <summary>
        /// 添加或更新皮肤信息
        /// </summary>
        public static void AddSkin(int characterId, int skinId, string name)
        {
            if (!Data.Skins.ContainsKey(characterId))
            {
                Data.Skins[characterId] = new Dictionary<int, string>();
            }
            Data.Skins[characterId][skinId] = name;
        }

        /// <summary>
        /// 设置角色的当前皮肤选择
        /// </summary>
        public static void SetCharacterSkin(int characterId, int skinId)
        {
            Data.CharacterSkinSelections[characterId] = skinId;
            Save(); // 自动保存
        }

        /// <summary>
        /// 获取角色的当前皮肤选择
        /// </summary>
        public static int GetCharacterSkin(int characterId)
        {
            if (Data.CharacterSkinSelections.TryGetValue(characterId, out int skinId))
            {
                return skinId;
            }
            return GetDefaultSkinId(characterId);
        }

        /// <summary>
        /// 获取角色的默认皮肤ID（优先使用缓存中已知皮肤的最小ID）
        /// </summary>
        public static int GetDefaultSkinId(int characterId)
        {
            if (Data.Skins.TryGetValue(characterId, out var skins) && skins.Count > 0)
            {
                return skins.Keys.Min();
            }
            return characterId + 200000;
        }

        /// <summary>
        /// 设置当前主角色
        /// </summary>
        public static void SetMainCharacter(int characterId)
        {
            Data.MainCharacterId = characterId;
            Save();
        }

        /// <summary>
        /// 获取当前主角色皮肤
        /// </summary>
        public static int GetMainCharacterSkin()
        {
            return GetCharacterSkin(Data.MainCharacterId);
        }

        /// <summary>
        /// 获取当前主角色ID
        /// </summary>
        public static int GetMainCharacterId()
        {
            return Data.MainCharacterId;
        }

        /// <summary>
        /// 设置当前称号
        /// </summary>
        public static void SetSelectedTitle(int titleId)
        {
            Data.SelectedTitle = titleId;
            Save();
        }

        /// <summary>
        /// 获取当前称号
        /// </summary>
        public static int GetSelectedTitle()
        {
            return Data.SelectedTitle;
        }

        /// <summary>
        /// 获取所有角色皮肤选择映射
        /// </summary>
        public static Dictionary<int, int> GetAllSelections()
        {
            return new Dictionary<int, int>(Data.CharacterSkinSelections);
        }

        /// <summary>
        /// 获取所有角色列表（用于 WebAPI）
        /// </summary>
        public static Dictionary<int, string> GetAllCharacters()
        {
            return new Dictionary<int, string>(Data.Characters);
        }

        /// <summary>
        /// 获取指定角色的皮肤列表（用于 WebAPI）
        /// </summary>
        public static Dictionary<int, string> GetCharacterSkins(int characterId)
        {
            if (Data.Skins.TryGetValue(characterId, out var skins))
            {
                return new Dictionary<int, string>(skins);
            }
            return new Dictionary<int, string>();
        }

        /// <summary>
        /// 获取所有皮肤列表（用于 WebAPI）
        /// </summary>
        public static Dictionary<int, string> GetAllSkins()
        {
            var allSkins = new Dictionary<int, string>();
            foreach (var kv in Data.Skins)
            {
                foreach (var skin in kv.Value)
                {
                    allSkins[skin.Key] = skin.Value;
                }
            }
            return allSkins;
        }

        /// <summary>
        /// 序列化缓存数据为 JSON
        /// </summary>
        private static string SerializeCacheJson(CachedCharacterData data)
        {
            try
            {
                var serializer = new DataContractJsonSerializer(typeof(CachedCharacterData), _jsonSettings);
                using (var ms = new MemoryStream())
                {
                    serializer.WriteObject(ms, data);
                    return Encoding.UTF8.GetString(ms.ToArray());
                }
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// 解析缓存 JSON
        /// </summary>
        private static CachedCharacterData ParseCacheJson(string json)
        {
            var data = new CachedCharacterData();
            if (string.IsNullOrEmpty(json)) return data;

            try
            {
                // 简单解析 MainCharacterId
                data.MainCharacterId = GetIntValue(json, "MainCharacterId", 200001);
                data.SelectedTitle = GetIntValue(json, "SelectedTitle", 0);

                // 解析 Characters
                int charStart = json.IndexOf("\"Characters\":");
                if (charStart >= 0)
                {
                    int braceStart = json.IndexOf('{', charStart);
                    int braceEnd = FindMatchingBrace(json, braceStart);
                    if (braceEnd > braceStart)
                    {
                        string charBlock = json.Substring(braceStart + 1, braceEnd - braceStart - 1);
                        data.Characters = ParseStringDict(charBlock);
                    }
                }

                // 解析 CharacterSkinSelections
                int selStart = json.IndexOf("\"CharacterSkinSelections\":");
                if (selStart >= 0)
                {
                    int braceStart = json.IndexOf('{', selStart);
                    int braceEnd = FindMatchingBrace(json, braceStart);
                    if (braceEnd > braceStart)
                    {
                        string selBlock = json.Substring(braceStart + 1, braceEnd - braceStart - 1);
                        data.CharacterSkinSelections = ParseIntDict(selBlock);
                    }
                }

                // 解析 Skins
                int skinStart = json.IndexOf("\"Skins\":");
                if (skinStart >= 0)
                {
                    int braceStart = json.IndexOf('{', skinStart);
                    int braceEnd = FindMatchingBrace(json, braceStart);
                    if (braceEnd > braceStart)
                    {
                        string skinBlock = json.Substring(braceStart + 1, braceEnd - braceStart - 1);
                        data.Skins = ParseSkinDict(skinBlock);
                    }
                }
            }
            catch (Exception ex)
            {
                Utils.MyLogger(BepInEx.Logging.LogLevel.Error, $"[CharacterDataCache] Parse failed: {ex.Message}");
            }

            return data;
        }

        private static CachedCharacterData DeserializeCacheJson(string json)
        {
            try
            {
                var serializer = new DataContractJsonSerializer(typeof(CachedCharacterData), _jsonSettings);
                using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    return serializer.ReadObject(ms) as CachedCharacterData;
                }
            }
            catch
            {
                return null;
            }
        }

        private static int FindMatchingBrace(string json, int start)
        {
            if (start < 0 || start >= json.Length) return -1;
            int depth = 1;
            for (int i = start + 1; i < json.Length; i++)
            {
                if (json[i] == '{') depth++;
                else if (json[i] == '}') depth--;
                if (depth == 0) return i;
            }
            return -1;
        }

        private static Dictionary<int, string> ParseStringDict(string block)
        {
            var result = new Dictionary<int, string>();
            // 简单的键值对解析
            var pairs = block.Split(',');
            foreach (var pair in pairs)
            {
                var kv = pair.Split(':');
                if (kv.Length >= 2)
                {
                    string keyStr = kv[0].Trim().Trim('"');
                    string valStr = kv[1].Trim().Trim('"');
                    if (int.TryParse(keyStr, out int key))
                    {
                        result[key] = valStr;
                    }
                }
            }
            return result;
        }

        private static Dictionary<int, int> ParseIntDict(string block)
        {
            var result = new Dictionary<int, int>();
            var pairs = block.Split(',');
            foreach (var pair in pairs)
            {
                var kv = pair.Split(':');
                if (kv.Length == 2)
                {
                    string keyStr = kv[0].Trim().Trim('"');
                    string valStr = kv[1].Trim();
                    if (int.TryParse(keyStr, out int key) && int.TryParse(valStr, out int val))
                    {
                        result[key] = val;
                    }
                }
            }
            return result;
        }

        private static Dictionary<int, Dictionary<int, string>> ParseSkinDict(string block)
        {
            var result = new Dictionary<int, Dictionary<int, string>>();
            int idx = 0;
            while (idx < block.Length)
            {
                int keyStart = block.IndexOf('"', idx);
                if (keyStart < 0) break;
                int keyEnd = block.IndexOf('"', keyStart + 1);
                if (keyEnd < 0) break;
                string keyStr = block.Substring(keyStart + 1, keyEnd - keyStart - 1).Trim();
                if (!int.TryParse(keyStr, out int charId))
                {
                    idx = keyEnd + 1;
                    continue;
                }

                int braceStart = block.IndexOf('{', keyEnd);
                if (braceStart < 0) break;
                int braceEnd = FindMatchingBrace(block, braceStart);
                if (braceEnd < 0) break;

                string skinBlock = block.Substring(braceStart + 1, braceEnd - braceStart - 1);
                var skins = ParseStringDict(skinBlock);
                result[charId] = skins;

                idx = braceEnd + 1;
            }
            return result;
        }

        private static int GetIntValue(string json, string key, int defaultValue)
        {
            try
            {
                string pattern = $"\"{key}\":";
                int idx = json.IndexOf(pattern);
                if (idx < 0) return defaultValue;

                int start = idx + pattern.Length;
                int end = json.IndexOfAny(new[] { ',', '}', '\n' }, start);
                string value = json.Substring(start, end - start).Trim();
                return int.TryParse(value, out int result) ? result : defaultValue;
            }
            catch { return defaultValue; }
        }

        private static string EscapeJson(string str)
        {
            if (str == null) return "";
            return str.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }
    }

    /// <summary>
    /// 缓存的角色数据
    /// </summary>
    public class CachedCharacterData
    {
        /// <summary>
        /// 当前主角色ID
        /// </summary>
        public int MainCharacterId { get; set; } = 200001;

        /// <summary>
        /// 当前使用的称号
        /// </summary>
        public int SelectedTitle { get; set; } = 0;

        /// <summary>
        /// 角色ID -> 角色名称
        /// </summary>
        public Dictionary<int, string> Characters { get; set; } = new Dictionary<int, string>();

        /// <summary>
        /// 角色ID -> (皮肤ID -> 皮肤名称)
        /// </summary>
        public Dictionary<int, Dictionary<int, string>> Skins { get; set; } = new Dictionary<int, Dictionary<int, string>>();

        /// <summary>
        /// 角色ID -> 选择的皮肤ID（用户的皮肤选择记录）
        /// 类似 MajsoulMax 的 settings['config']['characters']
        /// </summary>
        public Dictionary<int, int> CharacterSkinSelections { get; set; } = new Dictionary<int, int>();
    }
}
