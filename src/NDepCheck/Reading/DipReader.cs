﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using JetBrains.Annotations;

namespace NDepCheck.Reading {
    internal class DipReader : AbstractDependencyReader {
        private class DipReaderException : Exception {
            public DipReaderException(string msg)
                : base(msg) {
            }
        }

        private readonly DipReaderFactory _factory;

        public DipReader([NotNull] string fileName, [NotNull] DipReaderFactory factory) : base(fileName) {
            _factory = factory;
        }

        protected override IEnumerable<Dependency> ReadDependencies(InputContext inputContext, int depth) {
            Regex dipArrow = new Regex($@"\s*{EdgeConstants.DIP_ARROW}\s*");

            var result = new List<Dependency>(10000);
            using (var sr = new StreamReader(_fileName)) {
                var itemsDictionary = new Dictionary<Item, Item>();

                for (int lineNo = 1; ; lineNo++) {
                    string line = sr.ReadLine();
                    if (line == null) {
                        break;
                    }
                    // Remove comments
                    line = Regex.Replace(line, "//.*$", "").Trim();
                    if (line == "") {
                        continue;
                    }
                    if (!dipArrow.IsMatch(line)) {
                        string[] parts = line.Split(' ', '\t', ':');
                        RegisterType(parts[0], parts.Skip(1).Select(p => p.Split('.')));
                    } else {
                        string[] parts = dipArrow.Split(line);

                        if (parts.Length != 3) {
                            WriteError(_fileName, lineNo, "Line is not ... -> #;#;... -> ..., but " + parts.Length, line);
                        }

                        try {
                            Item foundUsingItem = GetOrCreateItem(parts[0].Trim(), itemsDictionary);
                            Item foundUsedItem = GetOrCreateItem(parts[2].Trim(), itemsDictionary);

                            string[] properties = parts[1].Split(new[] { ';' }, 6);
                            int ct, questionableCt, badCt;
                            var usage = Get(properties, 0);
                            if (!int.TryParse(Get(properties, 1), out ct)) {
                                throw new DipReaderException("Cannot parse count: " + Get(properties, 1));
                            }
                            if (!int.TryParse(Get(properties, 2), out questionableCt)) {
                                throw new DipReaderException("Cannot parse questionableCt: " + Get(properties, 2));
                            }
                            if (!int.TryParse(Get(properties, 3), out badCt)) {
                                throw new DipReaderException("Cannot parse badCt: " + Get(properties, 3));
                            }

                            string[] source = Get(properties, 4).Split('|');
                            int sourceLine = -1;
                            if (source.Length > 1) {
                                if (!int.TryParse(source[1], out sourceLine)) {
                                    sourceLine = -1;
                                }
                            }

                            string exampleInfo = Get(properties, 5);

                            var dependency = new Dependency(foundUsingItem, foundUsedItem,
                                string.IsNullOrWhiteSpace(source[0])
                                    ? new TextFileSource(_fileName, lineNo)
                                    : new TextFileSource(source[0], sourceLine < 0 ? null : (int?)sourceLine),
                                usage, ct, questionableCt, badCt, exampleInfo, inputContext);

                            result.Add(dependency);
                        } catch (DipReaderException ex) {
                            WriteError(FileName, lineNo, ex.Message + " - ignoring input line", line);
                        }
                    }
                }
                return result;
            }
        }

        [NotNull]
        private string Get(string[] properties, int i) {
            return i < properties.Length ? properties[i] : "";
        }

        private void RegisterType([NotNull] string name, [NotNull] IEnumerable<string[]> keySubKeyPairs) {
            _factory.AddItemType(ItemType.New(name, keySubKeyPairs.Select(pair => pair[0]).ToArray(), keySubKeyPairs.Select(pair => pair.Length > 1 ? pair[1] : "").ToArray()));
        }

        [NotNull]
        private Item GetOrCreateItem([NotNull] string part, [NotNull] Dictionary<Item, Item> items) {
            Item item = CreateItem(part);
            Item foundItem;
            if (!items.TryGetValue(item, out foundItem)) {
                items.Add(item, foundItem = item);
            }
            return foundItem;
        }

        [NotNull]
        private Item CreateItem(string s) {
            string[] parts = s.Split(':', ';');

            string descriptorName = parts.First();
            ItemType foundType = _factory.GetDescriptor(descriptorName);

            if (foundType == null) {
                throw new DipReaderException("Descriptor '" + descriptorName + "' has not been defined in this file previously");
            } else {
                return Item.New(foundType, parts.Skip(1).ToArray());
            }
        }

        private static void WriteError(string fileName, int lineNo, string msg, string line) {
            Log.WriteError(fileName + "/" + lineNo + ": " + msg + " - '" + line + "'");
        }
    }

}
