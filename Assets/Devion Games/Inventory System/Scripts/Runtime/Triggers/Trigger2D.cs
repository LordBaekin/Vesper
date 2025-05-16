using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DevionGames.InventorySystem
{
    public class Trigger2D : Trigger
    {
        public override bool CanUse()
        {
            //Return false if the trigger is already used
            if (InUse || (Trigger.CurrentUsedTrigger != null && Trigger.CurrentUsedTrigger.InUse))
            {
                InventoryManager.Notifications.inUse.Show();
                return false;
            }

            //Return false if the player is not in range
            if (!InRange)
            {
                InventoryManager.Notifications.toFarAway.Show();
                return false;
            }
            return true;
        }

        protected override void CreateTriggerCollider()
        {
            Vector2 position = Vector2.zero;
            GameObject handlerGameObject = new("TriggerRangeHandler");
            handlerGameObject.transform.SetParent(transform, false);
            handlerGameObject.layer = 2; // Ignore Raycast

            if (TryGetComponent<Collider2D>(out Collider2D collider))
            {
                position = collider.bounds.center;
                position.y = (collider.bounds.center - collider.bounds.extents).y;
                position = transform.InverseTransformPoint(position);
            }

            CircleCollider2D circleCollider = gameObject.AddComponent<CircleCollider2D>();
            circleCollider.isTrigger = true;
            circleCollider.offset = position;
            Vector3 scale = transform.lossyScale;
            circleCollider.radius = useDistance / Mathf.Max(scale.x, scale.y);
            if (!TryGetComponent<Rigidbody2D>(out _))
            {
                Rigidbody2D rigidbody = gameObject.AddComponent<Rigidbody2D>();
                rigidbody.bodyType = RigidbodyType2D.Kinematic;
            }
        }


        protected virtual void OnTriggerEnter2D(Collider2D other)
        {
            //Check if the collider other is player 
            if (InventoryManager.Current.GetPlayerInfo().gameObject.CompareTag(other.tag))
            {
                //Set that player is in range
                InRange = true;
            }
        }

        protected virtual void OnTriggerExit2D(Collider2D other)
        {
            //Check if the collider other is player
            if (InventoryManager.Current.GetPlayerInfo().gameObject.CompareTag(other.tag))
            {
                //Set that player is out of range
                InRange = false;
            }
        }
    }
}