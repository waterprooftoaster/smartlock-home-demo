
using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.Animations;
using System.Text.RegularExpressions;
using YLYEditorGUI;

public class FindUnUsedAssetWindow : EditorWindow {
	
	private enum Stage {
		None = 0,
		BeginGuidParse = 1,
		GuidParsing = 2,
		EndGuidParse = 3,
		GuidMatching = 4,
		EndGuidMatch = 5,
		BeginCodeParse = 6,
		CodeParsing = 7,
		EndCodeParse = 8,
		CodeMatching = 9,
		EndCodeMatch = 10,
	};

	
	private enum ErrorCode {
		None = 0,
		RefFindAssetNone = 1,
		WaitForRefFindGuidParse = 2,
	};

	
	private class ToggleFilter {
		public Rect rect;
		public bool oldIsSelect;
		public bool isSelect;
		public bool isSelChange;
		public string title;
		public ToggleFilter(Rect rect, bool isSelect, string title){
			this.rect = rect;
			this.isSelect = isSelect;
			this.oldIsSelect = isSelect;
			this.isSelChange = false;
			this.title = title;
		}

		public void OnGUI(){
			isSelect = GUI.Toggle(rect, isSelect, title);
			if (isSelect != oldIsSelect) {
				oldIsSelect = isSelect;
				isSelChange = true;
			} else {
				isSelChange = false;
			}
		}
	}

	
	private Dictionary<Stage, string> StageTip = new Dictionary<Stage, string>(){
		{Stage.None, ""},
		{Stage.BeginGuidParse, "guid"},
		{Stage.GuidParsing, "guid..."},
		{Stage.EndGuidParse, "guid"},
		{Stage.GuidMatching, "..."},
		{Stage.EndGuidMatch, ""},
		{Stage.BeginCodeParse, ""},
		{Stage.CodeParsing, "..."},
		{Stage.EndCodeParse, ""},
		{Stage.CodeMatching, "..."},
		{Stage.EndCodeMatch, ""},
	};

	
	private Dictionary<ErrorCode, string> ErrorTip = new Dictionary<ErrorCode, string>(){
		{ErrorCode.None, ""},
		{ErrorCode.RefFindAssetNone, "(！)"},
		{ErrorCode.WaitForRefFindGuidParse, "(guid-->)"},
	};

	
	private ToggleFilter[] codeFindToggleFilters = {
		new ToggleFilter(new Rect (10, 250, 50, 20), true, "all"),
		new ToggleFilter(new Rect (56, 250, 60, 20), true, ".prefab"),
		new ToggleFilter(new Rect (130, 250, 50, 20), true, ".png"),
		new ToggleFilter(new Rect (190, 250, 50, 20), true, ".unity"),
		new ToggleFilter(new Rect (250, 250, 50, 20), true, ".mat"),
		new ToggleFilter(new Rect (310, 250, 50, 20), true, ".fbx"),
		new ToggleFilter(new Rect (370, 250, 80, 20), true, ".controller"),
		new ToggleFilter(new Rect (460, 250, 70, 20), true, ".shader"),
		new ToggleFilter(new Rect (10, 270, 50, 20), true, ".tga"),
		new ToggleFilter(new Rect (56, 270, 60, 20), true, ".mp3"),
		new ToggleFilter(new Rect (130, 270, 50, 20), true, ".ogg"),
	};

