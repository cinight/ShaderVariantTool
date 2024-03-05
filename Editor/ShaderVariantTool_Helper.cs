using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using System;
using System.IO;
using System.Globalization;
using UnityEditor;

namespace GfxQA.ShaderVariantTool
{
    public static class Helper
    {
        public static string GetCSVFolderPath()
        {
            return Application.dataPath.Replace("/Assets","/");
        }


        private static string logNextBuildPopupPref = "ShaderVariantTool_LogNextBuildPopup";
        private static DialogOptOutDecisionType dialogOptOutDecisionType = DialogOptOutDecisionType.ForThisSession;
        public const string logNextBuildPref = "ShaderVariantTool_LogNextBuild";
        public static bool shouldUpdateLogNextBuildGUI = true;
        public static void LogNextBuildPopup()
        {
            if(EditorUtility.GetDialogOptOutDecision(dialogOptOutDecisionType, logNextBuildPopupPref)) return;
            
            string title = "ShaderVariantTool - Log next build?";
            string message = "If enabled, the tool lengthen shader stripping time. The impact will be listed on the tool under shader stripping time after building.\n\n" +
                             "You can also configure in Window > ShaderVariantTool > [Log Next Build].";
            bool enable = EditorUtility.DisplayDialog
            (
                title,message,
                "Enable",
                "Disable",
                dialogOptOutDecisionType,
                logNextBuildPopupPref
            );
            
            SetLogNextBuild(enable);
        }
        
        public static void InitiateLogNextBuild()
        {
            if (!shouldUpdateLogNextBuildGUI) return;
            bool logPref = SessionState.GetBool(logNextBuildPref,true);
            SVL.logNextBuild = logPref;
            shouldUpdateLogNextBuildGUI = false;
        }
        
        public static void SetLogNextBuild(bool v)
        {
            SessionState.SetBool(logNextBuildPref, v);
            SVL.logNextBuild = v;
            shouldUpdateLogNextBuildGUI = true;
        }

        private static string culturePref = "ShaderVariantTool_Culture";
        public static CultureInfo culture;
        public static CultureInfo[] cinfo;
        public static void SetupCultureInfo()
        {
            if(cinfo == null) cinfo = CultureInfo.GetCultures(CultureTypes.AllCultures & ~CultureTypes.NeutralCultures);
            string cultureString = EditorPrefs.GetString(culturePref,"English (United States)");
            if(culture == null ) culture = GetCultureInfoWithName(cultureString);
        }

        private static CultureInfo GetCultureInfoWithName(string cultureString)
        {
            return Array.Find<CultureInfo>(cinfo, x => x.DisplayName == cultureString);
        }

        public static void UpdateCultureInfo(string displayName)
        {
            EditorPrefs.SetString(culturePref,displayName);
            culture = GetCultureInfoWithName(displayName);
        }

        public static string NumberSeperator(string input)
        {
            UInt64 outNum = 0;
            bool success = UInt64.TryParse(input, out outNum);
            if(success)
            {
                return outNum.ToString("N0", culture);
            }
            else
            {
                return input;
            }
        }

        public static string TimeFormatString (double timeInSeconds)
        {
            SetupCultureInfo();

            float t = (float)timeInSeconds;

            float hour = t / 3600f;
            hour = Mathf.Floor(hour);
            t -= hour*3600f;

            float minute = t / 60f;
            minute = Mathf.Floor(minute);
            t -= minute*60f;

            float second = t;
            string secondString = minute > 0 ? Mathf.RoundToInt(second).ToString() : second.ToString("0.00");

            string timeString = "";

            if(hour > 0) timeString += hour + "hr ";
            if(minute > 0) timeString += minute + "m ";
            timeString += NumberSeperator(secondString) + "s";

            return timeString;
        }

        public static string GetRemainingString(string line, string from)
        {
            int index = line.IndexOf(from) + from.Length;
            return line.Substring( index , line.Length - index);
        }

        public static string ExtractString(string line, string from, string to, bool takeLastIndexOfTo = true)
        {
            int pFrom = 0;
            if(from != "")
            {
                int index = line.IndexOf(from);
                if(index >= 0) pFrom = index + from.Length;
            }
            
            int pTo = line.Length;
            if(to != "")
            {
                int index = line.LastIndexOf(to);
                if(!takeLastIndexOfTo)
                {
                    index = line.IndexOf(to);
                }

                if(index >= 0) pTo = index;
            }

            return line.Substring(pFrom, pTo - pFrom);
        }

        public static void DebugLog(string msg)
        {
            Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, msg);
        }

        public static string GetPlatformKeywordList(PlatformKeywordSet pks)
        {
            string enabledPKeys = "";
            foreach(BuiltinShaderDefine sd in System.Enum.GetValues(typeof(BuiltinShaderDefine))) 
            {
                //Only pay attention to SHADER_API_MOBILE, SHADER_API_DESKTOP and SHADER_API_GLES30
                if( sd.ToString().Contains("SHADER_API") && pks.IsEnabled(sd) )
                {
                    if(enabledPKeys != "") enabledPKeys += " ";
                    enabledPKeys += sd.ToString();
                }
            }
            return enabledPKeys;
        }

        public static string GetEditorLogPath()
        {
            string editorLogPath = "";
            switch(Application.platform)
            {
                case RuntimePlatform.WindowsEditor: editorLogPath=Environment.GetEnvironmentVariable("AppData").Replace("Roaming","")+"Local\\Unity\\Editor\\Editor.log"; break;
                case RuntimePlatform.OSXEditor: editorLogPath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Library")+"/Logs/Unity/Editor.log"; break;
                case RuntimePlatform.LinuxEditor: editorLogPath="~/.config/unity3d/Editor.log"; break;
            }
            return editorLogPath;
        }
    }
}