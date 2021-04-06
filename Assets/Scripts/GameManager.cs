using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class GameManager : MonoBehaviour
{
	public GameObject resBoxPrefab;
	public GameObject citizenPrefab;
	public TMP_Text gdpLabel;
	public float fieldRadius = 25f;
	public int citizenSpawnCount = 3;
	public int resBoxSpawnCount = 80;
	public float citizenStartingCapital = 30f;
	public float priceMagnifier = 100f;
	public float travelCostPerMeter = 1f;

	float _totalExpenditures;
	List<Citizen> _citizens;

	/// <summary>
	/// Represents a real-time snapshot of the nominal GDP.
	/// </summary>
	public float GDP => _totalExpenditures;

	public void RefreshGDP()
	{
		gdpLabel.text = $"ƒ{GDP:n2} GDP";
	}

	public void RegisterConsumption(float amountSpent)
	{
		_totalExpenditures += amountSpent;
		RefreshGDP();
	}

    // Start is called before the first frame update
    void Start()
    {
		_totalExpenditures = citizenSpawnCount * citizenStartingCapital; // the only bit of "government spending" to account into GDP for now
		RefreshGDP();

		SpawnCitizens();
		SpawnResBoxes();
    }

	void SpawnCitizens()
	{
		_citizens = new List<Citizen>();
		for (int i = 0; i < citizenSpawnCount; i++)
		{
			Vector3 target = new Vector3(0f, 1f, 0f);
			target.x = Random.value * fieldRadius * 2 - fieldRadius;
			target.z = Random.value * fieldRadius * 2 - fieldRadius;
			var go = Instantiate(citizenPrefab, target, Quaternion.identity);
			go.name = $"Citizen {i+1}";
			Citizen citizen = go.GetComponent<Citizen>();
			citizen.AddMoney(citizenStartingCapital);
			citizen.GetComponent<Renderer>().material.color = Color.HSVToRGB(1f / citizenSpawnCount * (i + 1), 1f, 1f);
			_citizens.Add(citizen);
		}
	}

	void SpawnResBoxes()
	{
		for (int i = 0; i < resBoxSpawnCount; i++)
		{
			Vector3 target = new Vector3(0f, .5f, 0f);
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
