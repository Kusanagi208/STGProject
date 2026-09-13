using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GenjitsuLAB.Data.Editor
{
    /// <summary>Rejects invalid authored keys and references before a player build.</summary>
    public sealed class DatabaseBuildValidation : IPreprocessBuildWithReport
    {
        /// <inheritdoc/>
        public int callbackOrder => 0;
        /// <inheritdoc/>
        public void OnPreprocessBuild(BuildReport report)
        {
            DatabaseEditorUtils.Invalidate();
            List<string> errors = DatabaseEditorUtils.Validate();
            if (errors.Count > 0)
            {
                throw new BuildFailedException(string.Join("\n", errors));
            }
        }

        /// <summary>Builds a standalone sample scene into the isolated validation project's output folder.</summary>
        public static void BuildValidationPlayer()
        {
            string scene = "Assets/STG/Scene/Main.unity";
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { scene },
                locationPathName = Path.GetFullPath("DatabaseValidationBuild/STG.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException("Database validation player build failed: " + report.summary.result);
            }
        }
    }
}
