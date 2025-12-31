--[[
    MajSoulHelper - LogTool 补丁
    用途: 阻止客户端日志发送到服务器
    
    核心修改:
    1. 屏蔽 LogToServer 函数
    2. 屏蔽 ReportError 函数
    3. 可选: 在本地控制台显示被屏蔽的内容
    
    安全性:
    - 防止本地修改被服务器记录
    - 防止错误信息泄露修改痕迹
    - 可配置是否显示被屏蔽的日志
]]--

-- 【方法1】覆盖日志函数
--[[
-- 保存原始函数
local original_LogToServer = App_LogTool.LogToServer
local original_ReportError = App_LogTool.ReportError

-- 覆盖 LogToServer
App_LogTool.LogToServer = function(category, message, ...)
    -- [MajSoulHelper] 阻止日志上传
    if MajSoulHelper_Config and MajSoulHelper_Config.BlockLogToServer then
        -- 可选: 在本地显示
        if MajSoulHelper_Config.EnableBlockedLogDisplay then
            print('[MajSoulHelper] 已阻止日志上传: ' .. tostring(category) .. ' - ' .. tostring(message))
        end
        return  -- 不执行原始逻辑
    end
    
    return original_LogToServer(category, message, ...)
end

-- 覆盖 ReportError
App_LogTool.ReportError = function(errorType, errorInfo, ...)
    -- [MajSoulHelper] 阻止错误上报
    if MajSoulHelper_Config and MajSoulHelper_Config.BlockLogToServer then
        if MajSoulHelper_Config.EnableBlockedLogDisplay then
            print('[MajSoulHelper] 已阻止错误上报: ' .. tostring(errorType))
        end
        return
    end
    
    return original_ReportError(errorType, errorInfo, ...)
end
]]--

-- 【方法2】正则替换方式（在C#中使用）
-- 在函数开头添加 return 语句
--
-- 查找: function App_LogTool:LogToServer(
-- 替换为: function App_LogTool:LogToServer(
-- -- [MajSoulHelper] 阻止日志上传
-- do return end

-- 请根据实际需要取消注释并使用上述代码
