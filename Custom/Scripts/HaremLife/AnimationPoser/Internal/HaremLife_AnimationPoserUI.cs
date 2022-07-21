using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System;
using System.Linq;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using MVR.FileManagementSecure;
using SimpleJSON;

namespace HaremLife
{
	public partial class AnimationPoser : MVRScript
	{
		private List<UIDynamic> myMenuCached = new List<UIDynamic>();
		private List<object> myMenuElements = new List<object>();

		private int myMenuItem = 0;
		private UIDynamicTabBar myMenuTabBar;
		private static JSONStorableStringChooser myMainAnimation;
		private static JSONStorableStringChooser myMainState;
		private static JSONStorableStringChooser myMainLayer;
		private JSONStorableString myGeneralInfo;
		private JSONStorableString myPlayInfo;
		private JSONStorableStringChooser myCapturesControlList;
		private JSONStorableStringChooser myCapturesMorphList;
		private UIDynamicPopup myCapturesMorphPopup;
		private JSONStorableString myCapturesMorphFullname;
		private JSONStorableBool myStateAutoTransition;
		private JSONStorableStringChooser myAnchorCaptureList;
		private JSONStorableStringChooser myAnchorModeList;
		private JSONStorableStringChooser myAnchorAtomListA;
		private JSONStorableStringChooser myAnchorAtomListB;
		private JSONStorableStringChooser myAnchorControlListA;
		private JSONStorableStringChooser myAnchorControlListB;
		private JSONStorableFloat myAnchorBlendRatio;
		private JSONStorableFloat myAnchorDampingTime;
		private JSONStorableStringChooser myMessageList;
		private JSONStorableStringChooser myTargetAnimationList;
		private JSONStorableStringChooser myTargetLayerList;
		private JSONStorableStringChooser mySourceAnimationList;
		private JSONStorableStringChooser mySourceLayerList;
		private JSONStorableStringChooser mySourceStateList;
		private JSONStorableStringChooser myTargetStateList;
		private JSONStorableStringChooser mySyncRoleList;
		private JSONStorableStringChooser mySyncLayerList;
		private JSONStorableStringChooser mySyncStateList;
		private JSONStorableStringChooser myRoleList;
		private JSONStorableStringChooser myPersonList;
		private JSONStorableBool myOptionsDefaultToWorldAnchor;
		private JSONStorableBool myDebugShowInfo;
		private JSONStorableBool myDebugShowPaths;
		private JSONStorableBool myDebugShowSelectedOnly;
		private JSONStorableBool myDebugShowTransitions;

		private static JSONStorableFloat myGlobalDefaultTransitionDuration;
		private JSONStorableFloat myGlobalDefaultEaseInDuration;
		private JSONStorableFloat myGlobalDefaultEaseOutDuration;
		private JSONStorableFloat myGlobalDefaultWaitDurationMin;
		private JSONStorableFloat myGlobalDefaultWaitDurationMax;

		private bool myIsFullRefresh = true;

		private readonly List<string> myAnchorModes = new List<string>() {
			"World",
			"Single Anchor",
			"Blend Anchor"
		};

		private const string FILE_EXTENSION = "animpose";
		private const string BASE_DIRECTORY = "Saves/PluginData/AnimationPoser";
		private const string COLORTAG_INTERMEDIATE = "<color=#804b00>";
		private const string COLORTAG_CONTROLPOINT = "<color=#005780>";

		private const string DEFAULT_MORPH_UID = "Left Thumb Bend";

		private const int MENU_PLAY        = 0;
		private const int MENU_ANIMATIONS  = 1;
		private const int MENU_LAYERS	   = 2;
		private const int MENU_STATES      = 3;
		private const int MENU_TRANSITIONS = 4;
		private const int MENU_TRIGGERS    = 5;
		private const int MENU_ANCHORS     = 6;
		private const int MENU_ROLES	   = 7;
		private const int MENU_MESSAGES    = 8;
		private const int MENU_OPTIONS     = 9;

		private GameObject myLabelWith2BXButtonPrefab;
		private GameObject myLabelWithMXButtonPrefab;

		// =======================================================================================


		private void InitUI()
		{
			FileManagerSecure.CreateDirectory(BASE_DIRECTORY);
			List<string> subdirectories = new List<string> {"Instances", "Animations", "Layers", "Roles", "Messages" };
			foreach(string subdirectory in subdirectories)
				FileManagerSecure.CreateDirectory(BASE_DIRECTORY+'/'+subdirectory);

			List<string> animationItems = new List<string>();
			myMainAnimation = new JSONStorableStringChooser("Animation", animationItems, "", "Animation");
			myMainAnimation.setCallbackFunction += UISelectAnimationAndRefresh;

			List<string> layerItems = new List<string>();
			myMainLayer = new JSONStorableStringChooser("Layer", layerItems, "", "Layer");
			myMainLayer.setCallbackFunction += UISelectLayerAndRefresh;

			List<string> stateItems = new List<string>();
			myMainState = new JSONStorableStringChooser("State", stateItems, "", "State");
			myMainState.setCallbackFunction += UISelectStateAndRefresh;

			myGeneralInfo = new JSONStorableString("Info", "");
			myPlayInfo = new JSONStorableString("Info", "");
			myPlayPaused = new JSONStorableBool("Animation Paused", false, UISetPaused);

			pluginLabelJSON.setCallbackFunction += (string label) => {
				if (!string.IsNullOrEmpty(label))
					label = ": " + label;
				myGeneralInfo.val = "<color=#606060><size=40><b>AnimationPoser"+label+"</b></size>\n" +
					"The most powerful random animation system.</color>";
			};
			pluginLabelJSON.setCallbackFunction(pluginLabelJSON.val);

			myCapturesControlList = new JSONStorableStringChooser("Control", new List<string>(), "", "Control");
			myCapturesMorphList = new JSONStorableStringChooser("Morphs", new List<string>(), "", "Morphs");
			myCapturesMorphFullname = new JSONStorableString("Fullname", "");
			myCapturesMorphList.setCallbackFunction += UIUpdateMorphFullename;

			myStateAutoTransition = new JSONStorableBool("Auto-Transition on State Change", true);

			myMainState = new JSONStorableStringChooser("State", stateItems, "", "State");
			myMainState.setCallbackFunction += UISelectStateAndRefresh;

			myAnchorCaptureList = new JSONStorableStringChooser("ControlCapture", new List<string>(), "", "Control Capture");
			myAnchorCaptureList.setCallbackFunction += (string v) => UIRefreshMenu();

			myAnchorModeList = new JSONStorableStringChooser("AnchorMode", myAnchorModes, "", "Anchor Mode");
			myAnchorModeList.setCallbackFunction += (string v) => UISetAnchors();
			myAnchorAtomListA = new JSONStorableStringChooser("AnchorAtomA", new List<string>(), "", "Anchor Atom A");
			myAnchorAtomListA.setCallbackFunction += (string v) => UISetAnchors();
			myAnchorAtomListB = new JSONStorableStringChooser("AnchorAtomB", new List<string>(), "", "Anchor Atom B");
			myAnchorAtomListB.setCallbackFunction += (string v) => UISetAnchors();
			myAnchorControlListA = new JSONStorableStringChooser("AnchorControlA", new List<string>(), "", "Anchor Control A");
			myAnchorControlListA.setCallbackFunction += (string v) => UISetAnchors();
			myAnchorControlListB = new JSONStorableStringChooser("AnchorControlB", new List<string>(), "", "Anchor Control B");
			myAnchorControlListB.setCallbackFunction += (string v) => UISetAnchors();
			myAnchorBlendRatio = new JSONStorableFloat("Anchor Blend Ratio", DEFAULT_ANCHOR_BLEND_RATIO, 0.0f, 1.0f, true, true);
			myAnchorBlendRatio.setCallbackFunction += (float v) => UISetAnchorsBlendDamp();
			myAnchorDampingTime = new JSONStorableFloat("Anchor Damping Time", DEFAULT_ANCHOR_DAMPING_TIME, 0.0f, 5.0f, true, true);
			myAnchorDampingTime.setCallbackFunction += (float v) => UISetAnchorsBlendDamp();

			myGlobalDefaultTransitionDuration = new JSONStorableFloat("Default Transition Duration", DEFAULT_TRANSITION_DURATION, 0.0f, 5.0f, true, true);
			myGlobalDefaultEaseInDuration = new JSONStorableFloat("Default Ease In Duration", DEFAULT_EASEIN_DURATION, 0.0f, 5.0f, true, true);
			myGlobalDefaultEaseOutDuration = new JSONStorableFloat("Default Ease Out Duration", DEFAULT_EASEOUT_DURATION, 0.0f, 5.0f, true, true);
			myGlobalDefaultWaitDurationMin = new JSONStorableFloat("Default Wait Duration Min", DEFAULT_WAIT_DURATION_MIN, 0.0f, 300.0f, true, true);
			myGlobalDefaultWaitDurationMax = new JSONStorableFloat("Default Wait Duration Max", DEFAULT_WAIT_DURATION_MAX, 0.0f, 300.0f, true, true);

			UIRefreshMenu();

			SuperController.singleton.BroadcastMessage("OnActionsProviderAvailable", this, SendMessageOptions.DontRequireReceiver);
		}

		private void OnDestroyUI()
		{
			SuperController.singleton.BroadcastMessage("OnActionsProviderDestroyed", this, SendMessageOptions.DontRequireReceiver);

			Utils.OnDestroyUI();

			if (myLabelWith2BXButtonPrefab != null)
				Destroy(myLabelWith2BXButtonPrefab);
			if (myLabelWithMXButtonPrefab != null)
				Destroy(myLabelWithMXButtonPrefab);
			myLabelWith2BXButtonPrefab = null;
			myLabelWithMXButtonPrefab = null;

			DestroyDebugCurves();
		}

		public void OnBindingsListRequested(List<object> bindings)
		{
			bindings.Add(new Dictionary<string, string>	{{ "Namespace", "HaremLife.AnimationPoser" }});
			bindings.Add(new JSONStorableAction("Open Play",        () => OpenTab(0)));
			bindings.Add(new JSONStorableAction("Open Captures",    () => OpenTab(1)));
			bindings.Add(new JSONStorableAction("Open States",      () => OpenTab(2)));
			bindings.Add(new JSONStorableAction("Open Transitions", () => OpenTab(3)));
			bindings.Add(new JSONStorableAction("Open Triggers",    () => OpenTab(4)));
			bindings.Add(new JSONStorableAction("Open Anchors",     () => OpenTab(5)));
			bindings.Add(new JSONStorableAction("Open Options",     () => OpenTab(6)));

			bindings.Add(new JSONStorableAction("Toggle Animation Paused", () => {
				myPlayPaused.val = !myPlayPaused.val;
				OpenTab(0);
			}));
			bindings.Add(new JSONStorableAction("Toggle Auto-Transition State", () => {
				myStateAutoTransition.val = !myStateAutoTransition.val;
				OpenTab(2);
			}));
		}

		private void OpenTab(int tabIdx)
		{
			// Code based on AcidBubbles Keybindings plugin. https://hub.virtamate.com/resources/keybindings.4400/

			// bring up Plugins UI
			SuperController sc = SuperController.singleton;
			sc.SelectController(containingAtom.mainController);
			sc.SetActiveUI("SelectedOptions");
			sc.ShowMainHUDMonitor();

			UITabSelector selector = containingAtom.gameObject.GetComponentInChildren<UITabSelector>(true);
			if (selector == null)
				return;
			selector.SetActiveTab("Plugins");

			// bring up this plugin's UI
			string lastPrefix = null;
			foreach (string uid in containingAtom.GetStorableIDs())
			{
				if (uid == null)
					continue;
				if (!uid.StartsWith("plugin#"))
					continue;
				int prefixEndIndex = uid.IndexOf("_");
				if (prefixEndIndex == -1)
					continue;
				string prefix = uid.Substring(0, prefixEndIndex);
				if (prefix == lastPrefix)
					continue;
				lastPrefix = prefix;
				MVRScript plugin = containingAtom.GetStorableByID(uid) as MVRScript;
				if (plugin != null)
					plugin.UITransform.gameObject.SetActive(plugin == this);
			}

			// switch to the desired tab
			UISelectMenu(tabIdx);
		}
		
		private RectTransform InitBasicRectTransformPrefab(string prefabName, Transform initTransform, Transform instTransform, float[] coordsArray, bool initialCast = true)
		{
			RectTransform myTransform = Instantiate(initTransform as RectTransform, instTransform);
			myTransform.name = prefabName;
			myTransform.anchorMax = new Vector2(coordsArray[0], coordsArray[1]);
			myTransform.anchorMin = new Vector2(coordsArray[2], coordsArray[3]);
			myTransform.offsetMax = new Vector2(coordsArray[4], coordsArray[5]);
			myTransform.offsetMin = new Vector2(coordsArray[6], coordsArray[7]);

			return myTransform;
		}

