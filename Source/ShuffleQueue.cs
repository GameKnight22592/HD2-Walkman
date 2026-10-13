using System;
using System.Collections.Generic;
namespace HD2Walkman {
    // A shuffled cycle visits every track once; history makes Back reversible.
    sealed class ShuffleQueue {
        readonly Random random;
        readonly List<int> remaining = new List<int>(), history = new List<int>();
        int count, cursor;
        public ShuffleQueue(Random random) { this.random = random; }
        void Fill(int exclude) {
            remaining.Clear();
            for (int i = 0; i < count; i++) if (i != exclude) remaining.Add(i);
            for (int i = remaining.Count - 1; i > 0; i--) {
                int j = random.Next(i + 1), value = remaining[i];
                remaining[i] = remaining[j]; remaining[j] = value;
            }
        }
        public void Reset(int total, int current) {
            count = total; history.Clear(); cursor = -1;
            if (current >= 0 && current < count) { history.Add(current); cursor = 0; }
            Fill(current);
        }
        public void Select(int index) {
            if (index < 0 || index >= count) return;
            remaining.Remove(index);
            if (cursor >= 0 && history[cursor] == index) return;
            Append(index);
        }
        void Append(int index) {
            if (cursor + 1 < history.Count) history.RemoveRange(cursor + 1, history.Count - cursor - 1);
            history.Add(index); cursor = history.Count - 1;
            if (history.Count > 256) { history.RemoveAt(0); cursor--; }
        }
        public int Next(int current) {
            if (count == 0) return -1;
            if (cursor + 1 < history.Count) return history[++cursor];
            if (remaining.Count == 0) {
                // The first new draw cannot equal the last track of the old cycle.
                Fill(-1);
                if (count > 1 && remaining[remaining.Count - 1] == current) {
                    int j = random.Next(remaining.Count - 1), value = remaining[j];
                    remaining[j] = current; remaining[remaining.Count - 1] = value;
                }
            }
            int result = remaining[remaining.Count - 1]; remaining.RemoveAt(remaining.Count - 1);
            Append(result); return result;
        }
        public int Previous(int current) { return cursor > 0 ? history[--cursor] : current; }
    }
}
