using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Citizen : MonoBehaviour
{
	Vector3 origin;
	Vector3 target;

	public float fieldRadius = 20f;
	public float speed = 5f;
	public float resBoxValuation = 0f;
	public int resBoxesCollected;

    // Start is called before the first frame update
    void Start()
    {
		origin = transform.position;
		target = origin;

		GetComponent<Renderer>().material.color = Random.ColorHSV(0, 1, .5f, 1, 1, 1, 1, 1);

		resBoxValuation = Random.value;
    }

    // Update is called once per frame
    void Update()
    {
		// idling
		if (Random.value < .001f)
		{
			origin = transform.position;
			target.x = Random.value * fieldRadius * 2 - fieldRadius;
			target.z = Random.value * fieldRadius * 2 - fieldRadius;
		}

		// look for boxes though, otherwise idle
		var boxes = FindObjectsOfType<ResBox>();
		if (boxes.Length > 0)
		{
			ResBox closestBox = boxes[0];
			float closestDist = Vector3.Distance(transform.position, boxes[0].transform.position);
			foreach (var box in boxes)
			{
				float dist = Vector3.Distance(transform.position, box.transform.position);
				if (dist < closestDist)
				{
					closestDist = dist;
					closestBox = box;
				}
			}
			target = closestBox.transform.position;
			target.y = origin.y;
		}

		if (Vector3.Distance(origin, target) > .01f)
			transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
	}

	void OnTriggerEnter(Collider collision)
	{
		var box = collision.gameObject.GetComponent<ResBox>();
		if (box)
		{
			resBoxesCollected++;
			Destroy(collision.gameObject);
		}
	}
}