		private void InitPrefabs()
		{
			{
				myLabelWith2BXButtonPrefab = new GameObject("Label2BXButton");
				myLabelWith2BXButtonPrefab.SetActive(false);
				RectTransform rt = myLabelWith2BXButtonPrefab.AddComponent<RectTransform>();
				rt.anchorMax = new Vector2(0, 1);
				rt.anchorMin = new Vector2(0, 1);
				rt.offsetMax = new Vector2(535, -500);
				rt.offsetMin = new Vector2(10, -600);
				LayoutElement le = myLabelWith2BXButtonPrefab.AddComponent<LayoutElement>();
				le.flexibleWidth = 1;
				le.minHeight = 50;
				le.minWidth = 350;
				le.preferredHeight = 50;
				le.preferredWidth = 500;

				RectTransform backgroundTransform = InitBasicRectTransformPrefab(
					"Background", manager.configurableScrollablePopupPrefab.transform.Find("Background"), myLabelWith2BXButtonPrefab.transform, new float[] {1, 1, 0, 0, 0, 0, 0, -10}
				);
				RectTransform xButtonTransform = InitBasicRectTransformPrefab(
					"ButtonX", manager.configurableScrollablePopupPrefab.transform.Find("Button"), myLabelWith2BXButtonPrefab.transform, new float[] {1, 1, 1, 0, 0, 0, -60, -10}
				);
				Button buttonX = xButtonTransform.GetComponent<Button>();
				Text xButtonText = xButtonTransform.Find("Text").GetComponent<Text>();
				xButtonText.text = "X";
				Image xButtonImage = xButtonTransform.GetComponent<Image>();

				RectTransform labelTransform = InitBasicRectTransformPrefab(
					"Text", xButtonText.rectTransform, myLabelWith2BXButtonPrefab.transform, new float[] {1, 1, 0, 0, -65, 0, 100, -10}
				);
				Text labelText = labelTransform.GetComponent<Text>();
				labelText.verticalOverflow = VerticalWrapMode.Overflow;

				RectTransform toggleBG1Transform = InitBasicRectTransformPrefab(
					"ToggleBG1", manager.configurableTogglePrefab.transform.Find("Panel"), myLabelWith2BXButtonPrefab.transform, new float[] {0, 1, 0, 0, 50, 0, 0, -10}
				);
				Image toggleBG1Image = toggleBG1Transform.GetComponent<Image>();
				toggleBG1Image.sprite = xButtonImage.sprite;
				toggleBG1Image.color = xButtonImage.color;
				Toggle toggle1 = toggleBG1Transform.gameObject.AddComponent<Toggle>();
				toggle1.isOn = true;

				RectTransform toggle1Check = InitBasicRectTransformPrefab(
					"Toggle1", manager.configurableTogglePrefab.transform.Find("Background/Checkmark"), toggleBG1Transform, new float[] {1, 1, 0, 0, 2, -10, 3, -10}
				);
				Image image1 = toggle1Check.GetComponent<Image>();

				RectTransform toggle1Label = InitBasicRectTransformPrefab(
					"Toggle1Label", xButtonText.rectTransform as Transform, toggle1Check, new float[] {1.0f, 1.0f, 0.0f, 0.5f, 0.0f, 4.0f, -4.0f, 4.0f}
				);
				Text toggle1Text = toggle1Label.GetComponent<Text>();
				toggle1Text.fontSize = 20;
				toggle1Text.text = "POS";
				toggle1Text.alignment = TextAnchor.UpperCenter;

				RectTransform toggleBG2Transform = InitBasicRectTransformPrefab(
					"ToggleBG2", manager.configurableTogglePrefab.transform.Find("Panel"), myLabelWith2BXButtonPrefab.transform, new float[] {0, 1, 0, 0, 100, 0, 50, -10}
				);
				Image toggleBG2Image = toggleBG2Transform.GetComponent<Image>();
				toggleBG2Image.sprite = xButtonImage.sprite;
				toggleBG2Image.color = xButtonImage.color;
				Toggle toggle2 = toggleBG2Transform.gameObject.AddComponent<Toggle>();
				toggle2.isOn = true;

				RectTransform toggle2Check = InitBasicRectTransformPrefab(
					"Toggle2", manager.configurableTogglePrefab.transform.Find("Background/Checkmark"), toggleBG2Transform, new float[] {1, 1, 0, 0, 2, -10, 3, -10}
				);
				Image image2 = toggle2Check.GetComponent<Image>();

				RectTransform toggle2Label = InitBasicRectTransformPrefab(
					"Toggle2Label", xButtonText.rectTransform, toggle2Check, new float[] {1.0f, 1.0f, 0.0f, 0.5f, 0.0f, 4.0f, -4.0f, 4.0f}
				);
				Text toggle2Text = toggle2Label.GetComponent<Text>();
				toggle2Text.fontSize = 20;
				toggle2Text.text = "ROT";
				toggle2Text.alignment = TextAnchor.UpperCenter;

				UIDynamicLabel2BXButton uid = myLabelWith2BXButtonPrefab.AddComponent<UIDynamicLabel2BXButton>();
				uid.label = labelText;
				uid.image1 = image1;
				uid.image2 = image2;
				uid.toggle1 = toggle1;
				uid.toggle2 = toggle2;
				uid.text1 = toggle1Text;
				uid.text2 = toggle2Text;
				uid.buttonX = buttonX;
			}

			{
				myLabelWithMXButtonPrefab = new GameObject("LabelMXButton");
				myLabelWithMXButtonPrefab.SetActive(false);
				RectTransform rt = myLabelWithMXButtonPrefab.AddComponent<RectTransform>();
				rt.anchorMax = new Vector2(0, 1);
				rt.anchorMin = new Vector2(0, 1);
				rt.offsetMax = new Vector2(535, -500);
				rt.offsetMin = new Vector2(10, -600);
				LayoutElement le = myLabelWithMXButtonPrefab.AddComponent<LayoutElement>();
				le.flexibleWidth = 1;
				le.minHeight = 50;
				le.minWidth = 350;
				le.preferredHeight = 50;
				le.preferredWidth = 500;

				RectTransform backgroundTransform = InitBasicRectTransformPrefab(
					"Background", manager.configurableScrollablePopupPrefab.transform.Find("Background"), myLabelWithMXButtonPrefab.transform, new float[] {1, 1, 0, 0, 0, 0, 0, -10}
				);

				RectTransform xButtonTransform = InitBasicRectTransformPrefab(
					"ButtonX", manager.configurableScrollablePopupPrefab.transform.Find("Button"), myLabelWithMXButtonPrefab.transform, new float[] {1, 1, 1, 0, 0, 0, -60, -10}
				);
				Button buttonX = xButtonTransform.GetComponent<Button>();
				Text xButtonText = xButtonTransform.Find("Text").GetComponent<Text>();
				xButtonText.text = "X";
				Image xButtonImage = xButtonTransform.GetComponent<Image>();

				RectTransform labelTransform = InitBasicRectTransformPrefab(
					"Text", xButtonText.rectTransform, myLabelWithMXButtonPrefab.transform, new float[] {1, 1, 0, 0, -65, 0, 50, -10}
				);
				Text labelText = labelTransform.GetComponent<Text>();
				labelText.verticalOverflow = VerticalWrapMode.Overflow;

				RectTransform toggleBGMTransform = InitBasicRectTransformPrefab(
					"ToggleBGM", manager.configurableTogglePrefab.transform.Find("Panel"), myLabelWithMXButtonPrefab.transform, new float[] {0, 1, 0, 0, 50, 0, 0, -10}
				);
				Image toggleBGMImage = toggleBGMTransform.GetComponent<Image>();
				toggleBGMImage.sprite = xButtonImage.sprite;
				toggleBGMImage.color = xButtonImage.color;
				Toggle toggleM = toggleBGMTransform.gameObject.AddComponent<Toggle>();
				toggleM.isOn = true;

				RectTransform toggleMCheck = InitBasicRectTransformPrefab(
					"ToggleM", manager.configurableTogglePrefab.transform.Find("Background/Checkmark"), toggleBGMTransform, new float[] {1, 1, 0, 0, 2, -5, 3, -5}
				);
				Image imageM = toggleMCheck.GetComponent<Image>();

				RectTransform toggleMLabel = InitBasicRectTransformPrefab(
					"ToggleMLabel", xButtonText.rectTransform, toggleMCheck, new float[] {0.5f, 1.0f, 0.0f, 0.5f, 0.0f, 0.0f, 0.0f, 0.0f}
				);
				Text toggleMText = toggleMLabel.GetComponent<Text>();
				toggleMText.fontSize = 22;
				toggleMText.text = "M";

				UIDynamicLabelMXButton uid = myLabelWithMXButtonPrefab.AddComponent<UIDynamicLabelMXButton>();
				uid.label = labelText;
				uid.imageM = imageM;
				uid.toggleM = toggleM;
				uid.buttonX = buttonX;
			}
		}

		private void UIRefreshMenu()
		{
			CleanupMenu();
			CreateMainUI();
			UISelectMenu(myMenuItem);
			myIsFullRefresh = true;
		}

		private IEnumerable<DictionaryEntry> CastDict(IDictionary dictionary)
		{
			foreach (DictionaryEntry entry in dictionary)
			{
				yield return entry;
			}
		}
		
		private List<string> GetAvailableOptions(
			Dictionary<string, AnimationObject> myAnimationObjects,
			bool singleChoice = false,
			AnimationObject mySingleChoice = null
		) {
			List<string> availableAnimationObjects = new List<string>();
			if (singleChoice) {
				availableAnimationObjects.Add(mySingleChoice.myName);
			} else {
				foreach (var a in myAnimationObjects)
				{
					AnimationObject source = a.Value;
					availableAnimationObjects.Add(source.myName);
				}
			}
			availableAnimationObjects.Sort();

			return availableAnimationObjects;
		}

		private JSONStorableStringChooser PopulateJSONChooserSelection(
			List<string> availableAnimationObjects,
			JSONStorableStringChooser myAnimationObjectList,
			string strLabel,
			bool singleChoice = false
		) {
			string selectedAnimationObject;
			if (availableAnimationObjects.Count == 0)
				selectedAnimationObject= "";
			else if(singleChoice || myAnimationObjectList == null || !availableAnimationObjects.Contains(myAnimationObjectList.val))
				selectedAnimationObject = availableAnimationObjects[0];
			else
				selectedAnimationObject = myAnimationObjectList.val;

			myAnimationObjectList = new JSONStorableStringChooser(strLabel, availableAnimationObjects, selectedAnimationObject, strLabel);
			myAnimationObjectList.setCallbackFunction += (string v) => UIRefreshMenu();

			return myAnimationObjectList;
		}
		
		private JSONStorableStringChooser CreateDropDown(
			Dictionary<string, AnimationObject> myAnimationObjects,
			JSONStorableStringChooser myAnimationObjectList,
			string strLabel,
			bool singleChoice = false,
			AnimationObject mySingleChoice = null
		) {

			List<string> availableAnimationObjects = GetAvailableOptions(myAnimationObjects, singleChoice, mySingleChoice);
			myAnimationObjectList = PopulateJSONChooserSelection(availableAnimationObjects, myAnimationObjectList, strLabel, singleChoice);

			return myAnimationObjectList;
		}

		private void CreateMainDropDown(Dictionary<string, AnimationObject> myChoices, JSONStorableStringChooser myMainSelection, AnimationObject myParent, string objectType){
			List<string> options = new List<string>();
			foreach (var option in myChoices)
				options.Add(option.Key);
			options.Sort();
			myMainSelection.choices = options;
			if (objectType == "state") {
				List<string> stateDisplays = new List<string>(options.Count);
				for (int i=0; i<options.Count; ++i)
				{
					State state = myCurrentLayer.myStates[options[i]];
					stateDisplays.Add(state.myName);
				}
				myMainSelection.displayChoices = stateDisplays;
			} else {
				myMainSelection.displayChoices = options;
			}
			if (options.Count == 0)
			{
				myMainSelection.valNoCallback = "";
			}
			else if (!options.Contains(myMainSelection.val))
			{
				myMainSelection.valNoCallback = options[0];
				if (objectType!="state") {
					AnimationObject selection;
					myChoices.TryGetValue(myMainSelection.val, out selection);
					if(objectType == "animation")
						SetAnimation(selection as Animation);
					else if (objectType == "layer")
						SetLayer(selection as Layer);
				}
				UIBlendToState();
			}
		}

		private void UISelectMenu(int menuItem)
		{
			myMenuItem = menuItem;

			for (int i=0; i<myMenuTabBar.buttons.Count; ++i)
				myMenuTabBar.buttons[i].interactable = (i != myMenuItem);

			myPaused = (myMenuItem != MENU_PLAY || myPlayPaused.val);
			myPlayMode = (myMenuItem == MENU_PLAY);
			// myMainAnimation.popup.visible = true; // Workaround for PopupPanel appearing behind other UI for some reason
			// myMainAnimation.popup.visible = false;
			// myMainLayer.popup.visible = true; // Workaround for PopupPanel appearing behind other UI for some reason
			// myMainLayer.popup.visible = false;
			// myMainState.popup.visible = true; // Workaround for PopupPanel appearing behind other UI for some reason
			// myMainState.popup.visible = false;

			CreateMainDropDown(
				CastDict(myAnimations).ToDictionary(entry => (string)entry.Key, entry => (AnimationObject)entry.Value),
				myMainAnimation,
				new AnimationObject("test"),
				"animation"
			);
			if (myCurrentAnimation != null)
				CreateMainDropDown(
					CastDict(myCurrentAnimation.myLayers).ToDictionary(entry => (string)entry.Key, entry => (AnimationObject)entry.Value),
					myMainLayer,
					myCurrentAnimation,
					"layer"
				);
			else
				CreateMainDropDown(
					new Dictionary<string, AnimationObject>(),
					myMainLayer,
					myCurrentAnimation,
					"layer"
				);
			if (myCurrentLayer != null)
				CreateMainDropDown(
					CastDict(myCurrentLayer.myStates).ToDictionary(entry => (string)entry.Key, entry => (AnimationObject)entry.Value),
					myMainState,
					myCurrentLayer,
					"state"
				);
			else
				CreateMainDropDown(
					new Dictionary<string, AnimationObject>(),
					myMainState,
					myCurrentLayer,
					"state"
				);

			Utils.OnInitUI(CreateUIElement);
			switch (myMenuItem)
			{
				case MENU_LAYERS:      CreateLayersMenu();      break;
				case MENU_ANIMATIONS:  CreateAnimationsMenu();  break;
				case MENU_STATES:      CreateStateMenu();       break;
				case MENU_TRANSITIONS: CreateTransitionsMenu(); break;
				case MENU_TRIGGERS:    CreateTriggers();        break;
				case MENU_ANCHORS:     CreateAnchorsMenu();     break;
				case MENU_ROLES:       CreateRolesMenu();       break;
				case MENU_MESSAGES:    CreateMessagesMenu();    break;
				case MENU_OPTIONS:     CreateOptionsMenu();     break;
				case MENU_PLAY:        CreatePlayMenu();        break;
			}
		}

