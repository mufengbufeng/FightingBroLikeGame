using System;
using System.Collections.Generic;

namespace EF.UI.WFramework.Collections {

	public class KeyedPriorityQueue<K, V, P> {

		public delegate void KeyedPriorityQueueHeadChangeDelegate(KeyedPriorityQueue<K, V, P> q, V oriHead, V newHead);

		public event KeyedPriorityQueueHeadChangeDelegate onHeadChanged;

		private Comparer<P> mComparer;

		private Dictionary<K, Node> mDict = new Dictionary<K, Node>();

		private List<Node> mHeap = new List<Node>();
		private int mCount = 0;

		public KeyedPriorityQueue() : this(null) { }

		public KeyedPriorityQueue(Comparison<P> priorityComparer) {
			mHeap.Add(null);
			mComparer = priorityComparer == null ? Comparer<P>.Default :
				Comparer<P>.Create(priorityComparer);
		}

		public void Enqueue(K key, V value, P priority) {
			if (mDict.ContainsKey(key)) {
				throw new ArgumentException("An element with the same key already exists in the KeyedPriorityQueue");
			}
			V oriHead = mCount > 0 ? mHeap[1].value : default(V);
			int i = ++mCount;
			if (mCount == mHeap.Count) { mHeap.Add(new Node()); }
			Node n = mHeap[mCount];
			n.key = key;
			n.value = value;
			n.priority = priority;
			int pi = i >> 1;
			while (pi > 0 && mComparer.Compare(priority, mHeap[pi].priority) < 0) {
				mHeap[i] = mHeap[pi];
				mHeap[i].index = i;
				i = pi;
				pi = i >> 1;
			}
			mHeap[i] = n;
			mDict.Add(key, n);
			n.index = i;
			if (i == 1 && onHeadChanged != null) {
				onHeadChanged(this, oriHead, mHeap[1].value);
			}
		}

		public V Dequeue() {
			if (mCount <= 0) {
				throw new InvalidOperationException("Empty Queue");
			}
			Node head = mHeap[1];
			mHeap[1] = mHeap[mCount];
			mHeap[1].index = 1;
			mHeap[mCount] = head;
			mCount--;
			mDict.Remove(head.key);
			Heapify(1);
			V ret = head.value;
			head.key = default(K);
			head.value = default(V);
			head.priority = default(P);
			return ret;
		}

		public V Dequeue(out K key, out P priority) {
			if (mCount <= 0) {
				throw new InvalidOperationException("Empty Queue");
			}
			Node head = mHeap[1];
			mHeap[1] = mHeap[mCount];
			mHeap[1].index = 1;
			mHeap[mCount] = head;
			mCount--;
			mDict.Remove(head.key);
			Heapify(1);
			key = head.key;
			priority = head.priority;
			V ret = head.value;
			head.key = default(K);
			head.value = default(V);
			head.priority = default(P);
			return ret;
		}

		public V Peek() {
			if (mCount <= 0) {
				throw new InvalidOperationException("Empty Queue");
			}
			Node head = mHeap[1];
			return head.value;
		}

		public V Peek(out K key, out P priority) {
			if (mCount <= 0) {
				throw new InvalidOperationException("Empty Queue");
			}
			Node head = mHeap[1];
			key = head.key;
			priority = head.priority;
			return head.value;
		}

		public bool RemoveFromQueue(K key) {
			if (mCount <= 0) { return false; }
			Node n;
			if (!mDict.TryGetValue(key, out n)) { return false; }
			mDict.Remove(key);
			int index = n.index;
			mHeap[index] = mHeap[mCount];
			mHeap[mCount] = n;
			mHeap[index].index = index;
			mCount--;
			if (index <= mCount && !Heapify(index)) { HeapUp(index); }
			if (index == 1 && onHeadChanged != null) {
				onHeadChanged(this, n.value, mCount > 0 ? mHeap[1].value : default(V));
			}
			n.key = default(K);
			n.value = default(V);
			n.priority = default(P);
			return true;
		}

		public bool RemoveFromQueue(K key, out V value, out P priority) {
			value = default(V);
			priority = default(P);
			if (mCount <= 0) { return false; }
			Node n;
			if (!mDict.TryGetValue(key, out n)) { return false; }
			mDict.Remove(key);
			int index = n.index;
			mHeap[index] = mHeap[mCount];
			mHeap[mCount] = n;
			mHeap[index].index = index;
			mCount--;
			if (index <= mCount && !Heapify(index)) { HeapUp(index); }
			if (index == 1 && onHeadChanged != null) {
				onHeadChanged(this, n.value, mCount > 0 ? mHeap[1].value : default(V));
			}
			value = n.value;
			priority = n.priority;
			n.key = default(K);
			n.value = default(V);
			n.priority = default(P);
			return true;
		}

		public bool TryGetItem(K key, out V value) {
			value = default(V);
			if (mCount <= 0) { return false; }
			P priority;
			return TryGetItem(key, out value, out priority);
		}

		public bool TryGetItem(K key, out V value, out P priority) {
			if (mCount <= 0) {
				value = default(V);
				priority = default(P);
				return false;
			}
			Node n;
			bool ret = mDict.TryGetValue(key, out n);
			value = n.value;
			priority = n.priority;
			return ret;
		}

		public bool Contains(K key) {
			return mDict.ContainsKey(key);
		}

		public int Count { get { return mCount; } }

		public void Clear() {
			mHeap.Clear();
			mDict.Clear();
			mHeap.Add(null);
			mCount = 0;
		}

		private bool Heapify(int i) {
			int l = i << 1;
			int r = l + 1;
			int h = i;
			if (l <= mCount && mComparer.Compare(mHeap[l].priority, mHeap[h].priority) < 0) {
				h = l;
			}
			if (r <= mCount && mComparer.Compare(mHeap[r].priority, mHeap[h].priority) < 0) {
				h = r;
			}
			if (h != i) {
				Node n = mHeap[h];
				mHeap[h] = mHeap[i];
				mHeap[h].index = h;
				mHeap[i] = n;
				n.index = i;
				Heapify(h);
				return true;
			}
			return false;
		}

		private int HeapUp(int i) {
			Node n = mHeap[i];
			P priority = n.priority;
			int pi = i >> 1;
			while (pi > 0 && mComparer.Compare(priority, mHeap[pi].priority) < 0) {
				mHeap[i] = mHeap[pi];
				mHeap[i].index = i;
				i = pi;
				pi = i >> 1;
			}
			mHeap[i] = n;
			n.index = i;
			return i;
		}

		private class Node {

			public K key;
			public V value;
			public P priority;
			public int index;

		}

	}

}
