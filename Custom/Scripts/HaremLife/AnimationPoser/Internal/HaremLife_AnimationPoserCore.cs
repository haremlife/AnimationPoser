/* /////////////////////////////////////////////////////////////////////////////////////////////////
AnimationPoser by HaremLife.
Based on Life#IdlePoser by MacGruber.
State-based idle animation system with anchoring.
https://www.patreon.com/MacGruber_Laboratory
https://github.com/haremlife/AnimationPoser

Licensed under CC BY-SA after EarlyAccess ended. (see https://creativecommons.org/licenses/by-sa/4.0/)

///////////////////////////////////////////////////////////////////////////////////////////////// */

#if !VAM_GT_1_20
	#error AnimationPoser requires VaM 1.20 or newer!
#endif

using UnityEngine;
using UnityEngine.Events;
using System;
using System.Linq;
using System.Collections.Generic;
using SimpleJSON;

namespace HaremLife
{
	public partial class AnimationPoser : MVRScript
	{
		private const int MAX_STATES = 4;
		private static readonly int[] DISTANCE_SAMPLES = new int[] { 0, 0, 0, 11, 20};

		private const float DEFAULT_TRANSITION_DURATION = 0.5f;
		private const float DEFAULT_BLEND_DURATION = 0.2f;
		private const float DEFAULT_EASEIN_DURATION = 0.0f;
		private const float DEFAULT_EASEOUT_DURATION = 0.0f;
		private const float DEFAULT_PROBABILITY = 0.5f;
		private const float DEFAULT_WAIT_DURATION_MIN = 0.0f;
		private const float DEFAULT_WAIT_DURATION_MAX = 0.0f;
		private const float DEFAULT_ANCHOR_BLEND_RATIO = 0.5f;
		private const float DEFAULT_ANCHOR_DAMPING_TIME = 0.2f;

		private Dictionary<string, Animation> myAnimations = new Dictionary<string, Animation>();
		private static Animation myCurrentAnimation;
		private static Layer myCurrentLayer;
		private static State myCurrentState;

		private static bool myPlayMode = false;
		private static bool myPaused = false;
		private static bool myNeedRefresh = false;
		private bool myWasLoading = true;

		private static JSONStorableString mySendMessage;
		private static JSONStorableString myLoadAnimation;
		private static JSONStorableBool myPlayPaused;

		public override void Init()
		{
			myWasLoading = true;

			InitUI();

			// trigger values
			mySendMessage = new JSONStorableString("SendMessage", "", ReceiveMessage);
			mySendMessage.isStorable = mySendMessage.isRestorable = false;
			RegisterString(mySendMessage);

			myPlayPaused = new JSONStorableBool("PlayPause", false, PlayPauseAction);
			myPlayPaused.isStorable = myPlayPaused.isRestorable = false;
			RegisterBool(myPlayPaused);

			myLoadAnimation = new JSONStorableString("LoadAnimation", "", LoadAnimationsAction);
			myLoadAnimation.isStorable = myLoadAnimation.isRestorable = false;
			RegisterString(myLoadAnimation);

			SuperController.singleton.onAtomUIDRenameHandlers += OnAtomRename;
			SimpleTriggerHandler.LoadAssets();
		}

		private void OnDestroy()
		{
			SuperController.singleton.onAtomUIDRenameHandlers -= OnAtomRename;
			OnDestroyUI();

			foreach (var ms in myAnimations)
			{
				ms.Value.Clear();
			}
		}

		public override JSONClass GetJSON(bool includePhysical = true, bool includeAppearance = true, bool forceStore = false)
		{
			JSONClass jc = base.GetJSON(includePhysical, includeAppearance, forceStore);
			if ((includePhysical && includeAppearance) || forceStore) // StoreType.Full
			{
				jc["idlepose"] = SaveAnimations();
				needsStore = true;
			}
			return jc;
		}

		public override void LateRestoreFromJSON(JSONClass jc, bool restorePhysical = true, bool restoreAppearance = true, bool setMissingToDefault = true)
		{
			base.LateRestoreFromJSON(jc, restorePhysical, restoreAppearance, setMissingToDefault);
			if (restorePhysical && restoreAppearance) // StoreType.Full
			{
				if (jc.HasKey("idlepose"))
					LoadAnimations(jc["idlepose"].AsObject);
				myNeedRefresh = true;
			}
		}

		public void ReceiveMessage(String messageString) {
			mySendMessage.valNoCallback = "";
			foreach(var l in myCurrentAnimation.myLayers) {
				Layer layer = l.Value;
				foreach(var m in layer.myMessages) {
					Message message = m.Value;
					if(message.myMessageString == messageString) {
						State currentState = layer.myCurrentState;
						if(message.mySourceStates.Values.ToList().Contains(currentState)) {
							Transition transition = new Transition(currentState, message);
							layer.SetTransition(transition);
						}
					}
				}
			}
		}

		private void LoadAnimationsAction(string v)
		{
			myLoadAnimation.valNoCallback = string.Empty;
			JSONClass jc = LoadJSON(BASE_DIRECTORY+"/"+v).AsObject;
			if (jc != null)
				LoadAnimations(jc);
				UIRefreshMenu();
		}

		private void PlayPauseAction(bool b)
		{
			myPlayPaused.val = b;
			myPaused = (myMenuItem != MENU_PLAY || myPlayPaused.val);
		}

		private Animation CreateAnimation(string name)
		{
			Animation a = new Animation(name);
			myAnimations[name] = a;
			return a;
		}

		private Layer CreateLayer(string name)
		{
			Layer l = new Layer(name);
			myCurrentAnimation.myLayers[name] = l;
			return l;
		}

