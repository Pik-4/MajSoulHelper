# MajSoulHelper 技术文档

## 架构概述

```
┌─────────────────────────────────────────────────────────────┐
│                      MajSoulHelper                          │
├─────────────────────────────────────────────────────────────┤
│  Main.cs          - BepInEx 插件入口                        │
│  Patcher.cs       - Harmony Hook 管理                       │
│  SkinUnlocker.cs  - Lua 代码补丁逻辑                        │
│  WebServer.cs     - HTTP 服务器和 REST API                  │
│  ConfigPersistence.cs - 配置持久化                          │
│  PluginConfig.cs  - 静态配置定义                            │
├─────────────────────────────────────────────────────────────┤
│                    Harmony Hooks                            │
│  luaL_loadbuffer  → PatchSteam.luaL_loadbuffer_Prefix      │
│  tolua_loadbuffer → PatchSteam.tolua_loadbuffer_Prefix     │
└─────────────────────────────────────────────────────────────┘
```

---

## 工作原理

### 1. Hook 机制

游戏使用 Lua 作为脚本语言，通过 Hook `luaL_loadbuffer` 函数拦截 Lua 代码加载：

```
游戏加载Lua → Hook拦截 → 检测模块名 → 应用补丁 → 返回修改后代码
```

### 2. 模块匹配

在 `PluginConfig.LuaPatchMapping` 中定义模块名与补丁类型的映射：

```csharp
{ "@GameUtility", LuaPatchType.ItemOwned },
{ "@GameMgr", LuaPatchType.CharacterInfo },
{ "@LobbyNetMgr", LuaPatchType.LobbyNetMgr },
// ...
```

### 3. 补丁应用

`SkinUnlocker.TryPatchLua()` 根据补丁类型调用对应的修改函数：

```csharp
switch (patchType)
{
    case LuaPatchType.ItemOwned:
        return PatchItemOwned(luaCode);
    case LuaPatchType.CharacterInfo:
        return PatchCharacterInfo(luaCode);
    // ...
}
```

---

## 补丁映射表

完整的补丁类型和对应模块见 [patches/README.md](../patches/README.md)。

### 核心补丁

| 模块 | 补丁类型 | 修改内容 |
|------|----------|----------|
| @GameUtility | ItemOwned | item_owned() 返回 true |
| @GameMgr | CharacterInfo | have_character(), skin_map, 角色注入 |
| @LobbyNetMgr | LobbyNetMgr | SendRequest() 请求拦截 |
| @DesktopMgr | DesktopMgr | InitRoom() 皮肤替换 |
| @Tools | ToolsModule | get_chara_audio() 语音解锁 |

### UI 补丁

| 模块 | 补丁类型 | 修改内容 |
|------|----------|----------|
| @UI_UI_Bag_SkinCell | SkinCellUI | 隐藏锁定图标 |
| @UI_UI_Character_Skin | CharacterSkinUI | Spine 动画加载 |
| @UI_UI_LiaoSheChangeSkin | ChangeSkinUI | 寮舍换肤解锁 |
| @UI_UI_Skin_Yulan | SkinPreviewUI | 皮肤预览 |

### 日志拦截

| 模块 | 补丁类型 | 修改内容 |
|------|----------|----------|
| @App_LogTool | LogTool | 日志上传阻止 |
| @LogStoreUtility | LogStoreUtility | 错误上报阻止 |
| @UI_UI_ErrorInfo | ErrorInfoUI | 错误UI上报阻止 |

---

## 补丁实现细节

### PatchItemOwned

修改 `GameUtility.item_owned()` 函数，对皮肤和角色类型返回 true：

```lua
function GameUtility.item_owned(c)
    local d = GameUtility.get_id_type(c)
    -- [MajSoulHelper] 皮肤和角色始终返回拥有
    if d == GameUtility.EIDType.skin or d == GameUtility.EIDType.character then
        return true
    end
    -- 原始逻辑...
end
```

### PatchCharacterInfo

1. **have_character()**: 始终返回 true
2. **skin_map 注入**: 从 ExcelMgr 读取所有皮肤 ID 并注入
3. **角色数据注入**: 创建未拥有角色的数据，包含完整字段

```lua
-- 注入所有皮肤
local allSkins = ExcelMgr.GetTable('item_definition', 'skin')
for skinId, _ in pairs(allSkins) do
    self.skin_map[skinId] = 1
end

-- 注入角色数据
local newChar = {
    charid = charId,
    level = 5,
    skin = charData.init_skin,
    is_upgraded = true,
    rewarded_level = {1, 2, 3, 4, 5},
    extra_emoji = {},
    finished_endings = {}
}
```

### PatchLobbyNetMgr

拦截 `LobbyNetMgr.SendRequest()` 函数，对特定请求进行本地处理：

```lua
function LobbyNetMgr.SendRequest(l, m, n, o)
    if m == 'changeCharacterSkin' then
        -- 本地更新数据
        GameMgr.Inst.characterInfo.characters[i].skin = n.skin
        -- 模拟成功响应
        o(nil, {error = {code = 0}})
        return  -- 不发送到服务器
    end
    -- 原始逻辑...
end
```

