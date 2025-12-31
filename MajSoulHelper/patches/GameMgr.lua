--[[
    MajSoulHelper - GameMgr 补丁
    用途: 修改角色拥有检查和皮肤映射初始化
    
    核心修改:
    1. have_character 函数始终返回 true
    2. makeCharacterInfo 中注入所有皮肤到 skin_map
    
    注意: 此文件用于替换字节码格式的 @GameMgr 模块
]]--

-- 【方法1】覆盖函数 (在模块加载后执行)
--[[
-- 保存原始函数
local original_have_character = GameMgr.have_character
local original_makeCharacterInfo = GameMgr.makeCharacterInfo

-- 覆盖 have_character
GameMgr.have_character = function(self, aX)
    -- [MajSoulHelper] 始终返回拥有角色
    return true
end

-- 覆盖 makeCharacterInfo 以注入所有皮肤
local original_makeCharacterInfo = GameMgr.makeCharacterInfo
GameMgr.makeCharacterInfo = function(self, t, s)
    -- 调用原始函数
    original_makeCharacterInfo(self, t, s)
    
    -- 注入所有皮肤到 skin_map
    pcall(function()
        local allSkins = ExcelMgr.GetTable('item_definition', 'skin')
        if allSkins then
            for skinId, _ in pairs(allSkins) do
                self.skin_map[skinId] = 1
            end
            if LogTool and LogTool.Info then
                LogTool.Info('MajSoulHelper', '已注入所有皮肤')
            end
        end
    end)
end
]]--

-- 【方法2】修改后的 have_character 函数
--[[
function GameMgr:have_character(aX)
    -- [MajSoulHelper] 始终返回拥有角色
    return true
    
    -- 原始逻辑 (已禁用)
    --[[
    if not self.characterInfo.characters or not aX then
        return false
    end
    for l = 1, #self.characterInfo.characters do
        if aX == self.characterInfo.characters[l].charid then
            return true
        end
    end
    return false
    ]]
end
]]--

-- 请根据实际需要取消注释并使用上述代码
