using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace MajSoulHelper
{
    /// <summary>
    /// 皮肤解锁器 - 处理Lua代码的动态修改
    /// 通过Hook luaL_loadbuffer实现本地解锁全部角色与皮肤
    /// </summary>
    public static class SkinUnlocker
    {
        private static bool _initialized = false;
        private static readonly object _lock = new object();

        // 缓存已修改的Lua代码，避免重复处理
        private static Dictionary<string, byte[]> _patchedLuaCache = new Dictionary<string, byte[]>();

        // 已打补丁的模块计数
        private static int _patchedModuleCount = 0;

        // 外部补丁文件目录
        private static string _patchDir = "BepInEx/patches";

        // ======== 缓存的游戏数据（用于WebAPI） ========
        private static Dictionary<int, string> _cachedCharacters = new Dictionary<int, string>();
        private static Dictionary<int, Dictionary<int, string>> _cachedSkins = new Dictionary<int, Dictionary<int, string>>();
        private static bool _configChanged = false;

        // ======== Lua 替换跟踪 ========
        private static string _currentPatchModule = "";

        /// <summary>
        /// 执行带警告的字符串替换。如果未找到匹配，输出警告日志
        /// </summary>
        private static string ReplaceWithWarning(string source, string oldValue, string newValue, string description = null)
        {
            if (!source.Contains(oldValue))
            {
                string desc = description ?? oldValue.Substring(0, Math.Min(50, oldValue.Length));
                Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, 
                    $"[SkinUnlocker] Lua替换未匹配 [{_currentPatchModule}]: {desc}...");
                return source;
            }
            return source.Replace(oldValue, newValue);
        }

        /// <summary>
        /// 执行带警告的正则替换。如果未找到匹配，输出警告日志
        /// </summary>
        private static string RegexReplaceWithWarning(string source, string pattern, string replacement, string description = null)
        {
            if (!Regex.IsMatch(source, pattern))
            {
                string desc = description ?? pattern.Substring(0, Math.Min(50, pattern.Length));
                Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, 
                    $"[SkinUnlocker] Lua正则替换未匹配 [{_currentPatchModule}]: {desc}...");
                return source;
            }
            return Regex.Replace(source, pattern, replacement);
        }

        /// <summary>
        /// 执行带警告的正则替换（带选项）
        /// </summary>
        private static string RegexReplaceWithWarning(string source, string pattern, string replacement, RegexOptions options, string description = null)
        {
            if (!Regex.IsMatch(source, pattern, options))
            {
                string desc = description ?? pattern.Substring(0, Math.Min(50, pattern.Length));
                Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, 
                    $"[SkinUnlocker] Lua正则替换未匹配 [{_currentPatchModule}]: {desc}...");
                return source;
            }
            return Regex.Replace(source, pattern, replacement, options);
        }

        /// <summary>
        /// 获取已补丁模块数量（用于Web状态显示）
        /// </summary>
        public static int GetPatchedModuleCount()
        {
            return _patchedModuleCount;
        }

        /// <summary>
        /// 获取缓存补丁数量（用于Web状态显示）
        /// </summary>
        public static int GetCachedPatchCount()
        {
            lock (_lock)
            {
                return _patchedLuaCache.Count;
            }
        }

        /// <summary>
        /// 清除补丁缓存
        /// </summary>
        public static void ClearCache()
        {
            lock (_lock)
            {
                _patchedLuaCache.Clear();
                _patchedModuleCount = 0;
            }
        }

        /// <summary>
        /// 获取缓存的角色列表
        /// </summary>
        public static Dictionary<int, string> GetCachedCharacters()
        {
            lock (_lock)
            {
                return new Dictionary<int, string>(_cachedCharacters);
            }
        }

        /// <summary>
        /// 获取指定角色的皮肤列表
        /// </summary>
        public static Dictionary<int, string> GetCachedSkins(int characterId)
        {
            lock (_lock)
            {
                if (_cachedSkins.TryGetValue(characterId, out var skins))
                {
                    return new Dictionary<int, string>(skins);
                }
                // 如果没有指定角色，返回所有皮肤
                var allSkins = new Dictionary<int, string>();
                foreach (var kv in _cachedSkins)
                {
                    foreach (var skin in kv.Value)
                    {
                        allSkins[skin.Key] = skin.Value;
                    }
                }
                return allSkins;
            }
        }

        /// <summary>
        /// 添加角色到缓存（由Lua回调调用）
        /// </summary>
        public static void CacheCharacter(int id, string name)
        {
            lock (_lock)
            {
                _cachedCharacters[id] = name;
            }
        }

        /// <summary>
        /// 添加皮肤到缓存（由Lua回调调用）
        /// </summary>
        public static void CacheSkin(int characterId, int skinId, string name)
        {
            lock (_lock)
            {
                if (!_cachedSkins.ContainsKey(characterId))
                {
                    _cachedSkins[characterId] = new Dictionary<int, string>();
                }
                _cachedSkins[characterId][skinId] = name;
            }
        }

        /// <summary>
        /// 通知配置已变更（触发游戏内刷新）
        /// </summary>
        public static void NotifyConfigChanged()
        {
            _configChanged = true;
            Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, "[SkinUnlocker] Config changed, will refresh on next opportunity");
        }

        /// <summary>
        /// 检查配置是否已变更（由Lua轮询调用）
        /// </summary>
        public static bool CheckAndClearConfigChanged()
        {
            if (_configChanged)
            {
                _configChanged = false;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 获取固定伪造角色配置（供Lua调用）
        /// </summary>
        public static int[] GetFixedFakeConfig()
        {
            if (!PluginConfig.EnableFixedFakeCharacter)
            {
                return new int[] { 0, 0, 0, 0 }; // 未启用
            }
            return new int[] {
                1, // 启用标志
                PluginConfig.FixedCharacterId,
                PluginConfig.FixedSkinId,
                PluginConfig.FixedTitleId
            };
        }

        /// <summary>
        /// 构建 MajSoulHelper_FakeConfig 初始化的 Lua 代码
        /// 从 PluginConfig 读取配置值并嵌入到 Lua 代码中
        /// </summary>
        private static string BuildFakeConfigInitLua()
        {
            bool enabled = PluginConfig.EnableFixedFakeCharacter;
            int charId = PluginConfig.FixedCharacterId;
            int skinId = PluginConfig.FixedSkinId;
            int titleId = PluginConfig.FixedTitleId;
            var views = PluginConfig.FixedViews;

            // 构建 views 表（Lua 格式）
            string viewsLua = "nil";
            if (views != null && views.Count > 0)
            {
                var viewsParts = new System.Collections.Generic.List<string>();
                foreach (var kv in views)
                {
                    viewsParts.Add($"[{kv.Key}]={kv.Value}");
                }
                viewsLua = "{" + string.Join(",", viewsParts) + "}";
            }

            return $"MajSoulHelper_FakeConfig={{enabled={enabled.ToString().ToLower()},charId={charId},skinId={skinId},titleId={titleId},views={viewsLua}}};";
        }

        /// <summary>
        /// 初始化皮肤解锁器
        /// </summary>
        public static void Initialize()
        {
            lock (_lock)
            {
                if (_initialized) return;

                // 确保补丁目录存在
                if (!Directory.Exists(_patchDir))
                {
                    Directory.CreateDirectory(_patchDir);
                }

                // 生成默认补丁文件（如果不存在）
                GenerateDefaultPatches();

                _initialized = true;
                Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, "[SkinUnlocker] Initialized successfully!");
            }
        }

        /// <summary>
        /// 检测字节数组是否为LuaJIT字节码
        /// LuaJIT字节码以 0x1B 'L' 'J' 开头
        /// </summary>
        public static bool IsLuaJITBytecode(byte[] data)
        {
            if (data == null || data.Length < 3) return false;
            return data[0] == 0x1B && data[1] == 0x4C && data[2] == 0x4A;
        }

        /// <summary>
        /// 检测字节数组是否为Lua 5.x字节码
        /// Lua 5.x字节码以 0x1B 'L' 'u' 'a' 开头
        /// </summary>
        public static bool IsLua5Bytecode(byte[] data)
        {
            if (data == null || data.Length < 4) return false;
            return data[0] == 0x1B && data[1] == 0x4C && data[2] == 0x75 && data[3] == 0x61;
        }

        /// <summary>
        /// 尝试修改Lua缓冲区
        /// </summary>
        /// <param name="name">Lua模块名称</param>
        /// <param name="originalBuff">原始字节数组</param>
        /// <param name="size">原始大小</param>
        /// <param name="modifiedBuff">修改后的字节数组</param>
        /// <param name="newSize">修改后的大小</param>
        /// <returns>是否进行了修改</returns>
        public static bool TryPatchLuaBuffer(string name, byte[] originalBuff, int size, 
            out byte[] modifiedBuff, out int newSize)
        {
            modifiedBuff = originalBuff;
            newSize = size;

            if (!PluginConfig.EnableSkinUnlock && !PluginConfig.EnableCharacterUnlock)
            {
                return false;
            }

            // 检查是否在补丁映射中
            string moduleName = ExtractModuleName(name);
            if (!PluginConfig.LuaPatchMapping.TryGetValue(moduleName, out LuaPatchType patchType))
            {
                return false;
            }

            // 检查缓存
            string cacheKey = $"{name}_{size}";
            if (_patchedLuaCache.TryGetValue(cacheKey, out byte[] cached))
            {
                modifiedBuff = cached;
                newSize = cached.Length;
                if (PluginConfig.EnableDebugLog)
                {
                    Utils.MyLogger(BepInEx.Logging.LogLevel.Debug, $"[SkinUnlocker] Using cached patch for: {name}");
                }
                return true;
            }

            // 检查是否为字节码
            if (IsLuaJITBytecode(originalBuff) || IsLua5Bytecode(originalBuff))
            {
                // 字节码需要特殊处理，尝试加载外部补丁文件
                if (TryLoadExternalPatch(moduleName, out byte[] externalPatch))
                {
                    modifiedBuff = externalPatch;
                    newSize = externalPatch.Length;
                    _patchedLuaCache[cacheKey] = externalPatch;
                    _patchedModuleCount++;
                    Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, $"[SkinUnlocker] Applied external patch for bytecode: {name}");
                    return true;
                }

                if (PluginConfig.EnableDebugLog)
                {
                    Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, $"[SkinUnlocker] Skipping bytecode file: {name}");
                }
                return false;
            }

            // 处理源码文本
            try
            {
                string luaCode = Encoding.UTF8.GetString(originalBuff, 0, size);
                string patchedCode = ApplyPatch(luaCode, patchType, moduleName);

                if (patchedCode != luaCode)
                {
                    modifiedBuff = Encoding.UTF8.GetBytes(patchedCode);
                    newSize = modifiedBuff.Length;
                    _patchedLuaCache[cacheKey] = modifiedBuff;
                    _patchedModuleCount++;

                    Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, 
                        $"[SkinUnlocker] Patched {name} ({patchType}): {size} -> {newSize} bytes");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Utils.MyLogger(BepInEx.Logging.LogLevel.Error, 
                    $"[SkinUnlocker] Failed to patch {name}: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// 从完整路径中提取模块名称
        /// </summary>
        private static string ExtractModuleName(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath)) return "";

            // 移除前缀路径，保留 @xxx 部分
            int atIndex = fullPath.LastIndexOf('@');
            if (atIndex >= 0)
            {
                return fullPath.Substring(atIndex);
            }

            // 处理其他格式
            string fileName = Path.GetFileNameWithoutExtension(fullPath);
            if (!fileName.StartsWith("@"))
            {
                fileName = "@" + fileName;
            }
            return fileName;
        }

        /// <summary>
        /// 应用补丁修改
        /// </summary>
        private static string ApplyPatch(string luaCode, LuaPatchType patchType, string moduleName)
        {
            // 设置当前模块名用于日志输出
            _currentPatchModule = moduleName;

            switch (patchType)
            {
                case LuaPatchType.ItemOwned:
                    return PatchItemOwned(luaCode);

                case LuaPatchType.CharacterInfo:
                    return PatchCharacterInfo(luaCode);

                case LuaPatchType.SkinCellUI:
                    return PatchSkinCellUI(luaCode);

                case LuaPatchType.BagUI:
                    return PatchBagUI(luaCode);

                case LuaPatchType.CharacterSkinUI:
                    return PatchCharacterSkinUI(luaCode);

                case LuaPatchType.ChangeSkinUI:
                    return PatchChangeSkinUI(luaCode);

                case LuaPatchType.RoleSetUI:
                    return PatchGenericSkinUI(luaCode);

                case LuaPatchType.SkinPreviewUI:
                    return PatchSkinPreviewUI(luaCode);

                case LuaPatchType.SkinShopUI:
                    return PatchSkinShopUI(luaCode);

                case LuaPatchType.LiaosheMainUI:
                    return PatchLiaosheMainUI(luaCode);

                case LuaPatchType.LiaosheSelectUI:
                    return PatchLiaosheSelectUI(luaCode);

                case LuaPatchType.VisitUI:
                    return PatchGenericSkinUI(luaCode);

                case LuaPatchType.ToolsModule:
                    return PatchToolsModule(luaCode);

                case LuaPatchType.LobbyNetMgr:
                    return PatchLobbyNetMgr(luaCode);

                case LuaPatchType.MJNetMgr:
                    return PatchMJNetMgr(luaCode);

                case LuaPatchType.DesktopMgr:
                    return PatchDesktopMgr(luaCode);

                case LuaPatchType.FriendRoomUI:
                    return PatchFriendRoomUI(luaCode);

                case LuaPatchType.LogTool:
                    return PatchLogTool(luaCode);

                case LuaPatchType.LogStoreUtility:
                    return PatchLogStoreUtility(luaCode);

                case LuaPatchType.ErrorInfoUI:
                    return PatchErrorInfoUI(luaCode);

                default:
                    return luaCode;
            }
        }

        /// <summary>
        /// 修改 GameUtility 模块
        /// 1. item_owned 函数对皮肤和角色始终返回true
        /// 2. GetFilterCharInServerCharData 函数返回所有角色（寮舍解锁）
        /// </summary>
        private static string PatchItemOwned(string luaCode)
        {
            // ======== 1. 修改 item_owned 函数 ========
            // 在 item_owned 函数开头插入返回true的逻辑 - 压缩格式处理
            // 使用正则表达式匹配不同的参数名（可能是 c, a, d 等单字母）
            string pattern = @"function GameUtility\.item_owned\((\w+)\)";
            string replacement = @"function GameUtility.item_owned($1)local _t=GameUtility.get_id_type($1);if _t==GameUtility.EIDType.skin or _t==GameUtility.EIDType.character then return true end;";

            luaCode = RegexReplaceWithWarning(luaCode, pattern, replacement, "item_owned函数");

            // ======== 2. 修改 GetFilterCharInServerCharData 函数 ========
            // 原始逻辑：仅从 GameMgr.Inst.characterInfo.characters 获取角色
            // 修改后：从 ExcelMgr 获取所有角色并为每个创建虚拟角色数据
            // 关键：使用 GameMgr.Inst.characterInfo.characters 而不是临时变量，确保修改影响全局
            // 压缩格式: function GameUtility.GetFilterCharInServerCharData(T,U,V,a5,X)local E={}local a6=GameMgr.Inst.characterInfo.characters;
            string filterCharPattern = @"function GameUtility\.GetFilterCharInServerCharData\((\w+),(\w+),(\w+),(\w+),(\w+)\)local (\w+)=\{\}local (\w+)=GameMgr\.Inst\.characterInfo\.characters;";
            string filterCharReplacement = @"function GameUtility.GetFilterCharInServerCharData($1,$2,$3,$4,$5)local $6={};pcall(function()local allChars=ExcelMgr.GetTable('item_definition','character');if allChars then local existMap={};for _,c in ipairs(GameMgr.Inst.characterInfo.characters or{})do existMap[c.charid]=c end;for charId,charData in pairs(allChars)do if not existMap[charId]then local initSkin=charData.init_skin or(charId*100+1);local newChar={charid=charId,level=5,exp=0,skin=initSkin,is_upgraded=true,views={},rewarded_level={1,2,3,4,5},extra_emoji={}};table.insert(GameMgr.Inst.characterInfo.characters,newChar);existMap[charId]=newChar end end end end);local $7=GameMgr.Inst.characterInfo.characters;";

            luaCode = RegexReplaceWithWarning(luaCode, filterCharPattern, filterCharReplacement, "GetFilterCharInServerCharData函数");

            // ======== 3. 修改 HasSkinOfChar 函数 ========
            // 让所有角色都被认为拥有皮肤
            string hasSkinPattern = @"function GameUtility\.HasSkinOfChar\((\w+)\)";
            string hasSkinReplacement = @"function GameUtility.HasSkinOfChar($1)do return true end;";

            luaCode = RegexReplaceWithWarning(luaCode, hasSkinPattern, hasSkinReplacement, "HasSkinOfChar函数");

            return luaCode;
        }

        /// <summary>
        /// 修改 GameMgr:makeCharacterInfo 函数
        /// 注入所有皮肤ID到skin_map和characterInfo.skins数组
        /// </summary>
        private static string PatchCharacterInfo(string luaCode)
        {
            // ===== 首先处理压缩格式的 Lua 代码 =====
            // 如果是压缩格式，这些替换会生效，后续的正则不会再匹配
            
            // 修改 have_character 函数 - 压缩格式
            // 使用正则匹配任意参数名（如 aX, a, b 等）
            string compressedHaveCharPattern = @"function GameMgr:have_character\((\w+)\)if not self\.characterInfo\.characters or not \1 then return false end;";
            string compressedHaveCharReplacement = @"function GameMgr:have_character($1)do return true end;if not self.characterInfo.characters or not $1 then return false end;";
            
            if (Regex.IsMatch(luaCode, compressedHaveCharPattern))
            {
                luaCode = Regex.Replace(luaCode, compressedHaveCharPattern, compressedHaveCharReplacement);
                
                // 修改 SortSkin 函数 - 压缩格式
                // 在 SortSkin 函数末尾添加皮肤和角色注入逻辑
                // 关键修复：保存原始角色ID列表用于服务器安全检查
                // 同时初始化 MajSoulHelper_FakeConfig 全局变量用于固定伪造配置
                // 配置值从 PluginConfig 读取并嵌入到 Lua 代码中
                // 新增：回调 C# 端缓存皮肤数据供 WebUI 使用
                string fakeConfigInit = BuildFakeConfigInitLua();
                luaCode = luaCode.Replace(
                    "function GameMgr:SortSkin()if self.characterInfo.skins then",
                    @"function GameMgr:SortSkin()pcall(function()if not MajSoulHelper_OriginalData then MajSoulHelper_OriginalData={skin_map={},character_skins={},characters={},main_character_id=nil,initialized=false}end;" + fakeConfigInit + @"if self.characterInfo.skins then for l=1,#self.characterInfo.skins do MajSoulHelper_OriginalData.skin_map[self.characterInfo.skins[l]]=1 end end;for i,c in ipairs(self.characterInfo.characters or{})do MajSoulHelper_OriginalData.characters[c.charid]=true;MajSoulHelper_OriginalData.character_skins[c.charid]=c.skin end;MajSoulHelper_OriginalData.main_character_id=self.characterInfo.main_character_id;MajSoulHelper_OriginalData.initialized=true end);" +
                    // 新增：回调 C# 端缓存皮肤和角色数据供 WebUI 使用
                    // 关键修复：先调用 ExcelTool.TryLoadBlock 加载所有分块数据
                    // 皮肤数据分布在 item_definition_skin 和 item_definition_skin_b1~b28 中
                    @"pcall(function()" +
                    // 先确保加载所有皮肤分块数据
                    @"local ExcelTool=require('ExcelTool');" +
                    @"local skinMod=require('Excels.Data.item_definition.skin');" +
                    @"local charMod=require('Excels.Data.item_definition.character');" +
                    @"if ExcelTool and ExcelTool.TryLoadBlock then " +
                    @"pcall(function()ExcelTool.TryLoadBlock(skinMod,'item_definition','skin')end);" +
                    @"pcall(function()ExcelTool.TryLoadBlock(charMod,'item_definition','character')end)end;" +
                    // 获取完整的皮肤和角色表
                    @"local allChars=ExcelMgr.GetTable('item_definition','character');" +
                    @"local allSkins=ExcelMgr.GetTable('item_definition','skin');" +
                    @"if allChars then for charId,charData in pairs(allChars)do pcall(function()CS.MajSoulHelper.CharacterDataCache.AddCharacter(charId,charData.name or'')end)end end;" +
                    @"if allSkins then for skinId,skinData in pairs(allSkins)do pcall(function()local cid=skinData.character_id or 0;local sname=skinData.name_chs or skinData.name or'';CS.MajSoulHelper.CharacterDataCache.AddSkin(cid,skinId,sname)end)end end;" +
                    @"pcall(function()CS.MajSoulHelper.CharacterDataCache.Save()end)end);" +
                    // 继续原有的皮肤注入逻辑
                    @"pcall(function()local allSkins=ExcelMgr.GetTable('item_definition','skin');if allSkins then for skinId,_ in pairs(allSkins)do self.skin_map[skinId]=1 end end;local allChars=ExcelMgr.GetTable('item_definition','character');if allChars and self.characterInfo and self.characterInfo.characters then for charId,charData in pairs(allChars)do local found=false;for i,c in ipairs(self.characterInfo.characters)do if c.charid==charId then found=true;c.level=5;c.is_upgraded=true;break end end;if not found then local initSkin=charData.init_skin;if not initSkin then initSkin=charId*100+1 end;local newChar={charid=charId,level=5,exp=0,skin=initSkin,is_upgraded=true,views={},rewarded_level={1,2,3,4,5},extra_emoji={},finished_endings={}};table.insert(self.characterInfo.characters,newChar)end end end end);if self.characterInfo.skins then");
            }
            
            // ===== 非压缩格式的处理 =====
            // 在 skin_map 初始化后添加全部皮肤注入逻辑

            // 方案1: 在 have_character 函数返回true (仅非压缩格式，有换行)
            string haveCharPattern = @"function GameMgr:have_character\((\w+)\)\s*\n";
            string haveCharReplacement = @"function GameMgr:have_character($1)
-- [MajSoulHelper] 本地解锁: 始终返回拥有角色
return true
--[[原始逻辑";

            if (Regex.IsMatch(luaCode, haveCharPattern))
            {
                luaCode = Regex.Replace(luaCode, haveCharPattern, haveCharReplacement);

                // 需要在函数结束处添加闭合注释
                string endPattern = @"(function GameMgr:have_character.*?)(return false\s*end)";
                string endReplacement = @"$1]]--$2";
                luaCode = Regex.Replace(luaCode, endPattern, endReplacement, RegexOptions.Singleline);
            }

            // 方案2: 修改 skin_map 和 skins 数组注入
            // 在 characterInfo 赋值后保存原始数据，然后注入所有皮肤
            string skinMapPattern = @"if t\.skins then\s+for l=1,#t\.skins do\s+self\.skin_map\[t\.skins\[l\]\]=1\s+end\s+end";
            string skinMapReplacement = @"if t.skins then
    for l=1,#t.skins do
        self.skin_map[t.skins[l]]=1
    end
end
-- [MajSoulHelper] 保存原始服务器数据到全局存储
pcall(function()
    if not MajSoulHelper_OriginalData then
        MajSoulHelper_OriginalData = {
            skin_map = {},
            character_skins = {},
            characters = {},
            main_character_id = nil,
            initialized = false,
            pending_server_sync = false
        }
    end
    if not MajSoulHelper_LocalSkinData then
        MajSoulHelper_LocalSkinData = {
            skin_map = {},
            main_character_id = nil,
            avatar_id = nil
        }
    end
    -- 初始化固定伪造配置（从 C# 端读取）
    -- 配置值会在每次 SortSkin 调用时更新
    MajSoulHelper_FakeConfig = MajSoulHelper_FakeConfig or {
        enabled = false,
        charId = 0,
        skinId = 0,
        titleId = 0,
        views = nil,
        allowDynamicRefresh = false
    }
    -- 保存原始拥有的皮肤列表
    if t.skins then
        for i = 1, #t.skins do
            MajSoulHelper_OriginalData.skin_map[t.skins[i]] = 1
        end
    end
    -- 保存每个角色的原始皮肤
    for i, char in ipairs(self.characterInfo.characters) do
        if char.charid and char.skin then
            MajSoulHelper_OriginalData.character_skins[char.charid] = char.skin
        end
    end
    MajSoulHelper_OriginalData.main_character_id = self.characterInfo.main_character_id
    MajSoulHelper_OriginalData.initialized = true
end)
-- [MajSoulHelper] 注入所有皮肤到skin_map和skins数组
pcall(function()
    local allSkins = ExcelMgr.GetTable('item_definition', 'skin')
    local injectedCount = 0
    if allSkins then
        for skinId, skinData in pairs(allSkins) do
            -- 注入到skin_map
            if not self.skin_map[skinId] then
                self.skin_map[skinId] = 1
                injectedCount = injectedCount + 1
            end
            -- 注入到skins数组（用于其他验证）
            if self.characterInfo.skins then
                local found = false
                for i = 1, #self.characterInfo.skins do
                    if self.characterInfo.skins[i] == skinId then
                        found = true
                        break
                    end
                end
                if not found then
                    table.insert(self.characterInfo.skins, skinId)
                end
            end
        end
    end
    -- 注入所有角色
    local allChars = ExcelMgr.GetTable('item_definition', 'character')
    if allChars then
        for charId, charData in pairs(allChars) do
            local found = false
            for i, char in ipairs(self.characterInfo.characters) do
                if char.charid == charId then
                    found = true
                    break
                end
            end
            if not found then
                -- 创建新角色数据（含完整解锁状态）
                local newChar = {
                    charid = charId,
                    level = 5,
                    exp = 0,
                    skin = charData.init_skin or (charId + 200000),
                    is_upgraded = true,
                    views = {},
                    rewarded_level = {1, 2, 3, 4, 5},  -- 已领取所有等级奖励
                    extra_emoji = {},                   -- 额外表情（后面填充）
                    finished_endings = {}              -- 已完成结局（后面填充）
                }
                -- 注入角色的额外表情
                local charEmoji = ExcelMgr.GetTable('character_emoji')
                if charEmoji then
                    for emojiId, emojiData in pairs(charEmoji) do
                        if emojiData and emojiData.charid == charId then
                            table.insert(newChar.extra_emoji, emojiId)
                        end
                    end
                end
                -- 注入角色的结局
                local charEnding = ExcelMgr.GetTable('character_ending')
                if charEnding then
                    for endingId, endingData in pairs(charEnding) do
                        if endingData and endingData.charid == charId then
                            table.insert(newChar.finished_endings, endingId)
                        end
                    end
                end
                table.insert(self.characterInfo.characters, newChar)
            else
                -- 已存在的角色也补充缺失字段
                for i, char in ipairs(self.characterInfo.characters) do
                    if char.charid == charId then
                        char.level = 5
                        char.is_upgraded = true
                        if not char.rewarded_level or #char.rewarded_level < 5 then
                            char.rewarded_level = {1, 2, 3, 4, 5}
                        end
                        if not char.extra_emoji then
                            char.extra_emoji = {}
                            local charEmoji = ExcelMgr.GetTable('character_emoji')
                            if charEmoji then
                                for emojiId, emojiData in pairs(charEmoji) do
                                    if emojiData and emojiData.charid == charId then
                                        table.insert(char.extra_emoji, emojiId)
                                    end
                                end
                            end
                        end
                        if not char.finished_endings then
                            char.finished_endings = {}
                            local charEnding = ExcelMgr.GetTable('character_ending')
                            if charEnding then
                                for endingId, endingData in pairs(charEnding) do
                                    if endingData and endingData.charid == charId then
                                        table.insert(char.finished_endings, endingId)
                                    end
                                end
                            end
                        end
                        break
                    end
                end
            end
        end
    end
end)";

            if (Regex.IsMatch(luaCode, skinMapPattern, RegexOptions.Singleline))
            {
                luaCode = Regex.Replace(luaCode, skinMapPattern, skinMapReplacement, RegexOptions.Singleline);
            }

            // 方案3: 修改 SortSkin 函数，确保在排序后也执行注入
            string sortSkinPattern = @"function GameMgr:SortSkin\(\)\s+if self\.characterInfo\.skins then\s+for l=1,#self\.characterInfo\.skins do\s+self\.skin_map\[self\.characterInfo\.skins\[l\]\]=1\s+end\s+end\s+end";
            string sortSkinReplacement = @"function GameMgr:SortSkin()
    if self.characterInfo.skins then
        for l=1,#self.characterInfo.skins do
            self.skin_map[self.characterInfo.skins[l]]=1
        end
    end
    -- [MajSoulHelper] 重新注入所有皮肤
    pcall(function()
        local allSkins = ExcelMgr.GetTable('item_definition', 'skin')
        if allSkins then
            for skinId, _ in pairs(allSkins) do
                self.skin_map[skinId] = 1
            end
        end
    end)
end";

            if (Regex.IsMatch(luaCode, sortSkinPattern, RegexOptions.Singleline))
            {
                luaCode = Regex.Replace(luaCode, sortSkinPattern, sortSkinReplacement, RegexOptions.Singleline);
            }

            return luaCode;
        }

        /// <summary>
        /// 修改 UI_Bag_SkinCell:Show 函数
        /// 隐藏锁定UI
        /// </summary>
        private static string PatchSkinCellUI(string luaCode)
        {
            if (!PluginConfig.HideLockUI) return luaCode;

            // 压缩格式：直接替换 SetActive(true) 为 SetActive(false)
            luaCode = luaCode.Replace(
                "self.container_lock.gameObject:SetActive(true)",
                "self.container_lock.gameObject:SetActive(false)");

            return luaCode;
        }

        /// <summary>
        /// 修改背包UI逻辑
        /// </summary>
        private static string PatchBagUI(string luaCode)
        {
            // 修改皮肤过滤逻辑，显示所有皮肤
            // 通常是检查 skin_map 的地方

            // 移除皮肤拥有检查
            luaCode = luaCode.Replace(
                "if GameMgr.Inst.skin_map[",
                "if true or GameMgr.Inst.skin_map[");

            return luaCode;
        }

        /// <summary>
        /// 通用皮肤UI修改
        /// </summary>
        private static string PatchGenericSkinUI(string luaCode)
        {
            // 移除各种皮肤拥有检查
            luaCode = luaCode.Replace(
                "if GameMgr.Inst.skin_map[",
                "if true or GameMgr.Inst.skin_map[");

            luaCode = luaCode.Replace(
                "GameUtility.item_owned(",
                "true or GameUtility.item_owned(");

            return luaCode;
        }

        /// <summary>
        /// 修改寮舍主界面 - UI_LiaosheMain
        /// 确保立绘正确显示，语音正常播放
        /// </summary>
        private static string PatchLiaosheMainUI(string luaCode)
        {
            // 1. 基础的皮肤拥有检查移除
            luaCode = PatchGenericSkinUI(luaCode);
            
            // 2. 确保 ChangeSkin 函数正确显示立绘动画
            // 原始: ignoreAnim=l (l是是否拥有的检查结果)
            luaCode = luaCode.Replace(
                "ignoreAnim=l",
                "ignoreAnim=false --[MajSoulHelper]");
            
            // 3. 确保语音按钮可点击
            // 移除语音按钮的禁用逻辑（如果有）
            
            return luaCode;
        }

        /// <summary>
        /// 修改寮舍选择界面 - UI_LiaosheSelect
        /// 确保所有角色都可以选择和使用
        /// </summary>
        private static string PatchLiaosheSelectUI(string luaCode)
        {
            // 1. 基础的皮肤拥有检查移除
            luaCode = PatchGenericSkinUI(luaCode);
            
            // 2. 修改 Switch 函数，确保选择任意角色时能正确切换
            // 关键：当选择角色并点击"使用"时，需要正确设置主角色
            // 原始逻辑检查 GameMgr.Inst.characterInfo.main_character_id != P.charid
            // 不需要修改，因为我们已经注入了所有角色
            
            // 3. 确保角色立绘正确显示
            // 原始: UI_LiaosheMain.Inst:ChangeSkin(C,P.skin)
            // 这里依赖注入的角色数据中的skin字段是正确的
            
            // 4. 修改 ShowSkin 函数传递的参数
            // 确保 curCharacter 正确设置
            
            // 5. 修复"正在使用"标记的显示逻辑
            // 不需要修改，只需要确保角色数据正确
            
            return luaCode;
        }

        /// <summary>
        /// 修改角色皮肤选择UI - UI_Character_Skin
        /// 处理Spine动画和皮肤加载逻辑
        /// </summary>
        private static string PatchCharacterSkinUI(string luaCode)
        {
            // 修改 IsNeedShowSpine 函数，确保动画可以正常显示
            // 原始逻辑只检查用户设置，不需要修改

            // 修改皮肤拥有检查
            luaCode = luaCode.Replace(
                "if GameMgr.Inst.skin_map[",
                "if true or GameMgr.Inst.skin_map[");

            // 修改 ignoreAnim 参数检查，确保动画正常加载
            // 原始代码: ignoreAnim=not d (d是skin_map检查结果)
            // 修改为: ignoreAnim=false，让动画始终可以播放
            luaCode = luaCode.Replace(
                "ignoreAnim=not d",
                "ignoreAnim=false --[MajSoulHelper]");

            return luaCode;
        }

        /// <summary>
        /// 修改换肤UI - UI_LiaoSheChangeSkin
        /// 处理锁定图标和使用按钮逻辑
        /// 关键修复：确保选中的皮肤可以正确应用到对应角色
        /// </summary>
        private static string PatchChangeSkinUI(string luaCode)
        {
            // 1. 修改皮肤列表显示锁定图标 - 隐藏所有锁定图标
            luaCode = luaCode.Replace(
                "if GameMgr.Inst.skin_map[self.skinIdList[W.skinIndex]]",
                "if true --[MajSoulHelper] or GameMgr.Inst.skin_map[self.skinIdList[W.skinIndex]]");

            // 2. 修改按钮状态判断 - 让所有皮肤都显示为"使用"而不是"购买"
            luaCode = luaCode.Replace(
                "local a2=GameMgr.Inst.skin_map[y]and true or false",
                "local a2=true --[MajSoulHelper] GameMgr.Inst.skin_map[y]and true or false");

            // 3. 移除锁定图标
            luaCode = luaCode.Replace(
                "W.lockRoot:SetActive(true)",
                "W.lockRoot:SetActive(false) --[MajSoulHelper]");

            luaCode = luaCode.Replace(
                "W.staticLock:SetActive(true)",
                "W.staticLock:SetActive(false) --[MajSoulHelper]");
            
            // 4. 修复 ChangeSkin 函数，确保正确更新角色皮肤
            // 原始流程：发送网络请求 -> 成功后更新本地数据
            // 修改后：直接更新本地数据（由 LobbyNetMgr 补丁处理网络请求拦截）
            
            // 5. 确保 RefreshData 获取所有皮肤（包括未拥有的）
            // 原始: local I=Tools.IsSkinOK(H) 已经被 PatchToolsModule 修改为始终返回true
            
            // 6. 修复立绘显示时的动画问题
            luaCode = luaCode.Replace(
                "ignoreAnim=not a2",
                "ignoreAnim=false --[MajSoulHelper]");
            
            // 7. 确保皮肤可以正确使用 - 不检查拥有状态
            // 原始: if a3 then (正在使用) ... elseif a2 then (已拥有) ... elseif a5 then (可购买)
            // 需要确保 a2=true 使得所有皮肤都走"已拥有"分支

            return luaCode;
        }

        /// <summary>
        /// 修改皮肤预览UI - UI_Skin_Yulan
        /// </summary>
        private static string PatchSkinPreviewUI(string luaCode)
        {
            // 1. 修改按钮显示逻辑 - 始终显示使用按钮
            luaCode = luaCode.Replace(
                "local n=GameUtility.item_owned(k.character_id)and GameUtility.item_owned(h)",
                "local n=true --[MajSoulHelper] GameUtility.item_owned");

            // 2. 修改 flip 参数 - 皮肤不翻转
            luaCode = luaCode.Replace(
                "flip=not d",
                "flip=false --[MajSoulHelper]");

            // 3. 确保动画正常播放
            luaCode = luaCode.Replace(
                "ignoreAnim=not d",
                "ignoreAnim=false --[MajSoulHelper]");

            return luaCode;
        }

        /// <summary>
        /// 修改皮肤商店预览UI - UI_SkinShop_Yulan
        /// </summary>
        private static string PatchSkinShopUI(string luaCode)
        {
            // 修改拥有检查
            luaCode = luaCode.Replace(
                "if GameMgr.Inst.skin_map[",
                "if true or GameMgr.Inst.skin_map[");

            return luaCode;
        }

        /// <summary>
        /// 修改 Tools 模块
        /// 1. IsSkinOK 始终返回true
        /// 2. get_chara_audio 解锁全部语音（移除等级和羁绊限制）
        /// 3. IsHiddenCharacter 始终返回false（显示隐藏角色）
        /// </summary>
        private static string PatchToolsModule(string luaCode)
        {
            // Tools.IsSkinOK 可能包含皮肤有效性检查
            // 使用正则匹配任意参数名，用 do return true end 立即返回
            string skinOkPattern = @"function Tools\.IsSkinOK\((\w+)\)";
            string skinOkReplacement = @"function Tools.IsSkinOK($1)do return true end;";
            
            luaCode = RegexReplaceWithWarning(luaCode, skinOkPattern, skinOkReplacement, "Tools.IsSkinOK函数");

            // ======== 语音解锁 ========
            // 核心修复：在 get_chara_audio 函数开头修改 k 和 o 的值
            // 原始代码: local k=h.level;...local o=0;if h.is_upgraded then o=1 end
            // 强制设置 k=5 (最高等级) 和 o=1 (已觉醒) 来解锁所有语音
            
            // 方法1：直接修改获取level的逻辑
            // 原始: function Tools.get_chara_audio(h,type)if not type or type==''then return nil end;local i=h.charid;
            string audioFuncPattern = @"function Tools\.get_chara_audio\((\w+),type\)if not type or type==''then return nil end;local (\w+)=\1\.charid;";
            string audioFuncReplacement = @"function Tools.get_chara_audio($1,type)if not type or type==''then return nil end;$1.level=5;$1.is_upgraded=true;local $2=$1.charid;";
            
            luaCode = RegexReplaceWithWarning(luaCode, audioFuncPattern, audioFuncReplacement, "Tools.get_chara_audio函数");
            
            // 方法2（备用）：直接替换条件检查 - 移除 level_limit 和 bond_limit 限制
            // 模式1：普通语音类型检查
            // 原始: if q and q.type==type and q.level_limit<=k and q.bond_limit<=o then
            luaCode = RegexReplaceWithWarning(luaCode, 
                @"if (\w+) and \1\.type==type and \1\.level_limit<=(\w+) and \1\.bond_limit<=(\w+) then",
                @"if $1 and $1.type==type then", "语音level_limit检查");
            
            // 模式2：lobby_limited类型的语音检查
            // 原始: if q and q.type=='lobby_limited'and q.level_limit<=k and q.bond_limit<=o then
            luaCode = RegexReplaceWithWarning(luaCode,
                @"if (\w+) and \1\.type=='lobby_limited'and \1\.level_limit<=(\w+) and \1\.bond_limit<=(\w+) then",
                @"if $1 and $1.type=='lobby_limited'then", "语音lobby_limited检查");

            // ======== 隐藏角色显示 ========
            // Tools.IsHiddenCharacter 始终返回false，让所有角色都可见
            string hiddenCharPattern = @"function Tools\.IsHiddenCharacter\((\w+)\)";
            string hiddenCharReplacement = @"function Tools.IsHiddenCharacter($1)do return false end;";
            
            luaCode = RegexReplaceWithWarning(luaCode, hiddenCharPattern, hiddenCharReplacement, "Tools.IsHiddenCharacter函数");

            return luaCode;
        }

        /// <summary>
        /// 修改 LobbyNetMgr 模块 - 拦截皮肤更换请求和房间相关响应
        /// 核心逻辑（参考 MajsoulMax 实现）：
        /// 1. 对于未拥有的角色/皮肤：完全阻止发送请求，本地保存选择，触发 NotifyAccountUpdate 更新UI
        /// 2. 对于 joinRoom/createRoom/fetchRoom 响应：在回调前替换自己的皮肤数据
        /// 3. 支持固定伪造配置（从 MajSoulHelper_FakeConfig 读取）
        /// 4. 类似 MajsoulMax 的 settings['config']['characters'] 持久化机制
        /// 关键修复：不再发送伪造请求到服务器，而是完全阻止并在本地模拟成功
        /// 注意: 使用单行格式以适配压缩的Lua代码
        /// </summary>
        private static string PatchLobbyNetMgr(string luaCode)
        {
            // ======== 1. 拦截 SendRequest ========
            string pattern = @"function LobbyNetMgr\.SendRequest\((\w+),(\w+),(\w+),(\w+)\)";
            
            string replacement = @"function LobbyNetMgr.SendRequest($1,$2,$3,$4)" +
                // ======== 初始化持久化皮肤选择表 ========
                // MajSoulHelper_CharacterSkins[character_id] = skin_id
                // 类似 MajsoulMax 的 settings['config']['characters']
                @"MajSoulHelper_CharacterSkins=MajSoulHelper_CharacterSkins or{};" +
                // 同步 C# 缓存的历史皮肤选择
                @"pcall(function()local cached=CS.MajSoulHelper.CharacterDataCache.GetAllSelections();if cached then for k,v in pairs(cached)do MajSoulHelper_CharacterSkins[k]=v end end end);" +
                // 初始化本地配置存储（类似 MajsoulMax 的 settings['config']）
                @"MajSoulHelper_LocalConfig=MajSoulHelper_LocalConfig or{title=0,loading_image={},views={},views_index=0};" +
                // 同步 C# 缓存的称号
                @"pcall(function()MajSoulHelper_LocalConfig.title=CS.MajSoulHelper.CharacterDataCache.GetSelectedTitle() or MajSoulHelper_LocalConfig.title end);" +
                
                // ======== 定义获取伪造配置的函数 ========
                // 优先级：固定配置 > 持久化皮肤选择 > 游戏内数据
                @"local function _MH_GetFakeConfig()local cfg=MajSoulHelper_FakeConfig or{};" +
                @"if cfg.enabled and cfg.charId and cfg.charId>0 then return cfg end;" +
                // 如果未启用固定配置，使用游戏内的数据和持久化的皮肤选择
                @"local result={enabled=false,charId=0,skinId=0,titleId=0,views=nil};" +
                @"if GameMgr.Inst and GameMgr.Inst.characterInfo then " +
                @"result.charId=GameMgr.Inst.characterInfo.main_character_id;" +
                // 优先从持久化选择中获取皮肤
                @"local persistedSkin=MajSoulHelper_CharacterSkins[result.charId];" +
                @"pcall(function()if not persistedSkin or persistedSkin==0 then persistedSkin=CS.MajSoulHelper.CharacterDataCache.GetCharacterSkin(result.charId) end end);" +
                @"if persistedSkin and persistedSkin>0 then result.skinId=persistedSkin else " +
                @"for _,c in ipairs(GameMgr.Inst.characterInfo.characters or{})do " +
                @"if c.charid==result.charId then result.skinId=c.skin;result.views=c.views;break end end end;" +
                // 获取本地保存的称号
                @"pcall(function()if not result.titleId or result.titleId==0 then result.titleId=CS.MajSoulHelper.CharacterDataCache.GetSelectedTitle() end end);" +
                @"if not result.titleId or result.titleId==0 then result.titleId=MajSoulHelper_LocalConfig.title or 0 end end;" +
                // 从缓存兜底主角色与皮肤，允许伪造未拥有角色
                @"pcall(function()if(not result.charId or result.charId==0)and CS and CS.MajSoulHelper then result.charId=CS.MajSoulHelper.CharacterDataCache.GetMainCharacterId() end end);" +
                @"pcall(function()if(not result.skinId or result.skinId==0)and result.charId and result.charId>0 then result.skinId=CS.MajSoulHelper.CharacterDataCache.GetCharacterSkin(result.charId) end end);" +
                // 默认皮肤兜底，避免 skinId 为空导致替换失败
                @"pcall(function()if(not result.skinId or result.skinId==0)and result.charId and result.charId>0 then result.skinId=CS.MajSoulHelper.CharacterDataCache.GetDefaultSkinId(result.charId) end end);" +
                @"if(not result.charId or result.charId==0)then result.charId=200001 end;" +
                @"if(not result.skinId or result.skinId==0)and result.charId and result.charId>0 then result.skinId=result.charId+200000 end;" +
                @"return result end;" +

                // ======== 定义皮肤替换函数 - 处理 room.persons 数据 ========
                // 参考 MajsoulMax 的 createRoom/fetchRoom/joinRoom 处理
                @"local function _MH_ReplaceRoomSkin(room)if not room or not room.persons or not GameMgr.Inst then return end;" +
                @"local myId=GameMgr.Inst.account_id;local cfg=_MH_GetFakeConfig();" +
                @"pcall(function()if(not cfg.charId or cfg.charId==0)and CS and CS.MajSoulHelper then cfg.charId=CS.MajSoulHelper.CharacterDataCache.GetMainCharacterId() end end);" +
                @"pcall(function()if(not cfg.skinId or cfg.skinId==0)and cfg.charId and cfg.charId>0 then cfg.skinId=CS.MajSoulHelper.CharacterDataCache.GetCharacterSkin(cfg.charId) end end);" +
                @"pcall(function()if(not cfg.skinId or cfg.skinId==0)and cfg.charId and cfg.charId>0 then cfg.skinId=CS.MajSoulHelper.CharacterDataCache.GetDefaultSkinId(cfg.charId) end end);" +
                @"if not cfg.skinId or cfg.skinId==0 then return end;" +
                // 获取装扮和头像框
                @"local lc=MajSoulHelper_LocalConfig or{};local myViews=nil;local avatarFrame=0;" +
                @"if lc.views and lc.views_index and lc.views[lc.views_index]then myViews=lc.views[lc.views_index];" +
                @"for _,v in ipairs(myViews)do if v.slot==5 then avatarFrame=v.item_id or 0;break end end end;" +
                // 遍历房间玩家，替换自己的皮肤数据
                @"for i,p in ipairs(room.persons)do if p.account_id==myId then " +
                @"p.avatar_id=cfg.skinId;" +
                @"if avatarFrame>0 then p.avatar_frame=avatarFrame end;" +
                @"if p.character then p.character.skin=cfg.skinId;p.character.charid=cfg.charId;p.character.level=5;p.character.exp=0;p.character.is_upgraded=true;" +
                // 添加 rewarded_level
                @"p.character.rewarded_level=p.character.rewarded_level or{};for rl=1,5 do table.insert(p.character.rewarded_level,rl)end;" +
                // 设置 views
                @"if myViews then p.character.views={};for _,v in ipairs(myViews)do table.insert(p.character.views,{slot=v.slot,item_id=v.item_id or 0})end end end;" +
                @"if cfg.titleId and cfg.titleId>0 then p.title=cfg.titleId end;" +
                @"break end end end;" +
                
                // ======== 定义完全阻止请求并返回成功的辅助函数 ========
                // 参考 MajsoulMax：使用 fake 标志完全阻止请求发送
                @"local function _MH_FakeSuccess(cb)if cb then TimeMgr.DelayFrame(1,function()cb(nil,{error={code=0}})end)end end;" +
                
                // ======== 处理 changeCharacterSkin 请求 ========
                // 关键修复：完全阻止发送到服务器，本地处理并触发 NotifyAccountUpdate
                @"if $2=='changeCharacterSkin'and $3 then local cid=$3.character_id;local sid=$3.skin;" +
                // 持久化保存用户选择
                @"MajSoulHelper_CharacterSkins[cid]=sid;" +
                @"pcall(function()CS.MajSoulHelper.CharacterDataCache.SetCharacterSkin(cid,sid)end);" +
                // 本地立即更新显示
                @"pcall(function()" +
                @"local found=false;for i,c in ipairs(GameMgr.Inst.characterInfo.characters)do if c.charid==cid then c.skin=sid;c.level=5;c.is_upgraded=true;found=true;break end end;" +
                @"if not found then table.insert(GameMgr.Inst.characterInfo.characters,{charid=cid,skin=sid,level=5,is_upgraded=true,exp=0,views={},rewarded_level={1,2,3,4,5}})end;" +
                @"if cid==GameMgr.Inst.characterInfo.main_character_id then GameMgr.Inst.account_data.avatar_id=sid end;" +
                // 触发 NotifyAccountUpdate 刷新UI（参考 MajsoulMax 的 inject 机制）
                @"local updateData={update={character={characters={{charid=cid,skin=sid,level=5,is_upgraded=true,exp=0}}}}};" +
                @"pcall(function()LobbyNetMgr.Inst:DispatchMsg('NotifyAccountUpdate',updateData)end)" +
                @"end);" +
                // 完全阻止请求发送，直接返回成功
                @"_MH_FakeSuccess($4);return end;" +
                
                // ======== 处理 changeMainCharacter 请求 ========
                @"if $2=='changeMainCharacter'and $3 then local cid=$3.character_id;" +
                @"pcall(function()" +
                @"GameMgr.Inst.characterInfo.main_character_id=cid;" +
                @"pcall(function()CS.MajSoulHelper.CharacterDataCache.SetMainCharacter(cid)end);" +
                @"local persistedSkin=MajSoulHelper_CharacterSkins[cid];" +
                @"if not persistedSkin then for _,c in ipairs(GameMgr.Inst.characterInfo.characters)do if c.charid==cid then persistedSkin=c.skin;break end end end;" +
                @"pcall(function()if not persistedSkin then persistedSkin=CS.MajSoulHelper.CharacterDataCache.GetCharacterSkin(cid) end end);" +
                @"if not persistedSkin then pcall(function()persistedSkin=CS.MajSoulHelper.CharacterDataCache.GetDefaultSkinId(cid) end) end;" +
                @"if not persistedSkin then persistedSkin=cid+200000 end;" +
                @"GameMgr.Inst.account_data.avatar_id=persistedSkin;" +
                @"local updateData={update={character={main_character_id=cid,characters={{charid=cid,skin=persistedSkin,level=5,is_upgraded=true}}}}};" +
                @"pcall(function()LobbyNetMgr.Inst:DispatchMsg('NotifyAccountUpdate',updateData)end)" +
                @"end);" +
                @"_MH_FakeSuccess($4);return end;" +
                
                // ======== 处理 useTitle 请求 ========
                @"if $2=='useTitle'and $3 then " +
                @"MajSoulHelper_LocalConfig.title=$3.title or 0;" +
                @"pcall(function()CS.MajSoulHelper.CharacterDataCache.SetSelectedTitle($3.title or 0)end);" +
                @"pcall(function()GameMgr.Inst.account_data.title=$3.title or 0 end);" +
                @"_MH_FakeSuccess($4);return end;" +
                
                // ======== 处理 setLoadingImage 请求 ========
                @"if $2=='setLoadingImage'and $3 then " +
                @"MajSoulHelper_LocalConfig.loading_image=$3.images or{};" +
                @"_MH_FakeSuccess($4);return end;" +
                
                // ======== 处理 saveCommonViews 请求 ========
                @"if $2=='saveCommonViews'and $3 then " +
                @"local idx=$3.save_index or 0;MajSoulHelper_LocalConfig.views[idx]=$3.views or{};" +
                @"if $3.is_use==1 then MajSoulHelper_LocalConfig.views_index=idx end;" +
                @"_MH_FakeSuccess($4);return end;" +
                
                // ======== 处理 useCommonView 请求 ========
                @"if $2=='useCommonView'and $3 then " +
                @"MajSoulHelper_LocalConfig.views_index=$3.index or 0;" +
                @"_MH_FakeSuccess($4);return end;" +
                
                // ======== 处理 updateCharacterSort 请求 ========
                @"if $2=='updateCharacterSort'and $3 then " +
                @"MajSoulHelper_LocalConfig.star_chars=$3.sort or{};" +
                @"_MH_FakeSuccess($4);return end;" +
                
                // ======== 处理 receiveCharacterRewards 请求 ========
                @"if $2=='receiveCharacterRewards'then " +
                @"_MH_FakeSuccess($4);return end;" +
                
                // ======== 处理 addFinishedEnding 请求 ========
                @"if $2=='addFinishedEnding'then " +
                @"_MH_FakeSuccess($4);return end;" +
                
                // ======== 处理 setRandomCharacter 请求 ========
                @"if $2=='setRandomCharacter'and $3 then " +
                @"MajSoulHelper_LocalConfig.random_character=$3;" +
                @"_MH_FakeSuccess($4);return end;" +
                
                // ======== 包装 joinRoom/createRoom/fetchRoom/fetchAccountInfo 的回调 ========
                // 关键：在调用原始回调之前先替换皮肤数据
                // 参考 MajsoulMax: fetchAccountInfo 用于资料页面显示
                @"local _origCallback=$4;" +
                @"if $2=='joinRoom'or $2=='createRoom'or $2=='fetchRoom'then " +
                @"$4=function(err,res)if res and res.room then pcall(function()_MH_ReplaceRoomSkin(res.room)end)end;" +
                @"if _origCallback then _origCallback(err,res)end end " +
                // 包装 fetchAccountInfo 回调 - 资料页面
                @"elseif $2=='fetchAccountInfo'then " +
                @"$4=function(err,res)pcall(function()if res and res.account and GameMgr.Inst then " +
                @"local myId=GameMgr.Inst.account_id;" +
                @"if res.account.account_id==myId then " +
                @"local cfg=_MH_GetFakeConfig();" +
                @"if cfg.skinId and cfg.skinId>0 then res.account.avatar_id=cfg.skinId end;" +
                @"if cfg.titleId and cfg.titleId>0 then res.account.title=cfg.titleId end;" +
                @"end end end);" +
                @"if _origCallback then _origCallback(err,res)end end " +
                // 包装 fetchAccountInfoByIds 回调 - 列表资料页面（友人房/观战）
                @"elseif $2=='fetchAccountInfoByIds'then " +
                @"$4=function(err,res)pcall(function()if res and GameMgr.Inst then " +
                @"local myId=GameMgr.Inst.account_id;local cfg=_MH_GetFakeConfig();" +
                @"local list=res.accounts or res.account_list or res.accountInfos;" +
                @"if list then for _,a in ipairs(list)do if a.account_id==myId then " +
                @"if cfg.skinId and cfg.skinId>0 then a.avatar_id=cfg.skinId end;" +
                @"if cfg.titleId and cfg.titleId>0 then a.title=cfg.titleId end;" +
                @"end end end end end);" +
                @"if _origCallback then _origCallback(err,res)end end " +
                // 包装 login/oauth2Login 回调 - 登录时设置初始 avatar_id（参考 MajsoulMax）
                @"elseif $2=='login'or $2=='oauth2Login'then " +
                @"$4=function(err,res)pcall(function()if res and res.account then " +
                // 保存原始服务器数据到全局
                @"MajSoulHelper_ServerData=MajSoulHelper_ServerData or{};" +
                @"MajSoulHelper_ServerData.account_id=res.account_id;" +
                @"MajSoulHelper_ServerData.avatar_id=res.account.avatar_id;" +
                @"MajSoulHelper_ServerData.title=res.account.title;" +
                @"MajSoulHelper_ServerData.loading_image=res.account.loading_image;" +
                // 应用伪造配置
                @"local cfg=_MH_GetFakeConfig();" +
                @"if cfg.skinId and cfg.skinId>0 then res.account.avatar_id=cfg.skinId end;" +
                @"if cfg.titleId and cfg.titleId>0 then res.account.title=cfg.titleId end;" +
                // 应用 loading_image
                @"local lc=MajSoulHelper_LocalConfig or{};" +
                @"if lc.loading_image and #lc.loading_image>0 then res.account.loading_image=lc.loading_image end;" +
                @"end end);" +
                @"if _origCallback then _origCallback(err,res)end end end;";
            
            luaCode = RegexReplaceWithWarning(luaCode, pattern, replacement, "LobbyNetMgr.SendRequest");

            // ======== 2. 拦截 AddMsgListener - 包装 NotifyRoomPlayerUpdate 回调 ========
            string listenerPattern = @"function LobbyNetMgr\.AddMsgListener\((\w+),(\w+)\)";
            string listenerReplacement = @"function LobbyNetMgr.AddMsgListener($1,$2)local _origCb=$2;" +
                // 获取伪造配置，增加称号与缓存兜底
                @"local function _MH_GetFakeConfig2()local cfg=MajSoulHelper_FakeConfig or{};" +
                @"if cfg.enabled and cfg.charId and cfg.charId>0 then return cfg end;" +
                @"local result={charId=0,skinId=0,views=nil,titleId=0};" +
                @"if GameMgr.Inst and GameMgr.Inst.characterInfo then " +
                @"result.charId=GameMgr.Inst.characterInfo.main_character_id;" +
                @"for _,c in ipairs(GameMgr.Inst.characterInfo.characters or{})do " +
                @"if c.charid==result.charId then result.skinId=c.skin;result.views=c.views;break end end;" +
                @"local persistedSkin=MajSoulHelper_CharacterSkins and MajSoulHelper_CharacterSkins[result.charId];" +
                @"pcall(function()if(not persistedSkin or persistedSkin==0)and CS and CS.MajSoulHelper and CS.MajSoulHelper.CharacterDataCache then persistedSkin=CS.MajSoulHelper.CharacterDataCache.GetCharacterSkin(result.charId) end end);" +
                @"if persistedSkin and persistedSkin>0 then result.skinId=persistedSkin end;" +
                @"local lc=MajSoulHelper_LocalConfig or{};result.titleId=lc.title or 0;" +
                @"pcall(function()if(not result.titleId or result.titleId==0)and CS and CS.MajSoulHelper then result.titleId=CS.MajSoulHelper.CharacterDataCache.GetSelectedTitle() end end);" +
                @"end;" +
                @"pcall(function()if(not result.charId or result.charId==0)and CS and CS.MajSoulHelper then result.charId=CS.MajSoulHelper.CharacterDataCache.GetMainCharacterId() end end);" +
                @"pcall(function()if(not result.skinId or result.skinId==0)and result.charId and result.charId>0 then result.skinId=CS.MajSoulHelper.CharacterDataCache.GetCharacterSkin(result.charId) end end);" +
                @"pcall(function()if(not result.skinId or result.skinId==0)and result.charId and result.charId>0 then result.skinId=CS.MajSoulHelper.CharacterDataCache.GetDefaultSkinId(result.charId) end end);" +
                @"if(not result.charId or result.charId==0)then result.charId=200001 end;" +
                @"if(not result.skinId or result.skinId==0)and result.charId and result.charId>0 then result.skinId=result.charId+200000 end;" +
                @"return result end;" +
                // 包装 NotifyRoomPlayerUpdate 通知的回调
                @"if $1=='NotifyRoomPlayerUpdate'then $2=function(msg)pcall(function()" +
                @"if msg and msg.player_list and GameMgr.Inst then " +
                @"local myId=GameMgr.Inst.account_id;local cfg=_MH_GetFakeConfig2();" +
                @"pcall(function()if(not cfg.skinId or cfg.skinId==0)and cfg.charId and cfg.charId>0 then cfg.skinId=CS.MajSoulHelper.CharacterDataCache.GetCharacterSkin(cfg.charId) end end);" +
                @"pcall(function()if(not cfg.skinId or cfg.skinId==0)and cfg.charId and cfg.charId>0 then cfg.skinId=CS.MajSoulHelper.CharacterDataCache.GetDefaultSkinId(cfg.charId) end end);" +
                @"if cfg.skinId and cfg.skinId>0 then for i,p in ipairs(msg.player_list)do if p.account_id==myId then " +
                @"p.avatar_id=cfg.skinId;" +
                @"if p.character then p.character.skin=cfg.skinId;p.character.charid=cfg.charId;p.character.level=5;p.character.is_upgraded=true;" +
                @"if cfg.views then p.character.views=cfg.views end end;" +
                @"break end end end end end);" +
                @"if _origCb then _origCb(msg)end end " +
                // 处理 NotifyAccountUpdate，避免返回大厅后角色/装扮被服务器数据覆盖
                @"elseif $1=='NotifyAccountUpdate'then $2=function(msg)pcall(function()" +
                @"if msg and GameMgr.Inst then local cfg=_MH_GetFakeConfig2();" +
                @"if(not cfg.charId or cfg.charId==0)then pcall(function()cfg.charId=CS.MajSoulHelper.CharacterDataCache.GetMainCharacterId() end)end;" +
                @"if(not cfg.skinId or cfg.skinId==0)and cfg.charId and cfg.charId>0 then pcall(function()cfg.skinId=CS.MajSoulHelper.CharacterDataCache.GetCharacterSkin(cfg.charId) end)end;" +
                @"if(not cfg.skinId or cfg.skinId==0)and cfg.charId and cfg.charId>0 then pcall(function()cfg.skinId=CS.MajSoulHelper.CharacterDataCache.GetDefaultSkinId(cfg.charId) end)end;" +
                @"if msg.update then " +
                @"if msg.update.character then " +
                @"if msg.update.character.main_character_id then msg.update.character.main_character_id=cfg.charId end;" +
                @"if msg.update.character.characters then for _,c in ipairs(msg.update.character.characters)do c.charid=cfg.charId;c.skin=cfg.skinId;c.level=5;c.is_upgraded=true end end end;" +
                @"if msg.update.account then msg.update.account.avatar_id=cfg.skinId;if cfg.titleId and cfg.titleId>0 then msg.update.account.title=cfg.titleId end end end;" +
                @"pcall(function()if GameMgr.Inst and GameMgr.Inst.characterInfo then GameMgr.Inst.characterInfo.main_character_id=cfg.charId;end end);" +
                @"pcall(function()if GameMgr.Inst and GameMgr.Inst.account_data then GameMgr.Inst.account_data.avatar_id=cfg.skinId;if cfg.titleId and cfg.titleId>0 then GameMgr.Inst.account_data.title=cfg.titleId end end end);" +
                @"end end);if _origCb then _origCb(msg)end end end;";

            luaCode = RegexReplaceWithWarning(luaCode, listenerPattern, listenerReplacement, "LobbyNetMgr.AddMsgListener");
            
            return luaCode;
        }

        /// <summary>
        /// 修改 MJNetMgr 模块 - 对局网络管理器
        /// 拦截 authGame 响应，在玩家数据传递给 DesktopMgr:InitRoom 之前替换自己的皮肤
        /// 这是实现对局中皮肤显示的核心（参考 MajsoulMax 的 .lq.FastTest.authGame 处理）
        /// 支持固定伪造配置（从 MajSoulHelper_FakeConfig 读取）
        /// 完整替换：character.charid, character.skin, avatar_id, views, title
        /// </summary>
        private static string PatchMJNetMgr(string luaCode)
        {
            if (!PluginConfig.EnableInGameSkinReplace)
            {
                return luaCode;
            }

            // 方法1：修改 authGame 响应回调，完全替换自己的角色和皮肤信息
            // 压缩格式：self:SendRequest('FastTest','authGame',t,function(C,D)
            // 参考 MajsoulMax：完整替换 character, avatar_id, views, title
            string authGamePattern = @"self:SendRequest\('FastTest','authGame',(\w+),function\((\w+),(\w+)\)";
            string authGameReplacement = @"self:SendRequest('FastTest','authGame',$1,function($2,$3)" +
                // 定义获取完整伪造配置的函数（参考 MajsoulMax）
                @"local function _MH_GetFullFakeCfg()local cfg=MajSoulHelper_FakeConfig or{};" +
                @"if cfg.enabled and cfg.charId and cfg.charId>0 then return cfg end;" +
                // 从游戏内数据和本地配置构建完整配置
                @"local result={charId=0,skinId=0,titleId=0,views=nil,avatarFrame=0};" +
                @"if GameMgr.Inst and GameMgr.Inst.characterInfo then " +
                @"result.charId=GameMgr.Inst.characterInfo.main_character_id;" +
                // 优先从持久化皮肤选择中获取
                @"local persistedSkin=MajSoulHelper_CharacterSkins and MajSoulHelper_CharacterSkins[result.charId];" +
                @"if persistedSkin and persistedSkin>0 then result.skinId=persistedSkin else " +
                @"for _,c in ipairs(GameMgr.Inst.characterInfo.characters or{})do " +
                @"if c.charid==result.charId then result.skinId=c.skin;result.views=c.views;break end end end;" +
                @"pcall(function()if(not result.skinId or result.skinId==0)and CS and CS.MajSoulHelper then result.skinId=CS.MajSoulHelper.CharacterDataCache.GetCharacterSkin(result.charId) end end);" +
                @"pcall(function()if(not result.skinId or result.skinId==0)and CS and CS.MajSoulHelper then result.skinId=CS.MajSoulHelper.CharacterDataCache.GetDefaultSkinId(result.charId) end end);" +
                // 从本地配置获取称号和装扮
                @"local lc=MajSoulHelper_LocalConfig or{};" +
                @"result.titleId=lc.title or 0;" +
                @"pcall(function()if not result.titleId or result.titleId==0 then result.titleId=CS.MajSoulHelper.CharacterDataCache.GetSelectedTitle() end end);" +
                @"if lc.views and lc.views_index and lc.views[lc.views_index]then result.views=lc.views[lc.views_index];" +
                // 查找头像框 (slot=5)
                @"for _,v in ipairs(result.views)do if v.slot==5 then result.avatarFrame=v.item_id or 0;break end end end end;" +
                @"pcall(function()if(not result.charId or result.charId==0)and CS and CS.MajSoulHelper then result.charId=CS.MajSoulHelper.CharacterDataCache.GetMainCharacterId() end end);" +
                @"pcall(function()if(not result.skinId or result.skinId==0)and result.charId and result.charId>0 then result.skinId=CS.MajSoulHelper.CharacterDataCache.GetCharacterSkin(result.charId) end end);" +
                @"pcall(function()if(not result.skinId or result.skinId==0)and result.charId and result.charId>0 then result.skinId=CS.MajSoulHelper.CharacterDataCache.GetDefaultSkinId(result.charId) end end);" +
                @"if(not result.charId or result.charId==0)then result.charId=200001 end;" +
                @"if(not result.skinId or result.skinId==0)and result.charId and result.charId>0 then result.skinId=result.charId+200000 end;" +
                @"return result end;" +
                // 替换皮肤（参考 MajsoulMax 的完整实现）
                @"pcall(function()if $3 and $3.players and GameMgr.Inst then " +
                @"local myId=GameMgr.Inst.account_id;local cfg=_MH_GetFullFakeCfg();" +
                @"if cfg.skinId and cfg.skinId>0 then " +
                @"for i=1,#$3.players do local p=$3.players[i];" +
                @"if p.account_id==myId then " +
                // 完整替换所有字段（参考 MajsoulMax）
                @"p.character.charid=cfg.charId;" +
                @"p.character.skin=cfg.skinId;" +
                @"p.avatar_id=cfg.skinId;" +
                @"p.character.level=5;" +
                @"p.character.exp=0;" +
                @"p.character.is_upgraded=true;" +
                // 添加 rewarded_level（参考 MajsoulMax）
                @"p.character.rewarded_level=p.character.rewarded_level or{};for rl=1,5 do table.insert(p.character.rewarded_level,rl)end;" +
                // 设置头像框（参考 MajsoulMax: view['slot'] == 5）
                @"if cfg.avatarFrame and cfg.avatarFrame>0 then p.avatar_frame=cfg.avatarFrame end;" +
                // 清空并重新设置 views（参考 MajsoulMax: p.ClearField('views')）
                @"if cfg.views then p.views={};for _,v in ipairs(cfg.views)do " +
                @"local item_id=v.item_id or(v.item_id_list and v.item_id_list[1])or 0;" +
                @"table.insert(p.views,{slot=v.slot,item_id=item_id});" +
                // 如果是头像框slot，同时更新 avatar_frame
                @"if v.slot==5 then p.avatar_frame=item_id end end end;" +
                @"if cfg.titleId and cfg.titleId>0 then p.title=cfg.titleId end;" +
                @"break end end end end end);";

            luaCode = RegexReplaceWithWarning(luaCode, authGamePattern, authGameReplacement, "MJNetMgr authGame回调");

            // 方法2：修改处理 D.players 的循环，确保皮肤被正确替换
            // 原始：for L=1,#D.players do if D.players[L].account_id==K then E[n]=D.players[L]
            // 在赋值前添加皮肤替换
            string playersLoopPattern = @"if (\w+)\.players\[(\w+)\]\.account_id==(\w+) then (\w+)\[(\w+)\]=\1\.players\[\2\]";
            string playersLoopReplacement = @"if $1.players[$2].account_id==$3 then " +
                @"pcall(function()if $3==GameMgr.Inst.account_id then " +
                @"local p=$1.players[$2];local cfg=MajSoulHelper_FakeConfig or{};" +
                @"local charId,skinId,views,titleId=0,0,nil,0;" +
                @"if cfg.enabled and cfg.charId and cfg.charId>0 then charId=cfg.charId;skinId=cfg.skinId;views=cfg.views;titleId=cfg.titleId;" +
                @"else charId=GameMgr.Inst.characterInfo.main_character_id;" +
                @"local persistedSkin=MajSoulHelper_CharacterSkins and MajSoulHelper_CharacterSkins[charId];" +
                @"if persistedSkin and persistedSkin>0 then skinId=persistedSkin else " +
                @"for _,c in ipairs(GameMgr.Inst.characterInfo.characters or{})do " +
                @"if c.charid==charId then skinId=c.skin;views=c.views;break end end end;" +
                @"pcall(function()if(not skinId or skinId==0)and CS and CS.MajSoulHelper then skinId=CS.MajSoulHelper.CharacterDataCache.GetCharacterSkin(charId) end end);" +
                @"local lc=MajSoulHelper_LocalConfig or{};titleId=lc.title or 0;" +
                @"pcall(function()if(not titleId or titleId==0)and CS and CS.MajSoulHelper then titleId=CS.MajSoulHelper.CharacterDataCache.GetSelectedTitle() end end);" +
                @"end;" +
                @"pcall(function()if(not charId or charId==0)and CS and CS.MajSoulHelper then charId=CS.MajSoulHelper.CharacterDataCache.GetMainCharacterId() end end);" +
                @"pcall(function()if(not skinId or skinId==0)and charId and charId>0 then skinId=CS.MajSoulHelper.CharacterDataCache.GetDefaultSkinId(charId) end end);" +
                @"if skinId and skinId>0 then p.character.charid=charId;p.character.skin=skinId;p.avatar_id=skinId;" +
                @"p.character.level=5;p.character.exp=0;p.character.is_upgraded=true;" +
                @"if views then p.views={};for _,v in ipairs(views)do table.insert(p.views,{slot=v.slot,item_id=v.item_id or 0})end end;" +
                @"if titleId and titleId>0 then p.title=titleId end end end end);" +
                @"$4[$5]=$1.players[$2]";

            luaCode = RegexReplaceWithWarning(luaCode, playersLoopPattern, playersLoopReplacement, "MJNetMgr players循环");

            return luaCode;
        }

        /// <summary>
        /// 修改 DesktopMgr 模块 - 对局中皮肤处理
        /// 确保对局中使用本地设置的皮肤
        /// 参数顺序: function DesktopMgr:InitRoom(c2, c3, c4, c5, c6, c7, c8, c9, ca)
        /// c2=game_config, c3=players, c4=account_id, c5=mode, ...
        /// 注意: 使用单行格式以适配压缩的Lua代码
        /// 关键修复：使用本地 main_character_id 和持久化的皮肤选择替换
        /// </summary>
        private static string PatchDesktopMgr(string luaCode)
        {
            if (!PluginConfig.EnableInGameSkinReplace)
            {
                return luaCode;
            }

            // 压缩格式处理：在 InitRoom 开始处添加皮肤替换逻辑
            // 参数: $1=c2(game_config), $2=c3(players), $3=c4(account_id), $4=c5(mode), ...
            // 遍历 players 数组，找到自己的 account_id，替换皮肤
            string pattern = @"function DesktopMgr:InitRoom\((\w+),(\w+),(\w+),(\w+),(\w+),(\w+),(\w+),(\w+),(\w+)\)";
            // 修复：使用本地 main_character_id 和持久化皮肤选择
            string replacement = @"function DesktopMgr:InitRoom($1,$2,$3,$4,$5,$6,$7,$8,$9)" +
                @"pcall(function()if $2 and GameMgr.Inst and GameMgr.Inst.characterInfo then " +
                @"local cfg=MajSoulHelper_FakeConfig or{};" +
                @"local mainCharId=GameMgr.Inst.characterInfo.main_character_id;" +
                // 优先从持久化皮肤选择中获取
                @"local mySkin=MajSoulHelper_CharacterSkins and MajSoulHelper_CharacterSkins[mainCharId];" +
                @"local myViews=nil;" +
                @"if not mySkin then for _,c in ipairs(GameMgr.Inst.characterInfo.characters)do if c.charid==mainCharId then mySkin=c.skin;myViews=c.views;break end end end;" +
                @"pcall(function()if not mainCharId or mainCharId==0 then mainCharId=CS.MajSoulHelper.CharacterDataCache.GetMainCharacterId() end end);" +
                @"pcall(function()if(not mySkin or mySkin==0)and mainCharId and mainCharId>0 then mySkin=CS.MajSoulHelper.CharacterDataCache.GetCharacterSkin(mainCharId) end end);" +
                @"pcall(function()if(not mySkin or mySkin==0)and mainCharId and mainCharId>0 then mySkin=CS.MajSoulHelper.CharacterDataCache.GetDefaultSkinId(mainCharId) end end);" +
                @"if(not mainCharId or mainCharId==0)then mainCharId=200001 end;" +
                @"if(not mySkin or mySkin==0)and mainCharId and mainCharId>0 then mySkin=mainCharId+200000 end;" +
                // 应用固定伪造配置
                @"if cfg.enabled and cfg.charId and cfg.charId>0 then mainCharId=cfg.charId;mySkin=cfg.skinId or mySkin;myViews=cfg.views or myViews;end;" +
                // 从本地配置获取装扮
                @"local lc=MajSoulHelper_LocalConfig or{};" +
                @"if lc.views and lc.views_index then myViews=lc.views[lc.views_index]end;" +
                @"local myTitle=lc.title or 0;" +
                @"if cfg.titleId and cfg.titleId>0 then myTitle=cfg.titleId end;" +
                @"pcall(function()if not myTitle or myTitle==0 then myTitle=CS.MajSoulHelper.CharacterDataCache.GetSelectedTitle() end end);" +
                @"if mySkin then " +
                @"for i=1,#$2 do local p=$2[i];" +
                @"if p.account_id==$3 and p.character then " +
                @"p.character.charid=mainCharId;p.character.skin=mySkin;p.avatar_id=mySkin;" +
                @"p.character.level=5;p.character.exp=0;p.character.is_upgraded=true;" +
                @"if myViews then p.views={};for _,v in ipairs(myViews)do table.insert(p.views,{slot=v.slot,item_id=v.item_id or 0})end end;" +
                @"if myTitle and myTitle>0 then p.title=myTitle end;" +
                @"break end end end end end);";

            if (Regex.IsMatch(luaCode, pattern))
            {
                luaCode = Regex.Replace(luaCode, pattern, replacement);
            }

            return luaCode;
        }

        /// <summary>
        /// 修改友人房UI - UI_FriendRoom
        /// 确保房间内正确显示本地皮肤
        /// 核心：修改 UpdateData 和 _refreshPlayerInfo 函数
        /// 添加对 views（装扮）的支持
        /// </summary>
        private static string PatchFriendRoomUI(string luaCode)
        {
            // ======== 1. 修改 UpdateData 函数 ========
            // 在处理 room.persons 数据时，替换自己的皮肤和装扮
            // 原始格式: function UI_FriendRoom:UpdateData(H)
            string updateDataPattern = @"function UI_FriendRoom:UpdateData\((\w+)\)";
            string updateDataReplacement = @"function UI_FriendRoom:UpdateData($1)" +
                @"pcall(function()if $1 and $1.persons and GameMgr.Inst then " +
                @"local myId=GameMgr.Inst.account_id;local cfg=MajSoulHelper_FakeConfig or{};" +
                @"local mainCharId=GameMgr.Inst.characterInfo.main_character_id;" +
                @"if cfg.enabled and cfg.charId and cfg.charId>0 then mainCharId=cfg.charId end;" +
                @"local mySkin=cfg.skinId or 0;local myViews=cfg.views;" +
                @"if not mySkin or mySkin==0 then for _,c in ipairs(GameMgr.Inst.characterInfo.characters or{})do if c.charid==mainCharId then mySkin=c.skin;if not myViews then myViews=c.views end;break end end end;" +
                @"pcall(function()if not mainCharId or mainCharId==0 then mainCharId=CS.MajSoulHelper.CharacterDataCache.GetMainCharacterId() end end);" +
                @"pcall(function()if(not mySkin or mySkin==0)and mainCharId and mainCharId>0 then mySkin=CS.MajSoulHelper.CharacterDataCache.GetCharacterSkin(mainCharId) end end);" +
                @"pcall(function()if(not mySkin or mySkin==0)and mainCharId and mainCharId>0 then mySkin=CS.MajSoulHelper.CharacterDataCache.GetDefaultSkinId(mainCharId) end end);" +
                @"if(not mainCharId or mainCharId==0)then mainCharId=200001 end;" +
                @"if(not mySkin or mySkin==0)and mainCharId and mainCharId>0 then mySkin=mainCharId+200000 end;" +
                @"if mySkin and mySkin>0 then for i,p in ipairs($1.persons)do if p.account_id==myId then " +
                @"p.avatar_id=mySkin;" +
                @"if p.character then p.character.skin=mySkin;p.character.charid=mainCharId;p.character.level=5;p.character.is_upgraded=true;" +
                @"if myViews then p.character.views=myViews end end;" +
                @"if cfg.titleId and cfg.titleId>0 then p.title=cfg.titleId end;" +
                @"break end end end end end);";

            if (Regex.IsMatch(luaCode, updateDataPattern))
            {
                luaCode = Regex.Replace(luaCode, updateDataPattern, updateDataReplacement);
            }

            // ======== 2. 修改 OnPlayerChange 函数 ========
            // 在处理 NotifyRoomPlayerUpdate 时，替换自己的皮肤和装扮
            // 原始格式: function UI_FriendRoom:OnPlayerChange(i)
            string onPlayerChangePattern = @"function UI_FriendRoom:OnPlayerChange\((\w+)\)";
            string onPlayerChangeReplacement = @"function UI_FriendRoom:OnPlayerChange($1)" +
                @"pcall(function()if $1 and $1.player_list and GameMgr.Inst then " +
                @"local myId=GameMgr.Inst.account_id;local cfg=MajSoulHelper_FakeConfig or{};" +
                @"local mainCharId=GameMgr.Inst.characterInfo.main_character_id;" +
                @"if cfg.enabled and cfg.charId and cfg.charId>0 then mainCharId=cfg.charId end;" +
                @"local mySkin=cfg.skinId or 0;local myViews=cfg.views;" +
                @"if not mySkin or mySkin==0 then for _,c in ipairs(GameMgr.Inst.characterInfo.characters or{})do if c.charid==mainCharId then mySkin=c.skin;if not myViews then myViews=c.views end;break end end end;" +
                @"pcall(function()if not mainCharId or mainCharId==0 then mainCharId=CS.MajSoulHelper.CharacterDataCache.GetMainCharacterId() end end);" +
                @"pcall(function()if(not mySkin or mySkin==0)and mainCharId and mainCharId>0 then mySkin=CS.MajSoulHelper.CharacterDataCache.GetCharacterSkin(mainCharId) end end);" +
                @"pcall(function()if(not mySkin or mySkin==0)and mainCharId and mainCharId>0 then mySkin=CS.MajSoulHelper.CharacterDataCache.GetDefaultSkinId(mainCharId) end end);" +
                @"if(not mainCharId or mainCharId==0)then mainCharId=200001 end;" +
                @"if(not mySkin or mySkin==0)and mainCharId and mainCharId>0 then mySkin=mainCharId+200000 end;" +
                @"if mySkin and mySkin>0 then for i,p in ipairs($1.player_list)do if p.account_id==myId then " +
                @"p.avatar_id=mySkin;" +
                @"if p.character then p.character.skin=mySkin;p.character.charid=mainCharId;p.character.level=5;p.character.is_upgraded=true;" +
                @"if myViews then p.character.views=myViews end end;" +
                @"if cfg.titleId and cfg.titleId>0 then p.title=cfg.titleId end;" +
                @"break end end end end end);";

            if (Regex.IsMatch(luaCode, onPlayerChangePattern))
            {
                luaCode = Regex.Replace(luaCode, onPlayerChangePattern, onPlayerChangeReplacement);
            }

            // ======== 3. 修改 _refreshPlayerInfo 函数 ========
            // 在刷新玩家信息时，确保使用本地皮肤
            // 原始格式: function UI_FriendRoom:_refreshPlayerInfo(a9)
            string refreshPattern = @"function UI_FriendRoom:_refreshPlayerInfo\((\w+)\)";
            string refreshReplacement = @"function UI_FriendRoom:_refreshPlayerInfo($1)" +
                @"pcall(function()local H=self.players and self.players[$1];" +
                @"if H and H.account_id==GameMgr.Inst.account_id then " +
                @"local cfg=MajSoulHelper_FakeConfig or{};" +
                @"local mainCharId=GameMgr.Inst.characterInfo.main_character_id;" +
                @"if cfg.enabled and cfg.charId and cfg.charId>0 then mainCharId=cfg.charId end;" +
                @"local skin=cfg.skinId or 0;local views=cfg.views;" +
                @"if not skin or skin==0 then for _,c in ipairs(GameMgr.Inst.characterInfo.characters or{})do if c.charid==mainCharId then skin=c.skin;if not views then views=c.views end;break end end end;" +
                @"pcall(function()if not mainCharId or mainCharId==0 then mainCharId=CS.MajSoulHelper.CharacterDataCache.GetMainCharacterId() end end);" +
                @"pcall(function()if(not skin or skin==0)and mainCharId and mainCharId>0 then skin=CS.MajSoulHelper.CharacterDataCache.GetCharacterSkin(mainCharId) end end);" +
                @"pcall(function()if(not skin or skin==0)and mainCharId and mainCharId>0 then skin=CS.MajSoulHelper.CharacterDataCache.GetDefaultSkinId(mainCharId) end end);" +
                @"if(not mainCharId or mainCharId==0)then mainCharId=200001 end;" +
                @"if(not skin or skin==0)and mainCharId and mainCharId>0 then skin=mainCharId+200000 end;" +
                @"H.avatar_id=skin;" +
                @"if H.character then H.character.skin=skin;H.character.charid=mainCharId;" +
                @"if views then H.character.views=views end end;" +
                @"end end);";

            if (Regex.IsMatch(luaCode, refreshPattern))
            {
                luaCode = Regex.Replace(luaCode, refreshPattern, refreshReplacement);
            }

            return luaCode;
        }

        /// <summary>
        /// 尝试加载外部补丁文件
        /// </summary>
        private static bool TryLoadExternalPatch(string moduleName, out byte[] patchData)
        {
            patchData = null;

            string patchFile = Path.Combine(_patchDir, moduleName.TrimStart('@') + ".lua");
            if (File.Exists(patchFile))
            {
                try
                {
                    patchData = File.ReadAllBytes(patchFile);
                    return true;
                }
                catch (Exception ex)
                {
                    Utils.MyLogger(BepInEx.Logging.LogLevel.Error, 
                        $"[SkinUnlocker] Failed to load external patch {patchFile}: {ex.Message}");
                }
            }

            return false;
        }

        /// <summary>
        /// 生成默认补丁文件
        /// </summary>
        private static void GenerateDefaultPatches()
        {
            // 生成配置说明文件
            string configPath = Path.Combine(_patchDir, "README.txt");
            if (!File.Exists(configPath))
            {
                string readme = @"=== MajSoulHelper 皮肤解锁补丁目录 ===

本目录用于存放自定义Lua补丁文件。

文件命名规则:
- GameUtility.lua      -> 替换 @GameUtility 模块
- GameMgr.lua          -> 替换 @GameMgr 模块
- UI_UI_Bag_SkinCell.lua -> 替换 @UI_UI_Bag_SkinCell 模块

工作原理:
1. 插件会在 luaL_loadbuffer 时拦截Lua代码加载
2. 如果是源码文本，会直接修改代码
3. 如果是字节码，会尝试加载此目录下的同名.lua文件替换

注意事项:
- 补丁文件必须是有效的Lua源码
- 修改后的代码必须与原版本兼容
- 建议先备份原始文件再进行修改

修改映射关系:
@GameUtility       -> item_owned 函数修改，使皮肤/角色始终返回拥有
@GameMgr           -> have_character 修改 + skin_map 全量注入
@UI_UI_Bag_SkinCell -> 隐藏锁定图标
@UI_UI_Bag         -> 显示所有皮肤
@UI_UI_Character_Skin -> 角色皮肤选择页面
@UI_UI_LiaoSheChangeSkin -> 寮舍换肤页面
@UI_UI_RoleSet     -> 角色设置页面

配置选项 (在 PluginConfig.cs 中):
- EnableSkinUnlock: 启用皮肤解锁
- EnableCharacterUnlock: 启用角色解锁
- HideLockUI: 隐藏锁定图标
- EnableInGameSkinReplace: 对局中使用解锁皮肤
- EnableDebugLog: 启用调试日志
";
                File.WriteAllText(configPath, readme, Encoding.UTF8);
            }

            // 生成映射配置JSON
            string mappingPath = Path.Combine(_patchDir, "patch_mapping.json");
            if (!File.Exists(mappingPath))
            {
                string mapping = @"{
    ""description"": ""Lua模块补丁映射配置"",
    ""patches"": [
        {
            ""module"": ""@GameUtility"",
            ""type"": ""ItemOwned"",
            ""description"": ""修改item_owned函数，使皮肤和角色始终返回拥有状态"",
            ""originalFile"": ""leak/Pre_@GameUtility"",
            ""functions"": [""GameUtility.item_owned""]
        },
        {
            ""module"": ""@GameMgr"",
            ""type"": ""CharacterInfo"",
            ""description"": ""修改have_character函数和skin_map初始化"",
            ""originalFile"": ""leak/Pre_@GameMgr"",
            ""functions"": [""GameMgr:have_character"", ""GameMgr:makeCharacterInfo""]
        },
        {
            ""module"": ""@UI_UI_Bag_SkinCell"",
            ""type"": ""SkinCellUI"",
            ""description"": ""隐藏皮肤锁定图标"",
            ""originalFile"": ""leak/Pre_@UI_UI_Bag_SkinCell"",
            ""functions"": [""UI_Bag_SkinCell:Show""]
        }
    ],
    ""notes"": [
        ""所有修改仅本地生效"",
        ""不会发送网络请求"",
        ""其他玩家看到的仍是默认皮肤""
    ]
}";
                File.WriteAllText(mappingPath, mapping, Encoding.UTF8);
            }
        }

        /// <summary>
        /// 修改 LogTool 模块 - 屏蔽皮肤相关日志上报
        /// 确保不会将伪造的皮肤数据通过日志发送到服务器
        /// 注意: 代码是压缩格式，没有空格和换行
        /// </summary>
        private static string PatchLogTool(string luaCode)
        {
            // 压缩格式: function LogTool.Info(f,g,...)local h=": [Info] ("
            // 在每个日志函数开头注入过滤逻辑
            
            // 处理 LogTool.Info - 压缩格式匹配
            // 屏蔽时先输出被丢弃的内容到本地控制台，再返回
            luaCode = luaCode.Replace(
                "function LogTool.Info(f,g,...)local h=",
                @"function LogTool.Info(f,g,...)if f and(string.find(tostring(f),'MajSoulHelper')or string.find(tostring(f),'skin'))then print('[MajSoulHelper屏蔽Info]'..tostring(f)..':'..tostring(g));return end;if g and(string.find(tostring(g),'MajSoulHelper')or string.find(tostring(g),'skin_map'))then print('[MajSoulHelper屏蔽Info]'..tostring(f)..':'..tostring(g));return end;local h=");
            
            // 处理 LogTool.Warning - 压缩格式匹配
            luaCode = luaCode.Replace(
                "function LogTool.Warning(f,g,...)local h=",
                @"function LogTool.Warning(f,g,...)if f and(string.find(tostring(f),'MajSoulHelper')or string.find(tostring(f),'skin'))then print('[MajSoulHelper屏蔽Warning]'..tostring(f)..':'..tostring(g));return end;if g and(string.find(tostring(g),'MajSoulHelper')or string.find(tostring(g),'skin_map'))then print('[MajSoulHelper屏蔽Warning]'..tostring(f)..':'..tostring(g));return end;local h=");
            
            // 处理 LogTool.Error - 压缩格式匹配
            luaCode = luaCode.Replace(
                "function LogTool.Error(f,g,...)local j=",
                @"function LogTool.Error(f,g,...)if f and(string.find(tostring(f),'MajSoulHelper')or string.find(tostring(f),'skin'))then print('[MajSoulHelper屏蔽Error]'..tostring(f)..':'..tostring(g));return end;if g and(string.find(tostring(g),'MajSoulHelper')or string.find(tostring(g),'skin_map'))then print('[MajSoulHelper屏蔽Error]'..tostring(f)..':'..tostring(g));return end;local j=");
            
            // 处理 LogTool.Debug - 压缩格式匹配
            luaCode = luaCode.Replace(
                "function LogTool.Debug(f,g,...)local h=",
                @"function LogTool.Debug(f,g,...)if f and(string.find(tostring(f),'MajSoulHelper')or string.find(tostring(f),'skin'))then print('[MajSoulHelper屏蔽Debug]'..tostring(f)..':'..tostring(g));return end;if g and(string.find(tostring(g),'MajSoulHelper')or string.find(tostring(g),'skin_map'))then print('[MajSoulHelper屏蔽Debug]'..tostring(f)..':'..tostring(g));return end;local h=");

            // 处理 LogTool.InfoNet - 可能也包含敏感日志
            luaCode = luaCode.Replace(
                "function LogTool.InfoNet(f,g,...)local h=",
                @"function LogTool.InfoNet(f,g,...)if f and(string.find(tostring(f),'MajSoulHelper')or string.find(tostring(f),'skin'))then print('[MajSoulHelper屏蔽InfoNet]'..tostring(f)..':'..tostring(g));return end;if g and(string.find(tostring(g),'MajSoulHelper')or string.find(tostring(g),'skin_map'))then print('[MajSoulHelper屏蔽InfoNet]'..tostring(f)..':'..tostring(g));return end;local h=");

            // 处理 LogTool.WarningNet - 网络日志
            luaCode = luaCode.Replace(
                "function LogTool.WarningNet(f,g,...)local h=",
                @"function LogTool.WarningNet(f,g,...)if f and(string.find(tostring(f),'MajSoulHelper')or string.find(tostring(f),'skin'))then print('[MajSoulHelper屏蔽WarningNet]'..tostring(f)..':'..tostring(g));return end;if g and(string.find(tostring(g),'MajSoulHelper')or string.find(tostring(g),'skin_map'))then print('[MajSoulHelper屏蔽WarningNet]'..tostring(f)..':'..tostring(g));return end;local h=");

            // 处理 LogTool.ErrorNet - 网络错误日志
            luaCode = luaCode.Replace(
                "function LogTool.ErrorNet(f,g,...)local j=",
                @"function LogTool.ErrorNet(f,g,...)if f and(string.find(tostring(f),'MajSoulHelper')or string.find(tostring(f),'skin'))then print('[MajSoulHelper屏蔽ErrorNet]'..tostring(f)..':'..tostring(g));return end;if g and(string.find(tostring(g),'MajSoulHelper')or string.find(tostring(g),'skin_map'))then print('[MajSoulHelper屏蔽ErrorNet]'..tostring(f)..':'..tostring(g));return end;local j=");
            
            // 屏蔽 _insertCacheLog 中的敏感日志 - 压缩格式
            luaCode = luaCode.Replace(
                "function LogTool._insertCacheLog(b)if#LogTool",
                @"function LogTool._insertCacheLog(b)if b and(string.find(tostring(b),'MajSoulHelper')or string.find(tostring(b),'skin'))then print('[MajSoulHelper屏蔽CacheLog]'..tostring(b));return end;if#LogTool");

            return luaCode;
        }

        /// <summary>
        /// 修改 LogStoreUtility 模块 - 屏蔽错误上报
        /// 避免将皮肤相关错误发送到服务器
        /// 注意: 代码是压缩格式，格式为: function LogStoreUtility.HandleInfo(a,b,c)if LangMgr...
        /// </summary>
        private static string PatchLogStoreUtility(string luaCode)
        {
            // 压缩格式: function LogStoreUtility.HandleInfo(a,b,c)if LangMgr.is_yostar_client then return end;
            // 在函数开头添加皮肤数据过滤，并输出被屏蔽的内容
            luaCode = luaCode.Replace(
                "function LogStoreUtility.HandleInfo(a,b,c)if LangMgr.is_yostar_client then return end;",
                @"function LogStoreUtility.HandleInfo(a,b,c)if a and(string.find(tostring(a),'skin')or string.find(tostring(a),'character')or string.find(tostring(a),'MajSoulHelper'))then print('[MajSoulHelper屏蔽LogStore]'..tostring(a));return end;if LangMgr.is_yostar_client then return end;");

            return luaCode;
        }

        /// <summary>
        /// 修改 UI_ErrorInfo 模块 - 屏蔽皮肤相关错误上报
        /// 避免将任何皮肤相关信息发送到服务器
        /// 注意: 代码是压缩格式
        /// </summary>
        private static string PatchErrorInfoUI(string luaCode)
        {
            // 压缩格式: function UI_ErrorInfo.HandleInfo(e,log_category,p)local i={client_type='app',...
            // 在函数开头添加皮肤数据过滤，并输出被屏蔽的内容
            luaCode = luaCode.Replace(
                "function UI_ErrorInfo.HandleInfo(e,log_category,p)local i={",
                @"function UI_ErrorInfo.HandleInfo(e,log_category,p)if e and(string.find(tostring(e),'skin')or string.find(tostring(e),'character')or string.find(tostring(e),'MajSoulHelper')or string.find(tostring(e),'avatar'))then print('[MajSoulHelper屏蔽ErrorInfo]'..tostring(e)..':'..tostring(log_category));return end;local i={");

            // 压缩格式: function UI_ErrorInfo.HandleError(e,f,g,h)if(g:ToInt()==0 or
            // 过滤皮肤相关错误，并输出被屏蔽的内容
            luaCode = luaCode.Replace(
                "function UI_ErrorInfo.HandleError(e,f,g,h)if(g:ToInt()==0 or",
                @"function UI_ErrorInfo.HandleError(e,f,g,h)if e and(string.find(tostring(e),'skin')or string.find(tostring(e),'character')or string.find(tostring(e),'MajSoulHelper'))then print('[MajSoulHelper屏蔽HandleError]'..tostring(e)..':'..tostring(f));return end;if(g:ToInt()==0 or");

            return luaCode;
        }

        /// <summary>
        /// 恢复原始数据（用于补丁卸载时）
        /// 生成恢复原始服务器数据的Lua代码
        /// </summary>
        public static string GenerateRestoreCode()
        {
            return @"
-- [MajSoulHelper] 恢复原始服务器数据
pcall(function()
    if MajSoulHelper_OriginalData and MajSoulHelper_OriginalData.initialized then
        -- 恢复原始 skin_map
        if GameMgr and GameMgr.Inst then
            GameMgr.Inst.skin_map = {}
            for skinId, _ in pairs(MajSoulHelper_OriginalData.skin_map) do
                GameMgr.Inst.skin_map[skinId] = 1
            end
            
            -- 恢复每个角色的原始皮肤
            if GameMgr.Inst.characterInfo and GameMgr.Inst.characterInfo.characters then
                for i, char in ipairs(GameMgr.Inst.characterInfo.characters) do
                    if MajSoulHelper_OriginalData.character_skins[char.charid] then
                        GameMgr.Inst.characterInfo.characters[i].skin = MajSoulHelper_OriginalData.character_skins[char.charid]
                    end
                end
            end
            
            -- 恢复主角色
            if MajSoulHelper_OriginalData.main_character_id then
                GameMgr.Inst.characterInfo.main_character_id = MajSoulHelper_OriginalData.main_character_id
            end
        end
        
        -- 清理全局数据
        MajSoulHelper_OriginalData = nil
        MajSoulHelper_LocalSkinData = nil
    end
end)
";
        }

        /// <summary>
        /// 获取需要屏蔽的日志关键词列表
        /// </summary>
        private static readonly string[] BlockedLogKeywords = new string[]
        {
            "MajSoulHelper",
            "skin_map",
            "character_skins",
            "item_owned",
            "changeCharacterSkin",
            "changeMainCharacter",
            "setRandomCharacter",
            "本地解锁",
            "注入皮肤"
        };
    }
}
