namespace EF.UI.WFramework {

	public interface IUILoadingOverlay {

		void BeginLoading(string key);

		void EndLoading(string key);

	}

}
