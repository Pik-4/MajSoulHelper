using System.Collections.Generic;
using UnityEngine;

namespace MajSoulHelper
{
    public static class PluginConfig
    {
        public static int FrameRateBase = 120;
        public static bool isFrameRateBaseNeedUpdate = true;
        public static float TargetTimeScale = 1f;

        //    public static ConfigEntry<bool> isPluginEnable;
        //    public static void ConfigBind(ConfigFile config)
        //    {
        //        config.Clear();
        //        isPluginEnable = config.Bind("全局", "MajSoulHelper状态", true, "是否启用插件");

        //    }

        public static ConfigValue configValue = new ConfigValue();

        // ======== 皮肤解锁配置 ========
        /// <summary>
        /// 是否启用本地皮肤解锁功能
        /// </summary>
        public static bool EnableSkinUnlock = true;

        /// <summary>
        /// 是否启用本地角色解锁功能
        /// </summary>
        public static bool EnableCharacterUnlock = true;

        /// <summary>
        /// 是否启用UI锁定图标隐藏
        /// </summary>
        public static bool HideLockUI = true;

        /// <summary>
        /// 是否在对局中使用解锁的皮肤（本地生效）
        /// </summary>
        public static bool EnableInGameSkinReplace = true;

        /// <summary>
        /// 是否启用详细日志输出
        /// </summary>
        public static bool EnableDebugLog = false;

        /// <summary>
        /// 是否启用语音解锁（解锁全部角色语音，包括等级和羁绊限制的语音）
        /// </summary>
        public static bool EnableVoiceUnlock = true;

        /// <summary>
        /// 是否在屏蔽日志/上报时显示被丢弃的内容（输出到本地控制台）
        /// </summary>
        public static bool EnableBlockedLogDisplay = true;

        /// <summary>
        /// 是否阻止日志上传到服务器
        /// </summary>
        public static bool BlockLogToServer = true;

        /// <summary>
        /// 是否阻止对局信息上报
        /// </summary>
        public static bool BlockMatchInfo = true;

        // ======== 扩展功能配置 ========
        /// <summary>
        /// 是否启用称号解锁
        /// </summary>
        public static bool EnableTitleUnlock = true;

        /// <summary>
        /// 是否启用物品解锁（牌桌/牌背等装饰）
        /// </summary>
        public static bool EnableItemUnlock = true;

        /// <summary>
        /// 是否启用装扮解锁（Views系统）
        /// </summary>
        public static bool EnableViewsUnlock = true;

        /// <summary>
        /// 是否启用表情解锁
        /// </summary>
        public static bool EnableEmojiUnlock = true;

        // ======== WebServer配置 ========
        /// <summary>
        /// 是否启用Web配置服务器
        /// </summary>
        public static bool EnableWebServer = true;

        /// <summary>
        /// Web服务器端口
        /// </summary>
        public static int WebServerPort = 23333;

        // ======== 固定伪造角色配置 ========
        /// <summary>
        /// 是否启用固定伪造角色（启用后使用下方配置的角色/皮肤，而不是游戏内选择的）
        /// </summary>
        public static bool EnableFixedFakeCharacter = false;

        /// <summary>
        /// 固定伪造的主角色ID（如 200001 = 一姬）
        /// </summary>
        public static int FixedCharacterId = 0;

        /// <summary>
        /// 固定伪造的皮肤ID（如 400101 = 一姬默认皮肤）
        /// </summary>
        public static int FixedSkinId = 0;

        /// <summary>
        /// 固定伪造的称号ID
        /// </summary>
        public static int FixedTitleId = 0;

        /// <summary>
        /// 固定伪造的装扮（slot -> item_id）
        /// </summary>
        public static Dictionary<int, int> FixedViews = new Dictionary<int, int>();

        /// <summary>
        /// 允许对局中动态刷新伪造配置
        /// </summary>
        public static bool AllowDynamicRefresh = true;