		private void UISetPaused(bool paused)
		{
			if (myMenuItem == MENU_PLAY)
				myPaused = paused;
		}

		private void UIBlendToState()
		{
			State state;
			if (myStateAutoTransition.val && myCurrentLayer.myStates.TryGetValue(myMainState.val, out state))
			{
				myCurrentLayer.SetBlendTransition(state, true);
			}
		}

		private void UISelectAnimationAndRefresh(string name)
		{
			bool initPlayPaused = myPlayPaused.val;
			myCurrentAnimation = null;
			myCurrentLayer = null;
			myCurrentState = null;
			Animation animation;
			myAnimations.TryGetValue(myMainAnimation.val, out animation);
			SetAnimation(animation);
			myPlayPaused.val = initPlayPaused;

			UIRefreshMenu();
		}
		private void UISelectLayerAndRefresh(string name)
		{
			if(name.Length > 0) {
				Layer layer;
				myCurrentAnimation.myLayers.TryGetValue(myMainLayer.val, out layer);
				SetLayer(layer);
			}
			UIRefreshMenu();
		}

		private void UISelectStateAndRefresh(string name)
		{
			if(name.Length > 0) {
				UIBlendToState();
			}
			UIRefreshMenu();
		}

		private void CreateMainUI()
		{
			Utils.OnInitUI(CreateUIElement);
			CreateMenuInfo(myGeneralInfo, 108, false);

			CreateMenuPopup(myMainAnimation, false);
			CreateMenuPopup(myMainLayer, true);
			CreateMenuPopup(myMainState, true);
			CreateMenuSpacer(172, true);

			CreateTabs(new string[] { "Play", "Animations", "Layers", "States", "Transitions", "Triggers", "Anchors", "Roles", "Messages", "Options" });

		}

		private void CreateTabs(string[] menuItems) {
			GameObject tabbarPrefab = new GameObject("TabBar");
			LayoutElement le = tabbarPrefab.AddComponent<LayoutElement>();
			le.flexibleWidth = 1;
			le.minHeight = 90;
			le.minWidth = 1064;
			le.preferredHeight = 90;
			le.preferredWidth = 1064;

			UIDynamicTabBar uid = tabbarPrefab.AddComponent<UIDynamicTabBar>();

			float width = 142.0f;
			float padding = 5.0f;
			float x = 15;
			for (int i=0; i<menuItems.Length; ++i)
			{
				int secondRow = 0;
				if(i>=7) {
					secondRow = 70;
				}
				if(i==7) {
					x = 15;
				}
				float extraWidth = (i == MENU_TRANSITIONS || i == MENU_ANIMATIONS) ? 11.0f : 0.0f;

				RectTransform buttonTransform = InitBasicRectTransformPrefab(
					"Button", manager.configurableScrollablePopupPrefab.transform.Find("Button"), tabbarPrefab.transform, new float[] {0, 1, 0, 0, x+width+extraWidth, -15-secondRow, x, 15-secondRow}
				);
				Button buttonButton = buttonTransform.GetComponent<Button>();
				uid.buttons.Add(buttonButton);
				Text buttonText = buttonTransform.Find("Text").GetComponent<Text>();
				buttonText.text = menuItems[i];
				x += width + extraWidth + padding;
			}

			Transform t = CreateUIElement(tabbarPrefab.transform, false);
			myMenuTabBar = t.gameObject.GetComponent<UIDynamicTabBar>();
			myMenuElements.Add(myMenuTabBar);
			for (int i=0; i<myMenuTabBar.buttons.Count; ++i)
			{
				int menuItem = i;
				myMenuTabBar.buttons[i].onClick.AddListener(
					() => {myMenuItem = menuItem; UIRefreshMenu();}
				);
			}

			Destroy(tabbarPrefab);
			CreateMenuSpacer(60, false);
		}

		private void CreatePlayMenu()
		{
			CreateMenuInfoOneLine("<size=30><b>Play Idle</b></size>", false);

			if (myCurrentLayer != null && myCurrentLayer.myStates.Count > 0)
				myPlayInfo.val = "AnimationPoser is playing animations.";
			else
				myPlayInfo.val = "You need to add some states and transitions before you can play animations.";

			CreateMenuInfo(myPlayInfo, 300, false);
			CreateMenuToggle(myPlayPaused, false);

			if(myCurrentAnimation != null) {
				JSONStorableFloat animationSpeed = new JSONStorableFloat("AnimationSpeed", myCurrentAnimation.mySpeed, 0.0f, 10.0f, true, true);
				animationSpeed.setCallbackFunction = (float v) => {
					myCurrentAnimation.mySpeed = v;
				};

				CreateMenuSlider(animationSpeed, true);
			}
		}
		private void CreateAnimationsMenu()
		{
			CreateMenuInfoOneLine("<size=30><b>Manage Animations</b></size>", false);

			CreateMenuInfo("Careful! Loading the plugin instance overwrites everything!", 63, false);
			CreateLoadButton("Load Plugin Instance", UILoadAnimationsJSON, false, "Instances");
			CreateMenuInfo("Saving the plugin instance serves as a backup of the entire plugin state", 63, false);
			CreateMenuButton("Save Plugin Instance", UISaveJSONDialog(UISaveAnimationsJSON, "Instances"), false);

			CreateMenuInfo("Save and load the current animation:", 35, false);
			CreateLoadButton("Load Animation", UILoadAnimationJSON, false, "Animations");
			CreateMenuButton("Save Animation", UISaveJSONDialog(UISaveAnimationJSON, "Animations"), false);

			String animationName = "";
			if(myCurrentAnimation != null)
				animationName = myCurrentAnimation.myName;
			JSONStorableString name = new JSONStorableString("Animation Name",
				animationName, UIRenameAnimation);
			CreateMenuTextInput("Animation Name", name, true);

			CreateMenuButton("Add Animation", UIAddAnimation, true);

			CreateMenuButton("Remove Animation", UIRemoveAnimation, true);
		}

		private void CreateLayersMenu()
		{
			// control captures
			CreateMenuInfoOneLine("<size=30><b>Manage Layers</b></size>", false);

			CreateLoadButton("Load Layer", UILoadJSON, false, "Layers");
			CreateMenuButton("Save Layer As", UISaveJSONDialog(UISaveLayerJSON, "Layers"), false);

			String layerName = "";
			if(myCurrentLayer != null)
				layerName = myCurrentLayer.myName;
			JSONStorableString name = new JSONStorableString("Layer Name",
				layerName, UIRenameLayer);

			CreateMenuTextInput("Layer Name", name, false);

			CreateMenuButton("Add Layer", UIAddLayer, false);
			CreateMenuButton("Remove Layer", UIRemoveLayer, false);

			CreateMenuInfoOneLine("<size=30><b>Merge Layers</b></size>", true);

			List<string> layers = new List<string>();
			if(myCurrentAnimation != null)
			{
				foreach (var layer in myCurrentAnimation.myLayers)
					if(layer.Value != myCurrentLayer)
						layers.Add(layer.Key);
			}

			layers.Sort();

			JSONStorableStringChooser layerToMerge;
			layerToMerge = new JSONStorableStringChooser("Layer", layers, "", "Layer");

			CreateMenuPopup(layerToMerge, true);

			CreateMenuButton("Merge Layer", () => {
				if(layerToMerge.val == "") {
					return;
				}
				Layer layer = myCurrentAnimation.myLayers[layerToMerge.val];
				foreach(var s in layer.myStates) {
					State state = s.Value;
					state.myName = layer.myName + "#" + state.myName;
					myCurrentLayer.myStates[state.myName] = state;
					state.myLayer = myCurrentLayer;

					List<ControlCapture> entries = state.myControlEntries.Keys.ToList();
					for(int i=0; i<entries.Count; i++){
						ControlCapture oldControlCapture = entries[i];
						ControlCapture newControlCapture;
						int j = myCurrentLayer.myControlCaptures.FindIndex(cc => cc.myName == oldControlCapture.myName);
						if (j >= 0) {
							newControlCapture = myCurrentLayer.myControlCaptures[j];
						} else {
							newControlCapture = new ControlCapture(this, oldControlCapture.myName);
							myCurrentLayer.myControlCaptures.Add(newControlCapture);
						}

						ControlEntryAnchored entry = state.myControlEntries[oldControlCapture];
						entry.myControlCapture = newControlCapture;
						state.myControlEntries.Remove(oldControlCapture);
						state.myControlEntries[newControlCapture] = entry;
					}

					List<MorphCapture> mEntries = state.myMorphEntries.Keys.ToList();
					for(int i=0; i<mEntries.Count; i++){
						MorphCapture oldMorphCapture = mEntries[i];
						MorphCapture newMorphCapture;
						int j = myCurrentLayer.myMorphCaptures.FindIndex(cc => cc.myMorph == oldMorphCapture.myMorph);
						if (j >= 0) {
							newMorphCapture = myCurrentLayer.myMorphCaptures[j];
						} else {
							newMorphCapture = new MorphCapture(this, oldMorphCapture.myGender, oldMorphCapture.myMorph);
							myCurrentLayer.myMorphCaptures.Add(newMorphCapture);
						}

						float entry = state.myMorphEntries[oldMorphCapture];
						state.myMorphEntries.Remove(oldMorphCapture);
						state.myMorphEntries[newMorphCapture] = entry;
					}
				}
				myCurrentAnimation.myLayers.Remove(layer.myName);

				UIRefreshMenu();
			}, true);

			CreateMenuInfoOneLine("<size=30><b>Control Captures</b></size>", false);

			FreeControllerV3[] atomControls = GetAllControllers();
			List<string> availableControls = new List<string>();
			for (int i=0; i<atomControls.Length; ++i)
			{
				string control = atomControls[i].name;
				if (control.StartsWith("hair"))
					continue;
				if (myCurrentLayer.myControlCaptures.FindIndex(cc => cc.myName == control) >= 0)
					continue;
				availableControls.Add(control);
			}
			myCapturesControlList.choices = availableControls;
			if (availableControls.Count == 0)
				myCapturesControlList.val = "";
			else if (!availableControls.Contains(myCapturesControlList.val))
				myCapturesControlList.val = availableControls[0];

			if (availableControls.Count > 0)
			{
				CreateMenuPopup(myCapturesControlList, false);
				CreateMenuButton("Add Controller", UIAddControlCapture, false);
			}

			if (myCurrentLayer.myControlCaptures.Count > 0)
			{
				CreateMenuTwinButton(
					"Toggle Positions", UIToggleControlCapturePosition,
					"Toggle Rotations", UIToggleControlCaptureRotation, false);
				CreateMenuSpacer(15, false);
			}

			for (int i=0; i<myCurrentLayer.myControlCaptures.Count; ++i)
			{
				ControlCapture cc = myCurrentLayer.myControlCaptures[i];
				CreateMenuLabel2BXButton(
					cc.myName, "POS", "ROT", cc.myApplyPosition, cc.myApplyRotation,
					(bool v) => { cc.myApplyPosition = v; },
					(bool v) => { cc.myApplyRotation = v; },
					() => UIRemoveControlCapture(cc), false
				);
			}

			// morph captures
			CreateMenuInfoOneLine("<size=30><b>Morph Captures</b></size>", true);

			DAZCharacterSelector geometry = containingAtom.GetStorableByID("geometry") as DAZCharacterSelector;
			if (geometry != null)
			{
				if (myIsFullRefresh)
				{
					List<string> morphUIDs = new List<string>();
					bool isMale = geometry.gender == DAZCharacterSelector.Gender.Male;
					if (!isMale || geometry.GetBoolParamValue("useFemaleMorphsOnMale"))
						UICollectMorphsUIDs(DAZCharacterSelector.Gender.Female, geometry, morphUIDs);
					if (isMale || geometry.GetBoolParamValue("useMaleMorphsOnFemale"))
						UICollectMorphsUIDs(DAZCharacterSelector.Gender.Male, geometry, morphUIDs);
					morphUIDs.Sort();
					List<string> morphNames = new List<string>();
					UICollectMorphsNames(geometry, morphUIDs, morphNames);

					myCapturesMorphList.choices = morphUIDs;
					myCapturesMorphList.displayChoices = morphNames;
					if (morphUIDs.Count == 0)
					{
						myCapturesMorphList.val = "";
					}
					else if (!morphUIDs.Contains(myCapturesMorphList.val))
					{
						string defaultMorph = DEFAULT_MORPH_UID + "#" + geometry.gender.ToString();
						myCapturesMorphList.val = morphUIDs.Contains(defaultMorph) ? defaultMorph : morphUIDs[0];
					}
				}

				CreateMenuFilterPopupCached(myCapturesMorphList, true, ref myCapturesMorphPopup);
				CreateMenuInfo(myCapturesMorphFullname, 50, true);

				CreateMenuButton("Add Morph", UIAddMorphCapture, true);
			}

			if (myCurrentLayer.myMorphCaptures.Count > 0)
			{
				CreateMenuButton("Toggle Morphs", UIToggleMorphCapture, true);
				CreateMenuSpacer(15, true);
			}

			for (int i=0; i<myCurrentLayer.myMorphCaptures.Count; ++i)
			{
				MorphCapture mc = myCurrentLayer.myMorphCaptures[i];
				DAZMorph m = mc.myMorph;
				string gender = mc.myGender.ToString().Substring(0,1);
				string label;
				if (m.hasBoneModificationFormulas) // this is a "high cost" morph...red warning
					label = "<color=#CC6060>"+gender+": " + m.resolvedRegionName + "</color><color=#FF0000>\n" + m.resolvedDisplayName + "</color>";
				else
					label = "<color=#606060>"+gender+": " + m.resolvedRegionName + "</color>\n" + m.resolvedDisplayName;
				CreateMenuLabelMXButton(label, mc.myApply, (bool v) => { mc.myApply = v; },	() => UIRemoveMorphCapture(mc), true);

				JSONStorableFloat morphValue = new JSONStorableFloat(m.resolvedDisplayName, m.morphValue, -1.0f, 1.0f, true, true);
				morphValue.constrained = false;

				morphValue.setCallbackFunction = (float v) => {
					m.morphValue = v;
				};
				CreateMenuSlider(morphValue, true);
			}
		}

