using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using System.Linq;

public class GameManager : MonoBehaviour
{
	/// <summary>
	/// Represents a real-time snapshot of the total nominal GDP.
	/// </summary>
	public float AllTimeNominalGDP => _totalExpenditures;

	public float PeriodicalNominalGDP => _gdpPerPeriod;

	public static string CurrencySymbol => "€"; // ƒ

	public static string ResBoxSymbol => "BOX";

	public float CurrentAverageResBoxTradingPrice => _historicalUnitPrices.Sum() / _historicalUnitPrices.Count;

	public float CurrentAverageResBoxValuation => _citizens.Select(x => x.BaseResBoxValuation).Sum() / _citizens.Count;

	public float PriceMagnifier => _priceMagnifier;

	public float TravelCostPerMeter => _travelCostPerMeter;

	[SerializeField] private GameObject _resBoxPrefab;
	[SerializeField] private GameObject _citizenPrefab;
	[SerializeField] private TMP_Text _gdpLabel;
	[SerializeField] private TMP_Text _totalMoneyLabel;
	[SerializeField] private TMP_Text _averageValuationLabel;
	[SerializeField] private TMP_Text _averageTradingPriceLabel;

	[Header("Settings")]
	[SerializeField] private float _fieldRadius = 25f;
	[SerializeField] private int _citizenSpawnCount = 3;
	[SerializeField] private int _resBoxSpawnCount = 80;
	[SerializeField] private float _citizenStartingMoneyMin = 10f;
	[SerializeField] private float _citizenStartingMoneyMax = 100f;
	[SerializeField] private int _citizenMinStartingResBoxes = 0;
	[SerializeField] private int _citizenMaxStartingResBoxes = 10;
	[SerializeField] private float _priceMagnifier = 100f;
	[SerializeField] private float _travelCostPerMeter = 1f;

	[Header("Metrics")]
	[SerializeField, ReadOnly] private int _totalResBoxesInMarket;
	[SerializeField, ReadOnly] private float _totalExpenditures;
	[SerializeField, ReadOnly] private float _gdpTimer;
	[SerializeField] private float _gdpPeriodInSeconds = 5f;
	[SerializeField, ReadOnly] private float _gdpPerPeriod;
	[SerializeField, ReadOnly] private float _previousMeasuredGdp;
	[SerializeField] private RunChart _gdpChart;

	private List<Citizen> _citizens;
	private List<float> _historicalUnitPrices;

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

	void Awake()
	{
		_historicalUnitPrices = new List<float>();
	}

	// Start is called before the first frame update
	void Start()
	{
		//_totalExpenditures = citizenSpawnCount * citizenStartingCapital; // the only bit of "government spending" to account into GDP for now
		_totalResBoxesInMarket = _resBoxSpawnCount;

		_gdpChart.ConfigureXAxis(1, 1);

		SpawnInitialCitizens();
		SpawnResBoxes();

		RefreshGDPMetrics();
		RefreshTotalMoneyLabel();
	}

	/// <summary>
	/// Updates GDP related GUIs.
	/// </summary>
	void RefreshGDPMetrics()
	{
		float gdp = PeriodicalNominalGDP;
		_gdpLabel.text = $"{FormatMoney(gdp == 0 ? 0 : gdp / _citizenSpawnCount)} GDP per capita";
	}

	void RefreshTotalMoneyLabel()
	{
		float totalMoney = _citizens.Select(x => x.MoneyInWallet).Sum();
		_totalMoneyLabel.text = $"{FormatMoney(totalMoney)} total";
	}

	void RefreshAverages()
	{
		_averageTradingPriceLabel.text = $"{FormatMoney(CurrentAverageResBoxTradingPrice)} avg. price";
		_averageValuationLabel.text = $"{CurrentAverageResBoxValuation:n3} avg. valuation";
	}

	void SpawnInitialCitizens()
	{
		_citizens = new List<Citizen>();
		for (int i = 0; i < _citizenSpawnCount; i++)
		{
			Vector3 target = new Vector3(0f, 1f, 0f);
			target.x = Random.value * _fieldRadius * 2 - _fieldRadius;
			target.z = Random.value * _fieldRadius * 2 - _fieldRadius;
			SpawnCitizen(target, $"Citizen {i + 1}", Color.HSVToRGB(1f / _citizenSpawnCount * (i + 1), 1f, 1f));
		}
	}

	void SpawnCitizen(Vector3 pos, string name, Color color)
	{
		var go = Instantiate(_citizenPrefab, pos, Quaternion.identity);
		go.name = name;
		Citizen citizen = go.GetComponent<Citizen>();
		citizen.GetComponent<Renderer>().material.color = color;
		float startingMoney = Random.Range(_citizenStartingMoneyMin, _citizenStartingMoneyMax);
		int startingBoxes = Random.Range(_citizenMinStartingResBoxes, _citizenMaxStartingResBoxes);
		citizen.GiveStartingCapital(startingMoney, startingBoxes);
		_totalResBoxesInMarket += startingBoxes;
		_citizens.Add(citizen);
	}

	void SpawnResBoxes()
	{
		for (int i = 0; i < _resBoxSpawnCount; i++)
		{
			Vector3 target = new Vector3(0f, .5f, 0f);
			target.x = Random.value * _fieldRadius * 2 - _fieldRadius;
			target.z = Random.value * _fieldRadius * 2 - _fieldRadius;
			Quaternion rot = Random.rotation;
			rot.x = 0f;
			rot.z = 0f;
			Instantiate(_resBoxPrefab, target, rot);
		}
	}

	void UpdateGDPMetrics()
	{
		_gdpTimer += Time.deltaTime;
		if (_gdpTimer >= _gdpPeriodInSeconds)
		{
			_gdpTimer = 0;
			_gdpPerPeriod = AllTimeNominalGDP - _previousMeasuredGdp;
			_previousMeasuredGdp = AllTimeNominalGDP;

			// Chart stuff
			_gdpChart.AddValue(_gdpPerPeriod);

			RefreshGDPMetrics();
		}
	}

	void ListenForSpawnInput()
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
	}

	// Update is called once per frame
	void Update()
    {
		ListenForSpawnInput();

		UpdateGDPMetrics();

		RefreshTotalMoneyLabel();
		RefreshAverages();
	}
}