        /// <summary>
        /// 需要修改的Lua模块名称与补丁类型映射
        /// Key: Lua模块名称, Value: 补丁类型
        /// </summary>
        public static Dictionary<string, LuaPatchType> LuaPatchMapping = new Dictionary<string, LuaPatchType>
        {
            { "@GameUtility", LuaPatchType.ItemOwned },
            { "@GameMgr", LuaPatchType.CharacterInfo },
            { "@UI_UI_Bag_SkinCell", LuaPatchType.SkinCellUI },
            { "@UI_UI_Bag", LuaPatchType.BagUI },
            { "@UI_UI_Character_Skin", LuaPatchType.CharacterSkinUI },
            { "@UI_UI_LiaoSheChangeSkin", LuaPatchType.ChangeSkinUI },
            { "@UI_UI_RoleSet", LuaPatchType.RoleSetUI },
            // 新增: 更多皮肤相关UI
            { "@UI_UI_Skin_Yulan", LuaPatchType.SkinPreviewUI },
            { "@UI_UI_SkinShop_Yulan", LuaPatchType.SkinShopUI },
            { "@UI_UI_LiaosheMain", LuaPatchType.LiaosheMainUI },
            { "@UI_UI_LiaosheSelect", LuaPatchType.LiaosheSelectUI },
            { "@UI_UI_Visit", LuaPatchType.VisitUI },
            { "@Tools", LuaPatchType.ToolsModule },
            // 网络请求拦截
            { "@LobbyNetMgr", LuaPatchType.LobbyNetMgr },
            // 对局网络管理器 - 拦截 authGame 响应
            { "@MJNetMgr", LuaPatchType.MJNetMgr },
            // 对局中玩家皮肤
            { "@DesktopMgr", LuaPatchType.DesktopMgr },
            // 友人房房间UI
            { "@UI_UI_FriendRoom", LuaPatchType.FriendRoomUI },
            { "@UI_FriendRoom", LuaPatchType.FriendRoomUI },
            // 日志和错误上报拦截
            { "@App_LogTool", LuaPatchType.LogTool },
            { "@LogStoreUtility", LuaPatchType.LogStoreUtility },
            // UI错误信息上报
            { "@UI_UI_ErrorInfo", LuaPatchType.ErrorInfoUI }
        };
    }

    /// <summary>
    /// Lua补丁类型枚举
    /// </summary>
    public enum LuaPatchType
    {
        None = 0,
        /// <summary>修改item_owned函数，使其始终返回true</summary>
        ItemOwned,
        /// <summary>修改角色信息初始化，注入所有皮肤到skin_map</summary>
        CharacterInfo,
        /// <summary>修改皮肤单元格UI，隐藏锁定图标</summary>
        SkinCellUI,
        /// <summary>修改背包UI皮肤显示逻辑</summary>
        BagUI,
        /// <summary>修改角色皮肤选择UI</summary>
        CharacterSkinUI,
        /// <summary>修改换肤UI逻辑</summary>
        ChangeSkinUI,
        /// <summary>修改角色设置UI逻辑</summary>
        RoleSetUI,
        /// <summary>皮肤预览界面</summary>
        SkinPreviewUI,
        /// <summary>皮肤商店预览</summary>
        SkinShopUI,
        /// <summary>寮舍主界面</summary>
        LiaosheMainUI,
        /// <summary>寮舍选择界面</summary>
        LiaosheSelectUI,
        /// <summary>拜访界面</summary>
        VisitUI,
        /// <summary>Tools模块</summary>
        ToolsModule,
        /// <summary>网络请求拦截 - 阻止皮肤更换请求发送到服务器</summary>
        LobbyNetMgr,
        /// <summary>对局网络管理器 - 拦截 authGame/enterGame 响应并替换皮肤</summary>
        MJNetMgr,
        /// <summary>对局桌面管理器 - 修改对局中玩家皮肤</summary>
        DesktopMgr,
        /// <summary>友人房房间UI - 处理房间内皮肤显示</summary>
        FriendRoomUI,
        /// <summary>日志工具 - 屏蔽皮肤相关日志上报</summary>
        LogTool,
        /// <summary>日志存储工具 - 屏蔽错误上报</summary>
        LogStoreUtility,
        /// <summary>错误信息UI - 屏蔽皮肤相关错误上报</summary>
        ErrorInfoUI
    }

    public class ConfigValue
    {
        //public bool IsPluginEnableValue
        //{
        //    get { return PluginConfig.isPluginEnable.Value; }
        //    set { PluginConfig.isPluginEnable.Value = value; }
        //}
        public int TargetFrameRate
        {
            get { return (int)(PluginConfig.TargetTimeScale * PluginConfig.FrameRateBase); }
        }
        public void UpdateFrameConfig()
        {
            QualitySettings.vSyncCount = 0;
            QualitySettings.SetQualityLevel((int)QualityLevel.Fantastic);
            QualitySettings.SetQualityLevel((int)QualityLevel.Fantastic, true);
            Time.timeScale = PluginConfig.TargetTimeScale;
            Application.targetFrameRate = this.TargetFrameRate;
            Utils.MyLogger(BepInEx.Logging.LogLevel.Warning, $"new time scale {PluginConfig.TargetTimeScale}, target frame rate {this.TargetFrameRate}");
        }
        public static ConfigValue Get()
        {
            return PluginConfig.configValue;
        }

    }

}
