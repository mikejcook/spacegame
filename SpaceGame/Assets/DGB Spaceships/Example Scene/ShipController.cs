using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DGB_Example;

namespace DGB_Example {

	[RequireComponent(typeof(Rigidbody2D))]
	public class ShipController : MonoBehaviour {
		
		// is the player controlling this ship? Please only control one ship at a time.
		public bool controlledByPlayer = true; 

		public List<ShipThruster> mainThrusters = new List<ShipThruster>();
		public List<ShipThruster> reverseThrusters = new List<ShipThruster>();
		public List<ShipThruster> rotateCWThrusters = new List<ShipThruster>();
		public List<ShipThruster> rotateCCWThrusters = new List<ShipThruster>();
		public List<ShipThruster> strafeLeftThrusters = new List<ShipThruster>();
		public List<ShipThruster> strafeRightThrusters = new List<ShipThruster>();


		private float mainThrustPower = 5f; // force added to RB
		private float reversePower = 2.5f; // force when reversing
		private float rotatePower = 4f; // degrees per second per second? Or whatever adding torque to a RB does.
		private float strafePower = 3f;  // force when strafing

		private Rigidbody2D rb;

		private List<SpaceWeapon> weapons;

		void Start () {
			rb = GetComponent<Rigidbody2D> ();

			// Find all child thruster objects and put 'em in the correct list, please.
			ShipThruster[] all_thrusters = GetComponentsInChildren<ShipThruster>();

			foreach (ShipThruster t in all_thrusters) {
				if (t.mainThruster) mainThrusters.Add (t);
				if (t.reverseThruster) reverseThrusters.Add (t);
				if (t.rotateCWThruster) rotateCWThrusters.Add (t);
				if (t.rotateCCWThruster) rotateCCWThrusters.Add (t);
				if (t.strafeLeftThruster) strafeLeftThrusters.Add (t);
				if (t.strafeRightThruster) strafeRightThrusters.Add (t);
			}

			weapons = new List<SpaceWeapon>( GetComponentsInChildren<SpaceWeapon>() );
		}

		void Update () {

			if (controlledByPlayer) {
				
				// shootin' control -- use SPACE to fire weapons!
				if (Input.GetKeyDown(KeyCode.Space)) FireWeapons ();

				// Using WASD controls, more or less.

				// Main thruster forward.
				// Key on and key off will turn thrusters on and off (this is just the visuals).
				if (Input.GetKeyDown(KeyCode.W)) foreach (ShipThruster t in mainThrusters) t.Activate ();
				else if (Input.GetKeyUp(KeyCode.W)) foreach (ShipThruster t in mainThrusters) t.Deactivate ();

				// Holding the key down will apply the thrust force per-frame.
				if (Input.GetKey(KeyCode.W)) rb.AddRelativeForce (Vector2.up * Time.deltaTime * mainThrustPower);


				// Reverse thrusters.
				if (Input.GetKeyDown(KeyCode.S)) foreach (ShipThruster t in reverseThrusters) t.Activate ();
				else if (Input.GetKeyUp(KeyCode.S)) foreach (ShipThruster t in reverseThrusters) t.Deactivate ();

				if (Input.GetKey(KeyCode.S)) rb.AddRelativeForce (Vector2.down * Time.deltaTime * reversePower);

				// Strafe Left - using "Q" which is, okay, non-standard
				if (Input.GetKeyDown(KeyCode.Q)) foreach (ShipThruster t in strafeLeftThrusters) t.Activate ();
				else if (Input.GetKeyUp(KeyCode.Q)) foreach (ShipThruster t in strafeLeftThrusters) t.Deactivate ();

				if (Input.GetKey(KeyCode.Q)) rb.AddRelativeForce (Vector2.left * Time.deltaTime * strafePower);

				// Strafe Right - using "E" which, again.
				if (Input.GetKeyDown(KeyCode.E)) foreach (ShipThruster t in strafeRightThrusters) t.Activate ();
				else if (Input.GetKeyUp(KeyCode.E)) foreach (ShipThruster t in strafeRightThrusters) t.Deactivate ();

				if (Input.GetKey(KeyCode.E)) rb.AddRelativeForce (Vector2.right * Time.deltaTime * strafePower);


				// Rotate CCW (aka "turn left")
				if (Input.GetKeyDown(KeyCode.A)) foreach (ShipThruster t in rotateCCWThrusters) t.Activate ();
				else if (Input.GetKeyUp(KeyCode.A)) foreach (ShipThruster t in rotateCCWThrusters) t.Deactivate ();

				if (Input.GetKey (KeyCode.A)) rb.AddTorque ( rotatePower * Time.deltaTime);

				// Rotate CW (aka "turn right")
				if (Input.GetKeyDown(KeyCode.D)) foreach (ShipThruster t in rotateCWThrusters) t.Activate ();
				else if (Input.GetKeyUp(KeyCode.D)) foreach (ShipThruster t in rotateCWThrusters) t.Deactivate ();

				if (Input.GetKey(KeyCode.D)) rb.AddTorque ( -rotatePower * Time.deltaTime);
			}
		}

		private void FireWeapons(){
			foreach (SpaceWeapon weapon in weapons) {
				weapon.Shoot ();
			}
		}
	}
}

