# MajSoulHelper Lua 补丁目录

本目录包含 Lua 补丁的参考实现和示例代码。

## 📁 文件说明

| 文件 | 用途 | 对应补丁类型 |
|------|------|--------------|
| [config.json](config.json) | 配置文件模板 | - |
| [GameUtility.lua](GameUtility.lua) | 物品拥有检查 | ItemOwned |
| [GameMgr.lua](GameMgr.lua) | 角色/皮肤数据注入 | CharacterInfo |
| [UI_UI_Bag_SkinCell.lua](UI_UI_Bag_SkinCell.lua) | 皮肤单元格UI | SkinCellUI |
| [LobbyNetMgr.lua](LobbyNetMgr.lua) | 网络请求拦截 | LobbyNetMgr |
| [DesktopMgr.lua](DesktopMgr.lua) | 对局皮肤替换 | DesktopMgr |
| [Tools.lua](Tools.lua) | 语音解锁 | ToolsModule |
| [LogTool.lua](LogTool.lua) | 日志阻止 | LogTool |

## 🔧 使用方式

### 1. 作为参考
这些文件主要作为补丁逻辑的参考文档，帮助理解插件是如何修改游戏代码的。

### 2. 外部补丁（高级）
如果需要自定义补丁逻辑，可以：
1. 复制对应的 `.lua` 文件
2. 修改其中的代码
3. 将修改后的文件放在此目录
4. 插件会优先使用外部补丁文件

## 📋 补丁类型映射

```
模块名称                    补丁类型            说明
──────────────────────────────────────────────────────────────
@GameUtility               ItemOwned           item_owned() 返回 true
@GameMgr                   CharacterInfo       角色/皮肤数据注入
@UI_UI_Bag_SkinCell        SkinCellUI          隐藏锁定图标
@UI_UI_Bag                 BagUI               背包皮肤显示
@UI_UI_Character_Skin      CharacterSkinUI     角色皮肤选择
@UI_UI_LiaoSheChangeSkin   ChangeSkinUI        寮舍换肤
@UI_UI_RoleSet             RoleSetUI           角色设置
@UI_UI_Skin_Yulan          SkinPreviewUI       皮肤预览
@UI_UI_SkinShop_Yulan      SkinShopUI          皮肤商店
@UI_UI_LiaosheMain         LiaosheMainUI       寮舍主界面
@UI_UI_LiaosheSelect       LiaosheSelectUI     寮舍选择
@UI_UI_Visit               VisitUI             拜访界面
@Tools                     ToolsModule         语音解锁
@LobbyNetMgr               LobbyNetMgr         请求拦截
@DesktopMgr                DesktopMgr          对局皮肤替换
@App_LogTool               LogTool             日志阻止
@LogStoreUtility           LogStoreUtility     错误上报阻止
@UI_UI_ErrorInfo           ErrorInfoUI         错误UI阻止
```

## 🛠️ 开发说明

### 补丁实现方式

插件使用两种方式应用补丁：

#### 1. 正则替换（主要方式）
```csharp
// C# 代码
string pattern = @"function GameUtility\.item_owned\(c\)";
string replacement = @"function GameUtility.item_owned(c)
-- [MajSoulHelper] 本地解锁
if GameUtility.get_id_type(c) == GameUtility.EIDType.skin then
    return true
end";
luaCode = Regex.Replace(luaCode, pattern, replacement);
```

#### 2. 代码注入（复杂场景）
```csharp
// 在特定位置插入代码
string injectionPoint = "self.characterInfo = t";
string injection = @"
-- [MajSoulHelper] 注入代码
pcall(function()
    -- 注入逻辑...
end)
";
luaCode = luaCode.Replace(injectionPoint, injectionPoint + injection);
```

### 添加新补丁

1. 在 `PluginConfig.cs` 的 `LuaPatchType` 枚举中添加类型
2. 在 `LuaPatchMapping` 中添加模块名映射
3. 在 `SkinUnlocker.cs` 的 `ApplyPatch()` 中添加 case
4. 实现 `PatchXxx()` 方法
5. 可选：在此目录添加参考实现文件

## ⚠️ 注意事项

- 游戏更新后补丁可能失效，需要重新适配
- 从 `BepInEx/leak/Pre_@xxx` 获取最新的反编译代码
- 测试时启用 `EnableDebugLog` 查看详细日志
- 所有修改仅本地生效，不影响服务器数据
