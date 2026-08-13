using System;
using System.Collections.Generic;

namespace EF.UI.WFramework.Collections {

	public class PriorityQueue<V, P> {

		private Comparer<P> mComparer;

		private List<Node> mHeap = new List<Node>();
		private int mCount = 0;

		public PriorityQueue() : this(null) { }

		public PriorityQueue(Comparison<P> priorityComparer) {
			mHeap.Add(new Node());
			mComparer = priorityComparer == null ? Comparer<P>.Default :
				Comparer<P>.Create(priorityComparer);
		}

		public void Enqueue(V value, P priority) {
			int i = ++mCount;
			if (mCount == mHeap.Count) { mHeap.Add(new Node()); }
			int pi = i >> 1;
			while (pi > 0 && mComparer.Compare(priority, mHeap[pi].priority) < 0) {
				mHeap[i] = mHeap[pi];
				i = pi;
				pi = i >> 1;
			}
			mHeap[i] = new Node(value, priority);
		}

		public V Dequeue() {
			if (mCount <= 0) {
				throw new InvalidOperationException("Empty Queue");
			}
			Node head = mHeap[1];
			mHeap[1] = mHeap[mCount];
			mHeap[mCount] = new Node();
			mCount--;
			Heapify(1);
			return head.value;
		}

		public V Dequeue(out P priority) {
			if (mCount <= 0) {
				throw new InvalidOperationException("Empty Queue");
			}
			Node head = mHeap[1];
			mHeap[1] = mHeap[mCount];
			mHeap[mCount] = new Node();
			mCount--;
			Heapify(1);
			priority = head.priority;
			return head.value;
		}

		public V Peek() {
			if (mCount <= 0) {
				throw new InvalidOperationException("Empty Queue");
			}
			Node head = mHeap[1];
			return head.value;
		}

		public V Peek(out P priority) {
			if (mCount <= 0) {
				throw new InvalidOperationException("Empty Queue");
			}
			Node head = mHeap[1];
			priority = head.priority;
			return head.value;
		}

		public int Count { get { return mCount; } }

		public void Clear() {
			mHeap.Clear();
			mHeap.Add(new Node());
			mCount = 0;
		}

		private void Heapify(int i) {
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
				mHeap[i] = n;
				Heapify(h);
			}
		}

		private struct Node {

			public V value;
			public P priority;

			public Node(V value, P priority) {
				this.value = value;
				this.priority = priority;
			}

		}

	}

}
