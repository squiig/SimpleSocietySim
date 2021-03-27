using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
	public GameObject resBoxPrefab;
	public float fieldRadius = 20f;
	public int resBoxSpawnCount = 100;

    // Start is called before the first frame update
    void Start()
    {
		SpawnResBoxes();
    }

	void SpawnResBoxes()
	{
		for (int i = 0; i < resBoxSpawnCount; i++)
		{
			Vector3 target = new Vector3(0f, 0.5f, 0f);
			target.x = Random.value * fieldRadius * 2 - fieldRadius;
			target.z = Random.value * fieldRadius * 2 - fieldRadius;
			Quaternion rot = Random.rotation;
			rot.x = 0f;
			rot.z = 0f;
			Instantiate(resBoxPrefab, target, rot);
		}
	}

    // Update is called once per frame
    void Update()
    {
        
    }
}
