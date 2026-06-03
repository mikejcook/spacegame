using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DGB_Example;

namespace DGB_Example {
	public class ShipThruster : MonoBehaviour {

		// select all that apply via the UnityEditor
		public bool mainThruster = false;
		public bool reverseThruster = false;
		public bool rotateCWThruster = false;
		public bool rotateCCWThruster = false;
		public bool strafeLeftThruster = false;
		public bool strafeRightThruster = false;

		private SpriteRenderer[] childSRs;
		private SpriteRenderer mySR;

		void Start () {
			mySR = GetComponent<SpriteRenderer> ();

			// Find all child gameobjects with sprite renderers.
			// We shall assume they are glows that must be locked to parent state.
			childSRs = GetComponentsInChildren<SpriteRenderer>();

			// Turn off all sprites. Engines start off, unless using idle glow.
			mySR.enabled = false;
			foreach (SpriteRenderer sr in childSRs) sr.enabled = false;
		}

		public void Activate(){
			mySR.enabled = true;
			foreach (SpriteRenderer sr in childSRs) sr.enabled = true;
		}

		public void Deactivate(){
			mySR.enabled = false;
			foreach (SpriteRenderer sr in childSRs) sr.enabled = false;
		}
	}
}
