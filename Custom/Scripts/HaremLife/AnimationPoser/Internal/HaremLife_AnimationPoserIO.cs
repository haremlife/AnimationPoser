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
			if (myCurrentState != null)
				jc["InitialState"] = myCurrentState.myName;
			jc["Paused"].AsBool = myPlayPaused.val;

			JSONArray anims = new JSONArray();

			foreach(var an in myAnimations)
			{
				Animation animation = an.Value;
				JSONClass anim = new JSONClass();
				anim["Name"] = animation.myName;
				anim["Speed"].AsFloat = animation.mySpeed;
				JSONArray llist = new JSONArray();
				foreach(var l in animation.myLayers){
					llist.Add("", SaveLayer(l.Value));
				}
				anim["Layers"] = llist;

				// save roles
				if (animation.myRoles.Keys.Count > 0)
				{
					JSONArray rlist = new JSONArray();
					foreach (var r in animation.myRoles)
					{
						Role role = r.Value;
						JSONClass rclass = new JSONClass();
						rclass["Name"] = role.myName;
						if(role.myPerson != null) {
							rclass["Person"] = role.myPerson.name;
						} else {
							rclass["Person"] = "";
						}
						// ccclass["Person"] = r.myPerson;
						rlist.Add("", rclass);
					}
					anim["Roles"] = rlist;
				}
				anims.Add("", anim);
			}

			jc["Animations"] = anims;

			return jc;
		}

		private void LoadAnimations(JSONClass jc)
		{
			// load info
			myAnimations.Clear();
			int version = jc["Info"].AsObject["Version"].AsInt;

			// load captures
			JSONArray anims = jc["Animations"].AsArray;
			for (int l=0; l<anims.Count; ++l)
			{
				JSONClass anim = anims[l].AsObject;
				myCurrentAnimation = CreateAnimation(anim["Name"]);
				myCurrentAnimation.mySpeed = anim["Speed"].AsFloat;
			}

			for (int l=0; l<anims.Count; ++l)
			{
				JSONClass anim = anims[l].AsObject;

				// load roles
				if (anim.HasKey("Roles"))
				{
					JSONArray rlist = anim["Roles"].AsArray;
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
						myCurrentAnimation.myRoles[r.myName] = r;
					}
				}
			}

			for (int l=0; l<anims.Count; ++l)
			{
				JSONClass anim = anims[l].AsObject;
				if (!myAnimations.TryGetValue(anim["Name"], out myCurrentAnimation))
					continue;
				JSONArray layers = anim["Layers"].AsArray;
				for(int m=0; m<layers.Count; m++)
				{
					JSONClass layer = layers[m].AsObject;
					LoadLayer(layer, false);
				}
			}

			for (int l=0; l<anims.Count; ++l)
			{
				JSONClass anim = anims[l].AsObject;
				if (!myAnimations.TryGetValue(anim["Name"], out myCurrentAnimation))
					continue;
				JSONArray layers = anim["Layers"].AsArray;
				for(int m=0; m<layers.Count; m++)
				{
					JSONClass layer = layers[m].AsObject;
					LoadTransitions(layer);
					LoadMessages(layer);
				}
			}

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
		}

		private JSONClass SaveLayer(Layer layerToSave)
		{
			JSONClass jc = new JSONClass();
			// save settings
			if (myCurrentState != null)
				jc["InitialState"] = myCurrentState.myName;
			jc["Paused"].AsBool = myPlayPaused.val;

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
					Transition transition = state.myTransitions[i];
					JSONClass t = new JSONClass();
					t["SourceState"] = transition.mySourceState.myName;
					t["TargetState"] = transition.myTargetState.myName;
					if(transition.myTargetState.myLayer == transition.mySourceState.myLayer)
						t["TargetLayer"] = "[Self]";
					else
						t["TargetLayer"] = transition.myTargetState.myLayer.myName;
					if(transition.myTargetState.myAnimation == transition.mySourceState.myAnimation)
						t["TargetAnimation"] = "[Self]";
					else
						t["TargetAnimation"] = transition.myTargetState.myAnimation.myName;
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

			// save messages
			JSONArray mlist = new JSONArray();
			foreach(var mm in layerToSave.myMessages)
			{
				Message message = mm.Value;

				JSONClass m = new JSONClass();

				m["Name"] = message.myName;
				m["MessageString"] = message.myMessageString;

				JSONArray srcstlist = new JSONArray();
				foreach(var srcst in message.mySourceStates) {
					JSONClass src = new JSONClass();
					src["Name"] = srcst.Value.myName;
					srcstlist.Add(src);
				}
				m["SourceStates"] = srcstlist;
				m["TargetState"] = message.myTargetState.myName;

				string firstSrcState = message.mySourceStates.Keys.ToList()[0];
				if(message.myTargetState.myLayer == message.mySourceStates[firstSrcState].myLayer)
					m["TargetLayer"] = "[Self]";
				else
					m["TargetLayer"] = message.myTargetState.myLayer.myName;
				if(message.myTargetState.myAnimation == message.mySourceStates[firstSrcState].myAnimation)
					m["TargetAnimation"] = "[Self]";
				else
					m["TargetAnimation"] = message.myTargetState.myAnimation.myName;
				m["Duration"].AsFloat = message.myDuration;
				m["DurationNoise"].AsFloat = message.myDurationNoise;
				m["EaseInDuration"].AsFloat = message.myEaseInDuration;
				m["EaseOutDuration"].AsFloat = message.myEaseOutDuration;
				m["Probability"].AsFloat = message.myProbability;

				JSONClass synct = new JSONClass();
				foreach (var syncl in message.mySyncTargets) {
					synct[syncl.Key.myName] = syncl.Value.myName;
				}
				m["SyncTargets"] = synct;

				mlist.Add(m);
			}
			layer["Messages"] = mlist;

			jc["Layer"] = layer;

			return jc;
		}

		private Layer LoadLayer(JSONClass jc, bool newName)
		{
			// reset
            bool overwrite = false;
			if(myCurrentLayer != null & overwrite){
				if(myCurrentLayer.myStates.Count > 0){
					foreach (var s in myCurrentLayer.myStates)
					{
						State state = s.Value;
						state.EnterBeginTrigger.Remove();
						state.EnterEndTrigger.Remove();
						state.ExitBeginTrigger.Remove();
						state.ExitEndTrigger.Remove();
					}
				}
				myCurrentLayer.myControlCaptures.Clear();
				myCurrentLayer.myMorphCaptures.Clear();
				myCurrentLayer.myStates.Clear();
				myCurrentLayer.myClock = 0.0f;
				myCurrentState = null;
			}

			// load captures
			JSONClass layer = jc["Layer"].AsObject;

			if(!overwrite)
                if(newName)
                    myCurrentLayer = CreateLayer(FindNewLayerName());
                else
                    myCurrentLayer = CreateLayer(layer["Name"]);

			// load captures
			if (layer.HasKey("ControlCaptures"))
			{
				JSONArray cclist = layer["ControlCaptures"].AsArray;
				for (int i=0; i<cclist.Count; ++i)
				{
					ControlCapture cc;
					JSONClass ccclass = cclist[i].AsObject;
					cc = new ControlCapture(this, ccclass["Name"]);
					cc.myApplyPosition = ccclass["ApplyPos"].AsBool;
					cc.myApplyRotation = ccclass["ApplyRot"].AsBool;

					if (cc.IsValid())
						myCurrentLayer.myControlCaptures.Add(cc);
				}
			}
			if (layer.HasKey("MorphCaptures"))
			{
				JSONArray mclist = layer["MorphCaptures"].AsArray;
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
						myCurrentLayer.myMorphCaptures.Add(mc);
				}
			}

			// load states
			JSONArray slist = layer["States"].AsArray;
			for (int i=0; i<slist.Count; ++i)
			{
				// load state
				JSONClass st = slist[i].AsObject;
				State state = new State(this, st["Name"]) {
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


				if (myCurrentLayer.myStates.ContainsKey(state.myName))
					continue;
				myCurrentLayer.myStates[state.myName] = state;

				// load control captures
				if (myCurrentLayer.myControlCaptures.Count > 0)
				{
					JSONClass celist = st["ControlEntries"].AsObject;
					foreach (string ccname in celist.Keys)
					{
						ControlCapture cc = myCurrentLayer.myControlCaptures.Find(x => x.myName == ccname);
						if (cc == null)
							continue;

						JSONClass ceclass = celist[ccname].AsObject;
						ControlEntryAnchored ce = new ControlEntryAnchored(this, ccname, state, cc);
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
					for (int j=0; j<myCurrentLayer.myControlCaptures.Count; ++j)
					{
						if (!state.myControlEntries.ContainsKey(myCurrentLayer.myControlCaptures[j]))
							myCurrentLayer.myControlCaptures[j].CaptureEntry(state);
					}
				}

				// load morph captures
				if (myCurrentLayer.myMorphCaptures.Count > 0)
				{
					JSONClass melist = st["MorphEntries"].AsObject;
					foreach (string key in melist.Keys)
					{
						MorphCapture mc = null;
						mc = myCurrentLayer.myMorphCaptures.Find(x => x.mySID == key);
						if (mc == null)
						{
							continue;
						}
						float me = melist[key].AsFloat;
						state.myMorphEntries.Add(mc, me);
					}
					for (int j=0; j<myCurrentLayer.myMorphCaptures.Count; ++j)
					{
						if (!state.myMorphEntries.ContainsKey(myCurrentLayer.myMorphCaptures[j]))
							myCurrentLayer.myMorphCaptures[j].CaptureEntry(state);
					}
				}
			}

			// load settings
			myPlayPaused.valNoCallback = jc.HasKey("Paused") && jc["Paused"].AsBool;
			myPlayPaused.setCallbackFunction(myPlayPaused.val);

			SetLayer(myCurrentLayer);

			// blend to initial state
			if (jc.HasKey("InitialState"))
			{
				State initial;
				if (myCurrentLayer.myStates.TryGetValue(jc["InitialState"].Value, out initial))
				{
					myCurrentLayer.SetState(initial);
					myMainState.valNoCallback = initial.myName;
				}
			}
			return myCurrentLayer;
		}

		private void LoadTransitions(JSONClass jc)
		{
			JSONClass layer = jc["Layer"].AsObject;
			JSONArray slist = layer["States"].AsArray;
			for (int i=0; i<slist.Count; ++i)
			{
				JSONClass st = slist[i].AsObject;
				State source;
				if (!myCurrentLayer.myStates.TryGetValue(st["Name"], out source))
					continue;

				JSONArray tlist = st["Transitions"].AsArray;
				for (int j=0; j<tlist.Count; ++j)
				{
					JSONClass tclass = tlist[j].AsObject;

					Animation targetAnimation;
					if(String.Equals(tclass["TargetAnimation"], "[Self]"))
						targetAnimation = myCurrentAnimation;
					else if(!myAnimations.TryGetValue(tclass["TargetAnimation"], out targetAnimation))
						continue;

					Layer targetLayer;
					if(String.Equals(tclass["TargetLayer"], "[Self]"))
						targetLayer = myCurrentLayer;
					else if(!targetAnimation.myLayers.TryGetValue(tclass["TargetLayer"], out targetLayer))
						continue;

					State target;
					if(!targetLayer.myStates.TryGetValue(tclass["TargetState"], out target))
						continue;

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
						if (!targetAnimation.myRoles.TryGetValue(key, out role))
							continue;
						transition.myMessages[role] = msglist[key];
					}

					source.myTransitions.Add(transition);
				}
			}
		}

		private void LoadMessages(JSONClass jc)
		{
			JSONClass layer = jc["Layer"].AsObject;

			if (!myCurrentAnimation.myLayers.TryGetValue(layer["Name"], out myCurrentLayer))
				return;

			JSONArray mlist = layer["Messages"].AsArray;
			for (int i=0; i<mlist.Count; ++i)
			{
				JSONClass mclass = mlist[i].AsObject;

				Animation targetAnimation;
				if(String.Equals(mclass["TargetAnimation"], "[Self]"))
					targetAnimation = myCurrentAnimation;
				else if(!myAnimations.TryGetValue(mclass["TargetAnimation"], out targetAnimation))
					continue;

				Layer targetLayer;
				if(String.Equals(mclass["TargetLayer"], "[Self]"))
					targetLayer = myCurrentLayer;
				else if(!targetAnimation.myLayers.TryGetValue(mclass["TargetLayer"], out targetLayer))
					continue;

				State target;
				if(!targetLayer.myStates.TryGetValue(mclass["TargetState"], out target))
					continue;

				Message message = new Message(mclass["Name"]);
				message.myMessageString = mclass["MessageString"];
				message.myProbability = mclass["Probability"].AsFloat;
				message.myDuration = mclass["Duration"].AsFloat;
				message.myDurationNoise = mclass["DurationNoise"].AsFloat;
				message.myEaseInDuration = mclass["EaseInDuration"].AsFloat;
				message.myEaseOutDuration = mclass["EaseOutDuration"].AsFloat;

				JSONArray srcstlist = mclass["SourceStates"].AsArray;
				for (int j=0; j<srcstlist.Count; ++j)
				{
					JSONClass src = srcstlist[j].AsObject;
					State srcst = myCurrentLayer.myStates[src["Name"]];
					message.mySourceStates[srcst.myName] = srcst;
				}

				message.myTargetState = target;

				JSONClass synctlist = mclass["SyncTargets"].AsObject;
				foreach (string key in synctlist.Keys) {
					Layer syncLayer;
					if (!targetAnimation.myLayers.TryGetValue(key, out syncLayer))
						continue;
					State syncState;
					if (!syncLayer.myStates.TryGetValue(synctlist[key], out syncState))
						continue;
					message.mySyncTargets[syncLayer] = syncState;
				}

				myCurrentLayer.myMessages[message.myName] = message;
			}
		}
    }
}