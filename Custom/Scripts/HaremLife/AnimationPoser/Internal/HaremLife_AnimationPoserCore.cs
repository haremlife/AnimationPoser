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
		private const int MAX_STATES = 2;
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

		private static Dictionary<string, Animation> myAnimations = new Dictionary<string, Animation>();
		private static Dictionary<string, Message> myMessages = new Dictionary<string, Message>();
		private static Dictionary<string, Avoid> myAvoids = new Dictionary<string, Avoid>();
		private static Dictionary<string, Role> myRoles = new Dictionary<string, Role>();
		private static Animation myCurrentAnimation;
		private static Layer myCurrentLayer;
		private static State myCurrentState;

		private static bool myPlayMode = false;
		private static bool myPaused = false;
		private static bool myNeedRefresh = false;
		private bool myWasLoading = true;

		private static JSONStorableString mySendMessage;
		private static JSONStorableString myPlaceAvoid;
		private static JSONStorableString myLiftAvoid;
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

			myPlaceAvoid = new JSONStorableString("PlaceAvoid", "", PlaceAvoid);
			myPlaceAvoid.isStorable = myPlaceAvoid.isRestorable = false;
			RegisterString(myPlaceAvoid);

			myLiftAvoid = new JSONStorableString("LiftAvoid", "", LiftAvoid);
			myLiftAvoid.isStorable = myLiftAvoid.isRestorable = false;
			RegisterString(myLiftAvoid);

			JSONStorableFloat myAnimationSpeed = new JSONStorableFloat("AnimationSpeed", 1.0f, ChangeSpeed, 0.0f, 10.0f, true, true);
			myAnimationSpeed.isStorable = myAnimationSpeed.isRestorable = false;
			RegisterFloat(myAnimationSpeed);

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

		private void ChangeSpeed(float f)
		{
			if(myCurrentAnimation!=null)
				myCurrentAnimation.mySpeed = f;
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
			foreach (var m in myMessages) {
				Message message = m.Value;
				if(message.myMessageString == messageString) {
					foreach (var l in myCurrentAnimation.myLayers) {
						State currentState = l.Value.myCurrentState;
						if(message.mySourceStates.Values.ToList().Contains(currentState)) {
							currentState.myLayer.GoTo(message.myTargetState);
						}
					}
				}
			}
		}

		public void PlaceAvoid(String avoidString) {
			myPlaceAvoid.valNoCallback = "";
			foreach (var av in myAvoids) {
				Avoid avoid = av.Value;
				if(avoid.myName == avoidString) {
					avoid.myIsPlaced = true;
				}
			}
		}

		public void LiftAvoid(String avoidString) {
			myLiftAvoid.valNoCallback = "";
			foreach (var av in myAvoids) {
				Avoid avoid = av.Value;
				if(avoid.myName == avoidString) {
					avoid.myIsPlaced = false;
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
			return new Layer(name);
		}

		private State CreateState(string name)
		{
			State s = new State(this, name, myCurrentLayer) {
				myWaitDurationMin = myGlobalDefaultWaitDurationMin.val,
				myWaitDurationMax = myGlobalDefaultWaitDurationMax.val,
				myDefaultDuration = myGlobalDefaultTransitionDuration.val,
				myDefaultEaseInDuration = myGlobalDefaultEaseInDuration.val,
				myDefaultEaseOutDuration = myGlobalDefaultEaseOutDuration.val
			};
			myCurrentLayer.CaptureState(s);
			if(myCurrentLayer.myCurrentState != null) {
				setCaptureDefaults(s, myCurrentLayer.myCurrentState);
			}
			myCurrentLayer.myStates[name] = s;
			return s;
		}

		private static void SetAnimation(Animation animation)
		{
			if(myCurrentAnimation != null) {
				foreach(var l in myCurrentAnimation.myLayers) {
					Layer layer = l.Value;
					layer.myStateChain.Clear();
				}
			}
			myCurrentAnimation = animation;
			myMainAnimation.valNoCallback = animation.myName;

			List<string> layers = myCurrentAnimation.myLayers.Keys.ToList();
			layers.Sort();
			myMainLayer.choices = layers;
			foreach(var l in myCurrentAnimation.myLayers) {
				SetLayer(l.Value);
			}
		}

		private static void SetLayer(Layer layer)
		{
			myCurrentLayer = layer;
			myMainLayer.valNoCallback = layer.myName;

			State state = layer.myCurrentState;
			if(state != null)
				myMainState.valNoCallback = state.myName;
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
		private class AnimationObject
		{
			public string myName;

			public AnimationObject(string name)
			{
				myName = name;
			}
		}

		private class Animation : AnimationObject
		{
			public Dictionary<string, Layer> myLayers = new Dictionary<string, Layer>();
			public float mySpeed = 1.0f;

			public Animation(string name) : base(name){}

			public void Clear()
			{
				foreach (var l in myLayers)
				{
					Layer layer = l.Value;
					layer.Clear();
				}
			}

			public void InitAnimationLayers() {
				foreach(var l in myLayers) {
					Layer layer = l.Value;
					layer.GoToAnyState(myGlobalDefaultTransitionDuration.val, 0);
				}
			}

			public List<State> findPath(Animation target) {
				List<State> smallestPath = null;
				foreach(var l in myLayers) {
					Layer layer = l.Value;
					State state = layer.myCurrentState;
					if(state == null)
						continue;
					List<State> path = state.findPath(target);
					if(path != null && (smallestPath == null || path.Count < smallestPath.Count))
						smallestPath = path;
				}
				return smallestPath;
			}
		}

		// =======================================================================================
		private class Layer : AnimationObject
		{
			public Animation myAnimation;
			public Dictionary<string, State> myStates = new Dictionary<string, State>();
			public State myCurrentState;
			public List<ControlCapture> myControlCaptures = new List<ControlCapture>();
			public List<MorphCapture> myMorphCaptures = new List<MorphCapture>();
			private Transition myTransition;
			private float myTransitionNoise = 0.0f;
			public float myClock = 0.0f;
			public float myDuration = 1.0f;
			private List<TriggerActionDiscrete> myTriggerActionsNeedingUpdate = new List<TriggerActionDiscrete>();
			public List<State> myStateChain = new List<State>();

			public Layer(string name) : base(name)
			{
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
				myCurrentState = state;

				myClock = 0.0f;
				myDuration = UnityEngine.Random.Range(state.myWaitDurationMin, state.myWaitDurationMax);

				if (myMainLayer.val == myName) {
					myMainState.valNoCallback = myCurrentState.myName;
					myMainAnimation.valNoCallback = myCurrentState.myAnimation().myName;
				}
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

				bool paused = myPaused && myTransition == null && myStateChain.Count == 0;

				if(!paused) {
					myClock = Mathf.Min(myClock + Time.deltaTime*myCurrentAnimation.mySpeed, 100000.0f);

					// if a transition is possible but not yet chosen and the state duration is up
					if(myClock >= myDuration && myTransition == null) {
						SetNextTransition();
					} else if (myTransition != null) {
						float t = Smooth(myTransition.myEaseOutDuration, myTransition.myEaseInDuration, myTransition.myDuration, myClock-myDuration);
						UpdateCurve(t);

						if (myClock >= myDuration + myTransition.myDuration + myTransitionNoise)
							ArriveAtState();
					} else {
						UpdateState();
					}
				}
			}

			public void UpdateCurve(float t) {
				for (int i=0; i<myControlCaptures.Count; ++i)
					myControlCaptures[i].UpdateCurve(t);
				for (int i=0; i<myMorphCaptures.Count; ++i)
					myMorphCaptures[i].UpdateCurve(t);
			}

			public void UpdateState() {
				for (int i=0; i<myControlCaptures.Count; ++i)
					myControlCaptures[i].UpdateState(myCurrentState);
			}

			public void ArriveAtState() {
				SetState(myTransition.myTargetState);
				myTransition.EndTransition(myTriggerActionsNeedingUpdate);
				myTransition = null;
			}

			public State CreateBlendState()
			{
				State blendState = new State("BlendState", this) {
					myWaitDurationMin = 0.0f,
					myWaitDurationMax = 0.0f,
					myDefaultDuration = myGlobalDefaultTransitionDuration.val,
				};
				CaptureState(blendState);
				return blendState;
			}

			public void GoTo(State state)
			{
				myStateChain.Clear();
				if (myCurrentState == null || myCurrentState == state) {
					myStateChain.Add(CreateBlendState());
					myStateChain.Add(state);
				} else {
					List<State> path = myCurrentState.findPath(state);
					if(path != null) {
						myStateChain = path;
					} else {
						myStateChain.Add(myCurrentState);
						myStateChain.Add(state);
					}
				}
				SetNextTransition();
			}

			public void FillStateChain() {
				if(myStateChain.Count == 0)
					myStateChain.Add(myCurrentState);
				while(myStateChain.Count < MAX_STATES) {
					State state = myStateChain.Last();
					State nextState = state.sortNextState();
					if(nextState == null)
						break;
					List<State> path = state.findPath(nextState);
					path.RemoveAt(0);
					foreach(State s in path) {
						myStateChain.Add(s);
					}
				}
			}

			public void SetNextTransition() {
				if(!myPaused)
					FillStateChain();

				myClock = myDuration;

				myTransition = null;
				if(myStateChain.Count < 2)
					return;

				State sourceState = myStateChain[0];
				State targetState = myStateChain[1];
				myStateChain.RemoveAt(0);

				Transition transition;
				if(sourceState.isReachable(targetState)) {
					transition = sourceState.getIncomingTransition(targetState) as Transition;
				} else {
					transition = new Transition(sourceState, targetState);
				}
				transition.StartTransition(myTriggerActionsNeedingUpdate);
				float transitionNoise = UnityEngine.Random.Range(-transition.myDurationNoise, transition.myDurationNoise);

				if(targetState.myAnimation() != sourceState.myAnimation()) {
					Animation animation = targetState.myAnimation();
					Layer targetLayer = targetState.myLayer;

					foreach(var l in animation.myLayers) {
						Layer layer = l.Value;
						if(layer == targetLayer || transition.mySyncTargets.Keys.Contains(layer))
							continue;
						layer.GoToAnyState(transition.myDuration, transitionNoise);
					}

					targetLayer.BlendTo(targetState, transition.myDuration, transitionNoise);
					targetLayer.myStateChain = new List<State>(myStateChain);

					SetAnimation(animation);
				} else {
					SetTransition(transition, transitionNoise);
				}
			}

			public void GoToAnyState(float transitionDuration, float transitionNoise) {
				if(myCurrentState == null) {
					List<string> states = myStates.Keys.ToList();
					states.Sort();
					if(states.Count() > 0)
						BlendTo(myStates[states[0]], transitionDuration, transitionNoise);
				} else
					BlendTo(myCurrentState, transitionDuration, transitionNoise);
			}

			public void BlendTo(State targetState, float transitionDuration, float transitionNoise) {
				Transition transition = new Transition(CreateBlendState(), targetState, transitionDuration);
				SetTransition(transition, transitionNoise);
			}

			public void SetTransition(Transition transition, float transitionNoise) {
				List<State> stateChain = new List<State>(2);
				stateChain.Add(transition.mySourceState);
				stateChain.Add(transition.myTargetState);

				for (int i=0; i<myControlCaptures.Count; ++i)
					myControlCaptures[i].SetTransition(stateChain);
				for (int i=0; i<myMorphCaptures.Count; ++i)
					myMorphCaptures[i].SetTransition(stateChain);

				myTransition = transition;
				myTransitionNoise = transitionNoise;
			}
		}

		private class Role : AnimationObject
		{
			public Atom myPerson;

			public Role(string name) : base(name) {
			}
		}

		private class BaseTransition
		{
			public State mySourceState;
			public State myTargetState;
			public float myProbability;
		}

		private class IndirectTransition : BaseTransition
		{
			public IndirectTransition(State sourceState, State targetState)
			{
				mySourceState = sourceState;
				myTargetState = targetState;
				myProbability = targetState.myDefaultProbability;
			}

			public IndirectTransition(IndirectTransition t) {
				mySourceState = t.mySourceState;
				myTargetState = t.myTargetState;
				myProbability = t.myProbability;
			}
		}

		private class Transition : BaseTransition
		{
			public float myEaseInDuration;
			public float myEaseOutDuration;
			public float myDuration;
			public float myDurationNoise = 0.0f;
			public Dictionary<Layer, State> mySyncTargets = new Dictionary<Layer, State>();
			public Dictionary<Role, String> myMessages = new Dictionary<Role, String>();
			public Dictionary<Role, Dictionary<String, bool>> myAvoids = new Dictionary<Role, Dictionary<String, bool>>();

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

			public Transition(Transition t) {
				mySourceState = t.mySourceState;
				myTargetState = t.myTargetState;
				myProbability = t.myProbability;
				myEaseInDuration = t.myEaseInDuration;
				myEaseOutDuration = t.myEaseOutDuration;
				myDuration = t.myDuration;
				myDurationNoise = t.myDurationNoise;
				mySyncTargets = t.mySyncTargets;
				myMessages = t.myMessages;
				myAvoids = t.myAvoids;
			}

			public void StartTransition(List<TriggerActionDiscrete> triggerActionsNeedingUpdate) {
				SendStartTransitionTriggers(triggerActionsNeedingUpdate);

				foreach(var sc in mySyncTargets) {
					Layer syncLayer = sc.Key;
					State syncState = sc.Value;
					syncLayer.GoTo(syncState);
				}

				SendMessages();
				SendAvoids();
			}

			public void EndTransition(List<TriggerActionDiscrete> triggerActionsNeedingUpdate) {
				SendEndTransitionTriggers(triggerActionsNeedingUpdate);

				for (int i=0; i<myTargetState.myLayer.myControlCaptures.Count; ++i)
					myTargetState.myLayer.myControlCaptures[i].UpdateControllerStates();
			}

			public void SendStartTransitionTriggers(List<TriggerActionDiscrete> triggerActionsNeedingUpdate) {
				if (mySourceState.ExitBeginTrigger != null)
					mySourceState.ExitBeginTrigger.Trigger(triggerActionsNeedingUpdate);
				if (myTargetState.EnterBeginTrigger != null)
					myTargetState.EnterBeginTrigger.Trigger(triggerActionsNeedingUpdate);
			}

			public void SendEndTransitionTriggers(List<TriggerActionDiscrete> triggerActionsNeedingUpdate) {
				if (mySourceState.ExitEndTrigger != null)
					mySourceState.ExitEndTrigger.Trigger(triggerActionsNeedingUpdate);
				if (myTargetState.EnterEndTrigger != null)
					myTargetState.EnterEndTrigger.Trigger(triggerActionsNeedingUpdate);
			}

			public void SendMessages() {
				foreach(var m in myMessages) {
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

			public void SendAvoids() {
				foreach(var r in myAvoids) {
					Role role = r.Key;
					foreach(var a in r.Value) {
						String avoidString = a.Key;
						Atom person = role.myPerson;
						if (person == null) continue;
						var storableId = person.GetStorableIDs().FirstOrDefault(id => id.EndsWith("HaremLife.AnimationPoser"));
						if (storableId == null) continue;
						MVRScript storable = person.GetStorableByID(storableId) as MVRScript;
						if (storable == null) continue;
						// if (ReferenceEquals(storable, _plugin)) continue;
						if (!storable.enabled) continue;
						if(a.Value)
							storable.SendMessage(nameof(AnimationPoser.PlaceAvoid), avoidString);
						else
							storable.SendMessage(nameof(AnimationPoser.LiftAvoid), avoidString);
					}
				}
			}
		}

		private class Message : AnimationObject
		{
			public State myTargetState;
			public String myMessageString;
			public Dictionary<string, State> mySourceStates = new Dictionary<string, State>();

			public Message(string name) : base(name) {
				myName = name;
			}
		}

		private class Avoid : AnimationObject
		{
			public Dictionary<string, State> myAvoidStates = new Dictionary<string, State>();
			public bool myIsPlaced = false;

			public Avoid(string name) : base(name) {
			}
		}

		private class State : AnimationObject
		{
			public Layer myLayer;
			public float myWaitDurationMin;
			public float myWaitDurationMax;
			public float myDefaultDuration;
			public float myDefaultEaseInDuration;
			public float myDefaultEaseOutDuration;
			public float myDefaultProbability = DEFAULT_PROBABILITY;
			public bool myIsRootState = false;
			public bool myAvoid = false;
			public uint myDebugIndex = 0;
			public Dictionary<ControlCapture, ControlEntryAnchored> myControlEntries = new Dictionary<ControlCapture, ControlEntryAnchored>();
			public Dictionary<MorphCapture, float> myMorphEntries = new Dictionary<MorphCapture, float>();
			public List<BaseTransition> myTransitions = new List<BaseTransition>();
			public EventTrigger EnterBeginTrigger;
			public EventTrigger EnterEndTrigger;
			public EventTrigger ExitBeginTrigger;
			public EventTrigger ExitEndTrigger;

			public State(MVRScript script, string name, Layer layer) : base(name)
			{
				myLayer = layer;
				EnterBeginTrigger = new EventTrigger(script, "OnEnterBegin", name);
				EnterEndTrigger = new EventTrigger(script, "OnEnterEnd", name);
				ExitBeginTrigger = new EventTrigger(script, "OnExitBegin", name);
				ExitEndTrigger = new EventTrigger(script, "OnExitEnd", name);
			}

			public State(string name, Layer layer) : base(name)
			{
				myLayer = layer;
			}

			public State(string name, State source) : base(name)
			{
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

			public Animation myAnimation() {
				return myLayer.myAnimation;
			}

			public List<State> getDirectlyReachableStates() {
				List<State> states = new List<State>();
				for(int i=0; i<myTransitions.Count; i++) {
					if(myTransitions[i] is Transition) {
						states.Add(myTransitions[i].myTargetState);
					}
				}
				return states;
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

			public List<State> filterAvoided(List<State> states) {
				List<State> notAvoided = new List<State>();
				foreach(State state in states) {
					bool avoided = false;
					foreach(var a in myAvoids) {
						Avoid avoid = a.Value;
						if(!avoid.myIsPlaced)
							continue;
						if(avoid.myAvoidStates.Values.Contains(state))
							avoided = true;
					}
					if(!avoided)
						notAvoided.Add(state);
				}
				return notAvoided;
			}

			public Dictionary<State, List<State>> getPaths() {
				Dictionary<State, List<State>> paths = new Dictionary<State, List<State>>();

				paths[this] = new List<State>();
				paths[this].Add(this);

				while(true) {
					bool changed = false;

					List<State> keys = paths.Keys.ToList();
					for(int i=0; i<keys.Count(); i++) {
						State thisState = keys[i];

					List<State> states = filterAvoided(thisState.getDirectlyReachableStates());
					for(int j=0; j<states.Count(); j++) {
							State state = states[j];

							if(!paths.ContainsKey(state)) {
								paths[state] = new List<State>(paths[thisState]);
								paths[state].Add(state);
								changed = true;
							}
						}
					}
					if(!changed) {
						break;
					}
				}

				return paths;
			}

			public List<State> findPath(State target) {
				Dictionary<State, List<State>> paths = getPaths();

				if(paths.ContainsKey(target)) {
					List<State> path = paths[target];
					return path;
				} else {
					return null;
				}
			}

			public List<State> findPath(Animation target) {
				Dictionary<State, List<State>> paths = getPaths();

				List<State> smallestPath = null;
				foreach(var s in paths) {
					State state = s.Key;
					if(state.myAnimation() == target) {
						List<State> path = paths[state];
						if(smallestPath == null || path.Count < smallestPath.Count)
							smallestPath = path;
					}
				}

				return smallestPath;
			}

			public State sortNextState() {
				List<State> states = filterAvoided(getReachableStates());
				if(states.Count == 0)
					return null;

				float sum = 0.0f;
				for (int i=0; i<myTransitions.Count; ++i)
					sum += myTransitions[i].myProbability;
				if (sum == 0.0f)
				{
					return null;
				}
				else
				{
					float threshold = UnityEngine.Random.Range(0.0f, sum);
					sum = 0.0f;
					int i;
					for (i=0; i<myTransitions.Count-1; ++i)
					{
						sum += myTransitions[i].myProbability;
						if (threshold <= sum)
							break;
					}
					return states[i];
				}

			}

			public BaseTransition getIncomingTransition(State state) {
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
			private ControlEntryAnchored[] myCurve = new ControlEntryAnchored[MAX_STATES];
			private int myEntryCount = 0;
			public bool myApplyPosition = true;
			public bool myApplyRotation = true;
			FreeControllerV3 myController;

			private static Quaternion[] ourTempQuaternions = new Quaternion[MAX_STATES-1];
			private static float[] ourTempDistances = new float[DISTANCE_SAMPLES[DISTANCE_SAMPLES.Length-1] + 2];

			public ControlCapture(AnimationPoser plugin, string control)
			{
				myPlugin = plugin;
				myName = control;
				FreeControllerV3 controller = plugin.containingAtom.GetStorableByID(control) as FreeControllerV3;
				if (controller != null)
					myTransform = controller.transform;

				myController = controller;
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
				FreeControllerV3.PositionState positionState;
				FreeControllerV3.RotationState rotationState;
				if(myController.name == "control") {
					positionState = FreeControllerV3.PositionState.On;
					rotationState = FreeControllerV3.RotationState.On;
				} else {
					positionState = myController.currentPositionState;
					rotationState = myController.currentRotationState;
				}
				entry.Capture(myTransform.position, myTransform.rotation,
								positionState, rotationState);
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
				entry.myAnchorAType = oldEntry.myAnchorAType;
				entry.myAnchorBType = oldEntry.myAnchorBType;
			}

			public void SetTransition(List<State> stateChain)
			{
				myEntryCount = stateChain.Count;
				for(int i=0; i<myEntryCount; i++) {
					if (!stateChain[i].myControlEntries.TryGetValue(this, out myCurve[i]))
					{
						CaptureEntry(stateChain[i]);
						myCurve[i] = stateChain[i].myControlEntries[this];
					}
					myCurve[i].Initialize();
				}
			}

			public void UpdateControllerStates() {
				myController.currentPositionState = myCurve[1].myPositionState;
				myController.currentRotationState = myCurve[1].myRotationState;
			}

			public void UpdateCurve(float t)
			{
				for (int i=0; i<myEntryCount; ++i)
					myCurve[i].Update();

				//t = ArcLengthParametrization(t);

				if (myApplyPosition && myCurve[1].myPositionState != FreeControllerV3.PositionState.Off)
				{
					switch (myEntryCount)
					{
						case 4:	myTransform.position = EvalBezierCubicPosition(t);          break;
						case 3: myTransform.position = EvalBezierQuadraticPosition(t);      break;
						case 2: myTransform.position = EvalBezierLinearPosition(t);         break;
						default: myTransform.position = myCurve[0].myEntry.myPosition; break;
					}
				}
				if (myApplyRotation && myCurve[1].myRotationState != FreeControllerV3.RotationState.Off)
				{
					switch (myEntryCount)
					{
						case 4: myTransform.rotation = EvalBezierCubicRotation(t);          break;
						case 3: myTransform.rotation = EvalBezierQuadraticRotation(t);      break;
						case 2: myTransform.rotation = EvalBezierLinearRotation(t);         break;
						default: myTransform.rotation = myCurve[0].myEntry.myRotation; break;
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
				Vector3 previous = myCurve[0].myEntry.myPosition;
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

				distance += Vector3.Distance(previous, myCurve[myEntryCount-1].myEntry.myPosition);
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
				return Vector3.LerpUnclamped(myCurve[0].myEntry.myPosition, myCurve[1].myEntry.myPosition, t);
			}

			private Vector3 EvalBezierQuadraticPosition(float t)
			{
				// evaluating quadratic B??zier curve using Bernstein polynomials
				float s = 1.0f - t;
				return      (s*s) * myCurve[0].myEntry.myPosition
					 + (2.0f*s*t) * myCurve[1].myEntry.myPosition
					 +      (t*t) * myCurve[2].myEntry.myPosition;
			}

			private Vector3 EvalBezierCubicPosition(float t)
			{
				// evaluating cubic B??zier curve using Bernstein polynomials
				float s = 1.0f - t;
				float t2 = t*t;
				float s2 = s*s;
				return      (s*s2) * myCurve[0].myEntry.myPosition
					 + (3.0f*s2*t) * myCurve[1].myEntry.myPosition
					 + (3.0f*s*t2) * myCurve[2].myEntry.myPosition
					 +      (t*t2) * myCurve[3].myEntry.myPosition;
			}

			private Quaternion EvalBezierLinearRotation(float t)
			{
				return Quaternion.SlerpUnclamped(myCurve[0].myEntry.myRotation, myCurve[1].myEntry.myRotation, t);
			}

			private Quaternion EvalBezierQuadraticRotation(float t)
			{
				// evaluating quadratic B??zier curve using de Casteljau's algorithm
				ourTempQuaternions[0] = Quaternion.SlerpUnclamped(myCurve[0].myEntry.myRotation, myCurve[1].myEntry.myRotation, t);
				ourTempQuaternions[1] = Quaternion.SlerpUnclamped(myCurve[1].myEntry.myRotation, myCurve[2].myEntry.myRotation, t);
				return Quaternion.SlerpUnclamped(ourTempQuaternions[0], ourTempQuaternions[1], t);
			}

			private Quaternion EvalBezierCubicRotation(float t)
			{
				// evaluating cubic B??zier curve using de Casteljau's algorithm
				for (int i=0; i<3; ++i)
					ourTempQuaternions[i] = Quaternion.SlerpUnclamped(myCurve[i].myEntry.myRotation, myCurve[i+1].myEntry.myRotation, t);
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

			public const int ANCHORTYPE_OBJECT = 0;
			public const int ANCHORTYPE_ROLE = 1;

			public ControlEntry myEntry;
			public FreeControllerV3.PositionState myPositionState;
			public FreeControllerV3.RotationState myRotationState;
			public ControlEntry myAnchorOffset;
			public Transform myAnchorATransform;
			public Transform myAnchorBTransform;
			public int myAnchorMode = ANCHORMODE_SINGLE;
			public int myAnchorAType = ANCHORTYPE_OBJECT;
			public int myAnchorBType = ANCHORTYPE_OBJECT;
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
				Capture(myEntry.myPosition, myEntry.myRotation,
							myPositionState, myRotationState);
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
					myAnchorATransform = GetTransform(myAnchorAAtom, myAnchorAControl, myAnchorAType);
					if (myAnchorMode == ANCHORMODE_BLEND)
						myAnchorBTransform = GetTransform(myAnchorBAtom, myAnchorBControl, myAnchorBType);
					else
						myAnchorBTransform = null;
				}
			}

			private Transform GetTransform(string atomName, string controlName, int anchorType)
			{
				Atom atom = null;
				if (anchorType == ControlEntryAnchored.ANCHORTYPE_OBJECT)
					atom = SuperController.singleton.GetAtomByUid(atomName);
				else if(myRoles.Keys.Contains(atomName))
					atom = myRoles[atomName].myPerson;
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

			public void Capture(Vector3 position, Quaternion rotation,
									FreeControllerV3.PositionState positionState,
									FreeControllerV3.RotationState rotationState)
			{
				myPositionState = positionState;
				myRotationState = rotationState;

				myEntry.myPosition = position;
				myEntry.myRotation = rotation;

				Quaternion oldRootRotation = myAnchorOffset.myRotation;
				Vector3 oldRootPosition = myAnchorOffset.myPosition;

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

				if(myCurrentLayer != null) {
					State rootState = myCurrentLayer.myStates.Values.ToList().FirstOrDefault(s => s.myIsRootState);
					if(rootState != null && myState == rootState) {
						foreach(var s in myCurrentLayer.myStates) {
							State st = s.Value;
							if(st != rootState) {
								ControlCapture cc = st.myControlEntries.Keys.ToList().Find(ccx => ccx.myName == myControlCapture.myName);
								ControlEntryAnchored ce = st.myControlEntries[cc];

								Quaternion transformRotation = myAnchorOffset.myRotation * Quaternion.Inverse(oldRootRotation);
								Vector3 transformPosition = position - oldRootPosition;

								ce.myAnchorOffset.myPosition = myAnchorOffset.myPosition + transformRotation * (ce.myAnchorOffset.myPosition - oldRootPosition);
								ce.myAnchorOffset.myRotation = transformRotation * ce.myAnchorOffset.myRotation;
							}
						}
					}
				}
			}
		}


		private class MorphCapture
		{
			public string mySID;
			public DAZMorph myMorph;
			public DAZCharacterSelector.Gender myGender;
			private float[] myCurve = new float[MAX_STATES];
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

			public void SetTransition(List<State> stateChain)
			{
				myEntryCount = stateChain.Count;
				bool identical = true;
				float morphValue = myMorph.morphValue;

				for(int i=0; i<myEntryCount; i++) {
					if (!stateChain[i].myMorphEntries.TryGetValue(this, out myCurve[i]))
					{
						CaptureEntry(stateChain[i]);
						myCurve[i] = morphValue;
					}
					else
					{
						identical &= (myCurve[i] == morphValue);
					}
				}

				if (identical)
					myEntryCount = 0; // nothing to do, save some performance
			}

			public void UpdateCurve(float t)
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
						myMorph.morphValue = myCurve[0];
						break;
				}
			}

			private float EvalBezierLinear(float t)
			{
				return Mathf.LerpUnclamped(myCurve[0], myCurve[1], t);
			}

			private float EvalBezierQuadratic(float t)
			{
				// evaluating using Bernstein polynomials
				float s = 1.0f - t;
				return      (s*s) * myCurve[0]
					 + (2.0f*s*t) * myCurve[1]
					 +      (t*t) * myCurve[2];
			}

			private float EvalBezierCubic(float t)
			{
				// evaluating using Bernstein polynomials
				float s = 1.0f - t;
				float t2 = t*t;
				float s2 = s*s;
				return      (s*s2) * myCurve[0]
					 + (3.0f*s2*t) * myCurve[1]
					 + (3.0f*s*t2) * myCurve[2]
					 +      (t*t2) * myCurve[3];
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