		private void CreateStateMenu()
		{
			CreateMenuInfoOneLine("<size=30><b>State Manager</b></size>", false);
			CreateMenuSpacer(35, true);

			CreateMenuButton("Add State", UIAddState, false);
			State state;
			if (!myCurrentLayer.myStates.TryGetValue(myMainState.val, out state))
				return;

			CreateMenuTwinButton("Duplicate State", UIDuplicateState, "Remove State", UIRemoveState, false);
			CreateMenuToggle(myStateAutoTransition, false);

			CreateMenuButton("Capture State", UICaptureState, true);
			CreateMenuButton("Apply Anchors", UIApplyAnchors, true);

			CreateMenuSpacer(15, false);

			CreateMenuInfoOneLine("<size=30><b>State Settings</b></size>", false);
			CreateMenuSpacer(132, true);
			JSONStorableString name = new JSONStorableString("State Name", state.myName, UIRenameState);
			CreateMenuTextInput("Name", name, false);

			JSONStorableFloat probability = new JSONStorableFloat("Default Transition Probability", DEFAULT_PROBABILITY, 0.0f, 1.0f, true, true);
			probability.valNoCallback = state.myDefaultProbability;
			probability.setCallbackFunction = (float v) => {
				State s = UIGetState();
				if (s != null)
					s.myDefaultProbability = v;
			};
			CreateMenuSlider(probability, false);

			JSONStorableFloat duration = new JSONStorableFloat("Default Transition Duration", myGlobalDefaultTransitionDuration.val, 0.0f, 5.0f, true, true);
			duration.valNoCallback = state.myDefaultDuration;
			duration.setCallbackFunction = (float v) => {
				State s = UIGetState();
				if (s != null)
					s.myDefaultDuration = v;
			};
			CreateMenuSlider(duration, false);

			JSONStorableFloat easeInDuration = new JSONStorableFloat("Default Ease In Duration", myGlobalDefaultEaseInDuration.val, 0.0f, 5.0f, true, true);
			easeInDuration.valNoCallback = state.myDefaultEaseInDuration;
			easeInDuration.setCallbackFunction = (float v) => {
				State s = UIGetState();
				if (s != null)
					s.myDefaultEaseInDuration = v;
			};
			CreateMenuSlider(easeInDuration, false);

			JSONStorableFloat easeOutDuration = new JSONStorableFloat("Default Ease Out Duration", myGlobalDefaultEaseOutDuration.val, 0.0f, 5.0f, true, true);
			easeOutDuration.valNoCallback = state.myDefaultEaseOutDuration;
			easeOutDuration.setCallbackFunction = (float v) => {
				State s = UIGetState();
				if (s != null)
					s.myDefaultEaseOutDuration = v;
			};
			CreateMenuSlider(easeOutDuration, false);

			JSONStorableBool isRootState = new JSONStorableBool("Is Root State", state.myIsRootState);
			isRootState.setCallbackFunction = (bool v) => {
				State st = UIGetState();
				if (st != null)
				{
					if(v) {
						foreach (var s in st.myLayer.myStates) {
							s.Value.myIsRootState = false;
						}
						st.myIsRootState = true;
					} else {
						st.myIsRootState = false;
					}
				}
				UIRefreshMenu();
			};
			CreateMenuToggle(isRootState, true);

			JSONStorableFloat waitDurationMin = new JSONStorableFloat("Wait Duration Min", myGlobalDefaultWaitDurationMin.val, 0.0f, 300.0f, true, true);
			JSONStorableFloat waitDurationMax = new JSONStorableFloat("Wait Duration Max", myGlobalDefaultWaitDurationMax.val, 0.0f, 300.0f, true, true);
			waitDurationMin.valNoCallback = state.myWaitDurationMin;
			waitDurationMax.valNoCallback = state.myWaitDurationMax;

			waitDurationMin.setCallbackFunction = (float v) => {
				State s = UIGetState();
				if (s != null)
					s.myWaitDurationMin = v;
				if (waitDurationMax.val < v)
					waitDurationMax.val = v;
			};
			waitDurationMax.setCallbackFunction = (float v) => {
				State s = UIGetState();
				if (s != null)
					s.myWaitDurationMax = v;
				if (waitDurationMin.val > v)
					waitDurationMin.val = v;
			};

			CreateMenuSlider(waitDurationMin, true);
			CreateMenuSlider(waitDurationMax, true);
		}

		private void CreateAnchorsMenu()
		{
			CreateMenuInfoOneLine("<size=30><b>Anchors</b></size>", false);

			State state;
			if (!myCurrentLayer.myStates.TryGetValue(myMainState.val, out state))
			{
				CreateMenuInfo("You need to add a state before you can configure anchors.", 100, false);
				return;
			}

			List<string> captures = new List<string>(myCurrentLayer.myControlCaptures.Count);
			for (int i=0; i<myCurrentLayer.myControlCaptures.Count; ++i)
				captures.Add(myCurrentLayer.myControlCaptures[i].myName);
			myAnchorCaptureList.choices = captures;
			if (captures.Count == 0)
				myAnchorCaptureList.valNoCallback = "";
			else if (!captures.Contains(myAnchorCaptureList.val))
				myAnchorCaptureList.valNoCallback = captures[0];
			CreateMenuPopup(myAnchorCaptureList, false);

			ControlCapture controlCapture = myCurrentLayer.myControlCaptures.Find(cc => cc.myName == myAnchorCaptureList.val);
			if (controlCapture == null)
				return;

			CreateMenuSpacer(30, false);

			ControlEntryAnchored controlEntry;
			if (!state.myControlEntries.TryGetValue(controlCapture, out controlEntry))
				return;
			myAnchorModeList.valNoCallback = myAnchorModes[controlEntry.myAnchorMode];
			CreateMenuPopup(myAnchorModeList, false);

			if (controlEntry.myAnchorMode != ControlEntryAnchored.ANCHORMODE_WORLD)
			{
				bool isBlendMode = controlEntry.myAnchorMode == ControlEntryAnchored.ANCHORMODE_BLEND;

				List<string> atoms = new List<string>(GetAllAtomUIDs());
				atoms.Sort();
				myAnchorAtomListA.label = isBlendMode ? "Anchor Atom A" : "Anchor Atom";
				myAnchorAtomListA.valNoCallback = controlEntry.myAnchorAAtom;
				myAnchorAtomListA.choices = atoms;
				CreateMenuFilterPopup(myAnchorAtomListA, false);

				Atom atomA = GetAtomById(myAnchorAtomListA.val);
				List<string> controls = atomA?.GetStorableIDs() ?? new List<string>();
				controls.RemoveAll((string control) => {
					if (atomA == containingAtom && control == myAnchorCaptureList.val)
						return true;
					JSONStorable storable = atomA.GetStorableByID(control);
					if (storable is DAZBone || storable is FreeControllerV3)
						return false;
					return true;
				});
				myAnchorControlListA.label = isBlendMode ? "Anchor Control A" : "Anchor Control";
				myAnchorControlListA.valNoCallback = controlEntry.myAnchorAControl;
				myAnchorControlListA.choices = controls;
				if (controls.Count == 0)
					myAnchorControlListA.val = "";
				else if (!controls.Contains(myAnchorControlListA.val))
					myAnchorControlListA.val = controls[0];
				CreateMenuFilterPopup(myAnchorControlListA, false);

				if (isBlendMode)
				{
					myAnchorAtomListB.valNoCallback = controlEntry.myAnchorBAtom;
					myAnchorAtomListB.choices = atoms;
					CreateMenuFilterPopup(myAnchorAtomListB, false);

					Atom atomB = GetAtomById(myAnchorAtomListB.val);
					controls = atomB?.GetStorableIDs() ?? new List<string>();
					controls.RemoveAll((string control) => {
						if (atomB == containingAtom && control == myAnchorCaptureList.val)
							return true;
						JSONStorable storable = atomB.GetStorableByID(control);
						if (storable is DAZBone || storable is FreeControllerV3)
							return false;
						return true;
					});
					myAnchorControlListB.valNoCallback = controlEntry.myAnchorBControl;
					myAnchorControlListB.choices = controls;
					if (controls.Count == 0)
						myAnchorControlListB.val = "";
					else if (!controls.Contains(myAnchorControlListB.val))
						myAnchorControlListB.val = controls[0];
					CreateMenuFilterPopup(myAnchorControlListB, false);

					myAnchorBlendRatio.valNoCallback = controlEntry.myBlendRatio;
					CreateMenuSlider(myAnchorBlendRatio, false);
				}

				myAnchorDampingTime.valNoCallback = controlEntry.myDampingTime;
				CreateMenuSlider(myAnchorDampingTime, false);
			}
		}

