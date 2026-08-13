using System;

namespace EF.DataDriven {

	public class GreatEvent : GreatEventBase<Action> {
		public GreatEvent(out IGreatEventCtrl ctrl) : base(out ctrl) { }
		public void Invoke() {
			Invoking invoking = BeginInvoke();
			if (invoking == null) { return; }
			Action handler;
			while ((handler = invoking.Next()) != null) {
				handler.Invoke();
			}
		}
	}

	public class GreatEvent<T> : GreatEventBase<Action<T>> {
		public GreatEvent(out IGreatEventCtrl ctrl) : base(out ctrl) { }
		public void Invoke(T para) {
			Invoking invoking = BeginInvoke();
			if (invoking == null) { return; }
			Action<T> handler;
			while ((handler = invoking.Next()) != null) {
				handler.Invoke(para);
			}
		}
	}

	public class GreatEvent<T1, T2> : GreatEventBase<Action<T1, T2>> {
		public GreatEvent(out IGreatEventCtrl ctrl) : base(out ctrl) { }
		public void Invoke(T1 para1, T2 para2) {
			Invoking invoking = BeginInvoke();
			if (invoking == null) { return; }
			Action<T1, T2> handler;
			while ((handler = invoking.Next()) != null) {
				handler.Invoke(para1, para2);
			}
		}
	}

	public class GreatEvent<T1, T2, T3> : GreatEventBase<Action<T1, T2, T3>> {
		public GreatEvent(out IGreatEventCtrl ctrl) : base(out ctrl) { }
		public void Invoke(T1 para1, T2 para2, T3 para3) {
			Invoking invoking = BeginInvoke();
			if (invoking == null) { return; }
			Action<T1, T2, T3> handler;
			while ((handler = invoking.Next()) != null) {
				handler.Invoke(para1, para2, para3);
			}
		}
	}

	public class GreatEvent<T1, T2, T3, T4> : GreatEventBase<Action<T1, T2, T3, T4>> {
		public GreatEvent(out IGreatEventCtrl ctrl) : base(out ctrl) { }
		public void Invoke(T1 para1, T2 para2, T3 para3, T4 para4) {
			Invoking invoking = BeginInvoke();
			if (invoking == null) { return; }
			Action<T1, T2, T3, T4> handler;
			while ((handler = invoking.Next()) != null) {
				handler.Invoke(para1, para2, para3, para4);
			}
		}
	}

	public class GreatEvent<T1, T2, T3, T4, T5> : GreatEventBase<Action<T1, T2, T3, T4, T5>> {
		public GreatEvent(out IGreatEventCtrl ctrl) : base(out ctrl) { }
		public void Invoke(T1 para1, T2 para2, T3 para3, T4 para4, T5 para5) {
			Invoking invoking = BeginInvoke();
			if (invoking == null) { return; }
			Action<T1, T2, T3, T4, T5> handler;
			while ((handler = invoking.Next()) != null) {
				handler.Invoke(para1, para2, para3, para4, para5);
			}
		}
	}

}
