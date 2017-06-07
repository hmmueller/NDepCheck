﻿using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NDepCheck.Matching;

namespace NDepCheck.Transforming.SpecialItemMarking {
    public class MarkSpecialItems : ITransformer {
        public static readonly Option MatchOption = new Option("im", "item-match", "&", "Match to select items to check", @default: "select all", multiple: true);
        public static readonly Option AddMarkerOption = new Option("am", "add-marker", "&", "Marker added to identified items", @default: null);
        public static readonly Option RecursiveMarkOption = new Option("mr", "mark-recursively", "", "Repeat marking", @default: false);
        public static readonly Option MarkSinksOption = new Option("md", "mark-drains", "", "Marks sinks (or drains)", @default: false);
        public static readonly Option MarkSourcesOption = new Option("ms", "mark-sources", "", "Mark sources", @default: false);
        public static readonly Option ConsiderSelfCyclesOption = new Option("cl", "consider-single-loops", "", "Consider single cycles for source and sink detection", @default: false);
        public static readonly Option MarkSingleCyclesOption = new Option("mi", "mark-single-loops", "", "Mark single cycles", @default: false);

        private static readonly Option[] _transformOptions = {
            MatchOption, AddMarkerOption, RecursiveMarkOption,
            MarkSinksOption, MarkSourcesOption, ConsiderSelfCyclesOption, MarkSingleCyclesOption
        };

        private bool _ignoreCase;

        public string GetHelp(bool detailedHelp, string filter) {
            return $@"Mark items with special properties.

Configuration options: None

Transformer options: {Option.CreateHelp(_transformOptions, detailedHelp, filter)}";
        }

        public void Configure([NotNull] GlobalContext globalContext, [CanBeNull] string configureOptions, bool forceReload) {
            _ignoreCase = globalContext.IgnoreCase;
        }

        public int Transform([NotNull] GlobalContext globalContext, [NotNull, ItemNotNull] IEnumerable<Dependency> dependencies,
            string transformOptions, [NotNull] List<Dependency> transformedDependencies) {

            var matches = new List<ItemMatch>();
            bool markSources = false;
            bool markSinks = false;
            bool ignoreSelfCyclesInSourcesAndSinks = true;
            bool markSingleCycleNodes = false;
            bool recursive = false;
            string markerToAdd = null;

            Option.Parse(globalContext, transformOptions,
                MatchOption.Action((args, j) => {
                    matches.Add(new ItemMatch(Option.ExtractRequiredOptionValue(args, ref j, "missing match definition"), _ignoreCase, anyWhereMatcherOk: true));
                    return j;
                }), MarkSingleCyclesOption.Action((args, j) => {
                    markSingleCycleNodes = true;
                    return j;
                }), ConsiderSelfCyclesOption.Action((args, j) => {
                    ignoreSelfCyclesInSourcesAndSinks = false;
                    return j;
                }), RecursiveMarkOption.Action((args, j) => {
                    recursive = true;
                    return j;
                }), MarkSourcesOption.Action((args, j) => {
                    markSources = true;
                    return j;
                }), MarkSinksOption.Action((args, j) => {
                    markSinks = true;
                    return j;
                }), AddMarkerOption.Action((args, j) => {
                    markerToAdd = Option.ExtractRequiredOptionValue(args, ref j, "missing marker name").Trim('\'').Trim();
                    return j;
                }));

            Dependency[] matchingDependencies = dependencies
                .Where(d => !matches.Any()
                        || matches.Any(m => ItemMatch.IsMatch(m, d.UsingItem)) && matches.Any(m => ItemMatch.IsMatch(m, d.UsedItem)))
                .ToArray();

            MatrixDictionary<Item, int> aggregatedCounts = MatrixDictionary.CreateCounts(matchingDependencies, d => d.Ct, globalContext.CurrentGraph);

            // Force each item to exist on both matrix axes
            foreach (var from in aggregatedCounts.RowKeys) {
                aggregatedCounts.GetColumnSum(from);
            }
            foreach (var to in aggregatedCounts.ColumnKeys) {
                aggregatedCounts.GetRowSum(to);
            }

            // aggregatedCounts is used destructively in both following Mark runs; this is no problem,
            // because no sink can also be a source in NDepCheck: Nodes without any edges do not
            // appear; and nodes with only self cycles are either not a source and sink (if ignoreSelfCycles=false),
            // or they are both a source and a sink and hence are found in the first Mark run.

            if (markSinks) {
                Mark(aggregatedCounts, ac => ac.RowKeys, i => aggregatedCounts.GetRowSum(i),
                    ignoreSelfCyclesInSourcesAndSinks, recursive, markerToAdd);
            }
            if (markSources) {
                Mark(aggregatedCounts, ac => ac.ColumnKeys, i => aggregatedCounts.GetColumnSum(i),
                    ignoreSelfCyclesInSourcesAndSinks, recursive, markerToAdd);
            }

            var remainingNodes = new HashSet<Item>(aggregatedCounts.RowKeys);
            remainingNodes.UnionWith(aggregatedCounts.ColumnKeys);

            if (markSingleCycleNodes) {
                foreach (var d in matchingDependencies) {
                    if (Equals(d.UsingItem, d.UsedItem)) {
                        d.UsingItem.IncrementMarker(markerToAdd);
                    }
                }
            }

            transformedDependencies.AddRange(dependencies);

            return Program.OK_RESULT;
        }

