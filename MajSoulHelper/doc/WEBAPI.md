# MajSoulHelper Web API 文档

## 概述

MajSoulHelper 提供基于 HTTP 的 REST API 和 Web 控制面板，用于运行时配置管理。

**默认地址**: `http://127.0.0.1:23333/`

> ⚠️ Web 服务器仅监听本地回环地址，外部无法访问

---

## Web 控制面板

访问 `http://127.0.0.1:23333/` 即可打开控制面板。

### 功能

- 🔓 **解锁功能开关**: 皮肤/角色/语音/称号等
- 🎮 **对局设置**: 对局内皮肤替换、隐藏锁定图标
- 🛡️ **安全设置**: 日志屏蔽、对局信息屏蔽
- ⚡ **性能设置**: 帧率、时间倍率
- 📊 **状态监控**: 版本、已补丁模块数、缓存数
- 🗑️ **缓存管理**: 清除补丁缓存

### 界面预览

控制面板采用暗色主题，包含以下卡片：
- 运行状态
- 解锁功能
- 对局设置
- 安全设置
- 调试设置
- 性能设置

---

## REST API

### GET /api/config

获取当前配置。

**响应示例**:
```json
{
  "enableSkinUnlock": true,
  "enableCharacterUnlock": true,
  "enableVoiceUnlock": true,
  "enableTitleUnlock": true,
  "enableItemUnlock": true,
  "enableViewsUnlock": true,
  "enableEmojiUnlock": true,
  "hideLockUI": true,
  "enableInGameSkinReplace": true,
  "blockLogToServer": true,
  "blockMatchInfo": true,
  "enableDebugLog": false,
  "enableBlockedLogDisplay": true,
  "frameRateBase": 120,
  "targetTimeScale": 1.0,
  "webServerPort": 23333
}
```

---

### POST /api/config

更新配置（即时生效，不保存到文件）。

**请求体**:
```json
{
  "enableSkinUnlock": true,
  "enableCharacterUnlock": true,
  "frameRateBase": 144,
  "targetTimeScale": 1.5
}
```

**响应**:
```json
{
  "success": true,
  "message": "Configuration updated"
}
```

---

### GET /api/status

获取插件运行状态。

**响应示例**:
```json
{
  "running": true,
  "version": "1.0.0",
  "patchedModules": 15,
  "cachedPatches": 12
}
```

**字段说明**:
| 字段 | 说明 |
|------|------|
| running | 插件运行状态 |
| version | 插件版本号 |
| patchedModules | 已应用补丁的模块数 |
| cachedPatches | 已缓存的补丁数 |

---

### POST /api/save

保存当前配置到文件。

**响应**:
```json
{
  "success": true,
  "message": "Configuration saved to file"
}
```

配置文件位置：`BepInEx/config/MajSoulHelper.json`

---

### POST /api/cache/clear

清除补丁缓存。

**响应**:
```json
{
  "success": true,
  "message": "Cache cleared"
}
```

> ⚠️ 清除缓存后需要重启游戏才能重新加载补丁

---

## 固定伪造角色 API

用于配置固定的伪造角色/皮肤，使其在友人场和对局中生效。

### GET /api/fake/config

获取当前固定伪造角色配置。

**响应示例**:
```json
{
  "enabled": true,
  "characterId": 200001,
  "skinId": 200109,
  "titleId": 600001,
  "views": {1: 305014, 2: 305015},
  "allowDynamicRefresh": false
}
```

**字段说明**:
| 字段 | 类型 | 说明 |
|------|------|------|
| enabled | bool | 是否启用固定伪造角色 |
| characterId | int | 固定使用的角色ID |
| skinId | int | 固定使用的皮肤ID |
| titleId | int | 固定使用的称号ID |
| views | object | 装扮方案 {槽位: 道具ID} |
| allowDynamicRefresh | bool | 是否允许对局中动态刷新 |

---

### POST /api/fake/config

设置固定伪造角色配置。

**请求体**:
```json
{
  "enabled": true,
  "characterId": 200001,
  "skinId": 200109,
  "titleId": 600001,
  "views": {"1": 305014, "2": 305015},
  "allowDynamicRefresh": true
}
```

**响应**:
```json
{
  "success": true,
  "message": "Fake config updated. Restart game to apply."
}
```

> ⚠️ 配置更改后需要**重启游戏**才能生效

---

### GET /api/fake/characters

获取所有可用角色列表（缓存数据）。

**响应示例**:
```json
{
  "characters": {
    "200001": "一姬",
    "200002": "二阶堂美树",
    "200003": "千织"
  }
}
```

