--[[
    MajSoulHelper - GameUtility 补丁
    用途: 修改 item_owned 函数，使皮肤和角色始终返回拥有状态
    
    此文件用于替换字节码格式的 @GameUtility 模块
    如果原始模块是源码格式，插件会自动进行文本替换
    
    注意: 此文件需要与游戏版本匹配，如果游戏更新后出现问题，
          请从 BepInEx/leak/Pre_@GameUtility 获取最新版本并重新修改
]]--

-- 原始模块的修改版本
-- 核心修改: GameUtility.item_owned 函数

-- 【方法1】直接覆盖 item_owned 函数 (在模块加载后执行)
-- 将此代码放在原始模块末尾之前

--[[
local original_item_owned = GameUtility.item_owned
GameUtility.item_owned = function(c)
    local d = GameUtility.get_id_type(c)
    -- 皮肤和角色始终返回true
    if d == GameUtility.EIDType.skin or d == GameUtility.EIDType.character then
        return true
    end
    -- 其他物品调用原始逻辑
    return original_item_owned(c)
end
]]--

-- 【方法2】如果需要完整替换模块，请将原始模块内容复制到这里
-- 并修改 item_owned 函数定义

-- 以下是修改后的 item_owned 函数示例:
--[[
function GameUtility.item_owned(c)
    -- [MajSoulHelper] 本地解锁: 皮肤和角色始终返回拥有
    local d = GameUtility.get_id_type(c)
    if d == GameUtility.EIDType.skin or d == GameUtility.EIDType.character then
        return true
    end
    
    -- 原始逻辑
    if d == GameUtility.EIDType.character then
        local s = GameMgr.Inst.characterInfo.characters
        if s then
            for t = 1, #s do
                if s[t].charid == c then return true end
            end
        end
    end
    
    if d == GameUtility.EIDType.item then
        local u = ExcelMgr.GetData('item_definition', 'item', c)
        if not u then return false end
        if u.category == 4 or u.category == 5 or u.category == 8 then
            local v = UI_Bag.GetCount(c)
            if v > 0 then return true end
        end
    end
    
    if d == GameUtility.EIDType.skin then
        for t = 1, #GameMgr.Inst.characterInfo.skins do
            if GameMgr.Inst.characterInfo.skins[t] == c then
                return true
            end
        end
    end
    
    return false
end
]]--

-- 请根据实际需要取消注释并使用上述代码
