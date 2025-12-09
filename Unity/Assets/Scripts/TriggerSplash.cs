using UnityEngine;

public class TriggerSplash : MonoBehaviour
{
    public GameObject splash;

    private void OnCollisionEnter(Collision collision)
    {
        // Check if the object we hit has the "Display" tag
        if (collision.collider.CompareTag("Display"))
        {
            Debug.Log("OnCollisionEnter");

            Vector3 hitPoint = transform.position;

            // Make sure we have at least one contact point
            if (collision.contacts.Length > 0)
            {
                hitPoint = collision.contacts[0].point;
                Debug.Log("Collision at: " + hitPoint);
            }

            // STOP movement immediately
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.constraints = RigidbodyConstraints.FreezeAll;
                rb.isKinematic = true;
            }

            // Enable and unparent splash
            if (splash != null)
            {
                splash.SetActive(true);

                // Remove parent so it stands independently
                splash.transform.SetParent(null);

                // Move splash to impact point
                splash.transform.position = hitPoint;
            }
            else
            {
                Debug.LogWarning("Splash object is not assigned in the Inspector.");
            }

            // Optional: disable this object
            // gameObject.SetActive(false);
        }
    }
}