	private static FindUnUsedAssetWindow _instance = null;

	
	private SerializationMode oldSerializationMode = SerializationMode.ForceText;
	private HashSet<string> collectFiles = null;
	private float winWidthHalf = 0f;
	private float leftGuiWidth = 0f;
	private float rightGuiWidth = 0f;
	private bool isCollectAssetDly = false;
	private bool isParseCodeDly = true;
	private bool isParseGuidDly = false;
	

	
	private Stage codeFindStage = Stage.None;
	private string[] codeFindFiles = null;
	private string[] codeScriptFiles = null;
	private string[] codeShaderFiles = null;
	private string saveCodeFindRltPath = null;
	private EditorApplication.CallbackFunction codeFindUpdate = null;
	private int codeFindProgressCur = 0;
	private int codeFindProgressTotal = 0;
	private Vector2 codeFindRltScrollPos = Vector2.zero;
	private bool ignoreCheckScene = true;
	private HashSet<string> codeFindUnUsedFiles = null;
	private string[] codeFindUnUsedFilterFiles = null;
	private Dictionary<string, bool> codeFindUnUsedFileDic = null;
	private Dictionary<string, string> codeFindNameToPathDic = null;
	private bool isCodeFindToggleChg = false;
	private bool isCodeFindSearchChg = false;
	private string codeFindSearchStr = "";
	private ErrorCode codeFindErrorCode = ErrorCode.None;
	private string assetPathRegStr = @"['""]{1}((assets[/\\]{1}.+?)?[^'""/\*:\?\\]+?\.(prefab|png|tga|unity|mat|mp3|ogg))['""]{1}"; 
	private string shaderPathRegStr = @"Shader(?:[ \t]*|.Find[ \t]*\([ \t]*)['""]{1}([^'""]+?)['""]{1}";
	

	
	private Stage refFindStage = Stage.None;
	private string[] guidAssetfiles = null;
	private EditorApplication.CallbackFunction refFindUpdate = null;
	private int refFindProgressCur = 0;
	private int refFindProgressTotal = 0;
	private Vector2 refFindRltScrollPos = Vector2.zero;
	private TreeNode refFindTreeRootNode = null;
	private UnityEngine.Object refFindAsset = null;
	private ErrorCode refFindErrorCode = ErrorCode.None;
	private int curRefFindAssetId = 0;
	private List<string> refFindExtensions = new List<string>(){".prefab", ".unity", ".mat", ".controller", ".asset"};
	private Dictionary<string, HashSet<string>> refFindGuidDic = null;
	private string guidRegStr = @"guid: ([0-9a-z]+?),";
	
	
	[MenuItem("YLY/")]
	[MenuItem("Assets/YLY")]
	static void Init() 
	{
		if (_instance == null) {
			_instance = EditorWindow.GetWindow(typeof(FindUnUsedAssetWindow), false, "") as FindUnUsedAssetWindow;
			_instance.position = new UnityEngine.Rect (320f, 80f, 1112f, 786f);
			_instance.OnInit();
		}
		_instance.Show();
	} 

	void OnInit()
	{
		
		oldSerializationMode = EditorSettings.serializationMode;
		if(EditorSettings.serializationMode != SerializationMode.ForceText){
			EditorSettings.serializationMode = SerializationMode.ForceText;
		}
	}

	void OnDestroy() 
	{
		
		if(EditorSettings.serializationMode != oldSerializationMode){
			EditorSettings.serializationMode = oldSerializationMode;
		}

		codeFindStage = Stage.None;
		refFindStage = Stage.None;
		codeFindErrorCode = ErrorCode.None;
		refFindErrorCode = ErrorCode.None;

		if(refFindTreeRootNode != null){
			refFindTreeRootNode.Destroy();
		}
		if(codeFindUpdate != null){
			EditorApplication.update -= codeFindUpdate;
			codeFindUpdate = null;
		}
		if(refFindUpdate != null){
			EditorApplication.update -= refFindUpdate;
			refFindUpdate = null;
		}

		collectFiles = null;
		codeFindFiles = null;
		codeScriptFiles = null;
		codeShaderFiles = null;
		codeFindUnUsedFiles = null;
		codeFindUnUsedFilterFiles = null;
		codeFindUnUsedFileDic = null;
		codeFindNameToPathDic = null;
		codeFindToggleFilters = null;
		guidAssetfiles = null;
		refFindTreeRootNode = null;
		refFindAsset = null;
		if(refFindGuidDic != null){
			refFindGuidDic.Clear();
			refFindGuidDic = null;
		}

		_instance = null;
	}

