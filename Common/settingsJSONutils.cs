/*
 * Copyright 2017 Christian Rivera
 * 
 * Add Newtonsoft.Json reference via NuGet:
 * 
 *   PM> Install-Package Newtonsoft.Json
 * 
 */
using System.IO;
using Newtonsoft.Json.Linq; 

namespace sevenfloorsdown
{
    class settingsJSONutils
    {
        #region Properties
        public string SettingsFile { get { return settingsFile; } set { LoadSettingsFile(value); } }
        public JObject JSONSettings { get; set; }
        #endregion

        private string settingsFile;
        
        public settingsJSONutils(string jsonFile)
        {
            LoadSettingsFile(jsonFile);
        }

        #region Methods
        private void LoadSettingsFile(string jsonFile)
        {
            using (StreamReader r = new StreamReader(jsonFile))
            {
                string json = r.ReadToEnd();
                JSONSettings = JObject.Parse(json);
            }
        }

        public string GetSettingString(string setting, string valueIfNotFound, params string[] sections)
        { 
            if (sections.Length < 1)
            {
                if (JSONSettings[setting] == null) return valueIfNotFound;
                return (string)JSONSettings[setting].ToObject(typeof(string));
            }
            JToken settingPath = JSONSettings[sections[0]];
            for (int i = 1; i < sections.Length; i++)
            {
                settingPath = settingPath[sections[i]];
                if (settingPath== null) return valueIfNotFound;
            }

            if (settingPath[setting] == null) return valueIfNotFound;
            return (string)settingPath[setting].ToObject(typeof(string));
        }

        public int GetSettingInteger(string setting, int valueIfNotFound, params string[] sections)
        {
            if (sections.Length < 1)
            {
                if (JSONSettings[setting] == null) return valueIfNotFound;
                return (int)JSONSettings[setting].ToObject(typeof(int));
            }
            JToken settingPath = JSONSettings[sections[0]];

            for (int i = 1; i < sections.Length; i++)
            {
                settingPath = settingPath[sections[i]];
                if (settingPath == null) return valueIfNotFound;
            }

            if (settingPath[setting] == null) return valueIfNotFound;
            return (int)settingPath[setting].ToObject(typeof(int));
        }

        public float GetSettingInteger(string setting, float valueIfNotFound, params string[] sections)
        {
            if (sections.Length < 1)
            {
                if (JSONSettings[setting] == null) return valueIfNotFound;
                return (float)JSONSettings[setting].ToObject(typeof(float));
            }
            JToken settingPath = JSONSettings[sections[0]];

            for (int i = 1; i < sections.Length; i++)
            {
                settingPath = settingPath[sections[i]];
                if (settingPath == null) return valueIfNotFound;
            }

            if (settingPath[setting] == null) return valueIfNotFound;
            return (float)settingPath[setting].ToObject(typeof(float));
        }

        public bool GetSettingBoolean(string setting, bool valueIfNotFound, params string[] sections)
        {
            if (sections.Length < 1)
            {
                if (JSONSettings[setting] == null) return valueIfNotFound;
                if (JSONSettings[setting].ToString().ToLower() == "true") return true;
                else return false;
            }
            JToken settingPath = JSONSettings[sections[0]];

            for (int i = 1; i < sections.Length; i++)
            {
                settingPath = settingPath[sections[i]];
                if (settingPath == null) return valueIfNotFound;
            }
            if (settingPath[setting] == null) return valueIfNotFound;
            if (settingPath[setting].ToString().ToLower() == "true") return true;
            else return false;
        }

        public JToken GetSettingSection(string setting, params string[] sections)
        {
            if (sections.Length < 1)
            {
                if (JSONSettings[setting] == null) return null;
                return JSONSettings[setting];
            }
            JToken settingPath = JSONSettings[sections[0]];

            for (int i = 1; i < sections.Length; i++)
            {
                settingPath = settingPath[sections[i]];
                if (settingPath == null) return null;
            }

            if (settingPath[setting] == null) return null;
            return settingPath[setting];
        }
    }
    #endregion
}