        private static void Mark(MatrixDictionary<Item, int> aggregatedCounts,
                                   Func<MatrixDictionary<Item, int>, IEnumerable<Item>> getKeys,
                                   Func<Item, int> sum, bool ignoreSelfCyclesInSourcesAndSinks, bool recursive, string markerToAdd) {
            bool itemRemoved;
            do {
                itemRemoved = false;
                foreach (var i in getKeys(aggregatedCounts).ToArray()) {
                    if (sum(i) == (ignoreSelfCyclesInSourcesAndSinks ? aggregatedCounts.Get(i, i) : 0)) {
                        aggregatedCounts.RemoveRow(i);
                        aggregatedCounts.RemoveColumn(i);
                        i.IncrementMarker(markerToAdd);
                        itemRemoved = true;
                    }
                }
            } while (recursive && itemRemoved);
        }

        public IEnumerable<Dependency> CreateSomeTestDependencies(WorkingGraph transformingGraph) {
            Item a = transformingGraph.CreateItem(ItemType.SIMPLE, "Ax");
            Item b = transformingGraph.CreateItem(ItemType.SIMPLE, "Bx");
            Item c = transformingGraph.CreateItem(ItemType.SIMPLE, "Cloop");
            Item d = transformingGraph.CreateItem(ItemType.SIMPLE, "Dloop");
            Item e = transformingGraph.CreateItem(ItemType.SIMPLE, "Eselfloop");
            Item f = transformingGraph.CreateItem(ItemType.SIMPLE, "Fy");
            Item g = transformingGraph.CreateItem(ItemType.SIMPLE, "Gy");
            Item h = transformingGraph.CreateItem(ItemType.SIMPLE, "Hy");
            Item i = transformingGraph.CreateItem(ItemType.SIMPLE, "Iy");
            Item j = transformingGraph.CreateItem(ItemType.SIMPLE, "Jy");
            return new[] {
                // Pure sources
                transformingGraph.CreateDependency(a, b, source: null, markers: "", ct: 10, questionableCt: 5, badCt: 3, notOkReason: "test data"),
                transformingGraph.CreateDependency(b, c, source: null, markers: "", ct: 1, questionableCt: 0, badCt: 0),

                // Long cycle
                transformingGraph.CreateDependency(c, d, source: null, markers: "", ct: 5, questionableCt: 0, badCt: 2, notOkReason: "test data"),
                transformingGraph.CreateDependency(d, c, source: null, markers: "", ct: 5, questionableCt: 0, badCt: 2, notOkReason: "test data"),

                transformingGraph.CreateDependency(d, e, source: null, markers: "", ct: 5, questionableCt: 3, badCt: 2, notOkReason: "test data"),
                // Self cycle
                transformingGraph.CreateDependency(e, e, source: null, markers: "", ct: 5, questionableCt: 3, badCt: 2, notOkReason: "test data"),
                // Pure sinks
                transformingGraph.CreateDependency(e, f, source: null, markers: "", ct: 5, questionableCt: 3, badCt: 2, notOkReason: "test data"),
                transformingGraph.CreateDependency(f, g, source: null, markers: "", ct: 5, questionableCt: 3, badCt: 2, notOkReason: "test data"),
                transformingGraph.CreateDependency(g, h, source: null, markers: "", ct: 5, questionableCt: 3, badCt: 2, notOkReason: "test data"),
                transformingGraph.CreateDependency(h, i, source: null, markers: "", ct: 5, questionableCt: 3, badCt: 2, notOkReason: "test data"),
                transformingGraph.CreateDependency(h, j, source: null, markers: "", ct: 5, questionableCt: 3, badCt: 2, notOkReason: "test data"),
            };
        }
    }
}
