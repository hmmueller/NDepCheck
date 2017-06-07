﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;

namespace NDepCheck.Transforming.Projecting {
    public partial class ProjectItems : AbstractTransformerWithFileConfiguration<ProjectionSet> {
        internal const string ABSTRACT_IT_LEFT = "<";
        internal const string ABSTRACT_IT_BOTH = "!";
        internal const string ABSTRACT_IT_RIGHT = ">";
        internal const string MAP = "---%";

        public static readonly Option ProjectionFileOption = new Option("pf", "projection-file", "filename", "File containing projections", @default: "");
        public static readonly Option ProjectionsOption = new Option("pl", "projection-list", "projections", "Inline projections", orElse: ProjectionFileOption);
        public static readonly Option MatcherStrategyOption = new Option("ms", "matcher-strategy", "[S|FL|PT]", "Internal strategy for optimized matcher access; S=Simple, FL=FirstLetter, PT=PrefixTrie", @default: "PT");

        private static readonly Option[] _configOptions = { ProjectionFileOption, ProjectionsOption, MatcherStrategyOption };

        public static readonly Option BackProjectionGraphOption = new Option("bp", "back-projection-graph", "[graphname]", "Do back projection to dependencies in indicated graph", @default: "back project to previous graph");

        private static readonly Option[] _transformOptions = { BackProjectionGraphOption };

        private IProjector _projector;

        private Func<Projection[], bool, IProjector> _createProjector;
        private IEnumerable<Projection> _allProjectionsForMatchCountLoggingOnly;

        public ProjectItems() : this((p, i) => new SimpleProjector(p, name: "default")) {
        }

        public ProjectItems(Func<Projection[], bool, IProjector> createProjector) {
            _createProjector = createProjector;
        }

        public override string GetHelp(bool detailedHelp, string filter) {
            string result = $@"  Project ('reduce') items and dependencies

Configuration options: {Option.CreateHelp(_configOptions, detailedHelp, filter)}

Transformer options: {Option.CreateHelp(_transformOptions, detailedHelp, filter)}";

            if (detailedHelp) {
                result += @"

Configuration format:

Configuration files support the standard options + for include,
// for comments, macro definitions (see -help files).

Mandatory type transformation line:
$ type ---% type

Projections:
< pattern [---% result]
! pattern [---% result]
> pattern [---% result]

where

type     is an NDepCheck type or type definition (see -help types)
pattern  is an NDepCheck item pattern (see -help itempattern)
result   is element[:element...], where each element is a string or \1, \2, ...
         \1 etc. refers to a matched group in the itempattern.
If no result is provided, result is assumed to be \1:\2:..., with the number
of groups in the pattern.
result can also be a single -, in which case a matching item is not projected.
Also, if all matched groups are empty, the matching item is not projected.

< matches only using items
| matches both using and used items
> matches only used items

After projecting the items, all dependencies are removed where the using or
used item is not projected.

Examples:
::(**)::(**)                project to 3rd and 5th field; e.g., the result of
                            projecting a.1:b.2:c.3:d.4:e.5:f.6 is c.3:e.5
::(**)::(**) ---% \1:\2     same as above

::a.(**)::   ---% MOD_A:\1  Projection with fixed string

::()                        do not project item (single group is always empty)
::           ---% -         same as above
";
            }
            return result;
        }

        #region Configure

        public override void Configure([NotNull] GlobalContext globalContext, [CanBeNull] string configureOptions, bool forceReload) {
            base.Configure(globalContext, configureOptions, forceReload);

            ProjectionSet orderedProjections = null;
            _projector = null;

            Option.Parse(globalContext, configureOptions,
                MatcherStrategyOption.Action((args, j) => {
                    string strategy = Option.ExtractRequiredOptionValue(args, ref j, "missing strategy");
                    switch (strategy) {
                        case "S":
                            _createProjector = (p, i) => new SimpleProjector(p, name: "default projector");
                            break;
                        case "PT":
                            _createProjector = (p, i) => new SelfOptimizingPrefixTrieProjector(p, i, 1000, name: "PT projector");
                            break;
                        case "FL":
                            _createProjector = (p, i) => new SelfOptimizingFirstLetterProjector(p, i, 1000, name: "FL projector");
                            break;
                        default:
                            Log.WriteWarning($"Unrecognized matcher optimization strategy {strategy} - using default");
                            break;
                    }
                    return j;
                }), ProjectionFileOption.Action((args, j) => {
                    string fullSourceName =
                        Path.GetFullPath(Option.ExtractRequiredOptionValue(args, ref j, "missing projections filename"));
                    orderedProjections = GetOrReadChildConfiguration(globalContext, () => new StreamReader(fullSourceName),
                        fullSourceName, globalContext.IgnoreCase, "????", forceReload);
                    return j;
                }), ProjectionsOption.Action((args, j) => {
                    orderedProjections = GetOrReadChildConfiguration(globalContext,
                            () => new StringReader(string.Join(Environment.NewLine, args.Skip(j + 1))),
                            ProjectionsOption.ShortName, globalContext.IgnoreCase, "????", forceReload: true);
                    // ... and all args are read in, so the next arg index is past every argument.
                    return int.MaxValue;
                }));


            if (orderedProjections == null || !orderedProjections.AllProjections.Any()) {
                Log.WriteWarning("No projections defined");
                _projector = new SimpleProjector(new Projection[0], name: "empty");
                _allProjectionsForMatchCountLoggingOnly = new Projection[0];
            } else {
                _projector = _createProjector(orderedProjections.AllProjections, globalContext.IgnoreCase);
                _allProjectionsForMatchCountLoggingOnly = orderedProjections.AllProjections;
            }
        }

