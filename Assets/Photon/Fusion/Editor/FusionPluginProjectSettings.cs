namespace Fusion.Editor {
  using System;
  using System.Collections.Generic;
  using System.IO;
  using System.Linq;
  using System.Text.RegularExpressions;
  using System.Xml.Linq;
  using UnityEditor;
  using UnityEngine;
  using Debug = UnityEngine.Debug;

  [CreateAssetMenu(menuName = "Fusion/Plugin Project Settings")]
  public partial class FusionPluginProjectSettings : FusionScriptableObject {

    public const string IncludeLabel = "FusionPluginInclude";
    public const string PartialProjectName = "Fusion.Plugin.Custom.csproj.include";
    public const string NetworkTypesFileName = "PluginNetworkTypes.Gen.cs";
    public const string NetworkDBFileName = "PluginNetworkObjectDB.json";
    private const string ExportDBFileMarker = "Temp/FusionPluginExportDB";

    /// <summary>
    /// Supports environment variables (%Name%) and relative paths.
    /// </summary>
    [Header("Custom Plugin Root Folder")] 
    public string OutputPath = "%FUSION_PLUGIN_SDK_PATH%/Fusion.Plugin.Custom";

    /// <summary>
    /// Assemblies to be excluded when exporting network types. Use * as a wildcard."
    /// </summary>
    [Header("Code Export Settings")]
    [InlineHelp]
    public List<string> IgnoredAssemblies = new List<string> { "*.Tests" };

    /// <summary>
    /// Additional files and folders can either be marked with FusionPluginInclude label or included here. They will
    /// be added to the project file.
    /// </summary>
    [InlineHelp]
    public string[] IncludePaths = Array.Empty<string>();


    [EditorButton("Export Code")]
    public void ExportCode() {
      try {
        if (EditorUtility.DisplayCancelableProgressBar("Exporting Fusion Plugin", "Exporting user types...", 0.1f)) {
          return;
        }

        // first, export user types as a singular .cs file
        var exporter = new FusionPluginNetworkTypesExporter(FusionPluginNetworkTypesExporter.Options.AddJsonNETAttributes);

        var filters = IgnoredAssemblies
          .Select(x => Regex.Escape(x).Replace("\\*", ".*?"))
          .Select(x => new Regex(x)).ToArray();

        // expand environment variables
        var expandedOutputPath = Environment.ExpandEnvironmentVariables(OutputPath);

        var exportedTypesContent = exporter.Export(x => !filters.Any(f => f.IsMatch(x.FullName)));
        var userTypesPath = Path.Combine(expandedOutputPath, NetworkTypesFileName);
        File.WriteAllText(userTypesPath, exportedTypesContent);
        FusionEditorLog.Log($"Exported user types to {userTypesPath}");

        if (EditorUtility.DisplayCancelableProgressBar("Exporting Fusion Plugin", "Exporting project...", 0.5f)) {
          return;
        }

        // now export the project file
        var projectFilePath = Path.Combine(expandedOutputPath, PartialProjectName);
        ExportProject(Path.Combine(expandedOutputPath, PartialProjectName), IncludeLabel, IncludePaths);
        FusionEditorLog.Log($"Exported partial project file to {projectFilePath}");
      } finally {
        EditorUtility.ClearProgressBar();
      }
    }

    public static void ExportProject(string outputPath, string includeLabel, IEnumerable<string> includePaths) {
      var includes = new List<string>();

      // add any file marked with a label
      if (!string.IsNullOrEmpty(includeLabel)) {
        var labeledAssets = AssetDatabase.FindAssets($"l:{IncludeLabel}")
          .Select(AssetDatabase.GUIDToAssetPath);
        includes.AddRange(labeledAssets);
      }

      // now remove duplicates and files otherwise included explicitly
      includes = includes.Distinct().ToList();

      // add explicit paths
      foreach (var path in includePaths) {
        if (Directory.Exists(path)) {
          // remove any path that starts with this folder
          var dirWithTrailingSlash = path.TrimEnd('/') + "/";
          includes.RemoveAll(x => x.StartsWith(dirWithTrailingSlash));
        }

        includes.Add(path);
      }

      // turn current path into path relative to outputPath
      var contents = GenerateCsProjInclude(
        Path.GetRelativePath(Path.GetDirectoryName(outputPath), Environment.CurrentDirectory),
        includes.ToArray()
      );

      File.WriteAllText(outputPath, contents);
    }

    static string GenerateCsProjInclude(string pathPrefix, string[] includes) {

      pathPrefix = PathUtils.Normalize(pathPrefix);
      var projectElement = new XElement("Project");

      projectElement.Add(new XComment("Properties"));
      var properties = new XElement("PropertyGroup");
      projectElement.Add(properties);

      projectElement.Add(new XComment("Includes"));

      var group = new XElement("ItemGroup");

      foreach (var source in includes.Select(x => Environment.ExpandEnvironmentVariables(x))) {
        if (Directory.Exists(source)) {
          group.Add(new XElement("Compile",
            new XAttribute("Include", $"{pathPrefix}/{source}/**/*.cs"),
            new XAttribute("LinkBase", CreateLinkName(source))));
        } else {
          group.Add(new XElement(Path.GetExtension(source) == ".cs" ? "Compile" : "None",
            new XAttribute("Include", $"{pathPrefix}/{source}"),
            new XElement("Link", CreateLinkName(source)))
          );
        }
      }

      projectElement.Add(group);

      var document = new XDocument(projectElement);
      return document.ToString();

      string CreateLinkName(string path) {
        // first of all, drop the Assets/ prefix, if present
        if (path.StartsWith("Assets/")) {
          path = path.Substring("Assets/".Length);
        }

        // now get rid of all "/../" as these will not work
        var parts = path.Split('/');

        for (int i = 0; i < parts.Length; ++i) {
          if (parts[i] != "..") {
            continue;
          }

          if (i == 0) {
            // just remove
            ArrayUtility.RemoveAt(ref parts, 0);
            i -= 1;
          } else {
            // remove this and the previous
            ArrayUtility.RemoveAt(ref parts, i);
            ArrayUtility.RemoveAt(ref parts, i - 1);
            i -= 2;
          }
        }

        return string.Join("/", parts);
      }
    }


    [EditorButton("Export Prefabs & Scenes")]
    public void ExportPrefabsAndScenes() {
      var expandedOutputPath = Environment.ExpandEnvironmentVariables(OutputPath);
      var dbPath = Path.Combine(expandedOutputPath, NetworkDBFileName);
      FusionNetworkObjectDBExporter.ExportNetworkObjectDB(dbPath);
      FusionEditorLog.Log($"Exported user types to {dbPath}");
    }
  }
}
