using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Scourt.Loc;
using System.Reflection;



namespace COM3D2.i18nEx.Core.Util
{
    internal class CompatLocalizationManager
    {
        public static readonly IEnumerable<KeyValuePair<string, Product.Language>> ScriptTranslationMark;

        static CompatLocalizationManager()
        {
            // LocalizationManager.ScriptTranslationMark is a static field of type IReadOnlyDictionary (3.41+) or Dictionary (2.x) depending on version of the game
            // obtain a reference via reflection as an IEnumerable<KeyValuePair<string,Product.Language>> so we can work with both cases

            var scriptTranslationMarkField = typeof(LocalizationManager).GetField("ScriptTranslationMark", BindingFlags.Public | BindingFlags.Static);
            if (scriptTranslationMarkField == null)
            {
                throw new Exception("Cannot find LocalizationManager.ScriptTranslationMark field");
            }

            ScriptTranslationMark = scriptTranslationMarkField.GetValue(null) as IEnumerable<KeyValuePair<string, Product.Language>>;
        }
    }
}
