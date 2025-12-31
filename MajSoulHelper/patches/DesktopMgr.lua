--[[
    MajSoulHelper - DesktopMgr 补丁
    用途: 在对局中替换自己的皮肤显示
    
    核心修改:
    1. InitRoom 函数中修改自己的皮肤数据
    2. 读取本地保存的皮肤设置
    3. 仅影响本地显示，其他玩家看到的仍是服务器数据
    
    注意:
    - 仅自己可见本地设置的皮肤
    - 其他玩家看到的是服务器原始数据
    - 战绩和排行榜显示服务器数据
]]--

-- 【方法1】Hook InitRoom 函数
--[[
local original_InitRoom = DesktopMgr.InitRoom

DesktopMgr.InitRoom = function(self, roomData, ...)
    -- 调用原始函数
    local result = original_InitRoom(self, roomData, ...)
    
    -- [MajSoulHelper] 替换自己的皮肤
    pcall(function()
        if not MajSoulHelper_LocalSkinData then return end
        
        local myAccountId = GameMgr.Inst.info.account_id
        
        -- 遍历所有玩家
        if self.players then
            for i, player in ipairs(self.players) do
                if player.account_id == myAccountId then
                    -- 这是自己，应用本地皮肤设置
                    local charId = player.character and player.character.charid
                    if charId and MajSoulHelper_LocalSkinData.skin_map[charId] then
                        player.character.skin = MajSoulHelper_LocalSkinData.skin_map[charId]
                    end
                    break
                end
            end
        end
    end)
    
    return result
end
]]--

-- 【方法2】修改后的 InitRoom 函数模板
--[[
function DesktopMgr:InitRoom(roomData, ...)
    -- 原始初始化逻辑
    -- ...
    
    -- [MajSoulHelper] 对局中皮肤替换
    if PluginConfig.EnableInGameSkinReplace then
        pcall(function()
            local myAccountId = GameMgr.Inst.info.account_id
            
            for i, player in ipairs(roomData.players or {}) do
                if player.account_id == myAccountId then
                    -- 获取本地设置的皮肤
                    local localSkin = MajSoulHelper_LocalSkinData and 
                                      MajSoulHelper_LocalSkinData.skin_map[player.character.charid]
                    if localSkin then
                        player.character.skin = localSkin
                        LogTool.Log('MajSoulHelper', '对局中替换皮肤: ' .. localSkin)
                    end
                    break
                end
            end
        end)
    end
    
    -- 继续原始逻辑...
end
]]--

-- 请根据实际需要取消注释并使用上述代码
