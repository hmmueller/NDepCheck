﻿using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NDepCheck.Transforming.Modifying {
    public class ModifyItems : AbstractTransformerWithConfigurationPerInputfile<IEnumerable<ItemAction>> {
        public static readonly Option ModificationsFileOption = new Option("mf", "modifications-file", "filename", "File containing modifications", @default: "");
        public static readonly Option ModificationsOption = new Option("ml", "modifications-list", "modifications", "Inline modifications", orElse: ModificationsFileOption);

        private static readonly Option[] _configOptions = { ModificationsFileOption, ModificationsOption };

        public override string GetHelp(bool detailedHelp, string filter) {
            return $@"Modify counts and markers on items.

Configuration options: {Option.CreateHelp(_configOptions, detailedHelp, filter)}

Transformer options: None";
        }

        public override bool RunsPerInputContext => false;

        private IEnumerable<ItemAction> _orderedActions;

        public override void Configure(GlobalContext globalContext, string configureOptions) {
            Option.Parse(configureOptions,
                ModificationsFileOption.Action((args, j) => {
                    string fullSourceName = Path.GetFullPath(Option.ExtractOptionValue(args, ref j));
                    _orderedActions = GetOrReadChildConfiguration(globalContext,
                        () => new StreamReader(fullSourceName), fullSourceName, globalContext.IgnoreCase, "????");
                    return j;
                }),
                ModificationsOption.Action((args, j) => {
                    // A trick is used: The first line, which contains all options, should be ignored; and
                    // also the last } (which is from the surrounding options braces). Thus, 
                    // * we add // to the beginning - this comments out the first line;
                    // * and trim } at the end.
                    _orderedActions = GetOrReadChildConfiguration(globalContext,
                        () => new StringReader("//" + configureOptions.Trim().TrimEnd('}')), ModificationsOption.ShortName, globalContext.IgnoreCase, "????");
                    // ... and all args are read in, so the next arg index is past every argument.
                    return int.MaxValue;
                })
            );
        }

        protected override IEnumerable<ItemAction> CreateConfigurationFromText(GlobalContext globalContext,
            string fullConfigFileName, int startLineNo, TextReader tr, bool ignoreCase, string fileIncludeStack) {

            var actions = new List<ItemAction>();
            ProcessTextInner(globalContext, fullConfigFileName, startLineNo, tr, ignoreCase, fileIncludeStack,
                onIncludedConfiguration: (e, n) => actions.AddRange(e),
                onLineWithLineNo: (line, lineNo) => {
                    actions.Add(new ItemAction(line.Trim(), ignoreCase, fullConfigFileName, startLineNo));
                    return true;
                });
            return actions;
        }

        public override int Transform(GlobalContext context, string dependenciesFilename, IEnumerable<Dependency> dependencies,
            string transformOptions, string dependencySourceForLogging, List<Dependency> transformedDependencies) {

            if (_orderedActions == null) {
                Log.WriteWarning($"No actions configured for {GetType().Name}");
            } else {

                Dictionary<Item, IEnumerable<Dependency>> items2incoming =
                    Item.CollectIncomingDependenciesMap(dependencies);
                Dictionary<Item, IEnumerable<Dependency>> items2outgoing =
                    Item.CollectIncomingDependenciesMap(dependencies);

                var allItems = new HashSet<Item>(items2incoming.Keys.Concat(items2outgoing.Keys));

                foreach (var i in allItems.ToArray()) {
                    IEnumerable<Dependency> incoming;
                    items2incoming.TryGetValue(i, out incoming);
                    IEnumerable<Dependency> outgoing;
                    items2outgoing.TryGetValue(i, out outgoing);

                    ItemAction firstMatchingAction = _orderedActions.FirstOrDefault(a => a.Matches(incoming, i, outgoing));
                    if (firstMatchingAction == null) {
                        Log.WriteWarning("No match in actions for item " + i + " - item is deleted");
                        allItems.Remove(i);
                    } else {
                        if (!firstMatchingAction.Apply(i)) {
                            allItems.Remove(i);
                        }
                    }
                }

                transformedDependencies.AddRange(
                    dependencies.Where(d => allItems.Contains(d.UsingItem) && allItems.Contains(d.UsedItem)));
            }
            return Program.OK_RESULT;
        }

        public override void FinishTransform(GlobalContext context) {
            // empty
        }

        public override IEnumerable<Dependency> GetTestDependencies() {
            var a = Item.New(ItemType.SIMPLE, "A");
            var b = Item.New(ItemType.SIMPLE, "B");
            return new[] {
                new Dependency(a, a, source: null, markers: "", ct:10, questionableCt:5, badCt:3),
                new Dependency(a, b, source: null, markers: "use+define", ct:1, questionableCt:0,badCt: 0),
                new Dependency(b, a, source: null, markers: "define", ct:5, questionableCt:0, badCt:2),
            };
        }
    }
}