		private void CreateTransitionsMenu()
		{
			CreateMenuInfoOneLine("<size=30><b>Transitions</b></size>", false);

			State state;
			if (!myCurrentLayer.myStates.TryGetValue(myMainState.val, out state))
			{
				CreateMenuInfo("You need to add some states before you can add transitions.", 100, false);
				return;
			}

			// collect transitions
			List<UITransition> transitions = new List<UITransition>(state.myTransitions.Count);
			for (int i=0; i<state.myTransitions.Count; ++i)
				transitions.Add(new UITransition(state.myTransitions[i].myTargetState, false, true));

			foreach (var s in myCurrentLayer.myStates)
			{
				State target = s.Value;
				if (state == target || !target.isReachable(state))
					continue;

				int idx = transitions.FindIndex(t => t.state == target);
				if (idx >= 0)
					transitions[idx] = new UITransition(target, true, true);
				else
					transitions.Add(new UITransition(target, true, false));
			}
			transitions.Sort((UITransition a, UITransition b) => a.state.myName.CompareTo(b.state.myName));

			myTargetAnimationList = CreateDropDown(
				CastDict(myAnimations).ToDictionary(entry => (string)entry.Key, entry => (AnimationObject)entry.Value),
				myTargetAnimationList,
				"Target Animation"
			);
			Animation targetAnimation;
			if(!myAnimations.TryGetValue(myTargetAnimationList.val, out targetAnimation)){
				return;
			};

			Layer targetLayer;
			List<string> availableLayers = new List<string>();
			bool singleChoice = false;
			AnimationObject mySingleChoice = null;
			if(targetAnimation != myCurrentAnimation) {
				singleChoice = false;
				mySingleChoice = null;
			} else {
				singleChoice = true;
				mySingleChoice = myCurrentLayer as AnimationObject;
			}
			myTargetLayerList = CreateDropDown(
				CastDict(targetAnimation.myLayers).ToDictionary(entry => (string)entry.Key, entry => (AnimationObject)entry.Value),
				myTargetLayerList,
				"Target Layer",
				singleChoice:singleChoice,
				mySingleChoice:mySingleChoice
			);
			if(!targetAnimation.myLayers.TryGetValue(myTargetLayerList.val, out targetLayer))
				return;

			List<string> availableStates = new List<string>();
			foreach (var s in targetLayer.myStates)
			{
				State target = s.Value;
				if (state == target)
					continue;
				availableStates.Add(target.myName);
			}
			availableStates.Sort();

			myTargetStateList = PopulateJSONChooserSelection(
				availableStates,
				myTargetStateList,
				"Target State"
			);

			if(myTargetAnimationList.choices.Count > 0) {
				CreateMenuPopup(myTargetAnimationList, false);
			}
			if(myTargetLayerList.choices.Count > 0) {
				CreateMenuPopup(myTargetLayerList, false);
			}
			if (myTargetStateList.choices.Count > 0)
			{
				CreateMenuPopup(myTargetStateList, false);
				CreateMenuButton("Add Transition", UIAddTransition, false);
				CreateMenuButton("Add Indirect Transition", UIAddIndirectTransition, false);

				if (transitions.Count > 0)
					CreateMenuSpacer(15, false);
				CreateMenuInfoOneLine("<size=30><b>Entire Layer Transitions</b></size>", false);
				CreateMenuButton("Add Sequential Transitions", UIAddSequentialTransitions, false);
				CreateMenuButton("Add All Transitions", UIAddAllTransitions, false);
			}
			else if (transitions.Count == 0)
			{
				CreateMenuInfo("You need to add a second state before you can add transitions.", 100, false);
			}

			for (int i=0; i<transitions.Count; ++i)
			{
				string label;
				State target = transitions[i].state;
				label = transitions[i].displayString();

				CreateMenuLabel2BXButton(
					label, "IN", "OUT", transitions[i].incoming, transitions[i].outgoing,
					(bool v) => UIUpdateTransition(target, state, v),
					(bool v) => UIUpdateTransition(state, target, v),
					() => UIRemoveTransition(state, target), false
				);
			}

			State targetState;
			targetLayer.myStates.TryGetValue(myTargetStateList.val, out targetState);
			BaseTransition baseTransition = state.getIncomingTransition(targetState);

			if(baseTransition != null && baseTransition is Transition) {
				Transition transition = baseTransition as Transition;
				
				mySyncRoleList = CreateDropDown(
					CastDict(myRoles).ToDictionary(entry => (string)entry.Key, entry => (AnimationObject)entry.Value),
					mySyncRoleList,
					"Sync Role"
				);

				CreateMenuSpacer(10, false);
				CreateMenuInfoOneLine("<size=30><b>Messages</b></size>", false);
				CreateMenuInfo("Use this to send messages to plugin instances in other person atoms when the transition finishes.", 100, false);

				CreateMenuPopup(mySyncRoleList, false);

				Role selectedRole;
				myRoles.TryGetValue(mySyncRoleList.val, out selectedRole);

				if(mySyncRoleList.val != "") {
					String messageString;
					transition.myMessages.TryGetValue(selectedRole, out messageString);
					if(messageString == null) {
						messageString = "";
					}
					JSONStorableString message = new JSONStorableString("Message String",
						messageString, (String mString) => {
							if(mString == "") {
								transition.myMessages.Remove(selectedRole);
							} else {
								transition.myMessages[selectedRole] = mString;
							}
							UIRefreshMenu();
						}
					);

					CreateMenuTextInput("Message string", message, false);
				}

				CreateMenuInfoOneLine("<size=30><b>Transition Settings</b></size>", true);

				JSONStorableFloat transitionProbability = new JSONStorableFloat("Relative Transition Probability", transition.myProbability, 0.00f, 1.0f, true, true);
				transitionProbability.valNoCallback = transition.myProbability;
				transitionProbability.setCallbackFunction = (float v) => {
					Transition t = UIGetTransition() as Transition;
					if (t != null)
						t.myProbability = v;
				};
				CreateMenuSlider(transitionProbability, true);

				JSONStorableFloat transitionDuration = new JSONStorableFloat("Transition Duration", transition.myDuration, 0.01f, 20.0f, true, true);
				transitionDuration.valNoCallback = transition.myDuration;
				transitionDuration.setCallbackFunction = (float v) => {
					Transition t = UIGetTransition() as Transition;
					if (t != null)
						t.myDuration = v;
				};
				CreateMenuSlider(transitionDuration, true);

				JSONStorableFloat transitionDurationNoise = new JSONStorableFloat("Transition Duration Noise", transition.myDurationNoise, 0.00f, 5.0f, true, true);
				transitionDurationNoise.setCallbackFunction = (float v) => {
					Transition t = UIGetTransition() as Transition;
					if (t != null)
						t.myDurationNoise = v;
				};
				CreateMenuSlider(transitionDurationNoise, true);

				JSONStorableFloat easeInDuration = new JSONStorableFloat("EaseIn Duration", transition.myEaseInDuration, 0.0f, 5.0f, true, true);
				easeInDuration.valNoCallback = transition.myEaseInDuration;
				easeInDuration.setCallbackFunction = (float v) => {
					Transition t = UIGetTransition() as Transition;
					if (t != null)
						t.myEaseInDuration = v;
				};
				CreateMenuSlider(easeInDuration, true);

				JSONStorableFloat easeOutDuration = new JSONStorableFloat("EaseOut Duration", transition.myEaseOutDuration, 0.0f, 5.0f, true, true);
				easeOutDuration.valNoCallback = transition.myEaseOutDuration;
				easeOutDuration.setCallbackFunction = (float v) => {
					Transition t = UIGetTransition() as Transition;
					if (t != null)
						t.myEaseOutDuration = v;
				};
				CreateMenuSlider(easeOutDuration, true);

				CreateMenuInfo("Use the following to sync other layers on target state arrival.", 80, true);

				List<string> syncLayers = new List<string>();
				foreach (var l in transition.myTargetState.myAnimation().myLayers)
				{
					Layer target = l.Value;
					if(target != transition.myTargetState.myLayer)
						syncLayers.Add(target.myName);
				}
				syncLayers.Sort();

				if (syncLayers.Count == 0) {
					return;
				}

				mySyncLayerList = PopulateJSONChooserSelection(
					syncLayers,
					mySyncLayerList,
					"Sync Layer"
				);

				CreateMenuPopup(mySyncLayerList, true);

				Layer syncLayer;
				if(!transition.myTargetState.myAnimation().myLayers.TryGetValue(mySyncLayerList.val, out syncLayer)){
					return;
				};

				if(transition.mySyncTargets.ContainsKey(syncLayer)) {
					CreateMenuLabelXButton(transition.mySyncTargets[syncLayer].myName, () => {
						Transition t = UIGetTransition() as Transition;
						if(t == null)
							return;

						Layer l;
						if(!t.myTargetState.myAnimation().myLayers.TryGetValue(mySyncLayerList.val, out l)){
							return;
						};

						t.mySyncTargets.Remove(l);

						UIRefreshMenu();
					}, true);
				} else {
					List<string> syncStates = new List<string>();
					foreach (var s in syncLayer.myStates)
					{
						State target = s.Value;
						syncStates.Add(target.myName);
					}
					syncStates.Sort();

					if (syncStates.Count == 0) {
						return;
					}

					mySyncStateList = PopulateJSONChooserSelection(
						syncStates,
						mySyncStateList,
						"Sync State"
					);

					CreateMenuPopup(mySyncStateList, true);
					CreateMenuButton("Sync State", () => {
						Transition t = UIGetTransition() as Transition;
						if(t == null)
							return;

						Layer l;
						if(!t.myTargetState.myAnimation().myLayers.TryGetValue(mySyncLayerList.val, out l)){
							return;
						};

						State s;
						if(!syncLayer.myStates.TryGetValue(mySyncStateList.val, out s)){
							return;
						};

						t.mySyncTargets[l] = s;

						UIRefreshMenu();
					}, true);
				}
			} else if (baseTransition != null && baseTransition is IndirectTransition) {
				IndirectTransition transition = baseTransition as IndirectTransition;
				CreateMenuInfoOneLine("<size=30><b>Indirect Transition Settings</b></size>", true);
				CreateMenuInfo("This is an indirect transition. Use this when you want to go straight to the target state through a chain of intermediate states, connected by regular transitions. The plugin's Path Finding algorithm will lead there.", 200, true);

				JSONStorableFloat transitionProbability = new JSONStorableFloat("Relative Transition Probability", transition.myProbability, 0.00f, 1.0f, true, true);
				transitionProbability.valNoCallback = transition.myProbability;
				transitionProbability.setCallbackFunction = (float v) => {
					IndirectTransition t = UIGetTransition() as IndirectTransition;
					if (t != null)
						t.myProbability = v;
				};
				CreateMenuSlider(transitionProbability, true);
			}
		}

		private void CreateTriggers()
		{
			CreateMenuInfoOneLine("<size=30><b>Internal Triggers</b></size>", false);
			State state;
			if (!myCurrentLayer.myStates.TryGetValue(myMainState.val, out state))
			{
				CreateMenuInfo("You need to add some states before you can add triggers.", 100, false);
			}
			else
			{
				CreateMenuButton("Actions " + state.EnterBeginTrigger.Name, state.EnterBeginTrigger.OpenPanel, false);
				CreateMenuButton("Actions " + state.EnterEndTrigger.Name,   state.EnterEndTrigger.OpenPanel,   false);
				CreateMenuButton("Actions " + state.ExitBeginTrigger.Name,  state.ExitBeginTrigger.OpenPanel,  false);
				CreateMenuButton("Actions " + state.ExitEndTrigger.Name,    state.ExitEndTrigger.OpenPanel,    false);
			}

			CreateMenuInfoOneLine("<size=30><b>External Triggers</b></size>", true);
			CreateMenuInfoScrolling(
				"AnimationPoser exposes the SendMessage trigger, which allows you to send a message to the plugin instace, which can be handled with a message receiver in the Messages tab.\n\n",
				800, true
			);
		}
		private void CreateRolesMenu()
		{
			CreateMenuInfo("The Roles tab allows you to define roles for a layer. Each role can be assigned to a person, and used in the transitions tab to sync the layers of that person. Like in a play, the roles can be assigned and switched between different persons with minimal work to the script writer :)", 230, false);

			CreateLoadButton("Load Roles", UILoadRolesJSON, false, "Roles");
			CreateMenuButton("Save Roles", UISaveJSONDialog(UISaveRolesJSON, "Roles"), false);

			CreateMenuSpacer(132, true);
			myRoleList = CreateDropDown(
				CastDict(myRoles).ToDictionary(entry => (string)entry.Key, entry => (AnimationObject)entry.Value),
				myRoleList,
				"Role"
			);

			CreateMenuPopup(myRoleList, true);

			List<string> people = new List<string>();

			foreach (var atom in SuperController.singleton.GetAtoms())
			{
				if (atom == null) continue;
				var storableId = atom.GetStorableIDs().FirstOrDefault(id => id.EndsWith("HaremLife.AnimationPoser"));
				if (storableId == null) continue;
				MVRScript storable = atom.GetStorableByID(storableId) as MVRScript;
				if (storable == null) continue;
				if (!storable.enabled) continue;
				people.Add(atom.name);
			}
			people.Sort();

			Role selectedRole;
			myRoles.TryGetValue(myRoleList.val, out selectedRole);

			if(selectedRole != null) {
				String selectedPersonName;
				if(selectedRole.myPerson != null) {
					selectedPersonName = selectedRole.myPerson.name;
				}
				else {
					selectedPersonName = "";
				}
				myPersonList = new JSONStorableStringChooser("Person", people, selectedPersonName, "Person");
				myPersonList.setCallbackFunction += (string v) => {
					Atom person = SuperController.singleton.GetAtoms().Find(a => String.Equals(a.name, v));
					selectedRole.myPerson = person;
					UIRefreshMenu();
				};

				CreateMenuPopup(myPersonList, true);
			}

			string roleName = null;
			if(selectedRole == null)
				roleName = "";
			else
				roleName = selectedRole.myName;
			JSONStorableString role = new JSONStorableString("Role Name",
				roleName, (String newName) => {
					myRoles.Remove(selectedRole.myName);
					selectedRole.myName = newName;
					myRoles.Add(newName, selectedRole);
					UIRefreshMenu();
				}
			);

			CreateMenuButton("Add Role", UIAddRole, false);

			CreateMenuTextInput("Role Name", role, false);

			CreateMenuButton("Remove Role", UIRemoveRole, false);
		}

		private void UIAddRole() {
			String name = FindNewName("Role", "roles", myRoles.Keys.ToList());
			Role role = new Role(name);
			myRoles[name] = role;
			myRoleList.val = name;
			UIRefreshMenu();
		}

		private void UIRemoveRole() {
			myRoles.Remove(myRoleList.val);

			List<string> roles = myRoles.Keys.ToList();
			roles.Sort();
			if(roles.Count > 0) {
				myRoleList.val = roles[0];
			} else {
				myRoleList.val = "";
			}

			UIRefreshMenu();
		}