		private State CreateState(string name)
		{
			State s = new State(this, name) {
				myWaitDurationMin = myGlobalDefaultWaitDurationMin.val,
				myWaitDurationMax = myGlobalDefaultWaitDurationMax.val,
				myDefaultDuration = myGlobalDefaultTransitionDuration.val,
				myDefaultEaseInDuration = myGlobalDefaultEaseInDuration.val,
				myDefaultEaseOutDuration = myGlobalDefaultEaseOutDuration.val
			};
			CaptureState(s);
			if(myCurrentLayer.myCurrentState != null) {
				setCaptureDefaults(s, myCurrentLayer.myCurrentState);
			}
			myCurrentLayer.myStates[name] = s;
			return s;
		}

		private static void SetAnimation(Animation animation)
		{
			myCurrentAnimation = animation;

			List<string> layers = animation.myLayers.Keys.ToList();
			layers.Sort();
			if(layers.Count > 0) {
				Layer layer;
				foreach (var layerKey in layers) {
					myCurrentAnimation.myLayers.TryGetValue(layerKey, out layer);
					SetLayer(layer);
				}
			}
		}

		private static void SetLayer(Layer layer)
		{
			myCurrentLayer = layer;
			List<string> states = layer.myStates.Keys.ToList();
			states.Sort();
			if(layer.myStates.Count > 0) {
				State state;
				layer.myStates.TryGetValue(states[0], out state);
				layer.SetBlendTransition(state);
			}
		}

		private void CaptureState(State state)
		{
			for (int i=0; i<myCurrentLayer.myControlCaptures.Count; ++i){
				myCurrentLayer.myControlCaptures[i].CaptureEntry(state);
			}
			for (int i=0; i<myCurrentLayer.myMorphCaptures.Count; ++i) {
				myCurrentLayer.myMorphCaptures[i].CaptureEntry(state);
			}
		}

		private void setCaptureDefaults(State state, State oldState)
		{
			for (int i=0; i<myCurrentLayer.myControlCaptures.Count; ++i)
				myCurrentLayer.myControlCaptures[i].setDefaults(state, oldState);
		}

		private void Update()
		{
			bool isLoading = SuperController.singleton.isLoading;
			bool isOrWasLoading = isLoading || myWasLoading;
			myWasLoading = isLoading;
			if (isOrWasLoading){
				return;
			}

			if (myNeedRefresh)
			{
				UIRefreshMenu();
				myNeedRefresh = false;
			}
			DebugUpdateUI();

			foreach (var layer in myCurrentAnimation.myLayers)
				layer.Value.UpdateLayer();
		}

		private void OnAtomRename(string oldid, string newid)
		{
			foreach (var s in myCurrentLayer.myStates)
			{
				State state = s.Value;
				state.EnterBeginTrigger.SyncAtomNames();
				state.EnterEndTrigger.SyncAtomNames();
				state.ExitBeginTrigger.SyncAtomNames();
				state.ExitEndTrigger.SyncAtomNames();

				foreach (var ce in state.myControlEntries)
				{
					ControlEntryAnchored entry = ce.Value;
					if (entry.myAnchorAAtom == oldid)
						entry.myAnchorAAtom = newid;
					if (entry.myAnchorBAtom == oldid)
						entry.myAnchorBAtom = newid;
				}
			}

			myNeedRefresh = true;
		}

		// =======================================================================================

		private class Animation
		{
			public string myName;
			public Dictionary<string, Layer> myLayers = new Dictionary<string, Layer>();
			public Dictionary<string, Role> myRoles = new Dictionary<string, Role>();
			public float mySpeed = 1.0f;

			public Animation(string name)
			{
				myName = name;
			}

			public void Clear()
			{
				foreach (var l in myLayers)
				{
					Layer layer = l.Value;
					layer.Clear();
				}
			}

		}

		// =======================================================================================
		private class Layer
		{
			public string myName;
			public Animation myAnimation;
			public Dictionary<string, State> myStates = new Dictionary<string, State>();
			public Dictionary<string, Message> myMessages = new Dictionary<string, Message>();
			public bool myNoValidTransition = false;
			public State myCurrentState;
			public List<ControlCapture> myControlCaptures = new List<ControlCapture>();
			public List<MorphCapture> myMorphCaptures = new List<MorphCapture>();
			private Transition myTransition;
			private float myTransitionNoise = 0.0f;
			public float myClock = 0.0f;
			public float myDuration = 1.0f;
			private List<TriggerActionDiscrete> myTriggerActionsNeedingUpdate = new List<TriggerActionDiscrete>();
			private State myBlendState = State.CreateBlendState();

			public Layer(string name)
			{
				myName = name;
				myAnimation = myCurrentAnimation;
			}
			public void CaptureState(State state)
			{
				for (int i=0; i<myControlCaptures.Count; ++i)
					myControlCaptures[i].CaptureEntry(state);
				for (int i=0; i<myMorphCaptures.Count; ++i)
					myMorphCaptures[i].CaptureEntry(state);
			}

			public void SetState(State state)
			{
				// SuperController.LogError("Set State");
				// SuperController.LogError(state.myName);
				myNoValidTransition = false;
				myCurrentState = state;

				myClock = 0.0f;
				myDuration = UnityEngine.Random.Range(state.myWaitDurationMin, state.myWaitDurationMax);
			}

			public void Clear()
			{
				foreach (var s in myStates)
				{
					State state = s.Value;
					state.EnterBeginTrigger.Remove();
					state.EnterEndTrigger.Remove();
					state.ExitBeginTrigger.Remove();
					state.ExitEndTrigger.Remove();
				}
			}

