--[[
    MajSoulHelper - LobbyNetMgr 补丁
    用途: 拦截皮肤/角色相关的网络请求，使更改仅在本地生效
    
    核心修改:
    1. SendRequest 函数添加请求拦截逻辑
    2. 针对皮肤相关请求返回模拟成功响应
    3. 不发送到服务器，防止数据被覆盖
    
    拦截的请求:
    - changeCharacterSkin: 更换角色皮肤
    - changeMainCharacter: 更换主角色
    - setRandomCharacter: 设置随机角色
    - updateCharacterSort: 更新角色排序
    - useTitle: 使用称号
    - setLoadingImage: 设置加载图
    - saveCommonViews: 保存装扮方案
    - useCommonView: 使用装扮方案
    - receiveCharacterRewards: 领取角色奖励
    - useSpecialEffect: 使用特效
    - useNewBGM: 使用BGM
    - setStarChar: 设置星标角色
]]--

-- 【方法1】覆盖 SendRequest 函数
--[[
local original_SendRequest = LobbyNetMgr.SendRequest

LobbyNetMgr.SendRequest = function(self, method, data, callback, ...)
    -- 定义需要拦截的请求列表
    local interceptList = {
        changeCharacterSkin = true,
        changeMainCharacter = true,
        setRandomCharacter = true,
        updateCharacterSort = true,
        useTitle = true,
        setLoadingImage = true,
        saveCommonViews = true,
        useCommonView = true,
        receiveCharacterRewards = true,
        useSpecialEffect = true,
        useNewBGM = true,
        setStarChar = true
    }
    
    if interceptList[method] then
        -- [MajSoulHelper] 拦截请求，本地处理
        
        -- 处理特定请求的本地数据更新
        if method == 'changeCharacterSkin' and data.skin then
            -- 更新本地皮肤数据
            pcall(function()
                local charId = nil
                local skinData = ExcelMgr.GetData('item_definition', 'skin', data.skin)
                if skinData and skinData.character_id then
                    charId = skinData.character_id
                end
                if charId and GameMgr.Inst.characterInfo.characters then
                    for i, char in ipairs(GameMgr.Inst.characterInfo.characters) do
                        if char.charid == charId then
                            char.skin = data.skin
                            break
                        end
                    end
                end
                -- 更新本地记录
                if MajSoulHelper_LocalSkinData and charId then
                    MajSoulHelper_LocalSkinData.skin_map[charId] = data.skin
                end
            end)
        elseif method == 'changeMainCharacter' and data.character_id then
            -- 更新本地主角色
            pcall(function()
                GameMgr.Inst.characterInfo.main_character_id = data.character_id
                if MajSoulHelper_LocalSkinData then
                    MajSoulHelper_LocalSkinData.main_character_id = data.character_id
                end
            end)
        elseif method == 'useTitle' and data.title then
            -- 更新本地称号
            pcall(function()
                GameMgr.Inst.info.title = data.title
                if MajSoulHelper_LocalSkinData then
                    MajSoulHelper_LocalSkinData.title = data.title
                end
            end)
        end
        
        -- 模拟成功响应
        if callback then
            callback(nil, { error = { code = 0 } })
        end
        
        return  -- 不发送到服务器
    end
    
    -- 其他请求调用原始逻辑
    return original_SendRequest(self, method, data, callback, ...)
end
]]--

-- 【方法2】完整替换的 SendRequest 函数模板
--[[
function LobbyNetMgr:SendRequest(method, data, callback, timeout)
    -- [MajSoulHelper] 请求拦截检查
    local interceptList = {
        changeCharacterSkin = true,
        changeMainCharacter = true,
        -- ... 其他需要拦截的请求
    }
    
    if interceptList[method] then
        -- 本地处理逻辑...
        if callback then
            callback(nil, { error = { code = 0 } })
        end
        return
    end
    
    -- 原始请求逻辑
    -- ...
end
]]--

-- 请根据实际需要取消注释并使用上述代码