		private void CreateMessagesMenu()
		{
			CreateMenuInfoOneLine("<size=30><b>Messages</b></size>", false);

			CreateMenuInfo("Use this to define special transitions that only take place when a given message is received.", 100, false);

			CreateLoadButton("Load Messages", UILoadMessagesJSON, false, "Messages");
			CreateMenuButton("Save Messages", UISaveJSONDialog(UISaveMessagesJSON, "Messages"), false);

			List<string> messages = new List<string>();
			foreach (var m in myMessages)
			{
				Message message = m.Value;
				messages.Add(message.myName);
			}
			messages.Sort();

			string selectedMessageName;
			if (messages.Count == 0)
				selectedMessageName = "";
			else if(myMessageList != null && messages.Contains(myMessageList.val))
				selectedMessageName = myMessageList.val;
			else
				selectedMessageName = messages[0];

			myMessageList = new JSONStorableStringChooser("Message", messages, selectedMessageName, "Message");
			myMessageList.setCallbackFunction += (string v) => UIRefreshMenu();

			CreateMenuPopup(myMessageList, false);

			CreateMenuButton("Add Message", UIAddMessage, false);
			CreateMenuButton("Remove Message", UIRemoveMessage, false);

			Message selectedMessage;
			myMessages.TryGetValue(selectedMessageName, out selectedMessage);

			if(selectedMessage == null) {
				return;
			}

			JSONStorableString messageName = new JSONStorableString("Message Name",
				selectedMessage.myName, (String newName) => {
					myMessages.Remove(selectedMessage.myName);
					selectedMessage.myName = newName;
					myMessages[newName] = selectedMessage;
					UIRefreshMenu();
				}
			);

			JSONStorableString messageString = new JSONStorableString("Message String",
				selectedMessage.myMessageString, (String newString) => {
					selectedMessage.myMessageString = newString;
					UIRefreshMenu();
				}
			);

			CreateMenuTextInput("Name", messageName, false);
			CreateMenuTextInput("String", messageString, false);

			mySourceAnimationList = CreateDropDown(
				CastDict(myAnimations).ToDictionary(entry => (string)entry.Key, entry => (AnimationObject)entry.Value),
				mySourceAnimationList,
				"Source Animation"
			);
			Animation sourceAnimation;
			if(!myAnimations.TryGetValue(mySourceAnimationList.val, out sourceAnimation)){
				return;
			};

			Animation targetAnimation;
			bool singleChoice = false;
			AnimationObject mySingleChoice = null;
			if (selectedMessage.myTargetState == null) {
				singleChoice=false;
				mySingleChoice=null;
			} else {
				singleChoice=true;
				mySingleChoice=selectedMessage.myTargetState.myAnimation() as AnimationObject;
			}
			myTargetAnimationList = CreateDropDown(
				CastDict(myAnimations).ToDictionary(entry => (string)entry.Key, entry => (AnimationObject)entry.Value),
				myTargetAnimationList,
				"Target Animation",
				singleChoice:singleChoice,
				mySingleChoice:mySingleChoice
			);
			if(!myAnimations.TryGetValue(myTargetAnimationList.val, out targetAnimation)){
				return;
			};

			Layer sourceLayer;
			if (selectedMessage.myTargetState == null || sourceAnimation != selectedMessage.myTargetState.myLayer.myAnimation) {
				singleChoice=false;
				mySingleChoice=null;
			} else {
				singleChoice=true;
				mySingleChoice=selectedMessage.myTargetState.myLayer as AnimationObject as AnimationObject;
			}
			mySourceLayerList = CreateDropDown(
				CastDict(sourceAnimation.myLayers).ToDictionary(entry => (string)entry.Key, entry => (AnimationObject)entry.Value),
				mySourceLayerList,
				"Source Layer",
				singleChoice:singleChoice,
				mySingleChoice:mySingleChoice
			);
			if(!sourceAnimation.myLayers.TryGetValue(mySourceLayerList.val, out sourceLayer))
				return;

			Layer targetLayer;
			List<string> availableLayers = new List<string>();
			if ((selectedMessage.myTargetState == null) & (targetAnimation != sourceAnimation)) {
				singleChoice=false;
				mySingleChoice=null;
			} else if (selectedMessage.myTargetState == null) {
				singleChoice=true;
				mySingleChoice=sourceLayer as AnimationObject;
			} else {
				singleChoice=true;
				mySingleChoice=selectedMessage.myTargetState.myLayer as AnimationObject;
			}
			myTargetLayerList = CreateDropDown(
				CastDict(targetAnimation.myLayers).ToDictionary(entry => (string)entry.Key, entry => (AnimationObject)entry.Value),
				myTargetLayerList,
				"Target Layer",
				singleChoice:singleChoice,
				mySingleChoice:mySingleChoice
			);
			if(!targetAnimation.myLayers.TryGetValue(myTargetLayerList.val, out targetLayer))
				return;

			List<string> availableSourceStates = new List<string>();
			foreach (var s in sourceLayer.myStates)
			{
				State source = s.Value;
				bool isAvailable = true;
				Dictionary<string, Message> equalMessages = new Dictionary<string, Message>();
				foreach(var m in myMessages) {
					if(m.Value.myName == selectedMessage.myName)
						continue;

					if(m.Value.myMessageString == selectedMessage.myMessageString) {
						equalMessages[m.Key] = m.Value;
					}
				}
				foreach(var m in equalMessages) {
					Message message = m.Value;
					foreach(var ss in message.mySourceStates) {
						if (source == ss.Value)
							isAvailable = false;
					}
				}
				if(selectedMessage != null) {
					foreach(var ss in selectedMessage.mySourceStates) {
						if(ss.Value == source) {
							isAvailable = false;
						}
					}
					if(selectedMessage.myTargetState == source) {
						isAvailable = false;
					}
				}
				if(isAvailable) {
					availableSourceStates.Add(source.myName);
				}
			}
			availableSourceStates.Sort();

			mySourceStateList = PopulateJSONChooserSelection(
				availableSourceStates,
				mySourceStateList,
				"Source State"
			);

			List<string> availableTargetStates = new List<string>();
			foreach (var s in targetLayer.myStates)
			{
				State target = s.Value;
				if (selectedMessage != null) {
					string qualStateName = $"{target.myLayer.myAnimation.myName}.{target.myLayer.myName}.{target.myName}";
					if (selectedMessage.mySourceStates.Keys.ToList().Contains(qualStateName)) {
						continue;
					}
				}

				availableTargetStates.Add(target.myName);
			}
			availableTargetStates.Sort();

			myTargetStateList = PopulateJSONChooserSelection(
				availableTargetStates,
				myTargetStateList,
				"Target State"
			);

			if (mySourceAnimationList.choices.Count > 0)
			{
				CreateMenuPopup(mySourceAnimationList, false);
			}
			if (mySourceLayerList.choices.Count > 0)
			{
				CreateMenuPopup(mySourceLayerList, false);
			}

			if (availableSourceStates.Count > 0)
			{
				CreateMenuPopup(mySourceStateList, false);
				CreateMenuButton("Add Source State", () => {
					Animation a = myAnimations[mySourceAnimationList.val];
					Layer l = a.myLayers[mySourceLayerList.val];
					State s = l.myStates[mySourceStateList.val];
					string qualStateName = $"{a.myName}.{l.myName}.{s.myName}";
					selectedMessage.mySourceStates[qualStateName] = s;
					UIRefreshMenu();
				}, false);
			}
			foreach(var s in selectedMessage.mySourceStates) {
				CreateMenuLabelXButton(
					s.Key,
					() => {
						selectedMessage.mySourceStates.Remove(s.Key);
						UIRefreshMenu();
					}, false
				);
			}
			if((myTargetAnimationList.choices.Count > 0) & (selectedMessage.myTargetState == null)) {
				CreateMenuPopup(myTargetAnimationList, true);
			}
			if((myTargetLayerList.choices.Count > 0) & (selectedMessage.myTargetState == null)) {
				CreateMenuPopup(myTargetLayerList, true);
			}
			if (availableTargetStates.Count > 0)
			{
				if(selectedMessage.myTargetState == null) {
					CreateMenuPopup(myTargetStateList, true);
					if(myTargetStateList.val != "") {
						CreateMenuButton("Add Target State", () => {
							State targetState = targetLayer.myStates[myTargetStateList.val];
							selectedMessage.myTargetState = targetState;
							UIRefreshMenu();
						}, true);
					}
				}
				else {
					CreateMenuLabelXButton(
						$"{selectedMessage.myTargetState.myLayer.myAnimation.myName}.{selectedMessage.myTargetState.myLayer.myName}.{selectedMessage.myTargetState.myName}",
						() => {
							selectedMessage.myTargetState = null;
							UIRefreshMenu();
						}, true
					);
				}
			}
			else
			{
				CreateMenuInfo("You need to add a second state before you can add messages.", 100, false);
				return;
			}
		}

		private void CreateOptionsMenu()
		{
			CreateMenuInfoOneLine("<size=30><b>General Options</b></size>", false);

			CreateMenuSlider(myGlobalDefaultTransitionDuration, false);
			CreateMenuSlider(myGlobalDefaultEaseInDuration, false);
			CreateMenuSlider(myGlobalDefaultEaseOutDuration, false);
			CreateMenuSlider(myGlobalDefaultWaitDurationMin, false);
			CreateMenuSlider(myGlobalDefaultWaitDurationMax, false);
		}

		// =======================================================================================

		private List<string> GetAllAtomUIDs()
		{
			// working around VaM weirdness not returning hidden atoms
			SuperController sc = SuperController.singleton;
			bool wasSorted = sc.sortAtomUIDs;
			sc.sortAtomUIDs = false;
			List<string> atomUIDs = sc.GetAtomUIDs();
			sc.sortAtomUIDs = wasSorted;
			return atomUIDs;
		}

		private void UILoadRolesJSON(string url)
		{
			JSONClass jc = LoadJSON(url).AsObject;
			if (jc != null)
				LoadRoles(jc);

			UIRefreshMenu();
		}

		private void UILoadMessagesJSON(string url)
		{
			JSONClass jc = LoadJSON(url).AsObject;
			if (jc != null)
				LoadMessages(jc);

			UIRefreshMenu();
		}

		private void UILoadAnimationsJSON(string url)
		{
			JSONClass jc = LoadJSON(url).AsObject;
			if (jc != null)
				LoadAnimations(jc);

			UIRefreshMenu();
		}

		private void UILoadAnimationJSON(string url)
		{
			JSONClass jc = LoadJSON(url).AsObject;
			if (jc != null){
				Animation animation = LoadAnimation(jc);
				myAnimations[animation.myName] = animation;
				SetAnimation(animation);
			}

			if (myCurrentLayer != null)
			{
				myMainLayer.valNoCallback = myCurrentLayer.myName;
			}

			if (myCurrentState != null)
			{
				myMainState.valNoCallback = myCurrentState.myName;
			}

			UIRefreshMenu();
		}

		private void UILoadJSON(string url)
		{
			JSONClass jc = LoadJSON(url).AsObject;
			if (jc != null) {
				Layer layer = LoadLayer(jc, true);
				JSONClass layerObj = jc["Layer"].AsObject;
				myCurrentAnimation.myLayers[layer.myName] = layer;
				layer.myAnimation = myCurrentAnimation;
				LoadTransitions(layer, layerObj);
				SetLayer(layer);
			}

			if (myCurrentState != null)
			{
				myMainState.valNoCallback = myCurrentState.myName;
			}
			UIRefreshMenu();
		}

