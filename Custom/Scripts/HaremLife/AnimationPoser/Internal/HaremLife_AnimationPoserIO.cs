/* /////////////////////////////////////////////////////////////////////////////////////////////////
AnimationPoser by HaremLife.
Based on Life#IdlePoser by MacGruber.
State-based idle animation system with anchoring.
https://www.patreon.com/MacGruber_Laboratory
https://github.com/haremlife/AnimationPoser

Licensed under CC BY-SA after EarlyAccess ended. (see https://creativecommons.org/licenses/by-sa/4.0/)

///////////////////////////////////////////////////////////////////////////////////////////////// */
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
		private JSONClass SaveAnimations()
		{
			JSONClass jc = new JSONClass();

			// save info
			JSONClass info = new JSONClass();
			info["Format"] = "HaremLife.AnimationPoser";
			info["Version"].AsInt = 9;
			string creatorName = UserPreferences.singleton.creatorName;
			if (string.IsNullOrEmpty(creatorName))
				creatorName = "Unknown";
			info["Author"] = creatorName;
			jc["Info"] = info;

			// save settings
			jc["Paused"].AsBool = myPlayPaused.val;

			JSONArray anims = new JSONArray();
			foreach(var an in myAnimations)
			{
				anims.Add("", SaveAnimation(an.Value));
			}

			SaveRoles(jc);
			SaveMessages(jc);
			jc["Animations"] = anims;

			return jc;
		}

		private void SaveRoles(JSONClass jc) {
			JSONArray rlist = new JSONArray();
			foreach (var r in myRoles)
			{
				rlist.Add("", SaveRole(r.Value));
			}
			jc["Roles"] = rlist;
		}

		private void SaveMessages(JSONClass jc) {
			JSONArray mlist = new JSONArray();
			foreach(var mm in myMessages)
			{
				mlist.Add("", SaveMessage(mm.Value));
			}
			jc["Messages"] = mlist;
		}

		private JSONClass SaveAnimation(Animation animationToSave)
		{
			JSONClass anim = new JSONClass();
			anim["Name"] = animationToSave.myName;
			anim["Speed"].AsFloat = animationToSave.mySpeed;
			JSONArray llist = new JSONArray();
			foreach(var l in animationToSave.myLayers){
				llist.Add("", SaveLayer(l.Value));
			}
			anim["Layers"] = llist;
			return anim;
		}

		private JSONClass SaveRole(Role roleToSave) {
			JSONClass rclass = new JSONClass();
			rclass["Name"] = roleToSave.myName;
			if(roleToSave.myPerson != null) {
				rclass["Person"] = roleToSave.myPerson.name;
			} else {
				rclass["Person"] = "";
			}
			// ccclass["Person"] = r.myPerson;
			return rclass;
		}

		private JSONClass SaveMessage(Message messageToSave) {
			JSONClass m = new JSONClass();

			m["Name"] = messageToSave.myName;
			m["MessageString"] = messageToSave.myMessageString;

			JSONArray srcstlist = new JSONArray();
			foreach(var srcst in messageToSave.mySourceStates) {
				JSONClass src = new JSONClass();
				src["AnimationName"] = srcst.Value.myLayer.myAnimation.myName;
				src["LayerName"] = srcst.Value.myLayer.myName;
				src["StateName"] = srcst.Value.myName;
				srcstlist.Add(src);
			}
			m["SourceStates"] = srcstlist;
			m["TargetState"] = messageToSave.myTargetState.myName;
			m["TargetLayer"] = messageToSave.myTargetState.myLayer.myName;
			m["TargetAnimation"] = messageToSave.myTargetState.myAnimation().myName;

			return m;
		}

		private JSONClass SaveLayer(Layer layerToSave)
		{
			JSONClass jc = new JSONClass();
			// save settings
			if (layerToSave.myCurrentState != null)
				jc["InitialState"] = layerToSave.myCurrentState.myName;

			JSONClass layer = new JSONClass();
			layer["Name"] = layerToSave.myName;

			// save captures
			if (layerToSave.myControlCaptures.Count > 0)
			{
				JSONArray cclist = new JSONArray();
				for (int i=0; i<layerToSave.myControlCaptures.Count; ++i)
				{
					ControlCapture cc = layerToSave.myControlCaptures[i];
					JSONClass ccclass = new JSONClass();
					ccclass["Name"] = cc.myName;
					ccclass["ApplyPos"].AsBool = cc.myApplyPosition;
					ccclass["ApplyRot"].AsBool = cc.myApplyRotation;
					cclist.Add("", ccclass);
				}
				layer["ControlCaptures"] = cclist;
			}
			if (layerToSave.myMorphCaptures.Count > 0)
			{
				JSONArray mclist = new JSONArray();
				for (int i=0; i<layerToSave.myMorphCaptures.Count; ++i)
				{
					MorphCapture mc = layerToSave.myMorphCaptures[i];
					JSONClass mcclass = new JSONClass();
					mcclass["UID"] = mc.myMorph.uid;
					mcclass["SID"] = mc.mySID;
					mcclass["Apply"].AsBool = mc.myApply;
					mclist.Add("", mcclass);
				}
				layer["MorphCaptures"] = mclist;
			}

			// save states
			JSONArray slist = new JSONArray();
			foreach (var s in layerToSave.myStates)
			{
				State state = s.Value;
				JSONClass st = new JSONClass();
				st["Name"] = state.myName;
				st["IsRootState"].AsBool = state.myIsRootState;
				st["WaitDurationMin"].AsFloat = state.myWaitDurationMin;
				st["WaitDurationMax"].AsFloat = state.myWaitDurationMax;
				st["DefaultDuration"].AsFloat = state.myDefaultDuration;
				st["DefaultEaseInDuration"].AsFloat = state.myDefaultEaseInDuration;
				st["DefaultEaseOutDuration"].AsFloat = state.myDefaultEaseOutDuration;
				st["DefaultProbability"].AsFloat = state.myDefaultProbability;

				JSONArray tlist = new JSONArray();
				for (int i=0; i<state.myTransitions.Count; ++i) {
					JSONClass t = new JSONClass();
					if(state.myTransitions[i] is Transition) {
						Transition transition = state.myTransitions[i] as Transition;
						t["Type"] = "Direct";
						t["SourceState"] = transition.mySourceState.myName;
						t["TargetState"] = transition.myTargetState.myName;
						if(transition.myTargetState.myLayer == transition.mySourceState.myLayer)
							t["TargetLayer"] = "[Self]";
						else
							t["TargetLayer"] = transition.myTargetState.myLayer.myName;
						if(transition.myTargetState.myAnimation() == transition.mySourceState.myAnimation())
							t["TargetAnimation"] = "[Self]";
						else
							t["TargetAnimation"] = transition.myTargetState.myAnimation().myName;
						t["Duration"].AsFloat = transition.myDuration;
						t["DurationNoise"].AsFloat = transition.myDurationNoise;
						t["EaseInDuration"].AsFloat = transition.myEaseInDuration;
						t["EaseOutDuration"].AsFloat = transition.myEaseOutDuration;
						t["Probability"].AsFloat = transition.myProbability;

						JSONClass synct = new JSONClass();
						foreach (var syncl in transition.mySyncTargets) {
							synct[syncl.Key.myName] = syncl.Value.myName;
						}
						t["SyncTargets"] = synct;

						JSONClass msgs = new JSONClass();
						foreach (var msg in transition.myMessages) {
							msgs[msg.Key.myName] = msg.Value;
						}
						t["Messages"] = msgs;
					} else {
						IndirectTransition transition = state.myTransitions[i] as IndirectTransition;
						t["Type"] = "Indirect";
						t["SourceState"] = transition.mySourceState.myName;
						t["TargetState"] = transition.myTargetState.myName;
						if(transition.myTargetState.myLayer == transition.mySourceState.myLayer)
							t["TargetLayer"] = "[Self]";
						else
							t["TargetLayer"] = transition.myTargetState.myLayer.myName;
						if(transition.myTargetState.myAnimation() == transition.mySourceState.myAnimation())
							t["TargetAnimation"] = "[Self]";
						else
							t["TargetAnimation"] = transition.myTargetState.myAnimation().myName;
						t["Probability"].AsFloat = transition.myProbability;
					}

					tlist.Add(t);
				}
				st["Transitions"] = tlist;

				if (state.myControlEntries.Count > 0)
				{
					JSONClass celist = new JSONClass();
					foreach (var e in state.myControlEntries)
					{
						ControlEntryAnchored ce = e.Value;
						JSONClass ceclass = new JSONClass();
						ceclass["PositionState"] = ce.myPositionState.ToString();
						ceclass["RotationState"] = ce.myRotationState.ToString();
						ceclass["PX"].AsFloat = ce.myAnchorOffset.myPosition.x;
						ceclass["PY"].AsFloat = ce.myAnchorOffset.myPosition.y;
						ceclass["PZ"].AsFloat = ce.myAnchorOffset.myPosition.z;
						Vector3 rotation = ce.myAnchorOffset.myRotation.eulerAngles;
						ceclass["RX"].AsFloat = rotation.x;
						ceclass["RY"].AsFloat = rotation.y;
						ceclass["RZ"].AsFloat = rotation.z;
						ceclass["AnchorMode"].AsInt = ce.myAnchorMode;
						if (ce.myAnchorMode >= ControlEntryAnchored.ANCHORMODE_SINGLE)
						{
							ceclass["DampingTime"].AsFloat = ce.myDampingTime;
							if(ce.myAnchorAAtom == containingAtom.uid)
								ceclass["AnchorAAtom"] = "[Self]";
							else
								ceclass["AnchorAAtom"] = ce.myAnchorAAtom;
							ceclass["AnchorAControl"] = ce.myAnchorAControl;
						}
						if (ce.myAnchorMode == ControlEntryAnchored.ANCHORMODE_BLEND)
						{
							ceclass["AnchorBAtom"] = ce.myAnchorBAtom;
							ceclass["AnchorBControl"] = ce.myAnchorBControl;
							ceclass["BlendRatio"].AsFloat = ce.myBlendRatio;
						}
						celist[e.Key.myName] = ceclass;
					}
					st["ControlEntries"] = celist;
				}

				if (state.myMorphEntries.Count > 0)
				{
					JSONClass melist = new JSONClass();
					foreach (var e in state.myMorphEntries)
					{
						melist[e.Key.mySID].AsFloat = e.Value;
					}
					st["MorphEntries"] = melist;
				}

				st[state.EnterBeginTrigger.Name] = state.EnterBeginTrigger.GetJSON(base.subScenePrefix);
				st[state.EnterEndTrigger.Name] = state.EnterEndTrigger.GetJSON(base.subScenePrefix);
				st[state.ExitBeginTrigger.Name] = state.ExitBeginTrigger.GetJSON(base.subScenePrefix);
				st[state.ExitEndTrigger.Name] = state.ExitEndTrigger.GetJSON(base.subScenePrefix);

				slist.Add("", st);
			}
			layer["States"] = slist;

			jc["Layer"] = layer;

			return jc;
		}

		private void LoadAnimations(JSONClass jc)
		{
			// load info
			myAnimations.Clear();
			int version = jc["Info"].AsObject["Version"].AsInt;

			LoadRoles(jc);

			// load captures
			JSONArray anims = jc["Animations"].AsArray;
			for (int l=0; l<anims.Count; ++l)
			{
				JSONClass anim = anims[l].AsObject;
				Animation animation = CreateAnimation(anim["Name"]);
				animation.mySpeed = anim["Speed"].AsFloat;

				JSONArray layers = anim["Layers"].AsArray;
				for(int m=0; m<layers.Count; m++)
				{
					JSONClass layerObj = layers[m].AsObject;
					Layer layer = LoadLayer(layerObj, false);
					animation.myLayers[layer.myName] = layer;
					layer.myAnimation = animation;
				}

				myAnimations[animation.myName] = animation;
			}

			for (int l=0; l<anims.Count; ++l)
			{
				JSONClass anim = anims[l].AsObject;
				Animation animation;
				if (!myAnimations.TryGetValue(anim["Name"], out animation))
					continue;
				JSONArray layers = anim["Layers"].AsArray;
				for(int m=0; m<layers.Count; m++)
				{
					JSONClass layerObj = layers[m].AsObject["Layer"].AsObject;

					Layer layer;
					if (!animation.myLayers.TryGetValue(layerObj["Name"], out layer))
						continue;

					LoadTransitions(layer, layerObj);
				}
			}

			if(myAnimations.Count == 0)
				return;
			SetAnimation(myAnimations.Values.ToList()[0]);

			// load settings
			myPlayPaused.valNoCallback = jc.HasKey("Paused") && jc["Paused"].AsBool;
			myPlayPaused.setCallbackFunction(myPlayPaused.val);

			if (myCurrentAnimation != null)
			{
				myMainAnimation.valNoCallback = myCurrentAnimation.myName;
				myMainAnimation.setCallbackFunction(myCurrentAnimation.myName);
			}
			if (myCurrentLayer != null)
			{
				myMainLayer.valNoCallback = myCurrentLayer.myName;
				myMainLayer.setCallbackFunction(myCurrentLayer.myName);
			}
			if (myCurrentState != null)
			{
				myMainState.valNoCallback = myCurrentState.myName;
				myMainState.setCallbackFunction(myCurrentState.myName);
			}
			LoadMessages(jc);
		}

		private Animation LoadAnimation(JSONClass anim)
		{
			Animation animation = CreateAnimation(anim["Name"]);
			animation.mySpeed = anim["Speed"].AsFloat;

			JSONArray layers = anim["Layers"].AsArray;
			for(int m=0; m<layers.Count; m++)
			{
				JSONClass layerObj = layers[m].AsObject;
				Layer layer = LoadLayer(layerObj, false);
				animation.myLayers[layer.myName] = layer;
				layer.myAnimation = animation;
			}

			for(int m=0; m<layers.Count; m++)
			{
				JSONClass layerObj = layers[m].AsObject["Layer"].AsObject;

				Layer layer;
				if (!animation.myLayers.TryGetValue(layerObj["Name"], out layer))
					continue;

				LoadTransitions(layer, layerObj);
			}

			return animation;
		}

		private FreeControllerV3.PositionState getPositionState(String state) {
			if(state == "On") {
				return FreeControllerV3.PositionState.On;
			} else if(state == "Comply") {
				return FreeControllerV3.PositionState.Comply;
			} else if(state == "Off") {
				return FreeControllerV3.PositionState.Off;
			} else if(state == "Hold") {
				return FreeControllerV3.PositionState.Hold;
			} else if(state == "Lock") {
				return FreeControllerV3.PositionState.Lock;
			} return FreeControllerV3.PositionState.On;
		}

		private FreeControllerV3.RotationState getRotationState(String state) {
			if(state == "On") {
				return FreeControllerV3.RotationState.On;
			} else if(state == "Comply") {
				return FreeControllerV3.RotationState.Comply;
			} else if(state == "Off") {
				return FreeControllerV3.RotationState.Off;
			} else if(state == "Hold") {
				return FreeControllerV3.RotationState.Hold;
			} else if(state == "Lock") {
				return FreeControllerV3.RotationState.Lock;
			} return FreeControllerV3.RotationState.On;
		}

		private Layer LoadLayer(JSONClass jc, bool newName)
		{
			JSONClass layerObj = jc["Layer"].AsObject;

			Layer layer;
			if(newName)
				layer = CreateLayer(FindNewName("Layer", "layers", new List<string>(myCurrentAnimation.myLayers.Keys)));
			else
				layer = CreateLayer(layerObj["Name"]);

			// load captures
			if (layerObj.HasKey("ControlCaptures"))
			{
				JSONArray cclist = layerObj["ControlCaptures"].AsArray;
				for (int i=0; i<cclist.Count; ++i)
				{
					ControlCapture cc;
					JSONClass ccclass = cclist[i].AsObject;
					cc = new ControlCapture(this, ccclass["Name"]);
					cc.myApplyPosition = ccclass["ApplyPos"].AsBool;
					cc.myApplyRotation = ccclass["ApplyRot"].AsBool;

					if (cc.IsValid())
						layer.myControlCaptures.Add(cc);
				}
			}
			if (layerObj.HasKey("MorphCaptures"))
			{
				JSONArray mclist = layerObj["MorphCaptures"].AsArray;
				DAZCharacterSelector geometry = containingAtom.GetStorableByID("geometry") as DAZCharacterSelector;
				for (int i=0; i<mclist.Count; ++i)
				{
					MorphCapture mc;
					JSONClass mcclass = mclist[i].AsObject;
					string uid = mcclass["UID"];
					if (uid.EndsWith(".vmi")) // handle custom morphs, resolve VAR packages
						uid = SuperController.singleton.NormalizeLoadPath(uid);
					string sid = mcclass["SID"];
					mc = new MorphCapture(geometry, uid, sid);
					mc.myApply = mcclass["Apply"].AsBool;

					if (mc.IsValid())
						layer.myMorphCaptures.Add(mc);
				}
			}

			// load states
			JSONArray slist = layerObj["States"].AsArray;
			for (int i=0; i<slist.Count; ++i)
			{
				// load state
				JSONClass st = slist[i].AsObject;
				State state = new State(this, st["Name"], layer) {
					myIsRootState = st["IsRootState"].AsBool,
					myWaitDurationMin = st["WaitDurationMin"].AsFloat,
					myWaitDurationMax = st["WaitDurationMax"].AsFloat,
					myDefaultDuration = st["DefaultDuration"].AsFloat,
					myDefaultEaseInDuration = st["DefaultEaseInDuration"].AsFloat,
					myDefaultEaseOutDuration = st["DefaultEaseOutDuration"].AsFloat,
					myDefaultProbability = st["DefaultProbability"].AsFloat,
				};

				state.EnterBeginTrigger.RestoreFromJSON(st, base.subScenePrefix, base.mergeRestore, true);
				state.EnterEndTrigger.RestoreFromJSON(st, base.subScenePrefix, base.mergeRestore, true);
				state.ExitBeginTrigger.RestoreFromJSON(st, base.subScenePrefix, base.mergeRestore, true);
				state.ExitEndTrigger.RestoreFromJSON(st, base.subScenePrefix, base.mergeRestore, true);


				if (layer.myStates.ContainsKey(state.myName))
					continue;
				layer.myStates[state.myName] = state;

				// load control captures
				if (layer.myControlCaptures.Count > 0)
				{
					JSONClass celist = st["ControlEntries"].AsObject;
					foreach (string ccname in celist.Keys)
					{
						ControlCapture cc = layer.myControlCaptures.Find(x => x.myName == ccname);
						if (cc == null)
							continue;

						JSONClass ceclass = celist[ccname].AsObject;
						ControlEntryAnchored ce = new ControlEntryAnchored(this, ccname, state, cc);
						ce.myPositionState = getPositionState(ceclass["PositionState"]);
						ce.myRotationState = getRotationState(ceclass["RotationState"]);
						ce.myAnchorOffset.myPosition.x = ceclass["PX"].AsFloat;
						ce.myAnchorOffset.myPosition.y = ceclass["PY"].AsFloat;
						ce.myAnchorOffset.myPosition.z = ceclass["PZ"].AsFloat;
						Vector3 rotation;
						rotation.x = ceclass["RX"].AsFloat;
						rotation.y = ceclass["RY"].AsFloat;
						rotation.z = ceclass["RZ"].AsFloat;
						ce.myAnchorOffset.myRotation.eulerAngles = rotation;
						ce.myAnchorMode = ceclass["AnchorMode"].AsInt;
						if (ce.myAnchorMode >= ControlEntryAnchored.ANCHORMODE_SINGLE)
						{
							ce.myDampingTime = ceclass["DampingTime"].AsFloat;
							ce.myAnchorAAtom = ceclass["AnchorAAtom"].Value;
							ce.myAnchorAControl = ceclass["AnchorAControl"].Value;

							if (ce.myAnchorAAtom == "[Self]") // legacy
								ce.myAnchorAAtom = containingAtom.uid;
						}
						if (ce.myAnchorMode == ControlEntryAnchored.ANCHORMODE_BLEND)
						{
							ce.myAnchorBAtom = ceclass["AnchorBAtom"].Value;
							ce.myAnchorBControl = ceclass["AnchorBControl"].Value;
							ce.myBlendRatio = ceclass["BlendRatio"].AsFloat;

							if (ce.myAnchorBAtom == "[Self]") // legacy
								ce.myAnchorBAtom = containingAtom.uid;
						}
						ce.Initialize();

						state.myControlEntries.Add(cc, ce);
					}
					for (int j=0; j<layer.myControlCaptures.Count; ++j)
					{
						if (!state.myControlEntries.ContainsKey(layer.myControlCaptures[j]))
							layer.myControlCaptures[j].CaptureEntry(state);
					}
				}

				// load morph captures
				if (layer.myMorphCaptures.Count > 0)
				{
					JSONClass melist = st["MorphEntries"].AsObject;
					foreach (string key in melist.Keys)
					{
						MorphCapture mc = null;
						mc = layer.myMorphCaptures.Find(x => x.mySID == key);
						if (mc == null)
						{
							continue;
						}
						float me = melist[key].AsFloat;
						state.myMorphEntries.Add(mc, me);
					}
					for (int j=0; j<layer.myMorphCaptures.Count; ++j)
					{
						if (!state.myMorphEntries.ContainsKey(layer.myMorphCaptures[j]))
							layer.myMorphCaptures[j].CaptureEntry(state);
					}
				}
			}

			// blend to initial state
			if (jc.HasKey("InitialState"))
			{
				State initial;
				if (layer.myStates.TryGetValue(jc["InitialState"].Value, out initial))
				{
					layer.SetState(initial);
				}
			}
			return layer;
		}

		private void LoadTransitions(Layer layer, JSONClass layerObj)
		{
			JSONArray slist = layerObj["States"].AsArray;
			for (int i=0; i<slist.Count; ++i)
			{
				JSONClass st = slist[i].AsObject;
				State source;
				if (!layer.myStates.TryGetValue(st["Name"], out source))
					continue;

				JSONArray tlist = st["Transitions"].AsArray;
				for (int j=0; j<tlist.Count; ++j)
				{
					JSONClass tclass = tlist[j].AsObject;

					Animation targetAnimation;
					if(String.Equals(tclass["TargetAnimation"], "[Self]"))
						targetAnimation = source.myLayer.myAnimation;
					else if(!myAnimations.TryGetValue(tclass["TargetAnimation"], out targetAnimation))
						continue;

					Layer targetLayer;
					if(String.Equals(tclass["TargetLayer"], "[Self]"))
						targetLayer = source.myLayer;
					else if(!targetAnimation.myLayers.TryGetValue(tclass["TargetLayer"], out targetLayer))
						continue;

					State target;
					if(!targetLayer.myStates.TryGetValue(tclass["TargetState"], out target))
						continue;

					if(String.Equals(tclass["Type"], "Direct") || tclass["Type"] == null) {
						Transition transition = new Transition(source, target);
						transition.myProbability = tclass["Probability"].AsFloat;
						transition.myDuration = tclass["Duration"].AsFloat;
						transition.myDurationNoise = tclass["DurationNoise"].AsFloat;
						transition.myEaseInDuration = tclass["EaseInDuration"].AsFloat;
						transition.myEaseOutDuration = tclass["EaseOutDuration"].AsFloat;
						transition.mySourceState = source;
						transition.myTargetState = target;

						JSONClass synctlist = tclass["SyncTargets"].AsObject;
						foreach (string key in synctlist.Keys) {
							Layer syncLayer;
							if (!targetAnimation.myLayers.TryGetValue(key, out syncLayer))
								continue;
							State syncState;
							if (!syncLayer.myStates.TryGetValue(synctlist[key], out syncState))
								continue;
							transition.mySyncTargets[syncLayer] = syncState;
						}

						JSONClass msglist = tclass["Messages"].AsObject;
						foreach (string key in msglist.Keys) {
							Role role;
							if (!myRoles.TryGetValue(key, out role))
								continue;
							transition.myMessages[role] = msglist[key];
						}
						source.myTransitions.Add(transition);
					} else {
						IndirectTransition transition = new IndirectTransition(source, target);
						transition.myProbability = tclass["Probability"].AsFloat;
						transition.mySourceState = source;
						transition.myTargetState = target;
						source.myTransitions.Add(transition);
					}
				}
			}
		}

		private void LoadRoles(JSONClass jc)
		{
			JSONArray rlist = jc["Roles"].AsArray;
			for (int i=0; i<rlist.Count; ++i)
			{
				Role r;
				JSONClass rclass = rlist[i].AsObject;
				r = new Role(rclass["Name"]);
				if(rclass["Person"] != "") {
					Atom person = SuperController.singleton.GetAtoms().Find(a => String.Equals(a.name, rclass["Person"]));
					if(person != null) {
						r.myPerson = person;
					}
				}
				myRoles[r.myName] = r;
			}
		}

		private void LoadMessages(JSONClass jc)
		{
			JSONArray mlist = jc["Messages"].AsArray;

			for (int i=0; i<mlist.Count; ++i)
			{
				JSONClass mclass = mlist[i].AsObject;

				Animation targetAnimation;
				if(!myAnimations.TryGetValue(mclass["TargetAnimation"], out targetAnimation))
					continue;

				Layer targetLayer;
				if(!targetAnimation.myLayers.TryGetValue(mclass["TargetLayer"], out targetLayer))
					continue;

				State target;
				if(!targetLayer.myStates.TryGetValue(mclass["TargetState"], out target))
					continue;

				Message message = new Message(mclass["Name"]);
				message.myMessageString = mclass["MessageString"];

				JSONArray srcstlist = mclass["SourceStates"].AsArray;
				for (int j=0; j<srcstlist.Count; ++j)
				{
					JSONClass src = srcstlist[j].AsObject;
					Animation srcan = myAnimations[src["AnimationName"]];
					Layer srcly = srcan.myLayers[src["LayerName"]];
					State srcst = srcly.myStates[src["StateName"]];
					string qualStateName = $"{srcan.myName}.{srcly.myName}.{srcst.myName}";
					message.mySourceStates[qualStateName] = srcst;
				}

				message.myTargetState = target;

				myMessages[message.myName] = message;
			}
		}
    }
}
