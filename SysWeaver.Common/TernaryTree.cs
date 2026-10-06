using System;
using System.Collections.Generic;

namespace SysWeaver
{



    /// <summary>
    /// A ternary search tree, a map from (non empty) string keys to values that supports prefix, wildcard and near (Hamming distance) searches.
    /// </summary>
    /// <remarks>
    /// Not thread safe for writes (reads concurrent with writes are also unsafe).
    /// A null value is treated as "no value", so prefix nodes and keys with a null value are indistinguishable.
    /// For non nullable value types every node counts as having a value, so prefix and near searches also return the default value for intermediate nodes.
    /// All operations are recursive (the stack depth is proportional to the key length plus the tree depth).
    /// Only used by <see cref="PrefixFinder"/>.
    /// </remarks>
    /// <typeparam name="T">The type of the values</typeparam>
    public class TernaryTree<T>
    {
        /// <summary>
        /// Create an empty tree
        /// </summary>
        /// <param name="caseSenesitive">True for ordinal compares, false to fold all chars (of keys and queries) using <see cref="CharExt.FastLower"/> (invariant culture)</param>
        public TernaryTree(bool caseSenesitive = true)
        {
            CaseSenesitive = caseSenesitive;
        }

        /// <summary>
        /// True if the keys are case sensitive (ordinal), false if all chars are folded to lower case (invariant culture)
        /// </summary>
        public readonly bool CaseSenesitive;


        /// <summary>
        /// The number of added keys (see <see cref="Length"/>)
        /// </summary>
        int N;

        /// <summary>
        /// The root node, null if the tree is empty
        /// </summary>
        Node root;

        /// <summary>
        /// A node, represents one char of one or more keys
        /// </summary>
        class Node
        {
            /// <summary>
            /// The (folded) char of this node
            /// </summary>
            internal char c;

            /// <summary>
            /// The left (smaller char), middle (next char of the key) and right (larger char) sub trees
            /// </summary>
            internal Node left, mid, right;

            /// <summary>
            /// The value of the key that ends at this node (null / default if no key ends here)
            /// </summary>
            internal T value;
            /// <summary>
            /// The sum of the prefix lengths (char index + 1) of all added keys that passes this node, used to compute <see cref="BestVolume"/>
            /// </summary>
            internal long volume;
        }

        /// <summary>
        /// The number of keys in the tree.
        /// </summary>
        /// <remarks>
        /// Not accurate: it's based on <see cref="Contains(string)"/>, so adding a key that is a prefix of an already added key isn't counted
        /// </remarks>
        public int Length
        {
            get
            {
                return N;
            }
        }

        /// <summary>
        /// Check if a node exists for a key.
        /// </summary>
        /// <param name="key">The key, may not be null or empty (if the tree isn't empty)</param>
        /// <returns>True if a node exists for the key. Note that this is also true if the key is only a prefix of an added key (the node doesn't need to have a value)</returns>
        /// <exception cref="IndexOutOfRangeException">The key is empty (and the tree isn't empty)</exception>
        public bool Contains(string key)
        {
            var node = Get(root, key, 0);
            if (node == null) return false;
            return true;
        }

        /// <summary>
        /// Get the value of a key
        /// </summary>
        /// <param name="key">The key, may not be null or empty (if the tree isn't empty)</param>
        /// <returns>The value of the key, or default if the key isn't found (or is only a prefix of an added key)</returns>
        /// <exception cref="IndexOutOfRangeException">The key is empty (and the tree isn't empty)</exception>
        public T this[string key]
        {
            get
            {
                var node = Get(root, key, 0);
                if (node == null)
                    return default;
                return node.value;
            }

        }

        /// <summary>
        /// Find the node of the last char of a key (recursive)
        /// </summary>
        /// <param name="node">The sub tree to search</param>
        /// <param name="key">The key</param>
        /// <param name="charIndex">The index of the char in the key to match against the node</param>
        /// <returns>The node of the last char of the key (it may not have a value), or null if not found</returns>
        Node Get(Node node, string key, int charIndex)
        {
            if (node == null) 
                return null;
            char c = key[charIndex];
            if (!CaseSenesitive)
                c = CharExt.FastLower(c);
            if (c < node.c) 
                return Get(node.left, key, charIndex);
            if (c > node.c) 
                return Get(node.right, key, charIndex);
            if (charIndex < key.Length - 1)
                return Get(node.mid, key, charIndex + 1);
            return node;
        }


        /// <summary>
        /// Find the first (shortest) node with a value along the path of a text (recursive)
        /// </summary>
        /// <param name="node">The sub tree to search</param>
        /// <param name="key">The text</param>
        /// <param name="charIndex">The index of the char in the text to match against the node</param>
        /// <returns>The node of the shortest key (with a value) that the text starts with, the node of the last char of the text if there is none, or null if the path doesn't exist</returns>
        Node GetFirstWithValue(Node node, string key, int charIndex)
        {
            if (node == null) 
                return null;
            char c = key[charIndex];
            if (!CaseSenesitive)
                c = CharExt.FastLower(c);
            if (c < node.c)
                return GetFirstWithValue(node.left, key, charIndex);
            if (c > node.c)
                return GetFirstWithValue(node.right, key, charIndex);
            if (node.value != null)
                return node;
            if (charIndex < key.Length - 1)
                return GetFirstWithValue(node.mid, key, charIndex + 1);
            return node;
        }


