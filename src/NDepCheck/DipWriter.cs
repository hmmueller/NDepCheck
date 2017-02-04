﻿using System;
using System.Collections.Generic;
using System.IO;

namespace NDepCheck {
    /// <summary>
    /// Writer for dependencies ("Edges") in standard "DIP" format
    /// </summary>
    internal static class DipWriter {
        public static void Write(IEnumerable<IEdge> edges, string filename) {
            var _writtenTypes = new HashSet<ItemType>();

            using (var sw = new StreamWriter(filename)) {
                sw.WriteLine("// Written " + DateTime.Now);
                sw.WriteLine();
                foreach (var e in edges) {
                    WriteItemType(_writtenTypes, e.UsingNode.Type, sw);
                    WriteItemType(_writtenTypes, e.UsedNode.Type, sw);

                    sw.WriteLine(e.AsStringWithTypes());
                }
            }
        }

        private static void WriteItemType(HashSet<ItemType> _writtenTypes, ItemType itemType, StreamWriter sw) {
            if (_writtenTypes.Add(itemType)) {
                sw.Write("// ITEMTYPE ");
                sw.WriteLine(itemType.Name);
                sw.Write(itemType.Name);
                for (int i = 0; i < itemType.Keys.Length; i++) {
                    sw.Write(' ');
                    sw.Write(itemType.Keys[i]);
                    sw.Write(itemType.SubKeys[i]);
                }
                sw.WriteLine();
                sw.WriteLine();
            }
        }
    }
}