		private UnityAction UISaveJSONDialog(uFileBrowser.FileBrowserCallback saveJSON, string path = "")
		{
			UnityAction action = new UnityAction(() => {
				SuperController sc = SuperController.singleton;
				sc.GetMediaPathDialog(saveJSON, FILE_EXTENSION, BASE_DIRECTORY + "/" + path, false, true, false, null, false, null, false, false);
				sc.mediaFileBrowserUI.SetTextEntry(true);
				if (sc.mediaFileBrowserUI.fileEntryField != null)
				{
					string filename = ((int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds).ToString();
					sc.mediaFileBrowserUI.fileEntryField.text = filename + "." + FILE_EXTENSION;
					sc.mediaFileBrowserUI.ActivateFileNameField();
				}
			});
			return action;
		}

		private void UISaveRolesJSON(string path)
		{
			if (string.IsNullOrEmpty(path))
				return;
			path = path.Replace('\\', '/');
			JSONClass jc = new JSONClass();
			SaveRoles(jc);
			SaveJSON(jc, path);
		}

		private void UISaveMessagesJSON(string path)
		{
			if (string.IsNullOrEmpty(path))
				return;
			path = path.Replace('\\', '/');
			JSONClass jc = new JSONClass();
			SaveMessages(jc);
			SaveJSON(jc, path);
		}

		private void UISaveAnimationsJSON(string path)
		{
			if (string.IsNullOrEmpty(path))
				return;
			path = path.Replace('\\', '/');
			JSONClass jc = SaveAnimations();
			SaveJSON(jc, path);
		}

		private void UISaveAnimationJSON(string path)
		{
			if (string.IsNullOrEmpty(path))
				return;
			path = path.Replace('\\', '/');
			JSONClass jc = SaveAnimation(myCurrentAnimation);
			SaveJSON(jc, path);
		}

		private void UISaveLayerJSON(string path)
		{
			if (string.IsNullOrEmpty(path))
				return;
			path = path.Replace('\\', '/');
			JSONClass jc = SaveLayer(myCurrentLayer);
			SaveJSON(jc, path);
		}

		private void UIAddControlCapture()
		{
			ControlCapture cc = new ControlCapture(this, myCapturesControlList.val);
			myCurrentLayer.myControlCaptures.Add(cc);
			foreach (var state in myCurrentLayer.myStates)
				cc.CaptureEntry(state.Value);
			UIRefreshMenu();
		}

		private void UIRemoveControlCapture(ControlCapture cc)
		{
			myCurrentLayer.myControlCaptures.Remove(cc);
			foreach (var state in myCurrentLayer.myStates)
				state.Value.myControlEntries.Remove(cc);
			UIRefreshMenu();
		}

		private void UIToggleControlCapturePosition()
		{
			bool apply = false;
			for (int i=0; i<myCurrentLayer.myControlCaptures.Count; ++i)
				apply |= !myCurrentLayer.myControlCaptures[i].myApplyPosition;
			for (int i=0; i<myCurrentLayer.myControlCaptures.Count; ++i)
				myCurrentLayer.myControlCaptures[i].myApplyPosition = apply;
			UIRefreshMenu();
		}

		private void UIToggleControlCaptureRotation()
		{
			bool apply = false;
			for (int i=0; i<myCurrentLayer.myControlCaptures.Count; ++i)
				apply |= !myCurrentLayer.myControlCaptures[i].myApplyRotation;
			for (int i=0; i<myCurrentLayer.myControlCaptures.Count; ++i)
				myCurrentLayer.myControlCaptures[i].myApplyRotation = apply;
			UIRefreshMenu();
		}

		private void UIAddMorphCapture()
		{
			DAZCharacterSelector geometry = containingAtom.GetStorableByID("geometry") as DAZCharacterSelector;
			if (geometry == null)
				return;

			string qualifiedName = myCapturesMorphList.val;
			bool morphIsMale = qualifiedName.EndsWith("#Male");
			if (!morphIsMale && !qualifiedName.EndsWith("#Female"))
				return;

			string uid = qualifiedName.Substring(0, qualifiedName.Length - (morphIsMale ? 5 : 7));
			GenerateDAZMorphsControlUI morphControl = morphIsMale ? geometry.morphsControlMaleUI : geometry.morphsControlFemaleUI;
			DAZMorph morph =  morphControl.GetMorphByUid(uid);
			if (morph == null)
				return;

			DAZCharacterSelector.Gender gender = morphIsMale ? DAZCharacterSelector.Gender.Male : DAZCharacterSelector.Gender.Female;
			if (myCurrentLayer.myMorphCaptures.FindIndex(x => x.myMorph == morph && x.myGender == gender) >= 0)
				return;

			MorphCapture mc = new MorphCapture(this, gender, morph);
			myCurrentLayer.myMorphCaptures.Add(mc);
			foreach (var state in myCurrentLayer.myStates)
				mc.CaptureEntry(state.Value);

			UIRefreshMenu();
		}

		private void UIUpdateMorphFullename(string qualifiedName)
		{
			myCapturesMorphFullname.val = "";

			DAZCharacterSelector geometry = containingAtom.GetStorableByID("geometry") as DAZCharacterSelector;
			if (geometry == null)
				return;

			bool personIsMale = geometry.gender == DAZCharacterSelector.Gender.Male;
			bool morphIsMale = qualifiedName.EndsWith("#Male");
			if (!morphIsMale && !qualifiedName.EndsWith("#Female"))
				return;

			string uid = qualifiedName.Substring(0, qualifiedName.Length - (morphIsMale ? 5 : 7));
			GenerateDAZMorphsControlUI morphControl = morphIsMale ? geometry.morphsControlMaleUI : geometry.morphsControlFemaleUI;
			DAZMorph morph =  morphControl.GetMorphByUid(uid);
			if (morph == null)
				return;

			StringBuilder builder = new StringBuilder(256);
			int idx = uid.IndexOf(":/");
			if (idx >= 0)
				builder.Append("<i><color=#FF8080>").Append(uid.Substring(0, idx)).Append("</color> / ");
			else
				builder.Append("<i><color=#808080>Local Morph</color> / ");

			builder.Append(morphIsMale == personIsMale ? "<color=#808080>" :  "<color=#FF8080>");
			builder.Append(morphIsMale ? "Male</color>\n" : "Female</color>\n");
			builder.Append("<color=#808080>").Append(morph.resolvedRegionName).Append("</color></i>\n");
			builder.Append(morph.resolvedDisplayName);

			myCapturesMorphFullname.val = builder.ToString();
		}

		private void UICollectMorphsUIDs(DAZCharacterSelector.Gender gender, DAZCharacterSelector geometry, List<string> collectedMorphUIDs)
		{
			bool isMale = gender == DAZCharacterSelector.Gender.Male;
			GenerateDAZMorphsControlUI morphsControl = isMale ? geometry.morphsControlMaleUI : geometry.morphsControlFemaleUI;
			List<DAZMorph> morphs = morphsControl.GetMorphs();
			for (int i=0; i<morphs.Count; ++i)
			{
				if (!morphs[i].visible)
					continue;
				string uid = morphs[i].uid.Replace(isMale ? "Custom/Atom/Person/Morphs/male/" : "Custom/Atom/Person/Morphs/female/", "&&");
				collectedMorphUIDs.Add(uid + (isMale ? "#Male" : "#Female"));
			}
		}

		private void UICollectMorphsNames(DAZCharacterSelector geometry, List<string> collectedMorphUIDs, List<string> collectedMorphsNames)
		{
			int maxPackageLen = 23;

			bool personIsMale = geometry.gender == DAZCharacterSelector.Gender.Male;
			StringBuilder builder = new StringBuilder(256);
			for (int i=0; i<collectedMorphUIDs.Count; ++i)
			{
				string qualifiedName = collectedMorphUIDs[i];
				bool morphIsMale = qualifiedName.EndsWith("#Male");
				qualifiedName = qualifiedName.Replace("&&", morphIsMale ? "Custom/Atom/Person/Morphs/male/" : "Custom/Atom/Person/Morphs/female/");
				collectedMorphUIDs[i] = qualifiedName;
				string uid = qualifiedName.Substring(0, qualifiedName.Length - (morphIsMale ? 5 : 7));
				int idx = uid.IndexOf(":/");
				if (idx > maxPackageLen)
				{
					builder.Append("<i><color=#FF8080>");
					builder.Append(uid.Substring(0, 3));
					builder.Append("...");
					builder.Append(uid.Substring(idx-maxPackageLen+6, maxPackageLen-6));
					builder.Append("</color> / ");
				}
				else if (idx >= 0)
				{
					builder.Append("<i><color=#FF8080>");
					builder.Append(uid.Substring(0, idx));
					builder.Append("</color> / ");
				}
				else
				{
					builder.Append("<i><color=#808080>Local Morph</color> / ");
				}

				builder.Append(personIsMale == morphIsMale ? "<color=#808080>" : "<color=#FF8080>");
				builder.Append(morphIsMale ? "M</color></i>\n" : "F</color></i>\n");

				GenerateDAZMorphsControlUI morphControl = morphIsMale ? geometry.morphsControlMaleUI : geometry.morphsControlFemaleUI;
				DAZMorph morph =  morphControl.GetMorphByUid(uid);
				if (morph != null)
					builder.Append(morph.resolvedDisplayName);

				collectedMorphsNames.Add(builder.ToString());
				builder.Length = 0;
			}
		}

		private void UIRemoveMorphCapture(MorphCapture mc)
		{
			myCurrentLayer.myMorphCaptures.Remove(mc);
			foreach (var state in myCurrentLayer.myStates)
				state.Value.myMorphEntries.Remove(mc);
			UIRefreshMenu();
		}

		private void UIToggleMorphCapture()
		{
			bool apply = false;
			for (int i=0; i<myCurrentLayer.myMorphCaptures.Count; ++i)
				apply |= !myCurrentLayer.myMorphCaptures[i].myApply;
			for (int i=0; i<myCurrentLayer.myMorphCaptures.Count; ++i)
				myCurrentLayer.myMorphCaptures[i].myApply = apply;
			UIRefreshMenu();
		}

		private void UIAddAnimation()
		{
			string name = FindNewName("Animation", "animations", new List<string>(myAnimations.Keys));
			if (name == null)
				return;

			CreateAnimation(name);
			myMainAnimation.choices = myAnimations.Keys.ToList();
			myMainAnimation.val = name;
			myMainLayer.valNoCallback = "";
			myMainState.valNoCallback = "";
		}

		private void UIAddLayer()
		{
			string name = FindNewName("Layer", "layers", new List<string>(myCurrentAnimation.myLayers.Keys));
			if (name == null)
				return;

			Layer layer = CreateLayer(name);
			myCurrentAnimation.myLayers[layer.myName] = layer;
			myMainLayer.choices = myCurrentAnimation.myLayers.Keys.ToList();
			myMainLayer.val = name;
			myMainState.val = "";
			UIRefreshMenu();
			return;
		}

		private void UIAddState()
		{
			string name = FindNewName("State", "states", new List<string>(myCurrentLayer.myStates.Keys));
			if (name == null)
				return;

			CreateState(name);
			myMainState.choices = myCurrentLayer.myStates.Keys.ToList();
			myMainState.val = name;
			UIRefreshMenu();
			return;
		}

		private void UIDuplicateState()
		{
			State source = UIGetState();
			if (source == null)
				return;

			string name = FindNewName("State", "states", new List<string>(myCurrentLayer.myStates.Keys));
			if (name == null)
				return;

			State duplicate = new State(name, source);
			duplicate.myTransitions = new List<BaseTransition>(source.myTransitions);
			foreach (var s in myCurrentLayer.myStates)
			{
				State state = s.Value;
				if (state != source && state.isReachable(source)) {
					BaseTransition transition = state.getIncomingTransition(source);
					if(transition is Transition) {
						Transition duplicatedTransition = new Transition(transition as Transition);
						state.myTransitions.Add(duplicatedTransition);
					} else {
						IndirectTransition duplicatedTransition = new IndirectTransition(transition as IndirectTransition);
						state.myTransitions.Add(duplicatedTransition);
					}
				}
			}
			foreach (var entry in source.myControlEntries)
			{
				ControlCapture cc = entry.Key;
				ControlEntryAnchored ce = entry.Value.Clone();
				duplicate.myControlEntries.Add(cc, ce);
			}
			myCurrentLayer.CaptureState(duplicate);
			myCurrentLayer.myStates[name] = duplicate;
			myMainState.val = name;
			UIRefreshMenu();
		}

		private void UIAddMessage()
		{
			string name = FindNewName("Message", "messages", new List<string>(myMessages.Keys));
			if (name == null)
				return;

			myMessages[name] = new Message(name);
			myMessageList.val = name;
			UIRefreshMenu();
		}

		private void UIRemoveMessage()
		{
			myMessages.Remove(myMessageList.val);

			List<string> messages = myMessages.Keys.ToList();
			messages.Sort();
			if(messages.Count > 0) {
				myMessageList.val = messages[0];
			} else {
				myMessageList.val = "";
			}

			UIRefreshMenu();
		}

		private string FindNewName(string nameBase, string objectNames, List<string> myExistingNames)
		{
			for (int i=1; i<1000; ++i)
			{
				string name = nameBase+"#" + i;
				if (!myExistingNames.Contains(name))
					return name;
			}
			SuperController.LogError("AnimationPoser: Too many " + objectNames + "!");
			return null;
		}

		private void UICaptureState()
		{
			State state = UIGetState();
			if (state == null)
				return;

			myCurrentLayer.CaptureState(state);
			UIRefreshMenu();
		}

		private void UIApplyAnchors()
		{
			foreach (var s in myCurrentLayer.myStates)
			{
				State state = s.Value;
				foreach (var ce in state.myControlEntries)
				{
					ControlEntryAnchored entry = ce.Value;
					entry.UpdateInstant();
				}
			}

			for (int i=0; i<myCurrentLayer.myControlCaptures.Count; ++i)
				myCurrentLayer.myControlCaptures[i].UpdateState(myCurrentState);
		}

		private void UIRemoveAnimation()
		{
			Animation animation = UIGetAnimation();
			if (animation == null) {
				return;
			}

			foreach (var a in myAnimations) {
				foreach (var l in a.Value.myLayers) {
					foreach (var s in l.Value.myStates)
						s.Value.myTransitions.RemoveAll(x => x.myTargetState.myAnimation() == animation);
				}
			}
			myAnimations.Remove(animation.myName);

			List<string> animations = myAnimations.Keys.ToList();
			animations.Sort();
			if(animations.Count > 0) {
				myMainAnimation.val = animations[0];
			} else {
				myMainAnimation.val = "";
			}

			UIRefreshMenu();
		}

		private void UIRemoveLayer()
		{
			Layer layer = UIGetLayer();
			if (layer == null)
				return;

			foreach (var a in myAnimations) {
				foreach (var l in a.Value.myLayers) {
					foreach (var s in l.Value.myStates)
						s.Value.myTransitions.RemoveAll(x => x.myTargetState.myLayer == layer);
				}
			}
			myCurrentAnimation.myLayers.Remove(layer.myName);

			List<string> layers = myCurrentAnimation.myLayers.Keys.ToList();
			layers.Sort();
			if(layers.Count > 0) {
				myMainLayer.val = layers[0];
			}

			UIRefreshMenu();
		}

		private void UIRemoveState()
		{
			State state = UIGetState();
			if (state == null)
				return;

			foreach (var a in myAnimations) {
				foreach (var l in a.Value.myLayers) {
					foreach (var s in l.Value.myStates)
						s.Value.myTransitions.RemoveAll(x => x.myTargetState == state);
				}
			}

			myCurrentLayer.myStates.Remove(state.myName);

			state.EnterBeginTrigger.Remove();
			state.EnterEndTrigger.Remove();
			state.ExitBeginTrigger.Remove();
			state.ExitEndTrigger.Remove();

			UIRefreshMenu();
		}

		private void UIRenameAnimation(string name)
		{
			if (myCurrentAnimation == null)
				return;
			if (myCurrentAnimation.myName == name)
				return;

			myAnimations.Remove(myCurrentAnimation.myName);

			myAnimations.Add(name, myCurrentAnimation);
			myCurrentAnimation.myName = name;
			myMainAnimation.valNoCallback = name;
			UIRefreshMenu();
		}
		private void UIRenameMessage(string name)
		{
			if (myCurrentAnimation == null)
				return;
			if (myCurrentAnimation.myName == name)
				return;

			myAnimations.Remove(myCurrentAnimation.myName);

			myAnimations.Add(name, myCurrentAnimation);
			myCurrentAnimation.myName = name;
			UIRefreshMenu();
		}


		private void UIRenameLayer(string name)
		{
			if (myCurrentLayer == null)
				return;
			if (myCurrentLayer.myName == name)
				return;

			myCurrentAnimation.myLayers.Remove(myCurrentLayer.myName);

			myCurrentAnimation.myLayers.Add(name, myCurrentLayer);
			myCurrentLayer.myName = name;
			UIRefreshMenu();
		}

		private void UIRenameState(string name)
		{
			State state = UIGetState();
			if (state == null)
				return;
			if (state.myName == name)
				return;

			myCurrentLayer.myStates.Remove(state.myName);

			int altIndex = 2;
			string baseName = name;
			while (myCurrentLayer.myStates.ContainsKey(name))
			{
				name = baseName + "#" + altIndex;
				++altIndex;
			}

			myCurrentLayer.myStates.Add(name, state);
			state.myName = name;
			state.EnterBeginTrigger.SecondaryName = name;
			state.EnterEndTrigger.SecondaryName = name;
			state.ExitBeginTrigger.SecondaryName = name;
			state.ExitEndTrigger.SecondaryName = name;
			myMainState.valNoCallback = name;
			UIRefreshMenu();
		}

		private void UISetAnchors()
		{
			State state = UIGetState();
			if (state == null)
				return;

			ControlCapture controlCapture = myCurrentLayer.myControlCaptures.Find(cc => cc.myName == myAnchorCaptureList.val);
			if (controlCapture == null)
				return;

			ControlEntryAnchored controlEntry;
			if (!state.myControlEntries.TryGetValue(controlCapture, out controlEntry))
				return;

			controlEntry.UpdateInstant();

			int anchorMode = myAnchorModes.FindIndex(m => m == myAnchorModeList.val);
			if (anchorMode >= 0)
				controlEntry.myAnchorMode = anchorMode;
			controlEntry.myBlendRatio = myAnchorBlendRatio.val;
			controlEntry.myDampingTime = myAnchorDampingTime.val;

			if (anchorMode >= ControlEntryAnchored.ANCHORMODE_SINGLE)
			{
				controlEntry.myAnchorAAtom = myAnchorAtomListA.val;
				controlEntry.myAnchorAControl = myAnchorControlListA.val;
			}
			if (anchorMode == ControlEntryAnchored.ANCHORMODE_BLEND)
			{
				controlEntry.myAnchorBAtom = myAnchorAtomListB.val;
				controlEntry.myAnchorBControl = myAnchorControlListB.val;
			}

			controlEntry.AdjustAnchor();

			UIRefreshMenu();
		}

		private void UISetAnchorsBlendDamp()
		{
			State state = UIGetState();
			if (state == null)
				return;

			ControlCapture controlCapture = myCurrentLayer.myControlCaptures.Find(cc => cc.myName == myAnchorCaptureList.val);
			if (controlCapture == null)
				return;

			ControlEntryAnchored controlEntry;
			if (!state.myControlEntries.TryGetValue(controlCapture, out controlEntry))
				return;

			controlEntry.myBlendRatio = myAnchorBlendRatio.val;
			controlEntry.myDampingTime = myAnchorDampingTime.val;
			controlEntry.AdjustAnchor();
		}

		private void UIAddTransition()
		{
			State source = UIGetState();
			if (source == null)
				return;
			Animation targetAnimation;
			if (!myAnimations.TryGetValue(myTargetAnimationList.val, out targetAnimation))
				return;
			Layer targetLayer;
			if(targetAnimation != myCurrentAnimation) {
				if (!targetAnimation.myLayers.TryGetValue(myTargetLayerList.val, out targetLayer))
					return;
			} else {
				targetLayer = myCurrentLayer;
			}
			State target;
			if (!targetLayer.myStates.TryGetValue(myTargetStateList.val, out target))
				return;

			if (!source.isReachable(target)) {
				Transition transition = new Transition(source, target);
				source.myTransitions.Add(transition);
			}

			UIRefreshMenu();
		}

		private void UIAddIndirectTransition()
		{
			State source = UIGetState();
			if (source == null)
				return;
			Animation targetAnimation;
			if (!myAnimations.TryGetValue(myTargetAnimationList.val, out targetAnimation))
				return;
			Layer targetLayer;
			if(targetAnimation != myCurrentAnimation) {
				if (!targetAnimation.myLayers.TryGetValue(myTargetLayerList.val, out targetLayer))
					return;
			} else {
				targetLayer = myCurrentLayer;
			}
			State target;
			if (!targetLayer.myStates.TryGetValue(myTargetStateList.val, out target))
				return;

			if (!source.isReachable(target)) {
				IndirectTransition transition = new IndirectTransition(source, target);
				source.myTransitions.Add(transition);
			}

			UIRefreshMenu();
		}

		private void UIAddSequentialTransitions()
		{
			if (myCurrentLayer == null || myCurrentLayer.myStates.Count == 0)
				return;
			List<string> myStates = myCurrentLayer.myStates.Keys.ToList();
			myStates.Sort();

			for (int i=0; i < (myStates.Count - 1); i++) {
				State source = myCurrentLayer.myStates[myStates[i]];
				State target = myCurrentLayer.myStates[myStates[i+1]];

				if (!source.isReachable(target)) {
					Transition transition = new Transition(source, target);
					source.myTransitions.Add(transition);
				}
			}
			UIRefreshMenu();
		}

		private void UIAddAllTransitions()
		{
			if (myCurrentLayer == null || myCurrentLayer.myStates.Count == 0)
				return;
			List<string> myStates = myCurrentLayer.myStates.Keys.ToList();
			myStates.Sort();

			for (int i=1; i < myStates.Count; i++) {
				for (int j=0; j < i; j++) {
					State source = myCurrentLayer.myStates[myStates[i]];
					State target = myCurrentLayer.myStates[myStates[j]];

					if (!source.isReachable(target)) {
						Transition transition = new Transition(source, target);
						source.myTransitions.Add(transition);
					}
					if (!target.isReachable(source)) {
						Transition transition = new Transition(target, source);
						target.myTransitions.Add(transition);
					}
				}
			}
			UIRefreshMenu();
		}

		private void UIUpdateTransition(State source, State target, bool transitionEnabled)
		{
			if (transitionEnabled && !source.isReachable(target)) {
				Transition transition = new Transition(source, target);
				source.myTransitions.Add(transition);
			} else if (!transitionEnabled)
				source.removeTransition(target);
			UIRefreshMenu();
		}

		private void UIRemoveTransition(State source, State target)
		{
			source.removeTransition(target);
			target.removeTransition(source);
			UIRefreshMenu();
		}

		private Animation UIGetAnimation()
		{
			Animation animation;
			if (!myAnimations.TryGetValue(myMainAnimation.val, out animation))
			{
				SuperController.LogError("AnimationPoser: Invalid animation selected!");
				return null;
			}
			else
			{
				return animation;
			}
		}

		private Layer UIGetLayer()
		{
			Layer layer;
			if (!myCurrentAnimation.myLayers.TryGetValue(myMainLayer.val, out layer))
			{
				SuperController.LogError("AnimationPoser: Invalid layer selected!");
				return null;
			}
			else
			{
				return layer;
			}
		}

		private State UIGetState()
		{
			State state;
			if (!myCurrentLayer.myStates.TryGetValue(myMainState.val, out state))
			{
				SuperController.LogError("AnimationPoser: Invalid state selected!");
				return null;
			}
			else
			{
				return state;
			}
		}
		private BaseTransition UIGetTransition()
		{
			State sourceState;

			if (!myCurrentLayer.myStates.TryGetValue(myMainState.val, out sourceState))
			{
				SuperController.LogError("AnimationPoser: Invalid transition selected!");
				return null;
			}
			else
			{
				Animation targetAnimation;
				if (!myAnimations.TryGetValue(myTargetAnimationList.val, out targetAnimation))
					return null;
				Layer targetLayer;
				if(targetAnimation != myCurrentAnimation) {
					if (!targetAnimation.myLayers.TryGetValue(myTargetLayerList.val, out targetLayer))
						return null;
				} else {
					targetLayer = myCurrentLayer;
				}
				State target;
				if (!targetLayer.myStates.TryGetValue(myTargetStateList.val, out target))
					return null;

				return sourceState.getIncomingTransition(target);
			}
		}



		// =======================================================================================


		private void CreateMenuButton(string label, UnityAction callback, bool rightSide)
		{
			UIDynamicButton uid = Utils.SetupButton(this, label, callback, rightSide);
			myMenuElements.Add(uid);
		}

		private void CreateLoadButton(string label, JSONStorableString.SetStringCallback callback, bool rightSide, string path = "")
		{
			JSONStorableUrl myDataFile;
			myDataFile = new JSONStorableUrl("AnimPose", "", callback, FILE_EXTENSION, true);
			UIDynamicButton button = CreateButton(label, rightSide);
			myDataFile.setCallbackFunction -= callback;
			myDataFile.allowFullComputerBrowse = false;
			myDataFile.allowBrowseAboveSuggestedPath = true;
			myDataFile.SetFilePath(BASE_DIRECTORY+"/"+path+"/");
			myDataFile.RegisterFileBrowseButton(button.button);
			myDataFile.setCallbackFunction += callback;
			myMenuElements.Add(button);
		}

		private void CreateMenuLabelXButton(string label, UnityAction callback, bool rightSide)
		{
			UIDynamicLabelXButton uid = Utils.SetupLabelXButton(this, label, callback, rightSide);
			myMenuElements.Add(uid);
		}

		private void CreateMenuLabel2BXButton(string label, string text1, string text2, bool value1, bool value2,
		                                      UnityAction<bool> callback1, UnityAction<bool> callback2, UnityAction callbackX, bool rightSide)
		{
			if (myLabelWith2BXButtonPrefab == null)
				InitPrefabs();

			Transform t = CreateUIElement(myLabelWith2BXButtonPrefab.transform, rightSide);
			UIDynamicLabel2BXButton uid = t.GetComponent<UIDynamicLabel2BXButton>();
			uid.label.text = label;
			uid.text1.text = text1;
			uid.text2.text = text2;

			Color color1 = uid.image1.color;
			color1.a = value1 ? 1.0f : 0.0f;
			uid.image1.color = color1;
			uid.toggle1.isOn = value1;
			uid.toggle1.onValueChanged.AddListener((bool v) => {
				Color color = uid.image1.color;
				color.a = uid.toggle1.isOn ? 1.0f : 0.0f;
				uid.image1.color = color;
				callback1(v);
			});

			Color color2 = uid.image2.color;
			color2.a = value2 ? 1.0f : 0.0f;
			uid.image2.color = color2;
			uid.toggle2.isOn = value2;
			uid.toggle2.onValueChanged.AddListener((bool v) => {
				Color color = uid.image2.color;
				color.a = uid.toggle2.isOn ? 1.0f : 0.0f;
				uid.image2.color = color;
				callback2(v);
			});
			uid.buttonX.onClick.AddListener(callbackX);

			myMenuElements.Add(uid);
			t.gameObject.SetActive(true);
		}

		private void CreateMenuLabelMXButton(string label, bool valueM, UnityAction<bool> mCallback, UnityAction callbackX, bool rightSide)
		{
			if (myLabelWithMXButtonPrefab == null)
				InitPrefabs();

			Transform t = CreateUIElement(myLabelWithMXButtonPrefab.transform, rightSide);
			UIDynamicLabelMXButton uid = t.GetComponent<UIDynamicLabelMXButton>();
			uid.label.text = label;

			Color colorM = uid.imageM.color;
			colorM.a = valueM ? 1.0f : 0.0f;
			uid.imageM.color = colorM;
			uid.toggleM.isOn = valueM;
			uid.toggleM.onValueChanged.AddListener((bool v) => {
				Color color = uid.imageM.color;
				color.a = uid.toggleM.isOn ? 1.0f : 0.0f;
				uid.imageM.color = color;
				mCallback(v);
			});

			uid.buttonX.onClick.AddListener(callbackX);

			myMenuElements.Add(uid);
			t.gameObject.SetActive(true);
		}

		private void CreateMenuTwinButton(string leftLabel, UnityAction leftCallback, string rightLabel, UnityAction rightCallback, bool rightSide)
		{
			UIDynamicTwinButton uid = Utils.SetupTwinButton(this, leftLabel, leftCallback, rightLabel, rightCallback, rightSide);
			myMenuElements.Add(uid);
		}

		private void CreateMenuToggle(JSONStorableBool storable, bool rightSide)
		{
			CreateToggle(storable, rightSide);
			myMenuElements.Add(storable);
		}

		private void CreateMenuSlider(JSONStorableFloat storable, bool rightSide)
		{
			UIDynamicSlider slider = CreateSlider(storable, rightSide);
			slider.rangeAdjustEnabled = true;
			myMenuElements.Add(slider);
		}

		private void CreateMenuPopup(JSONStorableStringChooser storable, bool rightSide)
		{
			CreateScrollablePopup(storable, rightSide);
			myMenuElements.Add(storable);
		}

		private void CreateMenuFilterPopup(JSONStorableStringChooser storable, bool rightSide)
		{
			CreateFilterablePopup(storable, rightSide);
			myMenuElements.Add(storable);
		}

		private void CreateMenuFilterPopupCached(JSONStorableStringChooser storable, bool rightSide, ref UIDynamicPopup cachedPopup)
		{
			if (cachedPopup == null)
			{
				cachedPopup = CreateFilterablePopup(storable, rightSide);
				myMenuCached.Add(cachedPopup);
			}
			else
			{
				cachedPopup.transform.SetAsLastSibling();
				cachedPopup.gameObject.SetActive(true);
			}
		}

		private void CreateMenuTextInput(string label, JSONStorableString storable, bool rightSide)
        {
			UIDynamicLabelInput uid = Utils.SetupTextInput(this, label, storable, rightSide);
			myMenuElements.Add(uid);
        }

		private void CreateMenuInfo(string text, float height, bool rightSide)
		{
			UIDynamicTextInfo uid = Utils.SetupInfoTextNoScroll(this, text, height, rightSide);
			myMenuElements.Add(uid);
		}

		private void CreateMenuInfo(JSONStorableString storable, float height, bool rightSide)
		{
			UIDynamicTextInfo uid = Utils.SetupInfoTextNoScroll(this, storable, height, rightSide);
			myMenuElements.Add(uid);
		}

		private void CreateMenuInfoWhiteBackground(string text, float height, bool rightSide)
		{
			UIDynamicTextInfo uid = Utils.SetupInfoTextNoScroll(this, text, height, rightSide);
			uid.background.GetComponent<Image>().color = Color.white;
			myMenuElements.Add(uid);
		}

		private void CreateMenuInfoScrolling(string text, float height, bool rightSide)
		{
			JSONStorableString storable = Utils.SetupInfoText(this, text, height, rightSide);
			myMenuElements.Add(storable);
		}

		private void CreateMenuInfoOneLine(string text, bool rightSide)
		{
			UIDynamicTextInfo uid = Utils.SetupInfoOneLine(this, text, rightSide);
			myMenuElements.Add(uid);
		}

		private void CreateMenuSpacer(float height, bool rightSide)
		{
			UIDynamic spacer = CreateSpacer(rightSide);
			spacer.height = height;
			myMenuElements.Add(spacer);
		}

		private void CleanupMenu()
		{
			Utils.RemoveUIElements(this, myMenuElements);

			for (int i=0; i<myMenuCached.Count; ++i)
			{
				UIDynamic uid = myMenuCached[i];
				if (uid is UIDynamicPopup)
					((UIDynamicPopup)uid).popup.visible = false;
				uid.gameObject.SetActive(false);
			}
		}


		// =======================================================================================

		private class UIDynamicLabel2BXButton : UIDynamicUtils
		{
			public Text label;
			public Image image1;
			public Image image2;
			public Toggle toggle1;
			public Toggle toggle2;
			public Text text1;
			public Text text2;
			public Button buttonX;
		}

		private class UIDynamicLabelMXButton : UIDynamicUtils
		{
			public Text label;
			public Image imageM;
			public Toggle toggleM;
			public Button buttonX;
		}

		private class UIDynamicTabBar : UIDynamicUtils
		{
			public List<Button> buttons = new List<Button>();
		}

		private struct UITransition
		{
			public State state;
			public Layer layer;
			public Animation animation;
			public bool incoming;
			public bool outgoing;

			public UITransition(State target, bool incoming, bool outgoing)
			{
				this.state = target;
				this.layer = target.myLayer;
				this.animation = target.myAnimation();
				this.incoming = incoming;
				this.outgoing = outgoing;
			}

			public string displayString() {
				if(animation != myCurrentAnimation) {
					return this.animation.myName + ":" + this.layer.myName + ":" + this.state.myName;
				} else if (layer != myCurrentLayer) {
					return this.layer.myName + ":" + this.state.myName;
				}
				return this.state.myName;
			}
		}
	}

}