### PatchToolsModule

解锁语音，移除等级和羁绊检查：

```lua
function Tools.get_chara_audio(f0, f1, f2, f3, f4)
    -- [MajSoulHelper] 解锁全部语音
    -- 跳过等级检查、跳过羁绊检查
    return originalAudioList
end
```

---

## 数据结构

### MajSoulHelper_OriginalData

存储服务器原始数据：

```lua
MajSoulHelper_OriginalData = {
    skin_map = {},           -- 原始皮肤映射
    character_skins = {},    -- 每个角色的原始皮肤
    main_character_id = nil, -- 原始主角色ID
    initialized = false,
    pending_server_sync = false
}
```

### MajSoulHelper_LocalSkinData

存储本地选择数据：

```lua
MajSoulHelper_LocalSkinData = {
    skin_map = {},           -- 本地选择的皮肤 [characterId] = skinId
    main_character_id = nil,
    avatar_id = nil,
    title = nil,
    common_views = {}
}
```

---

## WebServer 实现

### 架构

使用 `TcpListener` 实现轻量级 HTTP 服务器（避免 HttpListener 兼容性问题）：

```csharp
private static TcpListener _listener;
_listener = new TcpListener(IPAddress.Loopback, port);
```

### 请求处理流程

```
TcpClient → 读取HTTP请求 → 解析请求行和头 → 路由分发 → 生成响应 → 发送
```

### API 路由

```csharp
switch (path)
{
    case "/":
    case "/index.html":
        return WebUI.GetIndexHtml();
    case "/api/config":
        return method == "GET" ? HandleGetConfig() : HandlePostConfig(body);
    case "/api/status":
        return HandleGetStatus();
    case "/api/cache/clear":
        return HandleClearCache();
    case "/api/save":
        return HandleSaveConfig();
}
```

---

## 配置持久化

### 文件格式

JSON 格式，位置：`BepInEx/config/MajSoulHelper.json`

```json
{
  "EnableSkinUnlock": true,
  "EnableCharacterUnlock": true,
  "EnableVoiceUnlock": true,
  "HideLockUI": true,
  "EnableInGameSkinReplace": true,
  "EnableDebugLog": false,
  "WebServerPort": 23333
}
```

### 同步机制

```
ConfigPersistence.Config ←→ PluginConfig (静态类)
         ↓
    MajSoulHelper.json
```

- `ApplyToPluginConfig()`: 加载时同步到静态类
- `SyncFromPluginConfig()`: 保存前从静态类同步

---

## 缓存机制

### 补丁缓存

```csharp
private static Dictionary<string, byte[]> _patchedLuaCache;
```

- Key: 模块名称
- Value: 修改后的 Lua 字节数据
- 避免重复处理相同模块

### 缓存管理

```csharp
// 获取缓存数量
public static int GetCachedPatchCount()

// 清除缓存
public static void ClearCache()
```

---

## 调试

### 日志级别

- `LogLevel.Warning`: 重要信息（补丁应用、服务器启动等）
- `LogLevel.Debug`: 调试信息（启用 EnableDebugLog 时）
- `LogLevel.Error`: 错误信息

### 调试建议

1. 启用 `EnableDebugLog` 查看详细日志
2. 启用 `EnableBlockedLogDisplay` 查看被拦截的请求
3. 使用 Web 控制面板监控状态
4. 检查 BepInEx 控制台输出

---

## 扩展开发

### 添加新补丁

1. 在 `LuaPatchType` 枚举中添加新类型
2. 在 `LuaPatchMapping` 中添加模块映射
3. 在 `ApplyPatch()` 中添加 case 分支
4. 实现 `PatchXxx()` 方法
5. 在 `patches/` 目录添加参考实现文件

### 添加新 API

1. 在 `HandleRequest()` 的 switch 中添加路由
2. 实现 `HandleXxx()` 方法
3. 更新 WebUI HTML（如需要）

---

## patches 目录

`patches/` 目录包含 Lua 补丁的参考实现：

| 文件 | 补丁类型 | 说明 |
|------|----------|------|
| GameUtility.lua | ItemOwned | item_owned() 返回 true |
| GameMgr.lua | CharacterInfo | 角色/皮肤数据注入 |
| LobbyNetMgr.lua | LobbyNetMgr | 网络请求拦截 |
| DesktopMgr.lua | DesktopMgr | 对局皮肤替换 |
| Tools.lua | ToolsModule | 语音解锁 |
| LogTool.lua | LogTool | 日志上传阻止 |
| UI_UI_Bag_SkinCell.lua | SkinCellUI | 隐藏锁定图标 |

详细说明见 [patches/README.md](../patches/README.md)。

---

## leak 目录

`leak/` 目录包含从游戏中反编译的 Lua 代码，用于参考：

- `Pre_@GameUtility` - GameUtility 模块
- `Pre_@GameMgr` - GameMgr 模块
- `Pre_@LobbyNetMgr` - 网络请求模块
- `Pre_@Excels_Data_*` - Excel 数据表
- ...

这些文件帮助理解游戏代码结构，用于开发和维护补丁。