			public float Smooth(float a, float b, float d, float t)
			{
				d = Mathf.Max(d, 0.01f);
				t = Mathf.Clamp(t, 0.0f, d);
				if (a+b>d)
				{
					float scale = d/(a+b);
					a *= scale;
					b *= scale;
				}
				float n = d - 0.5f*(a+b);
				float s = d - t;

				// This is based on using the SmoothStep function (3x^2 - 2x^3) for velocity: https://en.wikipedia.org/wiki/Smoothstep
				// The result is a 3-piece curve consiting of a linear part in the middle and the integral of SmoothStep at both
				// ends. Additionally there is some scaling to connect the parts properly.
				// The resulting combined curve has smooth velocity and continuous acceleration/deceleration.
				float ta = t / a;
				float sb = s / b;
				if (t < a)
					return (a - 0.5f*t) * (ta*ta*ta/n);
				else if (s >= b)
					return (t - 0.5f*a) / n;
				else
					return (0.5f*s - b) * (sb*sb*sb/n) + 1.0f;
			}

			public void UpdateLayer()
			{
				for (int i=0; i<myTriggerActionsNeedingUpdate.Count; ++i){
					myTriggerActionsNeedingUpdate[i].Update();
				}
				myTriggerActionsNeedingUpdate.RemoveAll(a => !a.timerActive);

				bool paused = myPaused && myTransition == null;
				// if there is a transition selected or animation is unpaused
				if (!paused)
					myClock = Mathf.Min(myClock + Time.deltaTime*myCurrentAnimation.mySpeed, 100000.0f);

				float t;
				// if not paused
				if(!paused) {
					// if a transition is possible but not yet chosen and the state duration is up
					if(myClock >= myDuration && myTransition == null && !myNoValidTransition) {
						SetRandomTransition();
					// if transition is selected
					} else if (myTransition != null) {
						t = Smooth(myTransition.myEaseOutDuration, myTransition.myEaseInDuration, myTransition.myDuration, myClock-myDuration);

						for (int i=0; i<myControlCaptures.Count; ++i)
							myControlCaptures[i].UpdateTransition(t);
						for (int i=0; i<myMorphCaptures.Count; ++i)
							myMorphCaptures[i].UpdateTransition(t);

						if (myClock >= myDuration + myTransition.myDuration + myTransitionNoise)
						{
							if (myTransition.myTargetState != null)
							{
								State previousState = myCurrentState;
								SetState(myTransition.myTargetState);
								if (myMainLayer.val == myName)
									myMainState.valNoCallback = myCurrentState.myName;
									myMainAnimation.valNoCallback = myCurrentState.myAnimation.myName;

								if (previousState.ExitEndTrigger != null)
									previousState.ExitEndTrigger.Trigger(myTriggerActionsNeedingUpdate);
								if (myCurrentState.EnterEndTrigger != null)
									myCurrentState.EnterEndTrigger.Trigger(myTriggerActionsNeedingUpdate);
								foreach(var m in myTransition.myMessages) {
									Role role = m.Key;
									String message = m.Value;
									Atom person = role.myPerson;
									if (person == null) continue;
									var storableId = person.GetStorableIDs().FirstOrDefault(id => id.EndsWith("HaremLife.AnimationPoser"));
									if (storableId == null) continue;
									MVRScript storable = person.GetStorableByID(storableId) as MVRScript;
									if (storable == null) continue;
									// if (ReferenceEquals(storable, _plugin)) continue;
									if (!storable.enabled) continue;
									storable.SendMessage(nameof(AnimationPoser.ReceiveMessage), message);
								}
							}
							myTransition = null;
						}
					// if clock is less than duration
					} else if (myClock < myDuration) {
						for (int i=0; i<myControlCaptures.Count; ++i)
							myControlCaptures[i].UpdateState(myCurrentState);
					// if not paused but no transition possible (updates position relative to anchor)
					} else if (myNoValidTransition) {
						for (int i=0; i<myControlCaptures.Count; ++i)
							myControlCaptures[i].UpdateState(myCurrentState);
					}
				// if paused and no transition (updates position relative to anchor)
				} else if (myNoValidTransition) {
					for (int i=0; i<myControlCaptures.Count; ++i)
						myControlCaptures[i].UpdateState(myCurrentState);
				}
			}

			public void ArriveFromAnotherAnimation(Transition transition, State targetState) {
				targetState.myLayer.SetBlendTransition(targetState);

				myMainAnimation.valNoCallback = myCurrentAnimation.myName;
			}

			private void TransitionToAnotherAnimation(Transition transition)
			{
				State targetState = transition.myTargetState;
				Animation animation = targetState.myAnimation;
				Layer targetLayer = targetState.myLayer;
				myCurrentAnimation = animation;
				SetAnimation(animation);
				targetLayer.ArriveFromAnotherAnimation(transition, targetState);
				foreach(var sc in transition.mySyncTargets) {
					Layer syncLayer = sc.Key;
					State syncState = sc.Value;
					syncLayer.ArriveFromAnotherAnimation(transition, syncState);
				}

				// myMainAnimation.valNoCallback = myCurrentAnimation.myName;
				// myMainLayer.valNoCallback = myCurrentLayer.myName;
				// myMainState.valNoCallback = myCurrentState.myName;
			}

			public void SetTransition(Transition transition)
			{
				// SuperController.LogError("Set transition");
				// SuperController.LogError(myTransition.myDuration.ToString());
				// SuperController.LogError(myTransition.mySourceState.myName);
				// SuperController.LogError(myTransition.myTargetState.myAnimation.myName);
				// SuperController.LogError(myTransition.myTargetState.myName);

				myNoValidTransition = false;

				myClock = 0.0f;

				if(transition.myTargetState.myAnimation != myCurrentAnimation) {
					TransitionToAnotherAnimation(transition);
					return;
				}

				for (int i=0; i<myControlCaptures.Count; ++i)
					myControlCaptures[i].SetTransition(transition);
				for (int i=0; i<myMorphCaptures.Count; ++i)
					myMorphCaptures[i].SetTransition(transition);

				myTransition = transition;

				myTransitionNoise = UnityEngine.Random.Range(-transition.myDurationNoise, transition.myDurationNoise);

				if (transition.mySourceState.ExitBeginTrigger != null)
					transition.mySourceState.ExitBeginTrigger.Trigger(myTriggerActionsNeedingUpdate);
				if (transition.myTargetState.EnterBeginTrigger != null)
					transition.myTargetState.EnterBeginTrigger.Trigger(myTriggerActionsNeedingUpdate);
			}

