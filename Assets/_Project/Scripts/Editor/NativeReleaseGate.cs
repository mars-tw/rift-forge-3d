using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace RiftForge.Editor
{
    /// <summary>
    /// Rejects mobile release builds while the source is still a foundation.
    /// Configuration is an explicit release prerequisite, not proof of gameplay or device testing.
    /// </summary>
    public static class NativeReleaseGate
    {
        public const string MainScenePath = "Assets/_Project/Scenes/Main.unity";
        public const string ConfigRelativePath = "native/build-config.json";

        /// <summary>Development and TestRunner players remain outside this release gate.</summary>
        public static bool RequiresReleaseValidation(BuildTarget target, BuildOptions options)
        {
            bool mobile = target == BuildTarget.Android || target == BuildTarget.iOS;
            // The installed TestRunner uses Development together with IncludeTestAssemblies.
            // IncludeTestAssemblies alone must not exempt a non-development release.
            bool development = (options & BuildOptions.Development) != 0;
            return mobile && !development;
        }

        /// <summary>Runs before NativeExport mutates any output folder or player setting.</summary>
        public static void ValidateCurrentProject()
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            ValidateConfigFile(Path.Combine(projectRoot, ConfigRelativePath), scenePath =>
                File.Exists(Path.Combine(projectRoot, scenePath))
                && AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) != null);
        }

        /// <summary>Reads configuration without changing project files or build settings.</summary>
        public static void ValidateConfigFile(string configPath, Func<string, bool> isValidScene)
        {
            if (!File.Exists(configPath))
            {
                throw new BuildFailedException("Mobile release blocked: native/build-config.json is missing.");
            }

            string json;
            try
            {
                json = File.ReadAllText(configPath);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                throw new BuildFailedException("Mobile release blocked: native/build-config.json cannot be read.");
            }

            ValidateConfig(json, isValidScene);
        }

        /// <summary>Validates the release contract; the scene predicate must check the real asset.</summary>
        public static void ValidateConfig(string json, Func<string, bool> isValidScene)
        {
            if (isValidScene == null)
            {
                throw new ArgumentNullException(nameof(isValidScene));
            }

            XmlDocument document;
            try
            {
                string trimmed = json?.Trim();
                if (string.IsNullOrEmpty(trimmed) || !trimmed.StartsWith("{", StringComparison.Ordinal)
                    || !trimmed.EndsWith("}", StringComparison.Ordinal))
                {
                    throw new ArgumentException("Expected a JSON object.");
                }

                RequireSingleRootObject(trimmed);

                document = new XmlDocument { XmlResolver = null };
                using (XmlDictionaryReader reader = JsonReaderWriterFactory.CreateJsonReader(
                    Encoding.UTF8.GetBytes(trimmed), XmlDictionaryReaderQuotas.Max))
                {
                    document.Load(reader);
                    // Consume the complete document, rejecting extra root content as malformed.
                    if (reader.Read())
                    {
                        throw new XmlException("Unexpected trailing JSON content.");
                    }
                }
            }
            catch (Exception error) when (error is ArgumentException || error is XmlException)
            {
                throw new BuildFailedException("Mobile release blocked: native/build-config.json is malformed.");
            }

            XmlElement root = document.DocumentElement;
            if (root == null || root.GetAttribute("type") != "object")
            {
                throw new BuildFailedException("Mobile release blocked: configuration must be a JSON object.");
            }

            string schema = RequiredScalar(root, "schemaVersion", "number");
            string sourceStage = RequiredScalar(root, "sourceStage", "string");
            string ready = RequiredScalar(root, "readyForStore", "boolean");
            string mainScene = RequiredScalar(root, "mainScene", "string");

            if (schema != "1")
            {
                throw new BuildFailedException("Mobile release blocked: schemaVersion must be 1.");
            }

            if (!string.Equals(sourceStage, "complete-game", StringComparison.Ordinal))
            {
                throw new BuildFailedException("Mobile release blocked: sourceStage must be complete-game; foundation scenes are not release content.");
            }

            if (ready != "true")
            {
                throw new BuildFailedException("Mobile release blocked: readyForStore must be true after release validation.");
            }

            if (!string.Equals(mainScene, MainScenePath, StringComparison.Ordinal))
            {
                throw new BuildFailedException("Mobile release blocked: mainScene must be exactly " + MainScenePath + ". SampleScene is not a substitute.");
            }

            if (!isValidScene(MainScenePath))
            {
                throw new BuildFailedException("Mobile release blocked: the real Main.unity scene asset is missing or invalid.");
            }
        }

        private static string RequiredScalar(XmlElement root, string name, string jsonType)
        {
            XmlElement found = null;
            foreach (XmlNode node in root.ChildNodes)
            {
                if (!(node is XmlElement member))
                    continue;
                // JsonReader maps escaped/invalid XML property names to an item element.
                // Its item attribute contains the decoded JSON key, including escaped duplicates.
                string memberName = member.NamespaceURI.Length == 0 ? member.LocalName
                    : member.NamespaceURI == "item" && member.LocalName == "item" ? member.GetAttribute("item")
                    : null;
                if (memberName != name)
                    continue;
                if (found != null)
                    throw new BuildFailedException("Mobile release blocked: duplicate " + name + " field.");
                found = member;
            }

            if (found == null || found.GetAttribute("type") != jsonType)
                throw new BuildFailedException("Mobile release blocked: " + name + " must be a JSON " + jsonType + " field.");
            return found.InnerText;
        }

        // JsonReader may silently ignore a second root. This checks only the full
        // document envelope; actual JSON syntax and scalar types are checked above.
        private static void RequireSingleRootObject(string json)
        {
            var delimiters = new Stack<char>();
            bool inString = false;
            bool escaped = false;
            for (int i = 0; i < json.Length; i++)
            {
                char current = json[i];
                if (inString)
                {
                    if (escaped) escaped = false;
                    else if (current == '\\') escaped = true;
                    else if (current == '"') inString = false;
                    continue;
                }
                if (current == '"') { inString = true; continue; }
                if (current == '{' || current == '[') { delimiters.Push(current); continue; }
                if (current != '}' && current != ']') continue;
                if (delimiters.Count == 0 || delimiters.Pop() != (current == '}' ? '{' : '['))
                    throw new ArgumentException("Mismatched JSON delimiters.");
                if (delimiters.Count == 0 && i != json.Length - 1)
                    throw new ArgumentException("Unexpected content after the JSON root.");
            }
            if (inString || escaped || delimiters.Count != 0)
                throw new ArgumentException("Incomplete JSON document.");
        }
    }
}
