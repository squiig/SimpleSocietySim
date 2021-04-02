using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
	public GameObject resBoxPrefab;
	public GameObject citizenPrefab;
	public float fieldRadius = 20f;
	public int citizenSpawnCount = 5;
	public int resBoxSpawnCount = 100;
	public float citizenStartingCapital = 20f;
	public float priceMagnifier = 100f;
	public float transportCostPerMeter = 1f;

    // Start is called before the first frame update
    void Start()
    {
		SpawnCitizens();
		SpawnResBoxes();
    }

	void SpawnCitizens()
	{
		for (int i = 0; i < citizenSpawnCount; i++)
		{
			Vector3 target = new Vector3(0f, 1f, 0f);
			target.x = Random.value * fieldRadius * 2 - fieldRadius;
			target.z = Random.value * fieldRadius * 2 - fieldRadius;
			var go = Instantiate(citizenPrefab, target, Quaternion.identity);
			Citizen citizen = go.GetComponent<Citizen>();
			citizen.AddMoney(citizenStartingCapital);
		}
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