			public void SetRandomTransition()
			{
				List<State> states = myCurrentState.getReachableStates();

				int i;
				float sum = 0.0f;
				// for (i=0; i<states.Count; ++i)
				// 	sum += states[i].myDefaultProbability;
				for (i=0; i<myCurrentState.myTransitions.Count; ++i)
					sum += myCurrentState.myTransitions[i].myProbability;
				if (sum == 0.0f)
				{
					myTransition = null;
					myNoValidTransition = true;
				}
				else
				{
					float threshold = UnityEngine.Random.Range(0.0f, sum);
					sum = 0.0f;
					for (i=0; i<myCurrentState.myTransitions.Count-1; ++i)
					{
						sum += myCurrentState.myTransitions[i].myProbability;
						if (threshold <= sum)
							break;
					}
					SetTransition(myCurrentState.getIncomingTransition(states[i]));
				}
			}

			public void SetBlendTransition(State state, bool debug = false)
			{
				// SuperController.LogError("Set blend transition");
				// SuperController.LogError(myCurrentState.myName);
				// SuperController.LogError(state.myName);
				// if (myCurrentState != null)
				// {
				// 	List<State> states = new List<State>(16);
				// 	for (int i=0; i< myCurrentState.myTransitions.Count; i++) {
				// 		states.Add(myCurrentState.myTransitions[i]);
				// 	}
				// 	List<int> indices = new List<int>(4);
				// 	for (int i=0; i<states.Count; ++i)
				// 	{
				// 		if (states[i] == state)
				// 			indices.Add(i);
				// 	}
				// 	if (indices.Count == 0)
				// 	{
				// 		states.Clear();
				// 		for (int i=0; i< myCurrentState.myTransitions.Count; i++) {
				// 			states.Add(myCurrentState.myTransitions[i]);
				// 		}

				// 		for (int i=0; i<states.Count; ++i)
				// 		{
				// 			if (states[i] == state)
				// 				indices.Add(i);
				// 		}
				// 	}
				// 	if (indices.Count > 0)
				// 	{
				// 		int selected = UnityEngine.Random.Range(0, indices.Count);
				// 		myTransition.myTargetState = states[indices[selected]];
				// 	}
				// }

				if (myCurrentState == null)
				{
					CaptureState(myBlendState);
					myBlendState.AssignOutTriggers(myCurrentState);
					SetTransition(new Transition(myBlendState, state, 0.1f*myCurrentAnimation.mySpeed));
				} else {
					SetTransition(new Transition(myCurrentState, state, 0.1f*myCurrentAnimation.mySpeed));
				}
				myClock = myDuration;
			}
		}
		private class Role
		{
			public String myName;
			public Atom myPerson;

			public Role(string name){
				myName = name;
			}
		}

		private class BaseTransition
		{
			public Dictionary<Layer, State> mySyncTargets = new Dictionary<Layer, State>();
			public Dictionary<Role, String> myMessages = new Dictionary<Role, String>();
			public State myTargetState;
			public float myProbability;
			public float myEaseInDuration;
			public float myEaseOutDuration;
			public float myDuration;
			public float myDurationNoise = 0.0f;
		}

		private class Transition : BaseTransition
		{
			public State mySourceState;

			public Transition(State sourceState, State targetState)
			{
				mySourceState = sourceState;
				myTargetState = targetState;
				myProbability = targetState.myDefaultProbability;
				myEaseInDuration = targetState.myDefaultEaseInDuration;
				myEaseOutDuration = targetState.myDefaultEaseOutDuration;
				myDuration = targetState.myDefaultDuration;
			}

			public Transition(State sourceState, State targetState, float duration)
			{
				mySourceState = sourceState;
				myTargetState = targetState;
				myProbability = targetState.myDefaultProbability;
				myEaseInDuration = 0.0f;
				myEaseOutDuration = 0.0f;
				myDuration = duration;
			}

			private void BuildFromBaseTransition(BaseTransition t) {
				myTargetState = t.myTargetState;
				myProbability = t.myProbability;
				myEaseInDuration = t.myEaseInDuration;
				myEaseOutDuration = t.myEaseOutDuration;
				myDuration = t.myDuration;
				myDurationNoise = t.myDurationNoise;
				mySyncTargets = t.mySyncTargets;
			}

			public Transition(Transition transition)
			{
				mySourceState = transition.mySourceState;
				myMessages = transition.myMessages;
				BuildFromBaseTransition(transition);
			}

			public Transition(State sourceState, Message message)
			{
				mySourceState = sourceState;
				BuildFromBaseTransition(message);
			}
		}

		private class Message : BaseTransition
		{
			public String myMessageString;
			public String myName;
			public Dictionary<string, State> mySourceStates = new Dictionary<string, State>();

			public Message(string name) {
				myName = name;
			}
		}

		private class State
		{
			public string myName;
			public Animation myAnimation;
			public Layer myLayer;
			public float myWaitDurationMin;
			public float myWaitDurationMax;
			public float myDefaultDuration;
			public float myDefaultEaseInDuration;
			public float myDefaultEaseOutDuration;
			public float myDefaultProbability = DEFAULT_PROBABILITY;
			public bool myIsRootState = false;
			public uint myDebugIndex = 0;
			public Dictionary<ControlCapture, ControlEntryAnchored> myControlEntries = new Dictionary<ControlCapture, ControlEntryAnchored>();
			public Dictionary<MorphCapture, float> myMorphEntries = new Dictionary<MorphCapture, float>();
			public List<Transition> myTransitions = new List<Transition>();
			public EventTrigger EnterBeginTrigger;
			public EventTrigger EnterEndTrigger;
			public EventTrigger ExitBeginTrigger;
			public EventTrigger ExitEndTrigger;

