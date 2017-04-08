﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace NDepCheck.Transforming.SpecialDependencyMarking {
    public class MarkMinimalCutDeps {
        public static readonly Option MatchSourceOption = new Option("ms", "match-sources", "&", "Match to select source items", @default: null, multiple: true);
        public static readonly Option MatchTargetOption = new Option("mt", "match-targets", "&", "Match to select target items", @default: null, multiple: true);
        public static readonly Option DepsMarkerOption = new Option("dm", "dependency-cut-marker", "&", "Marker added to dependencies on minimal cut", @default: null);
        public static readonly Option SourceMarkerOption = new Option("sm", "source-set-marker", "&", "Marker added to items in source side graph (necessary for empty cut)", multiple: true, orElse: DepsMarkerOption);
        public static readonly Option UseQuestionableCountOption = new Option("uq", "use-questionable-count", "", "Use questionable count as weight", @default: "Use bad count");
        public static readonly Option UseCountOption = new Option("uc", "use-count", "", "Use count as weight", @default: "Use bad count");

        private static readonly Option[] _transformOptions = {
            MatchSourceOption, MatchTargetOption, DepsMarkerOption
        };

        private bool _ignoreCase;

        public string GetHelp(bool detailedHelp) {
            return $@"Mark dependencies with special properties - UNTESTED.

Configuration options: None

Transformer options: {Option.CreateHelp(_transformOptions, detailedHelp)}";
        }

        public bool RunsPerInputContext => true;

        public void Configure(GlobalContext globalContext, string configureOptions) {
            _ignoreCase = globalContext.IgnoreCase;
        }

        private class Edge {
            public readonly Dependency Dependency;
            public readonly int Capacity;
            public int Flow;

            public Edge(Dependency dependency, Func<Dependency, int> capacity) {
                Dependency = dependency;
                Capacity = capacity(dependency);
                Flow = 0;
            }

            public Item UsingItem => Dependency.UsingItem;
            public Item UsedItem => Dependency.UsedItem;

            public bool InResidual => Flow < Capacity;
            public bool ReverseInResidual => Flow > 0;
        }

        public int Transform(GlobalContext context, string dependenciesFilename, IEnumerable<Dependency> dependencies,
            string transformOptions, string dependencySourceForLogging, List<Dependency> transformedDependencies) {

            var sourceMatches = new List<ItemMatch>();
            var targetMatches = new List<ItemMatch>();
            string markerToAddToSourceSide = null;
            //string markerToAddToTargetSide = null;
            string markerToAddToCut = null;
            Func<Dependency, int> weightForCut = d => d.BadCt;

            Option.Parse(transformOptions,
                MatchSourceOption.Action((args, j) => {
                    sourceMatches.Add(new ItemMatch(null, Option.ExtractOptionValue(args, ref j), _ignoreCase));
                    return j;
                }),
                MatchTargetOption.Action((args, j) => {
                    targetMatches.Add(new ItemMatch(null, Option.ExtractOptionValue(args, ref j), _ignoreCase));
                    return j;
                }),
                UseQuestionableCountOption.Action((args, j) => {
                    weightForCut = d => d.QuestionableCt;
                    return j;
                }),
                UseCountOption.Action((args, j) => {
                    weightForCut = d => d.Ct;
                    return j;
                }),
                DepsMarkerOption.Action((args, j) => {
                    markerToAddToCut = Option.ExtractOptionValue(args, ref j).Trim('\'').Trim();
                    return j;
                }),
                SourceMarkerOption.Action((args, j) => {
                    markerToAddToSourceSide = Option.ExtractOptionValue(args, ref j).Trim('\'').Trim();
                    return j;
                }));

            var items = new HashSet<Item>(dependencies.SelectMany(d => new[] { d.UsingItem, d.UsedItem }));
            var sourceItems = new List<Item>(items.Where(i => sourceMatches.Any(m => m.Matches(i) != null)));
            var targetItems = new HashSet<Item>(items.Where(i => targetMatches.Any(m => m.Matches(i) != null)));
            if (!sourceItems.Any()) {
                throw new ApplicationException("No source items found - minimal cut cannot be computed");
            } else if (!targetItems.Any()) {
                throw new ApplicationException("No target items found - minimal cut cannot be computed");
            } else if (targetItems.Overlaps(sourceItems)) {
                throw new ApplicationException("Source and target items overlap - minimal cut cannot be computed");
            }

            // According to the "Max-flow min-cut theorem" (see e.g.
            // http://www.cs.princeton.edu/courses/archive/spr04/cos226/lectures/maxflow.4up.pdf, 
            // http://web.stanford.edu/class/cs97si/08-network-flow-problems.pdf,
            // https://en.wikipedia.org/wiki/Max-flow_min-cut_theorem), we first find the maximum 
            // flow; then the residual graph; and from this the minimal cut.

            // Find maximal flow from sources to targets via the Ford-Fulkerson algorithm - see e.g. 
            // http://www.cs.princeton.edu/courses/archive/spr04/cos226/lectures/maxflow.4up.pdf;
            Dictionary<Dependency, Edge> edges = dependencies.ToDictionary(d => d, d => new Edge(d, weightForCut));

            Dictionary<Item, List<Edge>> outgoing = Item.CollectMap(dependencies, d => d.UsingItem, d => edges[d]);
            SortByFallingCapacity(outgoing);

            Dictionary<Item, List<Edge>> incoming = Item.CollectMap(dependencies, d => d.UsedItem, d => edges[d]);
            SortByFallingCapacity(incoming);

            bool flowIncreased;
            do {
                flowIncreased = false;
                foreach (var i in sourceItems) {
                    int increase = IncreaseFlow(i, int.MaxValue, new HashSet<Item> { i }, outgoing, incoming, targetItems);
                    flowIncreased |= increase > 0;
                }
            } while (flowIncreased);

            // Find residual graph (only on sources side)
            var reachableFromSources = new HashSet<Item>(sourceItems);
            foreach (var i in sourceItems) {
                AddReachableInResidualGraph(i, reachableFromSources, outgoing, incoming);
            }

            // Find minimal cut and add markers
            foreach (var s in reachableFromSources) {
                if (markerToAddToSourceSide != null) {
                    s.AddMarker(markerToAddToSourceSide);
                }
                foreach (var d in GetList(outgoing, s).Where(e => !reachableFromSources.Contains(e.UsedItem))) {
                    d.Dependency.AddMarker(markerToAddToCut);
                }
            }

            transformedDependencies.AddRange(dependencies);

            return Program.OK_RESULT;
        }

        private static void SortByFallingCapacity(Dictionary<Item, List<Edge>> e) {
            foreach (var li in e.Values) {
                li.Sort((e1, e2) => e2.Capacity - e1.Capacity);
            }
        }

        private static IEnumerable<Edge> GetList(Dictionary<Item, List<Edge>> dictionary, Item item) {
            List<Edge> result;
            dictionary.TryGetValue(item, out result);
            return result ?? Enumerable.Empty<Edge>();
        }

        private int IncreaseFlow(Item item, int maxPossibleFlowIncreaseAlongPath, HashSet<Item> visited,
            Dictionary<Item, List<Edge>> outgoing, Dictionary<Item, List<Edge>> incoming, HashSet<Item> targetItems) {
            if (targetItems.Contains(item)) {
                return maxPossibleFlowIncreaseAlongPath;
            } else {
                foreach (var e in GetList(outgoing, item)) {
                    int possibleFlowIncrease = Math.Min(maxPossibleFlowIncreaseAlongPath, e.Capacity - e.Flow);
                    int actualIncrease = ComputeActualIncrease(visited, outgoing, incoming, targetItems,
                                                               possibleFlowIncrease, e.UsedItem);
                    if (actualIncrease > 0) {
                        // There is a path to the end with free capacity actualReduction - we reduce current edge!
                        e.Flow += actualIncrease;
                        return actualIncrease;
                    }
                }
                foreach (var e in GetList(incoming, item)) {
                    int possiblePushedBackFlow = Math.Min(maxPossibleFlowIncreaseAlongPath, e.Flow);
                    int actualIncrease = ComputeActualIncrease(visited, outgoing, incoming, targetItems,
                                                               possiblePushedBackFlow, e.UsingItem);
                    if (actualIncrease > 0) {
                        // There is a path to the end with free capacity actualReduction - we reduce current edge!
                        e.Flow -= actualIncrease;
                        return actualIncrease;
                    }
                }
                return 0;
            }
        }

        private int ComputeActualIncrease(HashSet<Item> visited, Dictionary<Item, List<Edge>> outgoing,
            Dictionary<Item, List<Edge>> incoming, HashSet<Item> targetItems, int possibleFlowIncrease, Item nextItem) {
            if (possibleFlowIncrease > 0 && visited.Add(nextItem)) {
                int result = IncreaseFlow(nextItem, possibleFlowIncrease, visited, outgoing, incoming, targetItems);
                visited.Remove(nextItem);
                return result;
            } else {
                return 0;
            }
        }

        private void AddReachableInResidualGraph(Item item, HashSet<Item> reachableFromSources,
            Dictionary<Item, List<Edge>> outgoing, Dictionary<Item, List<Edge>> incoming) {
            foreach (var e in GetList(outgoing, item).Where(e => e.InResidual)) {
                if (reachableFromSources.Add(e.UsedItem)) {
                    AddReachableInResidualGraph(e.UsedItem, reachableFromSources, outgoing, incoming);
                }
            }
            foreach (var e in GetList(incoming, item).Where(e => e.ReverseInResidual)) {
                if (reachableFromSources.Add(e.UsingItem)) {
                    AddReachableInResidualGraph(e.UsingItem, reachableFromSources, outgoing, incoming);
                }
            }
        }

        public void FinishTransform(GlobalContext context) {
            // empty
        }

        public IEnumerable<Dependency> GetTestDependencies() {
            // Graph from http://web.stanford.edu/class/cs97si/08-network-flow-problems.pdf p.7
            Item s = Item.New(ItemType.SIMPLE, "s");
            Item a = Item.New(ItemType.SIMPLE, "a");
            Item b = Item.New(ItemType.SIMPLE, "b");
            Item c = Item.New(ItemType.SIMPLE, "c");
            Item d = Item.New(ItemType.SIMPLE, "d");
            Item t = Item.New(ItemType.SIMPLE, "t");
            return new[] {
                new Dependency(s, a, null, "s->a", 101, 16, 0),
                new Dependency(s, c, null, "s->c", 103, 13, 0),
                new Dependency(a, b, null, "a->b", 112, 12, 0),
                new Dependency(a, c, null, "a->c", 113, 10, 0),
                new Dependency(b, c, null, "b->c", 123, 9, 0),
                new Dependency(b, t, null, "b->t", 125, 20, 0),
                new Dependency(c, a, null, "c->a", 131, 4, 0),
                new Dependency(c, d, null, "c->d", 134, 14, 0),
                new Dependency(d, b, null, "d->b", 142, 7, 0),
                new Dependency(d, t, null, "d->t", 145, 4, 0),
            };
        }
    }
}