using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using System.Linq;

public class GameManager : MonoBehaviour
{
	public GameObject resBoxPrefab;
	public GameObject citizenPrefab;
	public TMP_Text gdpLabel;
	public TMP_Text totalMoneyLabel;
	public TMP_Text averageValuationLabel;
	public TMP_Text averageTradingPriceLabel;
	public float fieldRadius = 25f;
	public int citizenSpawnCount = 3;
	public int resBoxSpawnCount = 80;
	public float citizenStartingCapitalMin = 10f;
	public float citizenStartingCapitalMax = 100f;
	public float priceMagnifier = 100f;
	public float travelCostPerMeter = 1f;

	float _totalExpenditures;
	List<Citizen> _citizens;
	List<float> _historicalUnitPrices;
	float _gdpTimer;
	[SerializeField] float _gdpPeriod = 10f;
	float _gdpPerPeriod;
	float _previousMeasuredGdp;

	/// <summary>
	/// Represents a real-time snapshot of the total nominal GDP.
	/// </summary>
	public float AllTimeNominalGDP => _totalExpenditures;

	public float NominalGDPPerPeriod => _gdpPerPeriod;

	public static string CurrencySymbol => "€"; // ƒ

	public static string ResBoxSymbol => "BOX";

	public float CurrentAverageResBoxTradingPrice => _historicalUnitPrices.Sum() / _historicalUnitPrices.Count;

	public float CurrentAverageResBoxValuation => _citizens.Select(x => x.baseResBoxValuation).Sum() / _citizens.Count;

	public static string FormatMoney(float amount)
	{
		return $"{CurrencySymbol}{amount:n2}";
	}

	public void RegisterSale(float amountSpent, int amountSold)
	{
		_totalExpenditures += amountSpent;

		float unitPrice = amountSpent / amountSold;

		_historicalUnitPrices.Add(unitPrice);
	}

	void RefreshGDP()
	{
		float gdp = NominalGDPPerPeriod;
		gdpLabel.text = $"{FormatMoney(gdp == 0 ? 0 : gdp / citizenSpawnCount)} GDP per capita";
	}

	void RefreshTotalMoneyLabel()
	{
		float totalMoney = _citizens.Select(x => x.MoneyInWallet).Sum();
		totalMoneyLabel.text = $"{FormatMoney(totalMoney)} total";
	}

	void RefreshAverages()
	{
		averageTradingPriceLabel.text = $"{FormatMoney(CurrentAverageResBoxTradingPrice)} avg. price";
		averageValuationLabel.text = $"{CurrentAverageResBoxValuation:n3} avg. valuation";
	}

	void Awake()
	{
		_historicalUnitPrices = new List<float>();
	}

	// Start is called before the first frame update
	void Start()
    {
		//_totalExpenditures = citizenSpawnCount * citizenStartingCapital; // the only bit of "government spending" to account into GDP for now

		SpawnInitialCitizens();
		SpawnResBoxes();

		RefreshGDP();
		RefreshTotalMoneyLabel();
    }

	void SpawnInitialCitizens()
	{
		_citizens = new List<Citizen>();
		for (int i = 0; i < citizenSpawnCount; i++)
		{
			Vector3 target = new Vector3(0f, 1f, 0f);
			target.x = Random.value * fieldRadius * 2 - fieldRadius;
			target.z = Random.value * fieldRadius * 2 - fieldRadius;
			SpawnCitizen(target, $"Citizen {i + 1}", Color.HSVToRGB(1f / citizenSpawnCount * (i + 1), 1f, 1f));
		}
	}

	void SpawnCitizen(Vector3 pos, string name, Color color)
	{
		var go = Instantiate(citizenPrefab, pos, Quaternion.identity);
		go.name = name;
		Citizen citizen = go.GetComponent<Citizen>();
		citizen.GiveStartingCapital(Random.Range(citizenStartingCapitalMin, citizenStartingCapitalMax));
		citizen.GetComponent<Renderer>().material.color = color;
		_citizens.Add(citizen);
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

	void UpdateGDPPerPeriod()
	{
		_gdpTimer += Time.deltaTime;
		if (_gdpTimer >= _gdpPeriod)
		{
			_gdpTimer = 0;
			_gdpPerPeriod = AllTimeNominalGDP - _previousMeasuredGdp;
			_previousMeasuredGdp = AllTimeNominalGDP;

			RefreshGDP();
		}
	}

	// Update is called once per frame
	void Update()
    {
        if (Input.GetMouseButtonUp(0))
		{
			RaycastHit hit;
			if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out hit, Camera.main.farClipPlane, ~LayerMask.NameToLayer("Floor")))
			{
				Vector3 pos = hit.point;
				pos.y = 1f;
				SpawnCitizen(pos, $"Citizen {_citizens.Count + 1} (new)", Color.white);
			}
		}

		UpdateGDPPerPeriod();

		RefreshTotalMoneyLabel();
		RefreshAverages();
	}
}