			private State(string name)
			{
				myName = name;
				myAnimation = myCurrentAnimation;
				myLayer = myCurrentLayer;
				// do NOT init event triggers
			}

			public State(MVRScript script, string name)
			{
				myName = name;
				myAnimation = myCurrentAnimation;
				myLayer = myCurrentLayer;
				EnterBeginTrigger = new EventTrigger(script, "OnEnterBegin", name);
				EnterEndTrigger = new EventTrigger(script, "OnEnterEnd", name);
				ExitBeginTrigger = new EventTrigger(script, "OnExitBegin", name);
				ExitEndTrigger = new EventTrigger(script, "OnExitEnd", name);
			}

			public State(string name, State source)
			{
				myName = name;
				myAnimation = myCurrentAnimation;
				myLayer = myCurrentLayer;
				myWaitDurationMin = source.myWaitDurationMin;
				myWaitDurationMax = source.myWaitDurationMax;
				myDefaultDuration = source.myDefaultDuration;
				myDefaultEaseInDuration = source.myDefaultEaseInDuration;
				myDefaultEaseOutDuration = source.myDefaultEaseOutDuration;
				myDefaultProbability = source.myDefaultProbability;
				myIsRootState = source.myIsRootState;
				EnterBeginTrigger = new EventTrigger(source.EnterBeginTrigger);
				EnterEndTrigger = new EventTrigger(source.EnterEndTrigger);
				ExitBeginTrigger = new EventTrigger(source.ExitBeginTrigger);
				ExitEndTrigger = new EventTrigger(source.ExitEndTrigger);
			}

			public List<State> getReachableStates() {
				List<State> states = new List<State>();
				for(int i=0; i<myTransitions.Count; i++)
					states.Add(myTransitions[i].myTargetState);
				return states;
			}

			public bool isReachable(State state) {
				List<State> states = this.getReachableStates();
				return states.Contains(state);
			}

			public Transition getIncomingTransition(State state) {
				for(int i=0; i<myTransitions.Count; i++)
					if(myTransitions[i].myTargetState == state)
						return myTransitions[i];
				return null;
			}
			public void removeTransition(State state) {
				for(int i=0; i<myTransitions.Count; i++)
					if(myTransitions[i].myTargetState == state)
						myTransitions.RemoveAt(i);
			}

			public static State CreateBlendState()
			{
				return new State("BlendState") {
					myWaitDurationMin = 0.0f,
					myWaitDurationMax = 0.0f,
					myDefaultDuration = myGlobalDefaultTransitionDuration.val,
				};
			}

			public void AssignOutTriggers(State other)
			{
				ExitBeginTrigger = other?.ExitBeginTrigger;
				ExitEndTrigger = other?.ExitEndTrigger;
			}
		}

		private class ControlCapture
		{
			public string myName;
			private AnimationPoser myPlugin;
			private Transform myTransform;
			private ControlEntryAnchored[] myTransition = new ControlEntryAnchored[MAX_STATES];
			private int myEntryCount = 0;
			public bool myApplyPosition = true;
			public bool myApplyRotation = true;

			private static Quaternion[] ourTempQuaternions = new Quaternion[MAX_STATES-1];
			private static float[] ourTempDistances = new float[DISTANCE_SAMPLES[DISTANCE_SAMPLES.Length-1] + 2];

			public ControlCapture(AnimationPoser plugin, string control)
			{
				myPlugin = plugin;
				myName = control;
				FreeControllerV3 controller = plugin.containingAtom.GetStorableByID(control) as FreeControllerV3;
				if (controller != null)
					myTransform = controller.transform;
			}

			public void CaptureEntry(State state)
			{
				ControlEntryAnchored entry;
				if (!state.myControlEntries.TryGetValue(this, out entry))
				{
					entry = new ControlEntryAnchored(myPlugin, myName, state, this);
					entry.Initialize();
					state.myControlEntries[this] = entry;
				}
				entry.Capture(myTransform.position, myTransform.rotation);
			}

			public void setDefaults(State state, State oldState)
			{
				ControlEntryAnchored entry;
				ControlEntryAnchored oldEntry;
				if (!state.myControlEntries.TryGetValue(this, out entry))
					return;
				if (!oldState.myControlEntries.TryGetValue(this, out oldEntry))
					return;
				entry.myAnchorAAtom = oldEntry.myAnchorAAtom;
				entry.myAnchorAControl = oldEntry.myAnchorAControl;
				entry.myAnchorMode = oldEntry.myAnchorMode;
			}

			public void SetTransition(Transition transition)
			{
				myEntryCount = 2;
				if (!transition.mySourceState.myControlEntries.TryGetValue(this, out myTransition[0]))
				{
					CaptureEntry(transition.mySourceState);
					myTransition[0] = transition.mySourceState.myControlEntries[this];
				}

				if (!transition.myTargetState.myControlEntries.TryGetValue(this, out myTransition[1]))
				{
					CaptureEntry(transition.myTargetState);
					myTransition[1] = transition.myTargetState.myControlEntries[this];
				}
			}

