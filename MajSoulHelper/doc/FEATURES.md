# MajSoulHelper 功能详解

## 🔓 解锁功能

### 1. 皮肤解锁

**原理**：通过修改 `GameUtility.item_owned()` 函数和 `GameMgr.skin_map` 表

**效果**：
- 所有皮肤在列表中显示为"已拥有"
- 可以选择并使用任意皮肤
- 皮肤预览正常显示 Spine 动画

**涉及模块**：
- `@GameUtility` - item_owned() 返回 true
- `@GameMgr` - skin_map 注入所有皮肤 ID
- `@UI_UI_Bag_SkinCell` - 隐藏锁定图标
- `@UI_UI_Character_Skin` - Spine 动画加载

---

### 2. 角色解锁

**原理**：修改 `GameMgr:have_character()` 始终返回 true

**效果**：
- 所有角色显示为已解锁
- 可以选择任意角色作为主角色
- 角色等级显示为满级(5级)

**涉及模块**：
- `@GameMgr` - have_character() 返回 true
- `@GameMgr` - 角色数据注入（level=5, is_upgraded=true）

---

### 3. 语音解锁

**原理**：修改 `Tools.get_chara_audio()` 函数，移除等级/羁绊检查

**效果**：
- 解锁所有角色的全部语音
- 包括等级限制的语音
- 包括羁绊限制的语音

**涉及模块**：
- `@Tools` - get_chara_audio() 解锁逻辑

---

### 4. 称号解锁

**原理**：在 fetchInfo 响应中注入所有称号 ID

**效果**：
- 所有称号显示为已获得
- 可以选择使用任意称号
- 称号更换请求被拦截，本地生效

**涉及模块**：
- `@GameMgr` - makeFetchInfoRes() 称号注入
- `@LobbyNetMgr` - useTitle 请求拦截

---

### 5. 装饰道具解锁

**原理**：在 fetchInfo 响应中注入所有装饰品

**效果**：
- 解锁所有牌桌、牌背等装饰
- 可以选择使用任意装饰品

**涉及模块**：
- `@GameMgr` - makeFetchInfoRes() 道具注入

---

### 6. 装扮方案解锁

**原理**：在 fetchInfo 响应中注入10个装扮槽位

**效果**：
- 解锁所有装扮方案槽位
- 可以保存和切换装扮方案

**涉及模块**：
- `@GameMgr` - makeFetchInfoRes() 装扮方案注入
- `@LobbyNetMgr` - saveCommonViews/useCommonView 请求拦截

---

### 7. 表情解锁

**原理**：在角色数据中注入 extra_emoji 列表

**效果**：
- 解锁所有角色的额外表情
- 从 character_emoji 表读取并注入

**涉及模块**：
- `@GameMgr` - 角色数据 extra_emoji 注入

---

## 🛡️ 安全功能

### 网络请求拦截

以下请求会被拦截，仅本地生效：

| 请求方法 | 说明 |
|----------|------|
| changeCharacterSkin | 更换角色皮肤 |
| changeMainCharacter | 更换主角色 |
| setRandomCharacter | 设置随机角色 |
| updateCharacterSort | 更新角色排序 |
| useTitle | 使用称号 |
| setLoadingImage | 设置加载图 |
| saveCommonViews | 保存装扮方案 |
| useCommonView | 使用装扮方案 |
| receiveCharacterRewards | 领取角色奖励 |
| useSpecialEffect | 使用特效 |
| useNewBGM | 使用BGM |
| setStarChar | 设置星标角色 |

### 日志上传阻止

**原理**：修改 `App_LogTool` 和 `LogStoreUtility` 模块

**效果**：
- 阻止客户端日志发送到服务器
- 可选择在本地控制台显示被屏蔽的内容

### 原始数据保护

**机制**：
1. 登录时保存服务器返回的原始数据到 `MajSoulHelper_OriginalData`
2. 每次更换皮肤时记录原始皮肤状态
3. 确保不会向服务器发送伪造数据

---

## ⚡ 性能功能

### 帧率设置

- **FrameRateBase**: 目标帧率 (30-240)
- 通过 Web 面板或配置文件设置

### 时间倍率

- **TargetTimeScale**: 游戏速度倍率 (0.5-4.0)
- 可用于加速动画和对局流程

---

## 🎮 对局中行为

### 自己视角
- 使用本地设置的皮肤
- 通过 DesktopMgr:InitRoom() 替换

### 其他玩家视角
- 看到的是服务器原始数据
- 无法修改（服务器直接推送）

### 战绩/排行榜
- 显示服务器原始数据
- 无法修改
---

## 🎭 固定伪造角色功能

### 功能说明

允许设置一个固定的伪造角色/皮肤配置，在友人场和对局中始终使用该配置。

### 配置方式

通过 Web API 配置:

```bash
# 设置固定伪造角色
curl -X POST http://127.0.0.1:23333/api/fake/config \
  -H "Content-Type: application/json" \
  -d '{
    "enabled": true,
    "characterId": 200001,
    "skinId": 200109,
    "titleId": 600001
  }'
```

### 工作原理

1. **进入房间时**：拦截 joinRoom/createRoom/fetchRoom 响应，替换自己的角色/皮肤数据
2. **对局开始时**：拦截 authGame 响应，在 InitRoom 之前替换皮肤
3. **皮肤更换请求**：发送拥有的默认值给服务器，本地保存用户真实选择

### 配置优先级

1. 如果启用固定伪造配置，使用 `PluginConfig` 中的固定值
2. 如果未启用，使用游戏内选择的角色/皮肤（本地保存的选择）

### 注意事项

- ⚠️ 配置更改后需要**重启游戏**才能生效
- ⚠️ 其他玩家看到的仍是你真正拥有的角色/皮肤
- ⚠️ 友人场/比赛房间中的显示会自动替换为伪造配置

### 涉及模块

| 模块 | 功能 |
|------|------|
| `@LobbyNetMgr` | 拦截房间相关响应，替换皮肤数据 |
| `@MJNetMgr` | 拦截 authGame 响应，替换对局皮肤 |
| `@DesktopMgr` | 确保对局中使用正确皮肤 |

### Lua 全局变量

```lua
MajSoulHelper_FakeConfig = {
    enabled = true,      -- 是否启用固定伪造
    charId = 200001,     -- 角色ID
    skinId = 200109,     -- 皮肤ID
    titleId = 600001,    -- 称号ID
    views = {[1]=305014} -- 装扮方案
}
```