	void OnGUI()
	{
		BeginWindows();

		winWidthHalf = position.width / 2;

		isCollectAssetDly = GUI.Toggle(new Rect (10, 10, 600, 20), isCollectAssetDly, "（：，；：，）");
		isParseCodeDly = GUI.Toggle(new Rect (10, 30, 600, 20), isParseCodeDly, "（：，；：，）");
		isParseGuidDly = GUI.Toggle(new Rect (10, 50, 600, 20), isParseGuidDly, "guid（：guid，；：guid，）");

		EditorGUI.DrawRect (new Rect (0, 70, position.width, 2), Color.green);
		EditorGUI.DrawRect (new Rect (winWidthHalf - 1, 70, 2, position.height - 70), Color.green);
		
		OnCodeFindGUI();
		OnRefFindGUI();

		EndWindows();
	}

	
	void OnCodeFindGUI()
	{
		leftGuiWidth = winWidthHalf - 20;

		bool oldCheckScene = ignoreCheckScene;
		ignoreCheckScene = GUI.Toggle(new Rect (10, 80, 150, 20), ignoreCheckScene, "");
		if(oldCheckScene != ignoreCheckScene){
			collectFiles = null;
			codeFindUnUsedFiles = null;
			refFindGuidDic = null;
			SetCodeFindStage(Stage.BeginCodeParse);
		}

		if (GUI.Button (new Rect (10, 110, leftGuiWidth, 50), "")) {
			SetCodeFindStage(Stage.BeginCodeParse);
		}

		float progressPercent = 1f;
		if (codeFindStage == Stage.BeginCodeParse) {
			SetCodeFindStage(Stage.CodeParsing);
			codeFindErrorCode = ErrorCode.None;
			CollectAssets ();
			ParseCode ();
		} else if (codeFindStage == Stage.CodeParsing) {
			if (codeFindProgressTotal > 0) {
				progressPercent = (float)codeFindProgressCur / (float)codeFindProgressTotal;
			}
			if (progressPercent >= 1f) {
				SetCodeFindStage(Stage.EndCodeParse);
			}
		} else if (codeFindStage == Stage.EndCodeParse) {
			SetCodeFindStage(Stage.CodeMatching);
			SetRefFindStage(Stage.BeginGuidParse);
		} else if (codeFindStage == Stage.CodeMatching) {
			if (refFindStage == Stage.EndGuidMatch) {
				codeFindErrorCode = ErrorCode.None;
				MatchCode ();
				isCodeFindToggleChg = true;
				SetCodeFindStage(Stage.EndCodeMatch);
				SetRefFindStage(Stage.GuidMatching);
			} else {
				codeFindErrorCode = ErrorCode.WaitForRefFindGuidParse;
			}
		}

		GUI.SetNextControlName("searchInputField");
		codeFindSearchStr = EditorGUI.TextField (new Rect (10, 290, 450, 18), codeFindSearchStr);
		if ((Event.current != null && Event.current.isKey && Event.current.type == EventType.KeyUp && 
			GUI.GetNameOfFocusedControl() == "searchInputField")) {
			isCodeFindSearchChg = true;
			Repaint();
		} else if (GUI.Button (new Rect (465, 290, 45, 18), "")) {
			isCodeFindSearchChg = true;
		}

		bool isAllSelectChg = false;
		for(int i=0; i<codeFindToggleFilters.Length; i++){
			if(isAllSelectChg){
				codeFindToggleFilters[i].isSelect = codeFindToggleFilters [0].isSelect;
			}

			codeFindToggleFilters[i].OnGUI();
			if(!isCodeFindToggleChg && codeFindToggleFilters[i].isSelChange){
				isCodeFindToggleChg = true;
			}

			if (i == 0) {
				isAllSelectChg = codeFindToggleFilters[i].isSelChange;
			}
		}

		if((isCodeFindToggleChg || isCodeFindSearchChg) && codeFindUnUsedFiles != null){
			isCodeFindToggleChg = false;
			isCodeFindSearchChg = false;
			if (codeFindToggleFilters [0].isSelect) {
				codeFindUnUsedFilterFiles = null;
				if (!string.IsNullOrEmpty (codeFindSearchStr)) {
					codeFindUnUsedFilterFiles = codeFindUnUsedFiles.Where (s => s.ToLower().Contains(codeFindSearchStr.ToLower())).ToArray ();
				} else {
					codeFindUnUsedFilterFiles = codeFindUnUsedFiles.ToArray();
				}
				Array.Sort<string>(codeFindUnUsedFilterFiles, StringComparer.OrdinalIgnoreCase);
			} else {
				List<string> codeFindRltExts = new List<string>();
				for(int i=1; i<codeFindToggleFilters.Length; i++){
					if (codeFindToggleFilters[i].isSelect) {
						codeFindRltExts.Add(codeFindToggleFilters[i].title);
					}
				}

				codeFindUnUsedFilterFiles = null;
				if (!string.IsNullOrEmpty (codeFindSearchStr)) {
					codeFindUnUsedFilterFiles = codeFindUnUsedFiles.Where (s => codeFindRltExts.Contains (Path.GetExtension (s).ToLower ()) && s.ToLower().Contains(codeFindSearchStr.ToLower())).ToArray ();
				} else {
					codeFindUnUsedFilterFiles = codeFindUnUsedFiles.Where (s => codeFindRltExts.Contains (Path.GetExtension (s).ToLower ())).ToArray ();
				}
				Array.Sort<string>(codeFindUnUsedFilterFiles, StringComparer.OrdinalIgnoreCase);
			}
		}

		if (codeFindStage == Stage.EndCodeMatch) {
			if (GUI.Button (new Rect (winWidthHalf - 96, 77, 86, 28), "")) {
				saveCodeFindRltPath = EditorUtility.SaveFilePanel("", saveCodeFindRltPath, "().txt", "txt");
				if (codeFindUnUsedFilterFiles != null && !string.IsNullOrEmpty (saveCodeFindRltPath)) {
					string dirPath = System.IO.Path.GetDirectoryName (saveCodeFindRltPath);
					if (!System.IO.Directory.Exists(dirPath)) {
						try {
							System.IO.Directory.CreateDirectory(dirPath);
						} catch (Exception exp) {
							Debug.LogError(string.Format("create directory fail {0}", exp.ToString()));
						}
					}

					StringBuilder sb = new StringBuilder();
					foreach (string assetName in codeFindUnUsedFilterFiles) {
						sb.Append(assetName);
						sb.Append("\n");
					}
					System.IO.File.WriteAllText(saveCodeFindRltPath, sb.ToString());
					sb.Remove(0, sb.Length);
					sb = null;
				}
			}

			GUI.Label (new Rect (10, 230, 360, 20), string.Format ("()（{0}）：", codeFindUnUsedFilterFiles != null ? codeFindUnUsedFilterFiles.Length : 0));

			codeFindRltScrollPos = GUI.BeginScrollView (new Rect (10, 310, leftGuiWidth, position.height - 310), codeFindRltScrollPos, new Rect (0, 0, 800, codeFindUnUsedFilterFiles != null ? codeFindUnUsedFilterFiles.Length * 20 : 1), true, true);
			if (codeFindUnUsedFilterFiles != null) {
				GUIStyle btnTextGuiStyle = new GUIStyle();
				btnTextGuiStyle.fontSize = 11;
				btnTextGuiStyle.normal.textColor = new Color (0.8f, 0.36f, 0.36f);
				btnTextGuiStyle.alignment = TextAnchor.MiddleLeft;

				int i = 0;
				foreach (string assetName in codeFindUnUsedFilterFiles) {
					if (GUI.Button (new Rect (0, i * 20, 800, 20), assetName, btnTextGuiStyle)) {
						Selection.activeObject = AssetDatabase.LoadAssetAtPath (assetName, typeof(UnityEngine.Object));
					}
					i++;
				}
			}
			GUI.EndScrollView ();
		} else {
			GUI.Label(new Rect(10, 230, 360, 20), "()：");
			codeFindRltScrollPos = GUI.BeginScrollView (new Rect (10, 310, leftGuiWidth, position.height - 310), codeFindRltScrollPos, new Rect (0, 0, 800, 1), true, true);
			GUI.EndScrollView ();
		}

		EditorGUI.ProgressBar (new Rect (10, 170, leftGuiWidth, 50), progressPercent, string.Format("{0}({1}/{2}) {3}", StageTip [codeFindStage], 
			codeFindProgressCur, codeFindProgressTotal, ErrorTip [codeFindErrorCode]));
	}

	
	void OnRefFindGUI()
	{
		rightGuiWidth = winWidthHalf - 20;

		if(refFindTreeRootNode == null){
			refFindTreeRootNode = new TreeNode("：");
			refFindTreeRootNode.isRoot = true;
		}

		GUI.Label(new Rect(position.width - rightGuiWidth - 10, 80, 150, 20), "");
		refFindAsset = EditorGUI.ObjectField (new Rect (position.width - rightGuiWidth + 150, 80, 250, 18), refFindAsset, typeof(UnityEngine.Object));

		if (refFindAsset != null) {
			int assetId = refFindAsset.GetInstanceID ();
			if (curRefFindAssetId != assetId) {
				curRefFindAssetId = assetId;
				if (refFindStage == Stage.EndGuidMatch) {
					SetRefFindStage(Stage.GuidMatching);
				}
			}
		} else {
			
			if (curRefFindAssetId != 0 && refFindStage == Stage.EndGuidMatch) {
				SetRefFindStage(Stage.GuidMatching);
			}
			curRefFindAssetId = 0;
		}

		if (GUI.Button (new Rect (position.width - rightGuiWidth - 10, 110, rightGuiWidth, 50), "")) {
			SetRefFindStage(Stage.BeginGuidParse);
		}

		float progressPercent = 1f;
		if (refFindStage == Stage.BeginGuidParse) {
			SetRefFindStage(Stage.GuidParsing);
			refFindErrorCode = ErrorCode.None;
			CollectAssets();
			ParseAssetGuid();
		} else if (refFindStage == Stage.GuidParsing) {
			if (refFindProgressTotal > 0) {
				progressPercent = (float)refFindProgressCur / (float)refFindProgressTotal;
			}
			if(progressPercent >= 1f){
				SetRefFindStage(Stage.EndGuidParse);
			}
		} else if (refFindStage == Stage.EndGuidParse) {
			SetRefFindStage(Stage.GuidMatching);
		} else if (refFindStage == Stage.GuidMatching) {
			refFindErrorCode = ErrorCode.None;
			MatchAssetGuid();
			SetRefFindStage(Stage.EndGuidMatch);
		}

		if (refFindStage == Stage.EndGuidMatch) {
			refFindTreeRootNode.title = string.Format ("（{0}）：", refFindTreeRootNode.children.Count);
		} else {
			refFindTreeRootNode.title = "：";
		}

		EditorGUI.ProgressBar (new Rect (position.width - rightGuiWidth - 10, 170, rightGuiWidth, 50), progressPercent, string.Format("{0}({1}/{2}) {3}", StageTip [refFindStage], 
			refFindProgressCur, refFindProgressTotal, ErrorTip [refFindErrorCode]));
		
		GUI.Label(new Rect(position.width - rightGuiWidth - 10, 230, 100, 20), "<--：");
		if (codeFindStage == Stage.EndCodeMatch) {
			GUI.Label (new Rect (position.width - rightGuiWidth + 90, 230, 100, 20), "!：");
		}

		refFindRltScrollPos = GUI.BeginScrollView (new Rect (position.width - rightGuiWidth - 10, 255, rightGuiWidth, position.height - 255), refFindRltScrollPos, new Rect (0, 0, refFindTreeRootNode.recursiveSize.x, refFindTreeRootNode.recursiveSize.y), true, true);
		refFindTreeRootNode.OnGUI();
		GUI.EndScrollView ();
	}

	
	void SetRefFindStage(Stage stage){
		refFindStage = stage;

		Repaint(); 
	}

	
	void SetCodeFindStage(Stage stage){
		codeFindStage = stage;

		Repaint(); 
	}

	
	void CollectAssets(){
		if(collectFiles != null && !isCollectAssetDly){
			return;
		}

		string[] files = AssetDatabase.GetAllAssetPaths();
		files = files.Where (s => s.StartsWith("Assets/") && !s.StartsWith("Assets/StreamingAssets/") && !s.EndsWith (".DS_Store") && File.Exists(s)).ToArray ();

		collectFiles = null;
		collectFiles = new HashSet<string>(files);

		
		codeFindFiles = null;
		if (ignoreCheckScene) {
			codeFindFiles = collectFiles.Where (s => !s.EndsWith (".cs") && !s.EndsWith (".lua") && !s.EndsWith (".unity")).ToArray ();
		} else {
			codeFindFiles = collectFiles.Where (s => !s.EndsWith (".cs") && !s.EndsWith (".lua")).ToArray ();
		}

		
		codeScriptFiles = null;
		codeScriptFiles = collectFiles.Where (s => s.EndsWith (".cs") || s.EndsWith (".lua")).ToArray ();

		codeShaderFiles = null;
		codeShaderFiles = collectFiles.Where (s => s.EndsWith (".shader")).ToArray ();

		codeFindNameToPathDic = null;
		codeFindNameToPathDic = codeFindFiles.ToLookup(k1 => Path.GetFileName(k1), v1 => v1).ToDictionary (k2 => k2.Key, v2 => v2.First());

		guidAssetfiles = null;
		guidAssetfiles = collectFiles.Where(s => refFindExtensions.Contains(Path.GetExtension(s).ToLower())).ToArray();
	}

	
	void ParseCode(){
		if(codeFindUnUsedFiles != null && !isParseCodeDly){
			return;
		}

		if(codeFindUpdate != null){
			EditorApplication.update -= codeFindUpdate;
			codeFindUpdate = null;
		}

		codeFindProgressCur = 0;
		codeFindProgressTotal = codeShaderFiles.Length + codeScriptFiles.Length;
		codeFindUnUsedFiles = null;
		codeFindUnUsedFiles = new HashSet<string>(codeFindFiles, StringComparer.OrdinalIgnoreCase); 

		Dictionary<string, string> shaderNameToPath = new Dictionary<string, string>();
		HashSet<string> findAssetPaths = new HashSet<string>();
		string absolutePath;
		string fileContent;
		codeFindUpdate = () => {
			Repaint();

			if(codeFindProgressCur >= codeFindProgressTotal){
				EditorApplication.update -= codeFindUpdate;
				codeFindUpdate = null;
				codeFindUnUsedFiles.ExceptWith(findAssetPaths);
				codeFindUnUsedFileDic = null;
				codeFindUnUsedFileDic = codeFindUnUsedFiles.ToDictionary (k => k, v => true);
				return;
			}
			
			if(codeFindProgressCur < codeShaderFiles.Length){
				
				absolutePath = System.IO.Path.Combine(Application.dataPath, Regex.Replace(codeShaderFiles[codeFindProgressCur], "^Assets/", ""));
				fileContent = File.ReadAllText(absolutePath);
				foreach (System.Text.RegularExpressions.Match mtch in Regex.Matches(fileContent, shaderPathRegStr)) {
					string shaderName = mtch.Groups [1].Value;
					if(!shaderNameToPath.ContainsKey(shaderName)){
						shaderNameToPath.Add(shaderName, codeShaderFiles[codeFindProgressCur].Replace(@"\\", @"/"));
					}
				}
			} else {
				
				absolutePath = System.IO.Path.Combine(Application.dataPath, Regex.Replace(codeScriptFiles[codeFindProgressCur - codeShaderFiles.Length], "^Assets/", ""));
				fileContent = File.ReadAllText(absolutePath);
				foreach (System.Text.RegularExpressions.Match mtch in Regex.Matches(fileContent, assetPathRegStr, RegexOptions.IgnoreCase)) {
					string assetName = mtch.Groups [1].Value;
					if(mtch.Groups [2].Value == ""){
						if(codeFindNameToPathDic.ContainsKey(assetName)){
							findAssetPaths.Add (codeFindNameToPathDic[assetName]);
						}
					} else {
						findAssetPaths.Add (assetName.Replace(@"\\", @"/"));
					}
				}
				
				foreach (System.Text.RegularExpressions.Match mtch in Regex.Matches(fileContent, shaderPathRegStr)) {
					string shaderName = mtch.Groups [1].Value;
					if(shaderNameToPath.ContainsKey(shaderName)){
						findAssetPaths.Add (shaderNameToPath[shaderName]);
					}
				}
			}

			codeFindProgressCur++;
		};

		EditorApplication.update += codeFindUpdate;
	}

	
	void ParseAssetGuid(){
		if(refFindGuidDic != null && !isParseGuidDly){
			return;
		}

		if(refFindUpdate != null){
			EditorApplication.update -= refFindUpdate;
			refFindUpdate = null;
		}

		refFindGuidDic = null;
		refFindGuidDic = new Dictionary<string, HashSet<string>>();
		refFindProgressCur = 0;
		refFindProgressTotal = guidAssetfiles.Length;

		string absolutePath;
		refFindUpdate = () => {
			Repaint();

			if(refFindProgressCur >= refFindProgressTotal){
				EditorApplication.update -= refFindUpdate;
				refFindUpdate = null;
				return;
			}

			absolutePath = System.IO.Path.Combine(Application.dataPath, Regex.Replace(guidAssetfiles[refFindProgressCur], "^Assets/", ""));

			foreach (System.Text.RegularExpressions.Match mtch in Regex.Matches(File.ReadAllText(absolutePath), guidRegStr)) {
				string guid = mtch.Groups [mtch.Groups.Count - 1].Value;
				HashSet<string> list = null;
				if (!refFindGuidDic.TryGetValue (guid, out list)) {
					list = new HashSet<string> ();
					refFindGuidDic.Add (guid, list);
				}
				list.Add (guidAssetfiles [refFindProgressCur]);
			}
			refFindProgressCur++;
		};

		EditorApplication.update += refFindUpdate;
	}

	
	void MatchCode(){
		if (codeFindUnUsedFiles == null || refFindGuidDic == null) {
			return;
		}

		HashSet<string> beRefAssetFiles = new HashSet<string>();
		HashSet<string> list = null;
		string guid;
		HashSet<string> allRefAssetPaths = new HashSet<string>();
		bool isAllRefUnUsed = false;

		foreach (string assetName in codeFindUnUsedFiles) {
			guid = AssetDatabase.AssetPathToGUID(assetName);
			if (guid != null && refFindGuidDic.TryGetValue (guid, out list)) {
				isAllRefUnUsed = true;
				allRefAssetPaths.Clear();
				GetAllRefAssets(assetName, allRefAssetPaths);
				foreach (string assetName1 in allRefAssetPaths) {
					if(!codeFindUnUsedFileDic.ContainsKey(assetName1)){
						isAllRefUnUsed = false;
						break;
					}
				}

				if(!isAllRefUnUsed){
					beRefAssetFiles.Add(assetName);
				}
			}
		}

		codeFindUnUsedFiles.ExceptWith(beRefAssetFiles);
		foreach (string assetName in beRefAssetFiles) {
			if(codeFindUnUsedFileDic.ContainsKey(assetName)){
				codeFindUnUsedFileDic.Remove(assetName);
			}
		}
		codeFindUnUsedFilterFiles = null;
		codeFindUnUsedFilterFiles = codeFindUnUsedFiles.ToArray();
		Array.Sort<string>(codeFindUnUsedFilterFiles, StringComparer.OrdinalIgnoreCase);
	}

