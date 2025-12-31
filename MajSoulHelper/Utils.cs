using BepInEx.Logging;


namespace MajSoulHelper
{
    public class Utils
    {
        public static void MyLogger(LogLevel level, object message)
        {
            var myLogSource = BepInEx.Logging.Logger.CreateLogSource(PluginInfo.PLUGIN_GUID + ".MyLogger");
            myLogSource.Log(level, message);
            BepInEx.Logging.Logger.Sources.Remove(myLogSource);
        }
        //public static void LeakSkins()
        //{
        //    SkeletonData.FindSkin("1111");

        //}
    }
}
