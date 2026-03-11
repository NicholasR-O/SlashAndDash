using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class CarRamHitbox : MonoBehaviour
{
    private CarController owner;

    private void Awake()
    {
        owner = GetComponentInParent<CarController>();

        BoxCollider box = GetComponent<BoxCollider>();
        if (box != null)
            box.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        owner?.TryApplyRamImpact(other, transform.position);
    }

    private void OnTriggerStay(Collider other)
    {
        owner?.TryApplyRamImpact(other, transform.position);
    }
}