        /// <summary>
        /// Add a key (or replace the value of an existing key).
        /// Also updates <see cref="Best"/> and <see cref="BestVolume"/>.
        /// </summary>
        /// <param name="key">The key, may not be null or empty</param>
        /// <param name="value">The value, a null value makes the key indistinguishable from a missing key</param>
        /// <exception cref="InvalidOperationException">The key is null or empty</exception>
        public void Add(string key, T value)
        {
            if (string.IsNullOrEmpty(key)) { throw new InvalidOperationException("Keys cannot be null or empty."); }
            if (!Contains(key)) N++;
            root = Add(root, key, value, 0);
        }



        /// <summary>
        /// Add a key to a sub tree (recursive)
        /// </summary>
        /// <param name="node">The sub tree, null to create a new node</param>
        /// <param name="key">The key</param>
        /// <param name="value">The value</param>
        /// <param name="charIndex">The index of the char in the key to match against the node</param>
        /// <returns>The (new) root of the sub tree</returns>
        Node Add(Node node, string key, T value, int charIndex)
        {
            char charAtIndex = key[charIndex];
            if (!CaseSenesitive)
                charAtIndex = CharExt.FastLower(charAtIndex);
            if (node == null)
            {
                node = new Node();
                node.c = charAtIndex;
            }
            if (charAtIndex < node.c)
                node.left = Add(node.left, key, value, charIndex);
            else if (charAtIndex > node.c)
                node.right = Add(node.right, key, value, charIndex);
            else if (charIndex < key.Length - 1)
                node.mid = Add(node.mid, key, value, charIndex + 1);
            else
                node.value = value;

            if (node.c == charAtIndex)
            {
                node.volume += (charIndex + 1);
                if (node.volume > BestVolume)
                {
                    BestVolume = node.volume;
                    Best = key.Substring(0, charIndex + 1);
                }
            }
            return node;
        }

        /// <summary>
        /// The highest "volume" of any node, where the volume of a node is the sum of the prefix lengths of all added keys that passes through it (adding the same key twice counts twice)
        /// </summary>
        public long BestVolume { get; private set; }

        /// <summary>
        /// The prefix of the node with the highest volume (see <see cref="BestVolume"/>), i.e. the most "valuable" shared prefix (with the casing of the key that last updated it), null for an empty tree
        /// </summary>
        public String Best { get; private set; }


        /// <summary>
        /// All keys (that have a non null value) in the tree, ordered by char (folded to lower case for case in-sensitive trees).
        /// A new collection is created on every call.
        /// </summary>
        public IEnumerable<string> Keys
        {
            get
            {
                Queue<string> queue = new Queue<string>();
                Collect(root, "", queue);
                return queue;
            }
        }

        /// <summary>
        /// Get all keys (that have a non null value) starting with a prefix (including the prefix itself).
        /// </summary>
        /// <param name="prefix">The prefix, may not be null or empty (if the tree isn't empty)</param>
        /// <returns>The keys, the prefix part have the casing of <paramref name="prefix"/>, the rest is folded for case in-sensitive trees</returns>
        /// <exception cref="IndexOutOfRangeException">The prefix is empty (and the tree isn't empty)</exception>
        public IEnumerable<string> PrefixMatch(string prefix)
        {
            Queue<string> queue = new Queue<string>();
            var node = Get(root, prefix, 0);
            if (node == null) return queue;
            if (node.value != null) queue.Enqueue(prefix);
            Collect(node.mid, prefix, queue);
            return queue;
        }

        /// <summary>
        /// Collect all keys (with a value) in a sub tree (recursive)
        /// </summary>
        /// <param name="node">The sub tree</param>
        /// <param name="prefix">The chars of the path to the sub tree</param>
        /// <param name="queue">Receives the keys</param>
        void Collect(Node node, string prefix, Queue<string> queue)
        {
            if (node == null) return;
            Collect(node.left, prefix, queue);
            if (node.value != null) queue.Enqueue(prefix + node.c);
            Collect(node.mid, prefix + node.c, queue);
            Collect(node.right, prefix, queue);
        }

        /// <summary>
        /// Get all keys (that have a non null value) that matches a wildcard pattern, where a '.' matches any single char (the key must have the same length as the pattern).
        /// </summary>
        /// <param name="pat">The pattern, may not be null or empty (if the tree isn't empty)</param>
        /// <returns>The matching keys (folded for case in-sensitive trees)</returns>
        public IEnumerable<string> WildcardMatch(string pat)
        {
            Queue<string> queue = new Queue<string>();
            Collect(root, "", 0, pat, queue);
            return queue;
        }

