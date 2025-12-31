--[[
    MajSoulHelper - UI_Bag_SkinCell 补丁
    用途: 隐藏皮肤锁定图标，使所有皮肤显示为已解锁
    
    核心修改:
    UI_Bag_SkinCell:Show 函数中的锁定检查逻辑
]]--

-- 【方法1】覆盖 Show 函数 (在模块加载后执行)
--[[
local original_Show = UI_Bag_SkinCell.Show

UI_Bag_SkinCell.Show = function(self, g)
    self.skin_id = g
    Tools.SetHeadImg(self.icon, g, 'bighead', self.image_tag)
    
    local h = ExcelMgr.GetData('item_definition', 'skin', self.skin_id)
    
    -- [MajSoulHelper] 始终隐藏锁定图标
    self.container_lock.gameObject:SetActive(false)
    
    -- 处理spine提示
    self.spine_tip.gameObject:SetActive(h.spine_type ~= 0)
    if h.spine_type ~= 0 then
        Tools.ImgOfLocalization('ui/lobby/main/pic/flag_skin_spine', self.flag_skin, true)
    else
        Tools.ImgOfLocalization('ui/lobby/main/pic/flag_skin', self.flag_skin, true)
    end
    
    self.go:SetActive(true)
end
]]--

-- 【方法2】修改后的完整 Show 函数
--[[
function UI_Bag_SkinCell:Show(g)
    self.skin_id = g
    Tools.SetHeadImg(self.icon, g, 'bighead', self.image_tag)
    
    local h = ExcelMgr.GetData('item_definition', 'skin', self.skin_id)
    
    -- [MajSoulHelper] 始终隐藏锁定图标
    self.container_lock.gameObject:SetActive(false)
    
    --[[ 原始逻辑 (已禁用)
    if GameMgr.Inst.skin_map[self.skin_id] then
        self.container_lock.gameObject:SetActive(false)
    else
        self.container_lock.gameObject:SetActive(true)
        if h and h.type ~= 0 then
            local i
            if GameMgr.Inst.prefer_language == "en" then
                i = h["name_en"]
            else
                i = h['lock_tips_' .. GameMgr.Inst.prefer_language]
            end
            self.container_lock_info.text = i
            self.container_lock_info.gameObject:SetActive(true)
            self.container_lock:Find('info_bg').gameObject:SetActive(true)
        else
            self.container_lock_info.gameObject:SetActive(false)
            self.container_lock:Find('info_bg').gameObject:SetActive(false)
        end
    end
    ]]--
    
    self.spine_tip.gameObject:SetActive(h.spine_type ~= 0)
    if h.spine_type ~= 0 then
        Tools.ImgOfLocalization('ui/lobby/main/pic/flag_skin_spine', self.flag_skin, true)
    else
        Tools.ImgOfLocalization('ui/lobby/main/pic/flag_skin', self.flag_skin, true)
    end
    
    self.go:SetActive(true)
end
]]--

-- 请根据实际需要取消注释并使用上述代码