			public void UpdateTransition(float t)
			{
				for (int i=0; i<myEntryCount; ++i)
					myTransition[i].Update();

				//t = ArcLengthParametrization(t);

				if (myApplyPosition)
				{
					switch (myEntryCount)
					{
						case 4:	myTransform.position = EvalBezierCubicPosition(t);          break;
						case 3: myTransform.position = EvalBezierQuadraticPosition(t);      break;
						case 2: myTransform.position = EvalBezierLinearPosition(t);         break;
						default: myTransform.position = myTransition[0].myEntry.myPosition; break;
					}
				}
				if (myApplyRotation)
				{
					switch (myEntryCount)
					{
						case 4: myTransform.rotation = EvalBezierCubicRotation(t);          break;
						case 3: myTransform.rotation = EvalBezierQuadraticRotation(t);      break;
						case 2: myTransform.rotation = EvalBezierLinearRotation(t);         break;
						default: myTransform.rotation = myTransition[0].myEntry.myRotation; break;
					}
				}
			}

			private float ArcLengthParametrization(float t)
			{
				if (myEntryCount <= 2 || myEntryCount > 4){
					return t;
				}

				int numSamples = DISTANCE_SAMPLES[myEntryCount];
				float numLines = (float)(numSamples+1);
				float distance = 0.0f;
				Vector3 previous = myTransition[0].myEntry.myPosition;
				ourTempDistances[0] = 0.0f;

				if (myEntryCount == 3)
				{
					for (int i=1; i<=numSamples; ++i)
					{
						Vector3 current = EvalBezierQuadraticPosition(i / numLines);
						distance += Vector3.Distance(previous, current);
						ourTempDistances[i] = distance;
						previous = current;
					}
				}
				else
				{
					for (int i=1; i<=numSamples; ++i)
					{
						Vector3 current = EvalBezierCubicPosition(i / numLines);
						distance += Vector3.Distance(previous, current);
						ourTempDistances[i] = distance;
						previous = current;
					}
				}

				distance += Vector3.Distance(previous, myTransition[myEntryCount-1].myEntry.myPosition);
				ourTempDistances[numSamples+1] = distance;

				t *= distance;

				int idx = Array.BinarySearch(ourTempDistances, 0, numSamples+2, t);
				if (idx < 0)
				{
					idx = ~idx;
					if (idx == 0){
						return 0.0f;
					}
					else if (idx >= numSamples+2){
						return 1.0f;
					}
					t = Mathf.InverseLerp(ourTempDistances[idx-1], ourTempDistances[idx], t);
					return Mathf.LerpUnclamped((idx-1) / numLines, idx / numLines, t);
				}
				else
				{
					return idx / numLines;
				}
			}

			private Vector3 EvalBezierLinearPosition(float t)
			{
				return Vector3.LerpUnclamped(myTransition[0].myEntry.myPosition, myTransition[1].myEntry.myPosition, t);
			}

			private Vector3 EvalBezierQuadraticPosition(float t)
			{
				// evaluating quadratic Bézier curve using Bernstein polynomials
				float s = 1.0f - t;
				return      (s*s) * myTransition[0].myEntry.myPosition
					 + (2.0f*s*t) * myTransition[1].myEntry.myPosition
					 +      (t*t) * myTransition[2].myEntry.myPosition;
			}

			private Vector3 EvalBezierCubicPosition(float t)
			{
				// evaluating cubic Bézier curve using Bernstein polynomials
				float s = 1.0f - t;
				float t2 = t*t;
				float s2 = s*s;
				return      (s*s2) * myTransition[0].myEntry.myPosition
					 + (3.0f*s2*t) * myTransition[1].myEntry.myPosition
					 + (3.0f*s*t2) * myTransition[2].myEntry.myPosition
					 +      (t*t2) * myTransition[3].myEntry.myPosition;
			}

			private Quaternion EvalBezierLinearRotation(float t)
			{
				return Quaternion.SlerpUnclamped(myTransition[0].myEntry.myRotation, myTransition[1].myEntry.myRotation, t);
			}

			private Quaternion EvalBezierQuadraticRotation(float t)
			{
				// evaluating quadratic Bézier curve using de Casteljau's algorithm
				ourTempQuaternions[0] = Quaternion.SlerpUnclamped(myTransition[0].myEntry.myRotation, myTransition[1].myEntry.myRotation, t);
				ourTempQuaternions[1] = Quaternion.SlerpUnclamped(myTransition[1].myEntry.myRotation, myTransition[2].myEntry.myRotation, t);
				return Quaternion.SlerpUnclamped(ourTempQuaternions[0], ourTempQuaternions[1], t);
			}

			private Quaternion EvalBezierCubicRotation(float t)
			{
				// evaluating cubic Bézier curve using de Casteljau's algorithm
				for (int i=0; i<3; ++i)
					ourTempQuaternions[i] = Quaternion.SlerpUnclamped(myTransition[i].myEntry.myRotation, myTransition[i+1].myEntry.myRotation, t);
				for (int i=0; i<2; ++i)
					ourTempQuaternions[i] = Quaternion.SlerpUnclamped(ourTempQuaternions[i], ourTempQuaternions[i+1], t);
				return Quaternion.SlerpUnclamped(ourTempQuaternions[0], ourTempQuaternions[1], t);
			}

			public void UpdateState(State state)
			{
				ControlEntryAnchored entry;
				if (state.myControlEntries.TryGetValue(this, out entry))
				{
					entry.Update();
					if (myApplyPosition)
						myTransform.position = entry.myEntry.myPosition;
					if (myApplyRotation)
						myTransform.rotation = entry.myEntry.myRotation;
				}
			}

			public bool IsValid()
			{
				return myTransform != null;
			}
		}

		private struct ControlEntry
		{
			public Quaternion myRotation;
			public Vector3 myPosition;
		}

		private class ControlEntryAnchored
		{
			public const int ANCHORMODE_WORLD = 0;
			public const int ANCHORMODE_SINGLE = 1;
			public const int ANCHORMODE_BLEND = 2;