	void GetAllRefAssets(string assetPath, HashSet<string> refRootAssetPaths){
		string guid = AssetDatabase.AssetPathToGUID(assetPath);

		HashSet<string> list = null;
		if (refFindGuidDic.TryGetValue (guid, out list)) {
			foreach (string assetName in list) {
				refRootAssetPaths.Add(assetName);
				GetAllRefAssets(assetName, refRootAssetPaths);
			}
		}
	}

	
	void MatchAssetGuid(){
		refFindTreeRootNode.DestroyAllChild();

		if (refFindGuidDic == null) {
			return;
		}

		if (refFindAsset == null) {
			return;
		}

		string assetPath = AssetDatabase.GetAssetPath(refFindAsset);
		GenerRefTreeNodeRecly(assetPath, null);
	}

	
	void GenerRefTreeNodeRecly(string assetPath, TreeNode childNode){
		if (string.IsNullOrEmpty (assetPath)) {
			return;
		}

		if(childNode == null){
			childNode = new TreeNode (null, AssetDatabase.LoadAssetAtPath (assetPath, typeof(UnityEngine.Object)));
			if (codeFindUnUsedFileDic != null && codeFindUnUsedFileDic.ContainsKey (assetPath)) {
				childNode.SetSubTitle ("<--!", Color.red);
			} else {
				childNode.SetSubTitle ("<--", Color.green);
			}
			GenerDependTreeNodes(assetPath, childNode);
		}

		HashSet<string> list = null;
		string guid = AssetDatabase.AssetPathToGUID(assetPath);
		if (refFindGuidDic.TryGetValue (guid, out list)) {
			foreach (string assetName in list) {
				
				TreeNode parentNode = new TreeNode (null, AssetDatabase.LoadAssetAtPath (assetName, typeof(UnityEngine.Object)));
				if (codeFindUnUsedFileDic != null && codeFindUnUsedFileDic.ContainsKey (assetName)) {
					parentNode.SetSubTitle ("!", Color.red);
				}
				if (childNode.parent == null) {
					parentNode.AddChild (childNode);
				} else {
					TreeNode childNodeClone = childNode.Clone();
					parentNode.AddChild(childNodeClone);
				}

				GenerRefTreeNodeRecly(assetName, parentNode);
			}
		} else {
			if (childNode != null) {
				refFindTreeRootNode.AddChild (childNode);
			}
		}
	}
	
	
	void GenerDependTreeNodes(string assetPath, TreeNode parentNode){
		if (string.IsNullOrEmpty (assetPath) || parentNode == null) {
			return;
		}

		string[] dependencies = AssetDatabase.GetDependencies(new string[] { assetPath }, false);
		if(dependencies.Length == 0){
			return;
		}
		Array.Sort<string>(dependencies, StringComparer.OrdinalIgnoreCase);

		foreach (string assetName in dependencies) {
			TreeNode childNode = new TreeNode (null, AssetDatabase.LoadAssetAtPath (assetName, typeof(UnityEngine.Object)));
			if (codeFindUnUsedFileDic != null && codeFindUnUsedFileDic.ContainsKey (assetName)) {
				childNode.SetSubTitle ("!", Color.red);
			}
			parentNode.AddChild(childNode);
		}
	}
}
