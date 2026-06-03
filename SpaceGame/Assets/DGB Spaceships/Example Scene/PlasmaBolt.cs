using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DGB_Example;

namespace DGB_Example {
	public class PlasmaBolt : MonoBehaviour {
		public float lifetime = 2f;
		public float speed = 100f;
	
		public void Launch(){
			// Plasma bolt moves at constant speed, so just apply it upon launch.
			GetComponent<Rigidbody2D> ().AddRelativeForce (Vector2.up * speed);
		}

		void Update () {
			
			// destroy self when life counter is done.
			lifetime -= Time.deltaTime;
			if (lifetime <= 0f) {
				Destroy (gameObject);
			}
		}

		void OnCollisionEnter2D( Collision2D collision){

			// If this runs into anything that isn't a PlasmaBolt, it'll disappear.
			if ( !collision.gameObject.name.Contains("PlasmaBolt") ) {
				Destroy (gameObject);
			}

			// Make stuff go boom here.
		}
	}
}