			public ControlEntry myEntry;
			public ControlEntry myAnchorOffset;
			public Transform myAnchorATransform;
			public Transform myAnchorBTransform;
			public int myAnchorMode = ANCHORMODE_SINGLE;
			public float myBlendRatio = DEFAULT_ANCHOR_BLEND_RATIO;
			public float myDampingTime = DEFAULT_ANCHOR_DAMPING_TIME;

			public string myAnchorAAtom;
			public string myAnchorBAtom;
			public string myAnchorAControl = "control";
			public string myAnchorBControl = "control";
			public ControlCapture myControlCapture;
			public State myState;

			public ControlEntryAnchored(AnimationPoser plugin, string control, State state, ControlCapture controlCapture)
			{
				myState = state;
				Atom containingAtom = plugin.GetContainingAtom();
				if (containingAtom.type != "Person" || control == "control")
					myAnchorMode = ANCHORMODE_WORLD;
				myAnchorAAtom = myAnchorBAtom = containingAtom.uid;
				myControlCapture = controlCapture;
				myAnchorAAtom = myAnchorBAtom = containingAtom.uid;
			}

			public ControlEntryAnchored Clone()
			{
				return (ControlEntryAnchored)MemberwiseClone();
			}

			public void Initialize()
			{
				GetTransforms();
				UpdateInstant();
			}

			public void AdjustAnchor()
			{
				GetTransforms();
				Capture(myEntry.myPosition, myEntry.myRotation);
			}

			private void GetTransforms()
			{
				if (myAnchorMode == ANCHORMODE_WORLD)
				{
					myAnchorATransform = null;
					myAnchorBTransform = null;
				}
				else
				{
					myAnchorATransform = GetTransform(myAnchorAAtom, myAnchorAControl);
					if (myAnchorMode == ANCHORMODE_BLEND)
						myAnchorBTransform = GetTransform(myAnchorBAtom, myAnchorBControl);
					else
						myAnchorBTransform = null;
				}
			}

			private Transform GetTransform(string atomName, string controlName)
			{
				Atom atom = SuperController.singleton.GetAtomByUid(atomName);
				return atom?.GetStorableByID(controlName)?.transform;
			}

			public void UpdateInstant()
			{
				float dampingTime = myDampingTime;
				myDampingTime = 0.0f;
				Update();
				myDampingTime = dampingTime;
			}

			public void Update()
			{
				if (myAnchorMode == ANCHORMODE_WORLD)
				{
					myEntry = myAnchorOffset;
				}
				else
				{
					ControlEntry anchor;
					if (myAnchorMode == ANCHORMODE_SINGLE)
					{
						if (myAnchorATransform == null)
							return;
						anchor.myPosition = myAnchorATransform.position;
						anchor.myRotation = myAnchorATransform.rotation;
					} else {
						if (myAnchorATransform == null || myAnchorBTransform == null)
							return;
						anchor.myPosition = Vector3.LerpUnclamped(myAnchorATransform.position, myAnchorBTransform.position, myBlendRatio);
						anchor.myRotation = Quaternion.SlerpUnclamped(myAnchorATransform.rotation, myAnchorBTransform.rotation, myBlendRatio);
					}
					anchor.myPosition = anchor.myPosition + anchor.myRotation * myAnchorOffset.myPosition;
					anchor.myRotation = anchor.myRotation * myAnchorOffset.myRotation;

					if (myDampingTime >= 0.001f)
					{
						float t = Mathf.Clamp01(Time.deltaTime / myDampingTime);
						myEntry.myPosition = Vector3.LerpUnclamped(myEntry.myPosition, anchor.myPosition, t);
						myEntry.myRotation = Quaternion.SlerpUnclamped(myEntry.myRotation, anchor.myRotation, t);
					}
					else
					{
						myEntry = anchor;
					}
				}
			}

			public void Capture(Vector3 position, Quaternion rotation)
			{
				// myEntry.myPosition = position;
				// myEntry.myRotation = rotation;

				State rootState = myCurrentLayer.myStates.Values.ToList().Find(s => s.myIsRootState);
				if(rootState != null && myState == rootState) {
					ControlCapture rootcc = rootState.myControlEntries.Keys.ToList().Find(ccx => ccx.myName == myControlCapture.myName);
					ControlEntryAnchored rootce = rootState.myControlEntries[rootcc];
					foreach(var s in myCurrentLayer.myStates) {
						State st = s.Value;
						if(st != rootState) {
							ControlCapture cc = st.myControlEntries.Keys.ToList().Find(ccx => ccx.myName == myControlCapture.myName);
							ControlEntryAnchored ce = st.myControlEntries[cc];
							ce.myAnchorOffset.myPosition = ce.myAnchorOffset.myPosition + (position - rootce.myAnchorOffset.myPosition);
							ce.myAnchorOffset.myRotation = Quaternion.Inverse(Quaternion.Inverse(rotation) * rootce.myAnchorOffset.myRotation) * ce.myAnchorOffset.myRotation;
						}
					}
				}
				if (myAnchorMode == ANCHORMODE_WORLD)
				{
					myAnchorOffset.myPosition = position;
					myAnchorOffset.myRotation = rotation;
				}
				else
				{
					ControlEntry root;
					if (myAnchorMode == ANCHORMODE_SINGLE)
					{
						if (myAnchorATransform == null){
							return;
						}
						root.myPosition = myAnchorATransform.position;
						root.myRotation = myAnchorATransform.rotation;
					} else {
						if (myAnchorATransform == null || myAnchorBTransform == null){
							return;
						}
						root.myPosition = Vector3.LerpUnclamped(myAnchorATransform.position, myAnchorBTransform.position, myBlendRatio);
						root.myRotation = Quaternion.SlerpUnclamped(myAnchorATransform.rotation, myAnchorBTransform.rotation, myBlendRatio);
					}

					myAnchorOffset.myPosition = Quaternion.Inverse(root.myRotation) * (position - root.myPosition);
					myAnchorOffset.myRotation = Quaternion.Inverse(root.myRotation) * rotation;
				}
			}
		}