---

### GET /api/fake/skins?characterId=200001

获取指定角色的所有皮肤列表。

**参数**:
| 参数 | 类型 | 说明 |
|------|------|------|
| characterId | int | 角色ID |

**响应示例**:
```json
{
  "characterId": 200001,
  "skins": {
    "200101": "默认皮肤",
    "200109": "繁花似锦"
  }
}
```

---

### POST /api/fake/refresh

强制刷新伪造数据到游戏（用于动态刷新）。

**响应**:
```json
{
  "success": true,
  "message": "Config change notified. Will refresh on next opportunity."
}
```

> ⚠️ 动态刷新功能目前受限，建议重启游戏应用配置

---

## 配置项说明

### 解锁功能

| 配置项 | 类型 | 默认值 | 说明 |
|--------|------|--------|------|
| enableSkinUnlock | bool | true | 解锁所有皮肤 |
| enableCharacterUnlock | bool | true | 解锁所有角色 |
| enableVoiceUnlock | bool | true | 解锁所有语音 |
| enableTitleUnlock | bool | true | 解锁所有称号 |
| enableItemUnlock | bool | true | 解锁所有道具 |
| enableViewsUnlock | bool | true | 解锁装扮方案 |
| enableEmojiUnlock | bool | true | 解锁所有表情 |

### 对局设置

| 配置项 | 类型 | 默认值 | 说明 |
|--------|------|--------|------|
| hideLockUI | bool | true | 隐藏锁定图标 |
| enableInGameSkinReplace | bool | true | 对局中皮肤替换 |

### 固定伪造角色设置

| 配置项 | 类型 | 默认值 | 说明 |
|--------|------|--------|------|
| enableFixedFakeCharacter | bool | false | 启用固定伪造角色 |
| fixedCharacterId | int | 0 | 固定使用的角色ID |
| fixedSkinId | int | 0 | 固定使用的皮肤ID |
| fixedTitleId | int | 0 | 固定使用的称号ID |
| fixedViews | object | {} | 装扮方案 {槽位: 道具ID} |
| allowDynamicRefresh | bool | false | 允许对局中动态刷新 |

> ⚠️ 固定伪造角色功能会覆盖游戏内的选择，配置更改需要重启游戏才能生效

### 安全设置

| 配置项 | 类型 | 默认值 | 说明 |
|--------|------|--------|------|
| blockLogToServer | bool | true | 阻止日志上传 |
| blockMatchInfo | bool | true | 阻止对局信息上报 |

### 调试设置

| 配置项 | 类型 | 默认值 | 说明 |
|--------|------|--------|------|
| enableDebugLog | bool | false | 输出调试日志 |
| enableBlockedLogDisplay | bool | true | 显示被屏蔽的内容 |

### 性能设置

| 配置项 | 类型 | 默认值 | 范围 | 说明 |
|--------|------|--------|------|------|
| frameRateBase | int | 120 | 30-240 | 目标帧率 |
| targetTimeScale | float | 1.0 | 0.5-4.0 | 时间倍率 |

### WebServer 设置

| 配置项 | 类型 | 默认值 | 说明 |
|--------|------|--------|------|
| webServerPort | int | 23333 | Web 服务器端口 |

---

## 使用示例

### cURL

```bash
# 获取配置
curl http://127.0.0.1:23333/api/config

# 更新配置
curl -X POST http://127.0.0.1:23333/api/config \
  -H "Content-Type: application/json" \
  -d '{"frameRateBase": 144}'

# 保存配置
curl -X POST http://127.0.0.1:23333/api/save

# 获取状态
curl http://127.0.0.1:23333/api/status

# 清除缓存
curl -X POST http://127.0.0.1:23333/api/cache/clear
```

### JavaScript

```javascript
// 获取配置
const config = await fetch('/api/config').then(r => r.json());

// 更新配置
await fetch('/api/config', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ enableSkinUnlock: true })
});

// 保存到文件
await fetch('/api/save', { method: 'POST' });
```

### Python

```python
import requests

# 获取配置
config = requests.get('http://127.0.0.1:23333/api/config').json()

# 更新配置
requests.post('http://127.0.0.1:23333/api/config', 
              json={'frameRateBase': 144})

# 保存
requests.post('http://127.0.0.1:23333/api/save')
```

---

## CORS 支持

API 支持跨域请求，响应头包含：

```
Access-Control-Allow-Origin: *
Access-Control-Allow-Methods: GET, POST, OPTIONS
Access-Control-Allow-Headers: Content-Type
```
