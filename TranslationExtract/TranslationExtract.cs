﻿// TODO: Fix for multi-language support

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using I2.Loc;
using UnityEngine;

namespace TranslationExtract
{
    internal static class Extensions
    {
        private static string EscapeCSVItem(string str)
        {
            if (str.Contains("\n") || str.Contains("\"") || str.Contains(","))
                return $"\"{str.Replace("\"", "\"\"")}\"";
            return str;
        }

        private static IEnumerable<(T1, T2)> ZipWith<T1, T2>(this IEnumerable<T1> e1, IEnumerable<T2> e2)
        {
            if (e1 == null || e2 == null)
                yield break;
            using var enum1 = e1.GetEnumerator();
            using var enum2 = e2.GetEnumerator();

            while (enum1.MoveNext() && enum2.MoveNext())
                yield return (enum1.Current, enum2.Current);
        }

        public static bool ContainsJapaneseCharacters(this string input)
        {
			// Define the ranges for Japanese characters
            var japaneseRanges = @"[\p{IsHiragana}\p{IsKatakana}\p{IsCJKUnifiedIdeographs}]";

            // Combine all ranges and exclude non-letter symbols and grammatical marks
            var regex = new Regex(japaneseRanges);

            return regex.IsMatch(input);
		}

		public static void WriteCSV<T>(this StreamWriter sw,
                                       string neiFile,
                                       string csvFile,
                                       Func<CsvParser, int, T> selector,
                                       Func<T, IEnumerable<string>> toString,
                                       Func<T, IEnumerable<string>> toTranslation,
                                       bool skipIfExists = false)
        {
            using var f = GameUty.FileOpen(neiFile);
            using var scenarioNei = new CsvParser();
            scenarioNei.Open(f);

            for (var i = 1; i < scenarioNei.max_cell_y; i++)
            {
                if (!scenarioNei.IsCellToExistData(0, i))
                    continue;

                var item = selector(scenarioNei, i);
                var prefixes = toString(item);
                var translations = toTranslation(item);

                foreach (var (prefix, tl) in prefixes.ZipWith(translations))
                {
                    if (skipIfExists && ((LocalizationManager.TryGetTranslation($"{csvFile}/{prefix}", out var _)) ||
                        tl.ContainsJapaneseCharacters() == false))
                        continue;

                    if (string.IsNullOrEmpty(tl))
                        continue;

					var csvName = EscapeCSVItem(tl);
                    if (!csvName.StartsWith("\""))
                        csvName = $"\"{csvName}\"";

                    var cleanPrefix = prefix.Replace("×", "_");

					sw.WriteLine($"\"{cleanPrefix}\",Text,,{csvName},");
                }
            }
        }
    }

    [BepInPlugin("horse.coder.com3d2.tlextract", "Translation Extractor", PluginInfo.PLUGIN_VERSION)]
    public class TranslationExtract : BaseUnityPlugin
    {
        public const string TL_DIR = "COM3D2_Localisation";
        private const int WIDTH = 200;
        private const int HEIGHT = 400;
        private const int MARGIN_X = 5;
        private const int MARGIN_TOP = 20;
        private const int MARGIN_BOTTOM = 5;

        private static readonly Regex textPattern = new("text=\"(?<text>.*)\"");
        private static readonly Regex namePattern = new("name=(?<name>.*)");
        private static readonly Encoding UTF8 = new UTF8Encoding(true);

        private static readonly Dictionary<string, string> NpcNames = new();


        private readonly HashSet<string> filesToSkip = new(StringComparer.InvariantCultureIgnoreCase);

        private readonly DumpOptions options = new();

        private GUIStyle bold;
        private bool displayGui;
        private bool dumping;

        private int translatedLines;

        private void Awake()
        {
            DontDestroyOnLoad(this);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.D))
                displayGui = !displayGui;
        }

        private void OnGUI()
        {
            if (!displayGui)
                return;
            if (bold == null)
                bold = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };


            void Toggle(string text, ref bool toggle)
            {
                toggle = GUILayout.Toggle(toggle, text);
            }

            void Window(int id)
            {
                GUILayout.BeginArea(new Rect(MARGIN_X, MARGIN_TOP, WIDTH - MARGIN_X * 2,
                                             HEIGHT                      - MARGIN_TOP - MARGIN_BOTTOM));
                {
                    GUILayout.BeginVertical();
                    {
                        GUILayout.Label("Refer to the README on how to use the tool!\n\n", bold);
                        GUILayout.Label("Base dumps");
                        Toggle("Story scripts", ref options.dumpScripts);
                        Toggle("UI translations", ref options.dumpUITranslations);

                        GUILayout.Label("Advanced dumps");
                        Toggle(".menu item names", ref options.dumpItemNames);
                        Toggle("VIP event names", ref options.dumpVIPEvents);
                        Toggle("Yotogi skills", ref options.dumpYotogis);
                        Toggle("Maid stats", ref options.dumpPersonalies);
                        Toggle("Event names", ref options.dumpEvents);

                        GUILayout.Label("Other");
                        Toggle("Skip translated items", ref options.skipTranslatedItems);

                        GUI.enabled = !dumping;
                        if (GUILayout.Button("Dump!"))
                        {
                            dumping = true;
                            StartCoroutine(DumpGame());
                        }

                        GUI.enabled = true;
                    }
                    GUILayout.EndVertical();
                }
                GUILayout.EndArea();
            }

            GUI.Window(6969, new Rect(Screen.width - WIDTH, (Screen.height - HEIGHT) / 2f, WIDTH, HEIGHT), Window,
                       "TranslationExtract");
        }

        private static void DumpI2Translations(LanguageSource src)
        {
            var i2Path = Path.Combine(TL_DIR, "UI");
            var sourcePath = Path.Combine(i2Path, src.name);
            if (!Directory.Exists(sourcePath))
                Directory.CreateDirectory(sourcePath);
            var categories = src.GetCategories(true);
            foreach (var category in categories)
            {
                var path = Path.Combine(sourcePath, $"{category}.csv");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, src.Export_CSV(category), UTF8);
            }
        }

        private IEnumerator DumpGame()
        {
            var opts = new DumpOptions(options);
            yield return null;
            Dump(opts);
            dumping = false;
        }

        private void DumpUI()
        {
            Debug.Log("Dumping UI localisation");

            var langs = LocalizationManager.GetAllLanguages();
            Debug.Log($"Currently {langs.Count} languages are known");
            foreach (var language in langs)
                Debug.Log($"{language}");

            Debug.Log($"Currently selected language is {LocalizationManager.CurrentLanguage}");
            Debug.Log($"There are {LocalizationManager.Sources.Count} language sources");

            foreach (var languageSource in LocalizationManager.Sources)
            {
                Debug.Log(
                          $"Dumping {languageSource.name} with languages: {string.Join(",", languageSource.mLanguages.Select(d => d.Name).ToArray())}. GSheets: {languageSource.HasGoogleSpreadsheet()}");
                DumpI2Translations(languageSource);
            }
        }

        private KeyValuePair<string, string> SplitTranslation(string txt)
        {
            int pos;
            if ((pos = txt.IndexOf("<e>", StringComparison.InvariantCultureIgnoreCase)) > 0)
            {
                translatedLines++;
                var orig = txt.Substring(0, pos).Trim();
                var tl = txt.Substring(pos + 3).Replace("…", "...").Trim();
                return new KeyValuePair<string, string>(orig, tl);
            }

            return new KeyValuePair<string, string>(txt.Trim(), string.Empty);
        }

        private static Dictionary<string, string> ParseTag(string line)
        {
            var result = new Dictionary<string, string>();
            var valueSb = new StringBuilder();
            var keySb = new StringBuilder();
            var captureValue = false;
            var quoted = false;
            var escapeNext = false;

            foreach (var c in line)
                if (captureValue)
                {
                    if (valueSb.Length == 0 && c == '"')
                    {
                        quoted = true;
                        continue;
                    }

                    if (escapeNext)
                    {
                        escapeNext = false;
                        valueSb.Append(c);
                        continue;
                    }

                    if (c == '\\')
                        escapeNext = true;

                    if (!quoted && char.IsWhiteSpace(c) || quoted && !escapeNext && c == '"')
                    {
                        quoted = false;
                        result[keySb.ToString()] = valueSb.ToString();
                        keySb.Length = 0;
                        valueSb.Length = 0;
                        captureValue = false;
                        continue;
                    }

                    valueSb.Append(c);
                }
                else
                {
                    if (keySb.Length == 0 && char.IsWhiteSpace(c))
                        continue;

                    if (char.IsWhiteSpace(c) && keySb.Length != 0)
                    {
                        result[keySb.ToString()] = "true";
                        keySb.Length = 0;
                        continue;
                    }

                    if (c == '=')
                    {
                        captureValue = true;
                        continue;
                    }

                    keySb.Append(c);
                }

            if (keySb.Length != 0)
                result[keySb.ToString()] = valueSb.Length == 0 ? "true" : valueSb.ToString();

            return result;
        }

        private static T GetOrDefault<T>(Dictionary<string, T> dic, string key, T def)
        {
            return dic.TryGetValue(key, out var val) ? val : def;
        }

        private void ExtractTranslations(string fileName, string script)
        {
            var tlDir = Path.Combine(TL_DIR, "Script");
            var dir = Path.Combine(tlDir, Path.GetDirectoryName(fileName));
            var name = Path.GetFileNameWithoutExtension(fileName);

            if (filesToSkip.Contains(name))
                return;

            Directory.CreateDirectory(dir);

            var lineList = new HashSet<string>();
            var lines = script.Split('\n');

            var sb = new StringBuilder();
            var captureTalk = false;
            var captureSubtitlePlay = false;
            SubtitleData subData = null;

            var captureSubtitlesList = new List<SubtitleData>();

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (trimmedLine.Length == 0)
                    continue;

                if (trimmedLine.StartsWith("@LoadSubtitleFile", StringComparison.InvariantCultureIgnoreCase))
                {
                    var sub = ParseTag(trimmedLine.Substring("@LoadSubtitleFile".Length));
                    var subFileName = sub["file"];

                    filesToSkip.Add(subFileName);

                    using var f = GameUty.FileOpen($"{subFileName}.ks");
                    var parseTalk = false;
                    string[] talkTiming = null;
                    var subSb = new StringBuilder();
                    foreach (var subLine in NUty.SjisToUnicode(f.ReadAll()).Split('\n').Select(s => s.Trim())
                                                .Where(s => s.Length != 0))
                        if (subLine.StartsWith("@talk", StringComparison.InvariantCultureIgnoreCase))
                        {
                            talkTiming = subLine.Substring("@talk".Length).Trim('[', ']', ' ').Split('-');
                            parseTalk = true;
                        }
                        else if (subLine.StartsWith("@hitret", StringComparison.InvariantCultureIgnoreCase) &&
                                 parseTalk)
                        {
                            parseTalk = false;
                            var startTime = int.Parse(talkTiming[0]);
                            var endTime = int.Parse(talkTiming[1]);
                            var parts = SplitTranslation(subSb.ToString());
                            captureSubtitlesList.Add(new SubtitleData
                            {
                                original = parts.Key,
                                translation = parts.Value,
                                startTime = startTime,
                                displayTime = endTime - startTime
                            });
                            subSb.Length = 0;
                            talkTiming = null;
                        }
                        else
                        {
                            subSb.Append(subLine);
                        }
                }
                else if (trimmedLine.StartsWith("@SubtitleDisplayForPlayVoice",
                                                StringComparison.InvariantCultureIgnoreCase))
                {
                    captureSubtitlePlay = true;
                    var sub = ParseTag(trimmedLine.Substring("@SubtitleDisplayForPlayVoice".Length));
                    var text = SplitTranslation(sub["text"]);
                    subData = new SubtitleData
                    {
                        addDisplayTime = int.Parse(GetOrDefault(sub, "addtime", "0")),
                        displayTime = int.Parse(GetOrDefault(sub, "wait", "-1")),
                        original = text.Key,
                        translation = text.Value,
                        isCasino = sub.ContainsKey("mode_c")
                    };
                }
                else if (trimmedLine.StartsWith("@PlayVoice", StringComparison.InvariantCultureIgnoreCase))
                {
                    if (captureSubtitlePlay)
                    {
                        captureSubtitlePlay = false;
                        var data = ParseTag(trimmedLine.Substring("@PlayVoice".Length));
                        if (!data.TryGetValue("voice", out var voiceName))
                        {
                            subData = null;
                            continue;
                        }

                        subData.voice = voiceName;
                        lineList.Add($"@VoiceSubtitle{JsonUtility.ToJson(subData, false)}");
                        subData = null;
                    }
                    else if (captureSubtitlesList.Count > 0)
                    {
                        var subTl = captureSubtitlesList[0];
                        captureSubtitlesList.RemoveAt(0);

                        var data = ParseTag(trimmedLine.Substring("@PlayVoice".Length));
                        if (!data.TryGetValue("voice", out var voiceName))
                            continue;

                        subTl.voice = voiceName;
                        lineList.Add($"@VoiceSubtitle{JsonUtility.ToJson(subTl, false)}");
                    }
                }
                else if (trimmedLine.StartsWith("@talk", StringComparison.InvariantCultureIgnoreCase))
                {
                    captureTalk = true;
                    var match = namePattern.Match(trimmedLine);
                    if (match.Success)
                    {
                        var m = match.Groups["name"];
                        var parts = SplitTranslation(m.Value.Trim('\"'));
                        if (parts.Key.StartsWith("[HF", StringComparison.InvariantCulture) ||
                            parts.Key.StartsWith("[SF", StringComparison.InvariantCulture))
                            continue;
                        NpcNames[parts.Key] = parts.Value;
                    }
                }
                else if (captureTalk)
                {
                    if (trimmedLine.StartsWith("@", StringComparison.InvariantCultureIgnoreCase))
                    {
                        captureTalk = false;
                        var parts = SplitTranslation(sb.ToString());
                        sb.Length = 0;
                        lineList.Add($"{parts.Key}\t{parts.Value}");
                        continue;
                    }

                    sb.Append(trimmedLine);
                }
                else if (trimmedLine.StartsWith("@ChoicesSet", StringComparison.InvariantCultureIgnoreCase))
                {
                    var match = textPattern.Match(trimmedLine);
                    if (!match.Success)
                    {
                        Debug.Log($"[WARNING] Failed to extract line from \"{trimmedLine}\"");
                        continue;
                    }

                    var m = match.Groups["text"];
                    var parts = SplitTranslation(m.Value);
                    lineList.Add($"{parts.Key}\t{parts.Value}");
                }
            }

            if (lineList.Count != 0)
                File.WriteAllLines(Path.Combine(dir, $"{name}.txt"), lineList.ToArray(), UTF8);
        }

        private void DumpScripts()
        {
            Debug.Log("Dumping game script translations...");
            Debug.Log("Getting all script files...");
            var scripts = GameUty.FileSystem.GetFileListAtExtension(".ks");
            Debug.Log($"Found {scripts.Length} scripts!");

            foreach (var scriptFile in scripts)
            {
                using var f = GameUty.FileOpen(scriptFile);
                var script = NUty.SjisToUnicode(f.ReadAll());
                Debug.Log(scriptFile);
                ExtractTranslations(scriptFile, script);
            }

            var tlDir = Path.Combine(TL_DIR, "Script");
            var namesFile = Path.Combine(tlDir, "__npc_names.txt");
            File.WriteAllLines(namesFile, NpcNames.Select(n => $"{n.Key}\t{n.Value}").ToArray(), UTF8);
            NpcNames.Clear();
            filesToSkip.Clear();
        }

        private void DumpSchedule(DumpOptions opts)
        {
            var i2Path = Path.Combine(TL_DIR, "UI");
            var unitPath = Path.Combine(i2Path, "zzz_schedule");
            Directory.CreateDirectory(unitPath);

            Debug.Log("Getting schedule.");

            void WriteSimpleData(string file, IList<(int, string)> columnIndexName, StreamWriter sw)
            {
				sw.WriteCSV(file, "SceneDaily",
                    (parser, i) =>
                    {
                        var columnText = columnIndexName
                                        .Select(column => (column.Item2, parser.GetCellAsString(column.Item1, i)))
                                        .ToList();

                        var translationData = new
                        {
                            id = parser.GetCellAsInteger(0, i),
                            columnText = columnText
					    };

                        return translationData;
                    },
                    arg =>
                    {
                        var keys = new string[arg.columnText.Count()];
                        var index = 0;
                        foreach (var columnTranslation in arg.columnText)
                        {
                            keys[index++] = columnTranslation.Item1 + columnTranslation.Item2;
					    }
                        return keys;
                    },
                    arg => arg.columnText.Select(r => r.Item2), 
                    opts.skipTranslatedItems);
			}

            var encoding = new UTF8Encoding(true);
            using (var sw = new StreamWriter(Path.Combine(unitPath, "SceneDaily.csv"), false, encoding))
            {
                sw.WriteLine("Key,Type,Desc,Japanese,English");
                WriteSimpleData("schedule_work_night.nei", new[]
                {
                    (1, "スケジュール/カテゴリー/"),
                    //(7, "説明"),
                    (12, "スケジュール/項目/"),
                    (13, "スケジュール/項目/"),
                    (14, "スケジュール/項目/"),
                    (15, "スケジュール/項目/"),
                    (16, "スケジュール/項目/"),
                    (17, "スケジュール/項目/"),
                    (18, "スケジュール/項目/"),
                    (19, "スケジュール/項目/"),
                    (20, "スケジュール/項目/"),
                    //(24,"実行条件：メイドクラス（取得している）"),
                    //(25,"実行条件：所持性癖")

                }, sw);
                
				WriteSimpleData("schedule_work_noon.nei", new[]
                {

                    (1, "スケジュール/項目/")

                }, sw);
                WriteSimpleData("schedule_work_night_category_list.nei", new[]
                {
                    (1, "スケジュール/カテゴリー/")
                }, sw);
			}
        }

		private void DumpScenarioEvents(DumpOptions opts)
        {
            var i2Path = Path.Combine(TL_DIR, "UI");
            var unitPath = Path.Combine(i2Path, "zzz_scenario_events");
            Directory.CreateDirectory(unitPath);

            Debug.Log("Getting scenario event data");

            var encoding = new UTF8Encoding(true);
            using var sw = new StreamWriter(Path.Combine(unitPath, "SceneScenarioSelect.csv"), false, encoding);

            sw.WriteLine("Key,Type,Desc,Japanese,English");
            sw.WriteCSV("select_scenario_data.nei", "SceneScenarioSelect",
                        (parser, i) => new
                        {
                            id = parser.GetCellAsInteger(0, i),
                            name = parser.GetCellAsString(1, i),
                            description = parser.GetCellAsString(2, i)
                        },
                        arg => new[] { $"{arg.id}/タイトル", $"{arg.id}/内容" },
                        arg => new[] { arg.name, arg.description });
        }

        private void DumpItemNames(DumpOptions opts)
        {
            var i2Path = Path.Combine(TL_DIR, "UI");
            var unitPath = Path.Combine(i2Path, "zzz_item_names");
            Directory.CreateDirectory(unitPath);

            var encoding = new UTF8Encoding(true);
            Debug.Log("Getting all .menu files (this might take a moment)...");
            var menus = GameUty.FileSystem.GetFileListAtExtension(".menu");

            Debug.Log($"Found {menus.Length} menus!");

            var swDict = new Dictionary<string, StreamWriter>();

            foreach (var menu in menus)
            {
                using var f = GameUty.FileOpen(menu);
                using var br = new BinaryReader(new MemoryStream(f.ReadAll()));
                Debug.Log(menu);

                br.ReadString();
                br.ReadInt32();
                br.ReadString();
                var filename = Path.GetFileNameWithoutExtension(menu);
                var name = br.ReadString();
                var category = br.ReadString().ToLowerInvariant();
                var info = br.ReadString();

                if (!swDict.TryGetValue(category, out var sw))
                {
                    swDict[category] =
                        sw = new StreamWriter(Path.Combine(unitPath, $"{category}.csv"), false, encoding);
                    sw.WriteLine("Key,Type,Desc,Japanese,English");
                }

                if (opts.skipTranslatedItems &&
                    LocalizationManager.TryGetTranslation($"{category}/{filename}|name", out var _))
                    continue;
                sw.WriteLine($"{filename}|name,Text,,{EscapeCSVItem(name)},{EscapeCSVItem(name)}");
                sw.WriteLine($"{filename}|info,Text,,{EscapeCSVItem(info)},{EscapeCSVItem(info)}");
            }

            foreach (var keyValuePair in swDict)
                keyValuePair.Value.Dispose();
        }

        private void DumpPersonalityNames(DumpOptions opts)
        {
            var i2Path = Path.Combine(TL_DIR, "UI");
            var unitPath = Path.Combine(i2Path, "zzz_personalities");
            Directory.CreateDirectory(unitPath);

            Debug.Log("Getting personality names");

            void WriteSimpleData(string file, string prefix, StreamWriter sw, int dataCol = 2, int idCol = 1)
            {
                sw.WriteCSV(file, "MaidStatus", (parser, i) => new
                            {
                                uniqueName = parser.GetCellAsString(idCol, i),
                                displayName = parser.GetCellAsString(dataCol, i)
                            },
                            arg => new[] { $"{prefix}/{arg.uniqueName}" },
                            arg => new[] { arg.displayName },
                            opts.skipTranslatedItems);
            }

            var encoding = new UTF8Encoding(true);
            using (var sw = new StreamWriter(Path.Combine(unitPath, "MaidStatus.csv"), false, encoding))
            {
                sw.WriteLine("Key,Type,Desc,Japanese,English");

                WriteSimpleData("maid_status_personal_list.nei", "性格タイプ", sw);

                WriteSimpleData("maid_status_yotogiclass_list.nei", "夜伽クラス", sw);
                WriteSimpleData("maid_status_yotogiclass_list.nei", "夜伽クラス", sw);

                WriteSimpleData("maid_status_jobclass_list.nei", "ジョブクラス", sw);
                WriteSimpleData("maid_status_jobclass_list.nei", "ジョブクラス/説明", sw, 4);

                WriteSimpleData("maid_status_title_list.nei", "ステータス称号", sw, 0, 0);

                WriteSimpleData("maid_status_feature_list.nei", "特徴タイプ", sw, 1);
            }
        }

        private void DumpYotogiData(DumpOptions opts)
        {
            var i2Path = Path.Combine(TL_DIR, "UI");
            var unitPath = Path.Combine(i2Path, "zzz_yotogi");
            Directory.CreateDirectory(unitPath);

            Debug.Log("Getting yotogi skills and commands");

            var encoding = new UTF8Encoding(true);
            using (var sw = new StreamWriter(Path.Combine(unitPath, "YotogiSkillName.csv"), false, encoding))
            {
                sw.WriteLine("Key,Type,Desc,Japanese,English");
                sw.WriteCSV("yotogi_skill_list.nei", "YotogiSkillName",
                            (parser, i) => new
                            {
                                skillName = parser.GetCellAsString(4, i)
                            },
                            arg => new[] { arg.skillName },
                            arg => new[] { arg.skillName },
                            opts.skipTranslatedItems);
            }

            var commandNames = new HashSet<string>();
            using (var sw = new StreamWriter(Path.Combine(unitPath, "YotogiSkillCommand.csv"), false, encoding))
            {
                using var f = GameUty.FileOpen("yotogi_skill_command_data.nei");
                using var scenarioNei = new CsvParser();
                sw.WriteLine("Key,Type,Desc,Japanese,English");
                scenarioNei.Open(f);

                for (var i = 0; i < scenarioNei.max_cell_y; i++)
                {
                    if (!scenarioNei.IsCellToExistData(2, i))
                    {
                        i += 2;
                        continue;
                    }

                    var commandName = scenarioNei.GetCellAsString(2, i);

                    if (opts.skipTranslatedItems &&
                        LocalizationManager.TryGetTranslation($"YotogiSkillCommand/{commandName}", out var _))
                        continue;
                    

                    if (commandNames.Contains(commandName))
                        continue;

                    commandNames.Add(commandName);

                    var csvName = EscapeCSVItem(commandName);
                    sw.WriteLine($"{csvName},Text,,{csvName},");
                }
            }
        }

        private void DumpVIPEvents(DumpOptions opts)
        {
            var i2Path = Path.Combine(TL_DIR, "UI");
            var unitPath = Path.Combine(i2Path, "zzz_vip_event");
            Directory.CreateDirectory(unitPath);

            Debug.Log("Getting VIP event names");

            var encoding = new UTF8Encoding(true);
            using var sw = new StreamWriter(Path.Combine(unitPath, "SceneDaily.csv"), false, encoding);

            sw.WriteLine("Key,Type,Desc,Japanese,English");
            sw.WriteCSV("schedule_work_night.nei", "SceneDaily", (parser, i) => new
                        {
                            vipName = parser.GetCellAsString(1, i),
                            vipDescription = parser.GetCellAsString(7, i)
                        },
                        arg => new[] { $"スケジュール/項目/{arg.vipName}", $"スケジュール/説明/{arg.vipDescription}" },
                        arg => new[] { arg.vipName, arg.vipDescription },
                        opts.skipTranslatedItems);
        }

        private string EscapeCSVItem(string str)
        {
            if (str.Contains("\n") || str.Contains("\"") || str.Contains(","))
                return $"\"{str.Replace("\"", "\"\"")}\"";
            return str;
        }

        private void Dump(DumpOptions opts)
        {
            Debug.Log("Dumping game localisation files! Please be patient!");

            if (opts.dumpUITranslations)
            {
                DumpUI();
                DumpSchedule(opts);
            }

            if (opts.dumpScripts)
                DumpScripts();

            if (opts.dumpItemNames)
                DumpItemNames(opts);

            if (opts.dumpEvents)
            {
                DumpScenarioEvents(opts);
                //DumpScheduleWorkCategory(opts);
            }

            if (opts.dumpPersonalies)
                DumpPersonalityNames(opts);

            if (opts.dumpYotogis)
                DumpYotogiData(opts);

            if (opts.dumpVIPEvents)
                DumpVIPEvents(opts);

            if (opts.dumpScripts)
                Debug.Log($"Dumped {translatedLines} lines");
            Debug.Log($"Done! Dumped translations are located in {TL_DIR}. You can now close the game!");
            Debug.Log("IMPORTANT: Delete this plugin (TranslationExtract.dll) if you want to play the game normally!");
        }

        [Serializable]
        internal class SubtitleData
        {
            public int addDisplayTime;
            public int displayTime = -1;
            public bool isCasino;
            public string original = string.Empty;
            public int startTime;
            public string translation = string.Empty;
            public string voice = string.Empty;
        }

        private class DumpOptions
        {
            public bool dumpEvents;
            public bool dumpItemNames;
            public bool dumpPersonalies;
            public bool dumpScripts = true;
            public bool dumpUITranslations = true;
            public bool dumpVIPEvents;
            public bool dumpYotogis;
            public bool skipTranslatedItems;
            public DumpOptions() { }

            public DumpOptions(DumpOptions other)
            {
                dumpScripts = other.dumpScripts;
                dumpUITranslations = other.dumpUITranslations;
                dumpItemNames = other.dumpItemNames;
                dumpVIPEvents = other.dumpVIPEvents;
                dumpYotogis = other.dumpYotogis;
                dumpPersonalies = other.dumpPersonalies;
                dumpEvents = other.dumpEvents;
                skipTranslatedItems = other.skipTranslatedItems;
            }
        }
    }
}