		private class MorphCapture
		{
			public string mySID;
			public DAZMorph myMorph;
			public DAZCharacterSelector.Gender myGender;
			private float[] myTransition = new float[MAX_STATES];
			private int myEntryCount = 0;
			public bool myApply = true;

			// used when adding a capture
			public MorphCapture(AnimationPoser plugin, DAZCharacterSelector.Gender gender, DAZMorph morph)
			{
				myMorph = morph;
				myGender = gender;
				mySID = plugin.GenerateMorphsSID(gender == DAZCharacterSelector.Gender.Female);
			}

			// legacy handling of old qualifiedName where the order was reversed
			public MorphCapture(AnimationPoser plugin, DAZCharacterSelector geometry, string oldQualifiedName)
			{
				bool isFemale = oldQualifiedName.StartsWith("Female#");
				if (!isFemale && !oldQualifiedName.StartsWith("Male#")){
					return;
				}
				GenerateDAZMorphsControlUI morphsControl = isFemale ? geometry.morphsControlFemaleUI : geometry.morphsControlMaleUI;
				string morphUID = oldQualifiedName.Substring(isFemale ? 7 : 5);
				myMorph = morphsControl.GetMorphByUid(morphUID);
				myGender = isFemale ? DAZCharacterSelector.Gender.Female : DAZCharacterSelector.Gender.Male;

				mySID = plugin.GenerateMorphsSID(isFemale);
			}

			// legacy handling before there were ShortIDs
			public MorphCapture(AnimationPoser plugin, DAZCharacterSelector geometry, string morphUID, bool isFemale)
			{
				GenerateDAZMorphsControlUI morphsControl = isFemale ? geometry.morphsControlFemaleUI : geometry.morphsControlMaleUI;
				myMorph = morphsControl.GetMorphByUid(morphUID);
				myGender = isFemale ? DAZCharacterSelector.Gender.Female : DAZCharacterSelector.Gender.Male;

				mySID = plugin.GenerateMorphsSID(isFemale);
			}

			// used when loading from JSON
			public MorphCapture(DAZCharacterSelector geometry, string morphUID, string morphSID)
			{
				bool isFemale = morphSID.Length > 0 && morphSID[0] == 'F';
				GenerateDAZMorphsControlUI morphsControl = isFemale ? geometry.morphsControlFemaleUI : geometry.morphsControlMaleUI;
				myMorph = morphsControl.GetMorphByUid(morphUID);
				myGender = isFemale ? DAZCharacterSelector.Gender.Female : DAZCharacterSelector.Gender.Male;
				mySID = morphSID;
			}

			public void CaptureEntry(State state)
			{
				state.myMorphEntries[this] = myMorph.morphValue;
			}

			public void SetTransition(Transition transition)
			{
				myEntryCount = 2;
				bool identical = true;
				float morphValue = myMorph.morphValue;

				if (!transition.mySourceState.myMorphEntries.TryGetValue(this, out myTransition[0]))
				{
					CaptureEntry(transition.mySourceState);
					myTransition[0] = morphValue;
				}
				else
				{
					identical &= (myTransition[0] == morphValue);
				}

				if (!transition.myTargetState.myMorphEntries.TryGetValue(this, out myTransition[1]))
				{
					CaptureEntry(transition.myTargetState);
					myTransition[1] = morphValue;
				}
				else
				{
					identical &= (myTransition[1] == morphValue);
				}

				if (identical)
					myEntryCount = 0; // nothing to do, save some performance
			}

			public void UpdateTransition(float t)
			{
				if (!myApply){
					return;
				}

				switch (myEntryCount)
				{
					case 4:
						myMorph.morphValue = EvalBezierCubic(t);
						break;
					case 3:
						myMorph.morphValue = EvalBezierQuadratic(t);
						break;
					case 2:
						myMorph.morphValue = EvalBezierLinear(t);
						break;
					default:
						myMorph.morphValue = myTransition[0];
						break;
				}
			}

			private float EvalBezierLinear(float t)
			{
				return Mathf.LerpUnclamped(myTransition[0], myTransition[1], t);
			}

			private float EvalBezierQuadratic(float t)
			{
				// evaluating using Bernstein polynomials
				float s = 1.0f - t;
				return      (s*s) * myTransition[0]
					 + (2.0f*s*t) * myTransition[1]
					 +      (t*t) * myTransition[2];
			}

			private float EvalBezierCubic(float t)
			{
				// evaluating using Bernstein polynomials
				float s = 1.0f - t;
				float t2 = t*t;
				float s2 = s*s;
				return      (s*s2) * myTransition[0]
					 + (3.0f*s2*t) * myTransition[1]
					 + (3.0f*s*t2) * myTransition[2]
					 +      (t*t2) * myTransition[3];
			}

			public bool IsValid()
			{
				return myMorph != null && mySID != null;
			}
		}

		private string GenerateMorphsSID(bool isFemale)
		{
			string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
			char[] randomChars = new char[6];
			randomChars[0] = isFemale ? 'F' : 'M';
			randomChars[1] = '-';
			for (int a=0; a<10; ++a)
			{
				// find unused shortID
				for (int i=2; i<randomChars.Length; ++i)
					randomChars[i] = chars[UnityEngine.Random.Range(0, chars.Length)];
				string sid = new string(randomChars);
				if (myCurrentLayer.myMorphCaptures.Find(x => x.mySID == sid) == null){
					return sid;
				}
			}

			return null; // you are very lucky, you should play lottery!
		}
	}
}