        protected override ProjectionSet CreateConfigurationFromText([NotNull] GlobalContext globalContext, string fullConfigFileName,
            int startLineNo, TextReader tr, bool ignoreCase, string fileIncludeStack, bool forceReloadConfiguration,
            Dictionary<string, string> configValueCollector) {

            ItemType sourceItemType = null;
            ItemType targetItemType = null;

            string ruleSourceName = fullConfigFileName;

            var elements = new List<IProjectionSetElement>();

            ProcessTextInner(globalContext, fullConfigFileName, startLineNo, tr, ignoreCase, fileIncludeStack,
                forceReloadConfiguration, onIncludedConfiguration: (e, n) => elements.Add(e),
                onLineWithLineNo: (line, lineNo) => {
                    if (line.StartsWith("$")) {
                        string typeLine = line.Substring(1).Trim();
                        int i = typeLine.IndexOf(MAP, StringComparison.Ordinal);
                        if (i < 0) {
                            return $"{line}: $-line must contain " + MAP;
                        }
                        sourceItemType = ItemType.New(typeLine.Substring(0, i).Trim(), globalContext.IgnoreCase);
                        targetItemType = ItemType.New(typeLine.Substring(i + MAP.Length).Trim(), globalContext.IgnoreCase);
                        return null;
                    } else {
                        bool left = line.StartsWith(ABSTRACT_IT_LEFT);
                        bool right = line.StartsWith(ABSTRACT_IT_RIGHT);
                        bool both = line.StartsWith(ABSTRACT_IT_BOTH);
                        if (left || both || right) {
                            Projection p = CreateProjection(sourceItemType, targetItemType,
                                ruleFileName: ruleSourceName, lineNo: lineNo, rule: line.Substring(1).Trim(),
                                ignoreCase: ignoreCase, forLeftSide: left || both, forRightSide: both || right);
                            elements.Add(p);
                            return null;
                        } else {
                            return $"{line}: line must start with $, {ABSTRACT_IT_LEFT}, {ABSTRACT_IT_BOTH}, or {ABSTRACT_IT_RIGHT}";
                        }
                    }
                }, configValueCollector: configValueCollector);
            return new ProjectionSet(elements);
        }

        private Projection CreateProjection([CanBeNull] ItemType sourceItemType,
            [CanBeNull] ItemType targetItemType, [NotNull] string ruleFileName, int lineNo, [NotNull] string rule,
            bool ignoreCase, bool forLeftSide, bool forRightSide) {
            if (sourceItemType == null || targetItemType == null) {
                Log.WriteError($"Itemtypes not defined - $ line is missing in {ruleFileName}, graph rules are ignored",
                    ruleFileName, lineNo);
                throw new ApplicationException("Itemtypes not defined");
            } else {
                int i = rule.IndexOf(MAP, StringComparison.Ordinal);
                string pattern;
                string[] targetSegments;
                if (i >= 0) {
                    pattern = rule.Substring(0, i).Trim();
                    targetSegments = rule.Substring(i + MAP.Length).Trim().Split(':').Select(s => s.Trim()).ToArray();
                } else {
                    pattern = rule.Trim();
                    targetSegments = null;
                }

                var p = new Projection(sourceItemType, targetItemType, pattern, targetSegments, ignoreCase, forLeftSide,
                                       forRightSide, ruleFileName + "/" + lineNo);

                if (Log.IsChattyEnabled) {
                    Log.WriteInfo("Reg.exps used for projecting " + pattern +
                                  (targetSegments == null ? "" : " to " + string.Join(":", targetSegments)) + " (" +
                                  ruleFileName + ":" + lineNo + ")");
                    Log.WriteInfo(p.ToString());
                }

                return p;
            }
        }

        #endregion Configure

        #region Transform

