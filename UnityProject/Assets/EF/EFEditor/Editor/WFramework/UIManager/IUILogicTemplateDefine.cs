using System;
using System.Collections.Generic;

namespace EF.UI.WFramework {

	public interface IUIStackLogicTemplateDefine {
		string GetNameSpace();
		string GetCodePath();
		Type GetBaseType();
		void GetPropertiesDefine(List<string> properties);
		void GetOnOpenDefine(out string def, out string gameObjectVar);
		string GetOnCloseDefine();
	}

	public interface IUIFixedLogicTemplateDefine {
		string GetNameSpace();
		string GetCodePath();
		Type GetBaseType();
		void GetPropertiesDefine(List<string> properties);
		void GetOnOpenDefine(out string def, out string gameObjectVar);
		string GetOnCloseDefine();
	}

}