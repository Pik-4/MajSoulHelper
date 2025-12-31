--[[
    MajSoulHelper - Tools 模块补丁
    用途: 解锁所有语音，移除等级和羁绊限制
    
    核心修改:
    1. get_chara_audio 函数移除等级检查
    2. 移除羁绊等级检查
    3. 返回完整的语音列表
    
    效果:
    - 解锁所有角色的全部语音
    - 包括需要特定等级才能解锁的语音
    - 包括需要羁绊等级才能解锁的语音
]]--

-- 【方法1】覆盖 get_chara_audio 函数
--[[
local original_get_chara_audio = Tools.get_chara_audio

Tools.get_chara_audio = function(charId, audioType, level, bond, ...)
    -- [MajSoulHelper] 解锁全部语音
    -- 将等级和羁绊设置为最大值来解锁所有语音
    local unlocked_level = 5  -- 最大等级
    local unlocked_bond = 99  -- 足够高的羁绊值
    
    return original_get_chara_audio(charId, audioType, unlocked_level, unlocked_bond, ...)
end
]]--

-- 【方法2】修改后的完整函数
--[[
function Tools.get_chara_audio(f0, f1, f2, f3, f4)
    -- f0: 角色ID
    -- f1: 语音类型
    -- f2: 角色等级
    -- f3: 羁绊等级
    -- f4: 其他参数
    
    -- [MajSoulHelper] 解锁语音: 跳过等级检查
    local unlocked_level = 5
    local unlocked_bond = 99
    
    -- 获取语音数据
    local audioData = ExcelMgr.GetData('character_audio', f0, f1)
    if not audioData then
        return nil
    end
    
    local result = {}
    for i, audio in ipairs(audioData) do
        -- 原始逻辑会检查 audio.level <= f2 和 audio.bond <= f3
        -- [MajSoulHelper] 修改: 使用解锁后的等级检查
        if audio.level <= unlocked_level and (audio.bond or 0) <= unlocked_bond then
            table.insert(result, audio)
        end
    end
    
    return result
end
]]--

-- 【方法3】正则替换方式（在C#中使用）
-- 查找: if f2 and audio.level > f2 then
-- 替换为: if false and audio.level > f2 then
-- 
-- 查找: if f3 and audio.bond and audio.bond > f3 then
-- 替换为: if false and audio.bond and audio.bond > f3 then

-- 请根据实际需要取消注释并使用上述代码