        /// <summary>
        /// Collect all keys in a sub tree that matches a wildcard pattern (recursive)
        /// </summary>
        /// <param name="node">The sub tree</param>
        /// <param name="prefix">The chars of the path to the sub tree</param>
        /// <param name="charIndex">The index of the char in the pattern to match against the node</param>
        /// <param name="pattern">The pattern ('.' matches any char)</param>
        /// <param name="query">Receives the matching keys</param>
        void Collect(Node node, string prefix, int charIndex, string pattern, Queue<string> query)
        {
            if (node == null) return;
            char charAtIndex = pattern[charIndex];
            if (!CaseSenesitive)
                charAtIndex = CharExt.FastLower(charAtIndex);


            if (charAtIndex == '.' || charAtIndex < node.c) Collect(node.left, prefix, charIndex, pattern, query);
            if (charAtIndex == '.' || charAtIndex == node.c)
            {
                if (charIndex == pattern.Length - 1 && node.value != null) query.Enqueue(prefix + node.c);
                if (charIndex < pattern.Length - 1) Collect(node.mid, prefix + node.c, charIndex + 1, pattern, query);
            }
            if (charAtIndex == '.' || charAtIndex > node.c) Collect(node.right, prefix, charIndex, pattern, query);
        }

        /// <summary>
        /// Get the values of all keys starting with a prefix (including the prefix itself if it's a key).
        /// </summary>
        /// <param name="prefix">The prefix, may not be null or empty (if the tree isn't empty)</param>
        /// <returns>The non null values, in key order</returns>
        /// <exception cref="IndexOutOfRangeException">The prefix is empty (and the tree isn't empty)</exception>
        public IEnumerable<T> Search(string prefix)
        {
            Queue<T> queue = new Queue<T>();
            var node = Get(root, prefix, 0);
            if (node == null) return queue;
            if (node.value != null)
                queue.Enqueue(node.value);
            Collect(node.mid, prefix, queue);
            return queue;
        }

        /// <summary>
        /// Find the value of the SHORTEST key that a text starts with.
        /// </summary>
        /// <param name="val">The value of the shortest matching key, or default if not found</param>
        /// <param name="value">The text to test, may not be null or empty (if the tree isn't empty)</param>
        /// <returns>True if a key (with a non null value) that the text starts with was found</returns>
        /// <exception cref="IndexOutOfRangeException">The text is empty (and the tree isn't empty)</exception>
        public bool TryFindStart(out T val, string value)
        {
            var node = GetFirstWithValue(root, value, 0);
            if (node == null)
            {
                val = default;
                return false;
            }
            var v = node.value;
            val = node.value ?? default;
            return v != null;
        }


        /// <summary>
        /// Collect the values of all keys in a sub tree (recursive)
        /// </summary>
        /// <param name="node">The sub tree</param>
        /// <param name="prefix">The chars of the path to the sub tree (not needed, but the strings are still built)</param>
        /// <param name="queue">Receives the non null values</param>
        void Collect(Node node, string prefix, Queue<T> queue)
        {
            if (node == null) return;
            Collect(node.left, prefix, queue);
            if (node.value != null) 
                queue.Enqueue(node.value);
            Collect(node.mid, prefix + node.c, queue);
            Collect(node.right, prefix, queue);
        }



        /// <summary>
        /// Get the values of keys that are (approximately) within a Hamming distance of a query.
        /// </summary>
        /// <remarks>
        /// The implementation is approximate: keys that extends a shorter key with a value are never visited (the search doesn't continue below a node with a value),
        /// keys shorter than the query can match, and a distance of 0 (an exact match) always returns nothing.
        /// </remarks>
        /// <param name="query">The query, null or white space returns nothing</param>
        /// <param name="distance">The max number of mismatched chars, must be greater than 0 (else nothing is returned)</param>
        /// <returns>The values found</returns>
        public IEnumerable<T> NearSearch(string query, int distance)
        {
            Queue<T> queue = new Queue<T>();
            if (!string.IsNullOrWhiteSpace(query) && distance > 0)
                Collect(query, root, queue, distance);
            return queue;
        }

        /// <summary>
        /// Collect the values of keys in a sub tree that are within a Hamming distance (recursive)
        /// </summary>
        /// <param name="query">The remaining part of the query</param>
        /// <param name="node">The sub tree</param>
        /// <param name="queue">Receives the values</param>
        /// <param name="d">The remaining number of allowed mismatches</param>
        void Collect(string query, Node node, Queue<T> queue, int d)
        {
            if (node == null) return;
            char c = query[0];
            if (!CaseSenesitive)
                c = CharExt.FastLower(c);
            if (d > 0 || c < node.c) { Collect(query, node.left, queue, d); }
            if (node.value != null)
            {
                if (query.Length <= d)
                {
                    queue.Enqueue(node.value);
                }
            }
            else
            {
                Collect(query.Length > 1 ? query.Substring(1) : query, node.mid, queue, c == node.c ? d : d - 1);
            }
            if (d > 0 || c > node.c) { Collect(query, node.right, queue, d); }

        }
    }

}
