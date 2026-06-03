using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DGB_Example;

namespace DGB_Example {
	public class SpaceWeapon : MonoBehaviour {

		public GameObject shotPrefab;
		public int weaponGroup;
		public float shotCooldown; // wait this long between shots.
		private float shotTimer = 0f;

		public void Shoot(){
			if (shotTimer <= 0f) {
				GameObject shot = Instantiate (shotPrefab, transform.position, transform.rotation);

				// bolt inherits parent ship's velocity because Physics. (Maybe you don't want this?)
				shot.GetComponent<Rigidbody2D> ().linearVelocity = transform.parent.GetComponent<Rigidbody2D> ().linearVelocity;

				// make sure bolt never collides with parent (unless you're masochistic?)
				Physics2D.IgnoreCollision( shot.GetComponent<Collider2D>(), transform.parent.GetComponent<Collider2D>() );

				// tell the shot to do anything it needs to do to start getting on its way.
				shot.SendMessage ("Launch");
			}
		}

		void Update() {
			if (shotTimer >= 0f) {
				shotTimer -= Time.deltaTime;
			}	
		}
	}
}
