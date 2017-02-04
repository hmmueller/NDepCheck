﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace NDepCheck.MSBuild {
    public class NDepCheck : Task {
        public string DotFilename { get; set; }
        public bool ShowTransitiveEdges { get; set; }
        public bool ShowUnusedQuestionableRules { get; set; }
        public ITaskItem DefaultRuleSet { get; set; }
        public int MaxCpuCount { get; set; }
        public bool Debug { get; set; }
        public bool Verbose { get; set; }
        public string XmlOutput { get; set; }
        public bool CheckOnlyAssemblyDependencies { get; set; }

        [Required]
        public ITaskItem[] Assemblies { get; set; }

        public ITaskItem[] Directories { get; set; }

        [Output]
        public int ExitCode { get; set; }

        public override bool Execute() {
            var logger = new MsBuildLogger(Log);
            global::NDepCheck.Log.Logger = logger;
            global::NDepCheck.Log.IsChattyEnabled = false;
            global::NDepCheck.Log.IsDebugEnabled = Debug;

            var args = new List<string>();

            var options = new Options {
                Chatty = false,
                Verbose = Verbose,
                ////ShowTransitiveEdges = ShowTransitiveEdges, TODO: Replace with transformation ___
                ShowUnusedQuestionableRules = ShowUnusedQuestionableRules,
                MaxCpuCount = MaxCpuCount == 0 || MaxCpuCount < -1 ? Environment.ProcessorCount : MaxCpuCount
            };
            //////Assemblies
            //////    .Select(item => new InputFileOption(item.ItemSpec, null))
            //////    .AddTo(options.InputFiles);

            if (DefaultRuleSet != null) {
                options.DefaultRuleSetFile = DefaultRuleSet.ItemSpec;
            }
            if (Directories != null) {
                Directories
                    .Select(GetDirectoryOptionFromTaskItem)
                    .AddTo(options.Directories);
            }


            ExitCode = new Program().Run(args.ToArray());

            return ExitCode == 0;
        }

        private static DirectoryOption GetDirectoryOptionFromTaskItem(ITaskItem taskItem) {
            bool recursive = true;
            bool tmp;
            var recursiveString = taskItem.GetMetadata("Recursive");
            if (!string.IsNullOrEmpty(recursiveString) && Boolean.TryParse(recursiveString, out tmp)) {
                recursive = tmp;
            }
            return new DirectoryOption(taskItem.ItemSpec, recursive);
        }
    }

    public static class EnumerableExtensions {
        public static void AddTo<T>(this IEnumerable<T> source, ICollection<T> target) {
            foreach (var item in source) {
                target.Add(item);
            }
        }
    }
}