        public override int Transform([NotNull] GlobalContext globalContext, 
                            [NotNull, ItemNotNull] IEnumerable<Dependency> dependencies,
                            string transformOptions, [NotNull] List<Dependency> transformedDependencies) {
            IEnumerable<Dependency> backProjectionDependencies = null;
            Option.Parse(globalContext, transformOptions,
                BackProjectionGraphOption.Action((args, j) => {
                    string backProjectionGraph = Option.ExtractOptionValue(args, ref j);
                    backProjectionDependencies = globalContext.FindDependenciesInGraph(backProjectionGraph);
                    if (backProjectionDependencies == null) {
                        throw new ArgumentException($"Could not find graph '{backProjectionGraph}'");
                    }
                    return j;
                })
            );


            if (backProjectionDependencies != null) {
                Dictionary<FromTo, Dependency> dependenciesForBackProjection = dependencies.ToDictionary(
                        d => new FromTo(d.UsingItem, d.UsedItem), d => d);

                var backProjected = new List<Dependency>();

                int missingPatternCount = 0;
                //int notBackProjected = 0;
                var localCollector = new Dictionary<FromTo, Dependency>();
                var mapItems = new Dictionary<Item, Item>();
                foreach (var d in backProjectionDependencies) {
                    FromTo f = ProjectDependency(globalContext.CurrentGraph, d, localCollector, () => OnMissingPattern(ref missingPatternCount));

                    if (f != null) {
                        Dependency projected;
                        if (dependenciesForBackProjection.TryGetValue(f, out projected)) {
                            //d.ResetBad(); <-- option
                            //d.ResetQuestionable(); <-- option
                            d.AggregateMarkersAndCounts(projected);
                            mapItems[d.UsedItem] = projected.UsedItem;
                            mapItems[d.UsingItem] = projected.UsingItem;
                            backProjected.Add(d);
                        } else {
                            //notBackProjected++;
                        }
                    }
                }

                foreach (var kvp in mapItems) {
                    Item target = kvp.Key;
                    Item source = kvp.Value;
                    target.MergeWithMarkers(source.MarkerSet);
                }

                transformedDependencies.AddRange(backProjected);
            } else {
                // Forward projection
                var localCollector = new Dictionary<FromTo, Dependency>();
                int missingPatternCount = 0;
                foreach (var d in dependencies) {
                    ProjectDependency(globalContext.CurrentGraph, d, localCollector, () => OnMissingPattern(ref missingPatternCount));
                }
                transformedDependencies.AddRange(localCollector.Values);
            }

            AfterAllTransforms();

            return Program.OK_RESULT;
        }

        private static bool OnMissingPattern(ref int missingPatternCount) {
            missingPatternCount++;
            if (missingPatternCount == 250) {
                Log.WriteError("After 250 missing patterns, no more missing patterns are logged");
            }
            return missingPatternCount < 250;
        }

        public interface IProjector {
            Item Project(WorkingGraph cachingGraph, Item item, bool left);
        }

        private FromTo ProjectDependency(WorkingGraph currentWorkingGraph, Dependency d, Dictionary<FromTo, Dependency> localCollector, 
                                         Func<bool> onMissingPattern) {
            Item usingItem = _projector.Project(cachingGraph: currentWorkingGraph, item: d.UsingItem, left: true);
            Item usedItem = _projector.Project(cachingGraph: currentWorkingGraph, item: d.UsedItem, left: false);

            if (usingItem == null) {
                if (onMissingPattern()) {
                    Log.WriteWarning("No projection pattern found for " + d.UsingItem.AsString() + " - I ignore it");
                }
                return null;
            } else if (usedItem == null) {
                if (onMissingPattern()) {
                    Log.WriteWarning("No projection pattern found for " + d.UsedItem.AsString() + " - I ignore it");
                }
                return null;
            } else if (usingItem.IsEmpty() || usedItem.IsEmpty()) {
                // ignore this edge!
                return null;
            } else {
                return new FromTo(usingItem, usedItem).AggregateDependency(currentWorkingGraph, d, localCollector);
            }
        }

        public override IEnumerable<Dependency> CreateSomeTestDependencies(WorkingGraph transformingGraph) {
            ItemType abc = ItemType.New("AB+(A:B)");
            Item a1 = transformingGraph.CreateItem(abc, "a", "1");
            Item a2 = transformingGraph.CreateItem(abc, "a", "2");
            Item b = transformingGraph.CreateItem(abc, "b", "");

            return new[] {
                FromTo(transformingGraph, a1, a1), FromTo(transformingGraph, a1, a2),
                FromTo(transformingGraph, a2, a1), FromTo(transformingGraph, a2, a2),
                FromTo(transformingGraph, a1, b)
            };
        }

        private Dependency FromTo(WorkingGraph graph, Item from, Item to) {
            return graph.CreateDependency(from, to, new TextFileSourceLocation("Test", 1), "Use", ct: 1);
        }

        private void AfterAllTransforms() {
            if (Log.IsVerboseEnabled) {
                List<Projection> asList = _allProjectionsForMatchCountLoggingOnly.ToList();
                asList.Sort((p, q) => p.MatchCount - q.MatchCount);

                Log.WriteInfo("Match counts - projection definitions:");
                foreach (var p in asList) {
                    Log.WriteInfo($"{p.MatchCount,5} - {p.Source}");
                }
            }
        }

        #endregion Transform
    }
}

