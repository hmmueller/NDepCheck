﻿using System.Collections.Generic;

namespace NDepCheck.Transforming.Projecting {
    public partial class ProjectItems {
        private class CharIgnoreCaseEqualityComparer : IEqualityComparer<char> {
            public bool Equals(char x, char y) {
                return char.ToUpperInvariant(x) == char.ToUpperInvariant(y);
            }

            public int GetHashCode(char obj) {
                return char.ToUpperInvariant(obj).GetHashCode();
            }
        }

        public abstract class AbstractProjector : IProjector {
            protected AbstractProjector(string name) {
                Name = name;
            }

            public string Name { get; }

            public abstract Item Project(WorkingGraph cachingGraph, Item item, bool left);
        }

        public abstract class AbstractProjectorWithProjectionList : AbstractProjector {

            protected readonly Projection[] _orderedProjections;
            private int _projectCount;
            private int _matchCount;

            protected AbstractProjectorWithProjectionList(Projection[] orderedProjections, string name) : base(name) {
                _orderedProjections = orderedProjections;
            }

            public int ProjectCount => _projectCount;

            public int MatchCount => _matchCount;

            protected Item ProjectBySequentialSearch(WorkingGraph cachingGraph, Item item, bool left) {
                _projectCount++;
                foreach (var p in _orderedProjections) {
                    _matchCount++;
                    Item result = p.Match(cachingGraph, item, left);
                    if (result != null) {
                        return result;
                    }
                }
                return null;
            }

            public void ReduceCostCountsInReorganizeToForgetHistory() {
                _matchCount = 9 * _matchCount / 10;
                _projectCount = 9 * _projectCount / 10;
            }
        }

        public class SimpleProjector : AbstractProjectorWithProjectionList {
            public SimpleProjector(Projection[] orderedProjections, string name) : base(orderedProjections, name) {
            }

            public override Item Project(WorkingGraph cachingGraph, Item item, bool left) {
                return ProjectBySequentialSearch(cachingGraph, item, left);
            }
        }
    }
}
