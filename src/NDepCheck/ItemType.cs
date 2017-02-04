using System;
using System.Linq;

namespace NDepCheck {
    public class ItemType : IEquatable<ItemType> {
        public readonly string Name;
        public readonly string[] Keys, SubKeys;

        public ItemType(string name, string[] keys, string[] subKeys) {
            if (keys.Length != subKeys.Length) {
                throw new ArgumentException("keys.Length != subKeys.Length", nameof(subKeys));
            }
            Keys = keys;
            if (subKeys.Any(subkey => !string.IsNullOrEmpty(subkey) && subkey.Length < 2 && subkey[0] != '.' && subkey.Substring(1).Contains("."))) {
                throw new ArgumentException("Subkey must either be empty or .name, but not " + string.Join(" ", subKeys), nameof(subKeys));
            }

            SubKeys = subKeys;
            Name = name;
        }

        public int Length => Keys.Length;

        public bool Equals(ItemType other) {
            // ReSharper disable once UseNullPropagation - clearer for me
            if (other == null) {
                return false;
            }
            if (Keys.Length != other.Keys.Length) {
                return false;
            }
            for (int i = 0; i < Keys.Length; i++) {
                if (Keys[i] != other.Keys[i] || SubKeys[i] != other.SubKeys[i]) {
                    return false;
                }
            }
            return true;
        }
    }
}